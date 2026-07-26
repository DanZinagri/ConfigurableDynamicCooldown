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
        Settings = GetSettings<ConfigurableDynamicCooldownSettings>();
        Settings.RebuildCaches();
    }

    public override string SettingsCategory() => "ConfigurableDynamicCooldownTitle".Translate();

    public override void WriteSettings()
    {
        base.WriteSettings();
        // Rebuild the cached diminishing curves whenever the player changes the sliders.
        // The actual per-pawn effect is applied live by StatPart_ManipulationCooldown, which
        // reads these settings on demand, so there is nothing to push onto the StatDefs here.
        Settings.RebuildCaches();
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

    // Cap so a near-incapacitated pawn (manipulation ~0) can't blow the cooldown up to infinity.
    public const float MaxCooldownMultiplier = 5f;

    // Cached diminishing-returns curves, rebuilt from the retained% sliders in RebuildCaches().
    private SimpleCurve rangedDiminishingCurve;
    private SimpleCurve meleeDiminishingCurve;

    public void RebuildCaches()
    {
        rangedDiminishingCurve = rebuildCurve(rangeddiminishingRetained);
        meleeDiminishingCurve = rebuildCurve(meleediminishingRetained);
    }

    // The multiplier applied to the cooldown stat for a pawn with the given manipulation level.
    // Returned as a plain factor so StatPart_ManipulationCooldown can multiply it into the stat,
    // composing cleanly with every other cooldown modifier instead of reshaping their sum.
    //   Manipulation is 1.0 at 100%; a factor < 1 means faster (shorter cooldown).
    public float GetManipulationFactor(bool ranged, float manipulation)
    {
        float range = ranged ? RangedCoodlownFactorRange : MeleeCooldownFactorRange;

        switch (curveMode)
        {
            case CooldownCurveMode.Inverse:
            {
                // cooldown = Manipulation^(-strength). strength 1 => 200% manip = 50%, 1000% = 10%.
                float exponent = Mathf.Clamp(ranged ? rangedInverseExponent : meleeInverseExponent, 0.05f, 5f);
                if (manipulation <= 0f)
                    return MaxCooldownMultiplier;
                return Mathf.Min(Mathf.Pow(manipulation, -exponent), MaxCooldownMultiplier);
            }

            case CooldownCurveMode.Diminishing:
            {
                // Reproduce the original curve exactly, but as a multiplier: it used
                // x = 1 - range*(M - 1) fed through the diminishing curve.
                SimpleCurve curve = ranged ? rangedDiminishingCurve : meleeDiminishingCurve;
                if (curve == null) { RebuildCaches(); curve = ranged ? rangedDiminishingCurve : meleeDiminishingCurve; }
                float x = 1f - range * (manipulation - 1f);
                return Mathf.Min(curve.Evaluate(x), MaxCooldownMultiplier);
            }

            default: // Linear
            {
                float x = 1f - range * (manipulation - 1f);
                return Mathf.Clamp(x, 0f, MaxCooldownMultiplier);
            }
        }
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
        // turned off (and has no stored curveMode, so it defaulted to Diminishing), honor that
        // as the Linear/no-curve mode.
        if (Scribe.mode == LoadSaveMode.LoadingVars && !DiminishingReturns && curveMode == CooldownCurveMode.Diminishing)
            curveMode = CooldownCurveMode.Linear;
        if (Scribe.mode == LoadSaveMode.LoadingVars)
            RebuildCaches();
    }
}

// Applies the manipulation-based cooldown effect as a multiplicative StatPart instead of a
// postProcessCurve. Because StatPart.TransformValue multiplies the already-combined stat value,
// our effect stacks cleanly with other cooldown modifiers (traits, apparel, expertise, genes, etc.)
// rather than sweeping their contributions through our curve and inverting buffs into nerfs.
// Added to RangedCooldownFactor / MeleeCooldownFactor via Patches/CooldownPatch.xml.
public class StatPart_ManipulationCooldown : StatPart
{
    // Resolves everything this part needs, bailing safely on any null/abnormal state. StatParts
    // can be invoked in odd contexts (worldgen, abstract stat requests, DefOfs not yet populated),
    // so every dereference is guarded and we simply no-op rather than risk throwing.
    private bool TryGetFactor(StatRequest req, out Pawn pawn, out float manipulation, out float factor)
    {
        pawn = null;
        manipulation = 1f;
        factor = 1f;

        var settings = ConfigurableDynamicCooldownMod.Settings;
        if (settings == null || parentStat == null)
            return false;

        // Only touch the two stats we patch; anything else is left untouched.
        bool ranged;
        if (parentStat.defName == "RangedCooldownFactor") ranged = true;
        else if (parentStat.defName == "MeleeCooldownFactor") ranged = false;
        else return false;

        if (ranged && !settings.RangedCooldownFactor) return false;
        if (!ranged && !settings.MeleeeCooldownFactor) return false;

        // Must be a fully-formed pawn with a health tracker and capacities handler.
        if (!req.HasThing || !(req.Thing is Pawn p)) return false;
        if (p.health == null || p.health.capacities == null) return false;

        // DefOf fields are null until defs finish loading; guard against very early evaluation.
        if (PawnCapacityDefOf.Manipulation == null) return false;

        pawn = p;
        manipulation = p.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);
        factor = settings.GetManipulationFactor(ranged, manipulation);
        return true;
    }

    public override void TransformValue(StatRequest req, ref float val)
    {
        if (TryGetFactor(req, out _, out _, out float factor))
            val *= factor;
    }

    public override string ExplanationPart(StatRequest req)
    {
        if (!TryGetFactor(req, out _, out float manipulation, out float factor))
            return null;
        return "ConfigurableDynamicCooldownTitle".Translate() + " (" + PawnCapacityDefOf.Manipulation.LabelCap
            + " " + manipulation.ToStringPercent() + "): " + factor.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor);
    }
}