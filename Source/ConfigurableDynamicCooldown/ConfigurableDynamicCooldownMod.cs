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
        if (Settings.RangedCooldownFactor)
        {
            var ranged = DefDatabase<StatDef>.GetNamedSilentFail("RangedCooldownFactor");
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

        // Melee
        if (Settings.MeleeeCooldownFactor)
        {
            var melee = DefDatabase<StatDef>.GetNamedSilentFail("MeleeCooldownFactor");
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
        listing.End();
    }
}

public class ConfigurableDynamicCooldownSettings : ModSettings
{
    public bool RangedCooldownFactor = true;
    public bool MeleeeCooldownFactor = true;
    public float MeleeCooldownFactorRange = 1f;
    public float RangedCoodlownFactorRange = 1f;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref RangedCooldownFactor, nameof(RangedCooldownFactor), true);
        Scribe_Values.Look(ref MeleeeCooldownFactor, nameof(MeleeeCooldownFactor), true);
        Scribe_Values.Look(ref MeleeCooldownFactorRange, nameof(MeleeCooldownFactorRange), 1f);
        Scribe_Values.Look(ref RangedCoodlownFactorRange, nameof(RangedCoodlownFactorRange), 1f);
    }
}