using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace ConfigurableDynamicCooldown;

public class ConfigurableDynamicCooldownMod : Mod
{
    public static ConfigurableDynamicCooldownSettings Settings;

    public ConfigurableDynamicCooldownMod(ModContentPack content) : base(content)
    {
        //Harm = new("DanZinagri.ConfigurableDynamicCooldown");
        Settings = GetSettings<ConfigurableDynamicCooldownSettings>();

        LongEventHandler.ExecuteWhenFinished(ApplySettings);
    }

    public override string SettingsCategory() => "ConfigurableDynamicCooldownTitle".Translate();

    public override void WriteSettings()
    {
        base.WriteSettings();
        // Re-apply whenever the player moves the sliders
        ApplySettings();
    }

    private void ApplySettings()
    {
        ApplyToStat(
            DefDatabase<StatDef>.GetNamedSilentFail("RangedCooldownFactor"),
            Settings.RangedCooldownFactor,
            Settings.RangedCoodlownFactorRange,
            Settings.rangeddiminishingRetained,
            Settings.rangedInverseExponent);

        ApplyToStat(
            DefDatabase<StatDef>.GetNamedSilentFail("MeleeCooldownFactor"),
            Settings.MeleeeCooldownFactor,
            Settings.MeleeCooldownFactorRange,
            Settings.meleediminishingRetained,
            Settings.meleeInverseExponent);
    }

    // Configures a single cooldown StatDef.
    //
    // The stat value that gets fed into the postProcessCurve is:
    //     x = baseValue(1) + (Manipulation - 1) * scale
    // because RimWorld's PawnCapacityOffset.GetOffset returns (min(level,max) - 1) * scale
    // (i.e. 100% manipulation contributes 0). So by choosing the scale and the curve
    // together we fully define how manipulation maps to the final cooldown multiplier.
    private void ApplyToStat(StatDef stat, bool enabled, float range, float retained, float exponent)
    {
        if (stat == null)
            return;

        PawnCapacityOffset manipOffset = stat.capacityOffsets?
            .FirstOrDefault(o => o.capacity == PawnCapacityDefOf.Manipulation);

        if (!enabled)
        {
            // Fully neutralize: 0 offset + no curve => stat stays at its base value of 1.
            if (manipOffset != null) manipOffset.scale = 0f;
            stat.postProcessCurve = null;
            return;
        }

        switch (Settings.curveMode)
        {
            case CooldownCurveMode.Inverse:
                // scale = 1  =>  x = 1 + (M - 1) = M, so the curve is indexed directly by
                // manipulation and maps M -> M^(-exponent).
                if (manipOffset != null) manipOffset.scale = 1f;
                stat.postProcessCurve = ConfigurableDynamicCooldownSettings.BuildInverseCurve(exponent, stat.minValue);
                break;

            case CooldownCurveMode.Diminishing:
                // scale = -range  =>  x = 1 - range*(M - 1); curve applies the diminishing shaping.
                if (manipOffset != null) manipOffset.scale = range * -1f;
                stat.postProcessCurve = Settings.rebuildCurve(retained);
                break;

            default: // CooldownCurveMode.Linear
                // scale = -range  =>  x = 1 - range*(M - 1) fed straight through, clamped to minValue.
                if (manipOffset != null) manipOffset.scale = range * -1f;
                stat.postProcessCurve = null;
                break;
        }
    }

    // Transient UI buffers for the manual-input text fields (kept out of ModSettings on purpose).
    private string rangedScaleBuffer;
    private string meleeScaleBuffer;
    private string meleeRetainedBuffer;
    private string rangedRetainedBuffer;
    private string rangedExponentBuffer;
    private string meleeExponentBuffer;

    public override void DoSettingsWindowContents(Rect inRect)
    {
        base.DoSettingsWindowContents(inRect);
        Listing_Standard listing = new();
        listing.Begin(inRect);

        listing.CheckboxLabeled("ConfigurableDynamicCooldown.RangedCooldownFactor".Translate(), ref Settings.RangedCooldownFactor, "ConfigurableDynamicCooldown.RangedCooldownFactor.Desc".Translate());
        listing.CheckboxLabeled("ConfigurableDynamicCooldown.MeleeeCooldownFactor".Translate(), ref Settings.MeleeeCooldownFactor, "ConfigurableDynamicCooldown.MeleeeCooldownFactor.Desc".Translate());
        listing.GapLine();

        // Curve mode selector: decides how Manipulation maps to the cooldown multiplier.
        listing.Label("ConfigurableDynamicCooldown.CurveMode".Translate());
        if (listing.RadioButton("ConfigurableDynamicCooldown.CurveMode.Diminishing".Translate(), Settings.curveMode == CooldownCurveMode.Diminishing, 8f))
            Settings.curveMode = CooldownCurveMode.Diminishing;
        if (listing.RadioButton("ConfigurableDynamicCooldown.CurveMode.Inverse".Translate(), Settings.curveMode == CooldownCurveMode.Inverse, 8f))
            Settings.curveMode = CooldownCurveMode.Inverse;
        if (listing.RadioButton("ConfigurableDynamicCooldown.CurveMode.Linear".Translate(), Settings.curveMode == CooldownCurveMode.Linear, 8f))
            Settings.curveMode = CooldownCurveMode.Linear;
        listing.GapLine();

        switch (Settings.curveMode)
        {
            case CooldownCurveMode.Inverse:
                Text.Font = GameFont.Tiny;
                listing.Label("ConfigurableDynamicCooldown.InverseInfo".Translate());
                Text.Font = GameFont.Small;
                // exponent 1.0 gives: 200% manip -> 50% cooldown, 1000% -> 10%.
                SliderWithField(listing, "ConfigurableDynamicCooldown.RangedInverseStrength".Translate(), ref Settings.rangedInverseExponent,
                    0.1f, 3f, 0.05f, ref rangedExponentBuffer, 1f, "");
                listing.Gap();
                SliderWithField(listing, "ConfigurableDynamicCooldown.MeleeInverseStrength".Translate(), ref Settings.meleeInverseExponent,
                    0.1f, 3f, 0.05f, ref meleeExponentBuffer, 1f, "");
                break;

            case CooldownCurveMode.Diminishing:
                SliderWithField(listing, "ConfigurableDynamicCooldown.RangedDiminishingReturns".Translate(), ref Settings.rangeddiminishingRetained,
                    0.01f, 1f, 0.01f, ref rangedRetainedBuffer, 100f, "%");
                listing.Gap();
                SliderWithField(listing, "ConfigurableDynamicCooldown.MeleeDiminishingReturns".Translate(), ref Settings.meleediminishingRetained,
                    0.01f, 1f, 0.01f, ref meleeRetainedBuffer, 100f, "%");
                listing.GapLine();
                // The "cooldown scale" also feeds the diminishing / linear modes (scales how fast
                // manipulation drives the value). Minimum is a small non-zero value so it never hits 0.
                SliderWithField(listing, "ConfigurableDynamicCooldown.Scale".Translate() + " (" + "ConfigurableDynamicCooldown.RangedCooldownFactor".Translate() + ")", ref Settings.RangedCoodlownFactorRange,
                    0.05f, 10f, 0.05f, ref rangedScaleBuffer, 1f, "");
                listing.Gap();
                SliderWithField(listing, "ConfigurableDynamicCooldown.Scale".Translate() + " (" + "ConfigurableDynamicCooldown.MeleeeCooldownFactor".Translate() + ")", ref Settings.MeleeCooldownFactorRange,
                    0.05f, 10f, 0.05f, ref meleeScaleBuffer, 1f, "");
                break;

            default: // Linear
                SliderWithField(listing, "ConfigurableDynamicCooldown.Scale".Translate() + " (" + "ConfigurableDynamicCooldown.RangedCooldownFactor".Translate() + ")", ref Settings.RangedCoodlownFactorRange,
                    0.05f, 10f, 0.05f, ref rangedScaleBuffer, 1f, "");
                listing.Gap();
                SliderWithField(listing, "ConfigurableDynamicCooldown.Scale".Translate() + " (" + "ConfigurableDynamicCooldown.MeleeeCooldownFactor".Translate() + ")", ref Settings.MeleeCooldownFactorRange,
                    0.05f, 10f, 0.05f, ref meleeScaleBuffer, 1f, "");
                break;
        }

        listing.End();
    }

    // Draws a labeled slider that snaps to clean increments when dragged, plus a numeric
    // text field beside it for precise manual input.
    //   roundTo      - increment the slider snaps to while dragging (in real units)
    //   displayScale - factor between the stored value and what the user sees/types
    //                  (1 = raw value, 100 = shown/typed as a percentage)
    private void SliderWithField(Listing_Standard listing, string label, ref float value,
        float min, float max, float roundTo, ref string buffer, float displayScale, string suffix)
    {
        string shown = displayScale == 1f
            ? (value * displayScale).ToString("0.##")
            : Mathf.RoundToInt(value * displayScale).ToString();
        listing.Label(label + ": " + shown + suffix);

        const float fieldWidth = 90f;
        const float gap = 10f;
        Rect row = listing.GetRect(28f);
        Rect sliderRect = new Rect(row.x, row.y + 4f, row.width - fieldWidth - gap, row.height - 8f);
        Rect fieldRect = new Rect(row.xMax - fieldWidth, row.y, fieldWidth, row.height);

        // Slider: round only when the user actually drags it, so the text field can hold
        // precise (unrounded) values without being snapped back every frame.
        float newVal = Widgets.HorizontalSlider(sliderRect, value, min, max);
        if (!Mathf.Approximately(newVal, value))
        {
            value = Mathf.Clamp(Mathf.Round(newVal / roundTo) * roundTo, min, max);
            buffer = null; // force the text field to re-sync from the new value
        }

        // Text field edits in display units (e.g. percent) for readability.
        float edit = value * displayScale;
        Widgets.TextFieldNumeric(fieldRect, ref edit, ref buffer, min * displayScale, max * displayScale);
        value = edit / displayScale;
    }
}

public enum CooldownCurveMode
{
    Diminishing, // original behavior: x = 1 - range*M shaped by the diminishing-returns curve
    Inverse,     // cooldown = Manipulation^(-strength); strength 1 => 200% manip -> 50%, 1000% -> 10%
    Linear,      // x = 1 - range*M fed straight through with no curve
}

public class ConfigurableDynamicCooldownSettings : ModSettings
{
    public bool RangedCooldownFactor = true;
    public bool MeleeeCooldownFactor = true;
    public bool DiminishingReturns = true; // legacy flag, kept only for save migration -> curveMode
    public CooldownCurveMode curveMode = CooldownCurveMode.Diminishing;
    public float MeleeCooldownFactorRange = 1f;
    public float RangedCoodlownFactorRange = 1f;
    public float meleediminishingRetained = 0.75f;
    public float rangeddiminishingRetained = 0.75f;
    public float rangedInverseExponent = 1f;
    public float meleeInverseExponent = 1f;

    // Inverse mode: final cooldown = Manipulation^(-exponent), sampled into a curve.
    // In this mode ApplyToStat sets the offset scale to 1, so the stat feeds x = M
    // (manipulation) straight into this curve. Each point is therefore stored at (M, y).
    //   exponent 1.0  ->  100% manip = 100% cooldown, 200% = 50%, 500% = 20%, 1000% = 10%
    public static SimpleCurve BuildInverseCurve(float exponent, float minValue)
    {
        exponent = Mathf.Clamp(exponent, 0.05f, 5f);
        const float maxCooldown = 5f; // cap so near-zero manipulation can't blow up to infinity

        float[] manipSamples = { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f, 2.5f, 3f, 4f, 5f, 7f, 10f, 14f, 20f };
        SimpleCurve curve = new SimpleCurve();
        foreach (float m in manipSamples)
        {
            float y = (m <= 0f) ? maxCooldown : Mathf.Pow(m, -exponent);
            y = Mathf.Clamp(y, minValue, maxCooldown);
            curve.Add(new CurvePoint(m, y));
        }
        return curve;
    }

    public SimpleCurve rebuildCurve(float s)
    {
        //s = 1f - s;
        s = Mathf.Clamp01(s); 
        float strength = 1f - s;

        const float floor = 0.02f;   // 1% cooldown floor
        const float p0 = 0.3f;    // exponent for strong curve
        const float k = 3f;      // weight falloff; higher = less effect near 1

        float Blend(float x)
        {
            if (x <= 0f)
                return floor;

            float baseVal = x;                   // vanilla
            float altVal = Mathf.Pow(x, p0);    // strongly curved
            float w = Mathf.Pow(1f - x, k); // big near 0, small near 1
            float t = strength * w;        // effective blend factor

            float y = baseVal * (1f - t) + altVal * t;
            return Mathf.Max(floor, y);          // don’t go below floor
        }

        SimpleCurve cooldownCurve = new SimpleCurve
        {
            new CurvePoint(0f,  Blend(floor)),
            // sample the power function at a few key points
            new CurvePoint(0.25f, Blend(0.25f)), // 175% region
            new CurvePoint(0.5f,  Blend(0.5f)),  // 150% region
            new CurvePoint(0.75f, Blend(0.75f)), // 125% region
            new CurvePoint(1f,   1f),
            new CurvePoint(3f,   2f),
        };
        return cooldownCurve;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref RangedCooldownFactor, nameof(RangedCooldownFactor), true);
        Scribe_Values.Look(ref MeleeeCooldownFactor, nameof(MeleeeCooldownFactor), true);
        Scribe_Values.Look(ref MeleeCooldownFactorRange, nameof(MeleeCooldownFactorRange), 1f);
        Scribe_Values.Look(ref RangedCoodlownFactorRange, nameof(RangedCoodlownFactorRange), 1f);

        Scribe_Values.Look(ref DiminishingReturns, nameof(DiminishingReturns), true);
        Scribe_Values.Look(ref curveMode, nameof(curveMode), CooldownCurveMode.Diminishing);
        Scribe_Values.Look(ref rangeddiminishingRetained, nameof(rangeddiminishingRetained), 0.75f);
        Scribe_Values.Look(ref meleediminishingRetained, nameof(meleediminishingRetained), 0.75f);
        Scribe_Values.Look(ref rangedInverseExponent, nameof(rangedInverseExponent), 1f);
        Scribe_Values.Look(ref meleeInverseExponent, nameof(meleeInverseExponent), 1f);

        // Migration: pre-mode saves used the DiminishingReturns bool. If an old save had it
        // turned off (and has no stored curveMode), honor that as the Linear/no-curve mode.
        if (Scribe.mode == LoadSaveMode.LoadingVars && !DiminishingReturns && curveMode == CooldownCurveMode.Diminishing)
            curveMode = CooldownCurveMode.Linear;
    }
}