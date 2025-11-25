using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using Verse;

namespace ConfigurableDynamicCooldown;

public abstract class PatchOperationEnabled : PatchOperation
{
    protected readonly PatchOperation match;
    protected readonly PatchOperation nomatch;
    abstract protected bool ShouldApply();
    protected override bool ApplyWorker(XmlDocument xml)
    {
        if (ShouldApply())
        {
            if (match != null)
            {
                return match.Apply(xml);
            }
        }
        else if (nomatch != null)
        {
            return nomatch.Apply(xml);
        }
        return true;
    }
}

public class PatchOp_rangedCoooldownFactorPatch : PatchOperationEnabled { protected override bool ShouldApply() => ConfigurableDynamicCooldownMod.Settings.RangedCooldownFactor; }
public class PatchOp_meleeCoooldownFactorPatch : PatchOperationEnabled { protected override bool ShouldApply() => ConfigurableDynamicCooldownMod.Settings.MeleeeCooldownFactor; }