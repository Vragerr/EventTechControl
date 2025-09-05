using System;
using System.Linq;
using RimWorld;
using Verse;
namespace TechGate
{
    public static class Util
    {
        public static bool IsWorldIncident(IncidentDef def)
        {
            if (def == null) return false;
            // По workerClass
            var wname = def.workerClass?.Name?.ToLowerInvariant() ?? "";
            if (wname.Contains("world") || wname.Contains("caravan") || wname.Contains("quest"))
                return true;
            // По targetTags
            if (def.targetTags != null)
            {
                foreach (var tag in def.targetTags)
                {
                    var name = tag?.defName?.ToLowerInvariant() ?? "";
                    if (name.Contains("world") || name.Contains("caravan") || name.Contains("site"))
                        return true;
                }
            }
            var ext = def.GetModExtension<TechGateExtension>();
            if (ext?.isWorldEventHint == true) return true;
            return false;
        }
        public static TechLevel MaxResearchedTechLevel()
        {
            var completed = DefDatabase<ResearchProjectDef>.AllDefsListForReading
                .Where(r => r?.IsFinished == true && r.techLevel != TechLevel.Undefined);
            if (!completed.Any())
                return Faction.OfPlayer?.def?.techLevel ?? TechLevel.Neolithic;
            return completed.Max(r => r.techLevel);
        }
        public static int TechRank(TechLevel lvl)
        {
            return (int)lvl;
        }
    }
}