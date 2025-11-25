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
        // Ranged
        var ranged = DefDatabase<StatDef>.GetNamedSilentFail("RangedCooldownFactor");
        if (Settings.RangedCooldownFactor)
        {
            
            if (ranged?.capacityOffsets != null)
            {
                foreach (var off in ranged.capacityOffsets)
                {
                    if (off.capacity == PawnCapacityDefOf.Manipulation)
                    {

                        // scale value comes from your slider
                        // (replace "scale" with the actual field you want to change, e.g. offset/postFactor)
                        off.scale = Settings.RangedCoodlownFactorRange * -1f;
                    }
                }
            }
        }
        if (Settings.DiminishingReturns && Settings.RangedCooldownFactor)
        {
            Settings.rangedcooldownCurve = Settings.rebuildCurve(Settings.rangeddiminishingRetained);
            ranged.postProcessCurve = Settings.rangedcooldownCurve;
        }
        else
        {
            ranged.postProcessCurve = null;
        }

        // Melee
        var melee = DefDatabase<StatDef>.GetNamedSilentFail("MeleeCooldownFactor");
        if (Settings.MeleeeCooldownFactor)
        {
            
            if (melee?.capacityOffsets != null)
            {
                foreach (var off in melee.capacityOffsets)
                {
                    if (off.capacity == PawnCapacityDefOf.Manipulation)
                    {

                        off.scale = Settings.MeleeCooldownFactorRange * -1f;
                    }
                }
            }
        }
        if (Settings.DiminishingReturns && Settings.MeleeeCooldownFactor)
        {
            Settings.meleecooldownCurve = Settings.rebuildCurve(Settings.meleediminishingRetained);
            melee.postProcessCurve = Settings.meleecooldownCurve;
        }
        else
        {
            melee.postProcessCurve = null;
        }
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        base.DoSettingsWindowContents(inRect);
        Listing_Standard listing = new();
        listing.Begin(inRect);
        listing.CheckboxLabeled("ConfigurableDynamicCooldown.RangedCooldownFactor".Translate(), ref Settings.RangedCooldownFactor, "ConfigurableDynamicCooldown.RangedCooldownFactor.Desc".Translate());
        listing.Label("ConfigurableDynamicCooldown.Scale".Translate() + ": " + Settings.RangedCoodlownFactorRange);
        //we're setting the minimum to anything but 0.
        Settings.RangedCoodlownFactorRange = listing.Slider(Settings.RangedCoodlownFactorRange, 0.000000000001f, 10f);
        listing.GapLine();
        
        listing.CheckboxLabeled("ConfigurableDynamicCooldown.MeleeeCooldownFactor".Translate(), ref Settings.MeleeeCooldownFactor, "ConfigurableDynamicCooldown.MeleeeCooldownFactor.Desc".Translate());
        listing.Label("ConfigurableDynamicCooldown.Scale".Translate() + ": " + Settings.MeleeCooldownFactorRange);
        Settings.MeleeCooldownFactorRange = listing.Slider(Settings.MeleeCooldownFactorRange, 0.000000000001f, 10f);

        listing.GapLine();
        listing.CheckboxLabeled("ConfigurableDynamicCooldown.DiminishingReturns".Translate(), ref Settings.DiminishingReturns, "ConfigurableDynamicCooldown.DiminishingReturns.Desc".Translate());
        listing.Label("ConfigurableDynamicCooldown.MeleeDiminishingReturns".Translate() + ": " + (int)(Settings.meleediminishingRetained *100) + "%");
        Settings.meleediminishingRetained = listing.Slider(Settings.meleediminishingRetained, 0.000000000001f, 1f);
        listing.Gap();
        listing.Label("ConfigurableDynamicCooldown.RangedDiminishingReturns".Translate() + ": " + (int)(Settings.rangeddiminishingRetained *100) + "%");
        Settings.rangeddiminishingRetained = listing.Slider(Settings.rangeddiminishingRetained, 0.000000000001f, 1f);
        listing.End();
    }
}

public class ConfigurableDynamicCooldownSettings : ModSettings
{
    public bool RangedCooldownFactor = true;
    public bool MeleeeCooldownFactor = true;
    public bool DiminishingReturns = true;
    public float MeleeCooldownFactorRange = 1f;
    public float RangedCoodlownFactorRange = 1f;
    public float meleediminishingRetained = 0.75f;
    public float rangeddiminishingRetained = 0.75f;

    public SimpleCurve meleecooldownCurve = new SimpleCurve
    {
        new CurvePoint(0f,   0.01f),
        new CurvePoint(0.5f, 0.5f),
        new CurvePoint(1f,   1f),
        new CurvePoint(3f,   2f),
    };

    public SimpleCurve rangedcooldownCurve = new SimpleCurve
    {
        new CurvePoint(0f,   0.01f),
        new CurvePoint(0.5f, 0.5f),
        new CurvePoint(1f,   1f),
        new CurvePoint(3f,   2f),
    };

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
        Scribe_Deep.Look(ref meleecooldownCurve, nameof(meleecooldownCurve), new SimpleCurve
        {
            new CurvePoint(0f,   0.01f),
            new CurvePoint(0.5f, 0.5f),
            new CurvePoint(1f,   1f),
            new CurvePoint(3f,   2f),
        });
        Scribe_Deep.Look(ref rangedcooldownCurve, nameof(rangedcooldownCurve), new SimpleCurve
        {
            new CurvePoint(0f,   0.01f),
            new CurvePoint(0.5f, 0.5f),
            new CurvePoint(1f,   1f),
            new CurvePoint(3f,   2f),
        });
        Scribe_Values.Look(ref rangeddiminishingRetained, nameof(rangeddiminishingRetained), 0.75f);
        Scribe_Values.Look(ref meleediminishingRetained, nameof(meleediminishingRetained), 0.75f);
    }
}