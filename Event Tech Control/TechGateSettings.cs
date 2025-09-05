using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
namespace TechGate
{
    public enum ColonyTechMode
    {
        AutoPreferTechAdvancing,
        FactionTech,
        MaxResearched,
        ManualOverride
    }
    public class TechGateSettings : ModSettings
    {
        public bool BlockForced = true;
        // инциденты: min tech
        public Dictionary<string, TechLevel?> IncidentMinTech = new Dictionary<string, TechLevel?>();
        // квесты: min tech
        public Dictionary<string, TechLevel?> QuestMinTech = new Dictionary<string, TechLevel?>();
        public ColonyTechMode TechMode = ColonyTechMode.AutoPreferTechAdvancing;
        public TechLevel ManualColonyTech = TechLevel.Neolithic;
        public override void ExposeData()
        {
            Scribe_Collections.Look(ref IncidentMinTech, "IncidentMinTech", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref QuestMinTech, "QuestMinTech", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref TechMode, "TechMode", ColonyTechMode.AutoPreferTechAdvancing);
            Scribe_Values.Look(ref ManualColonyTech, "ManualColonyTech", TechLevel.Neolithic);
            Scribe_Values.Look(ref BlockForced, "BlockForced", true);
        }
        public TechLevel? GetIncidentMinTech(IncidentDef def)
        {
            if (def == null) return null;
            if (IncidentMinTech.TryGetValue(def.defName, out var v)) return v;
            var ext = def.GetModExtension<TechGateExtension>();
            return ext?.minTech;
        }
        public void SetIncidentMinTech(IncidentDef def, TechLevel? value)
        {
            if (def == null) return;
            if (value == null) IncidentMinTech.Remove(def.defName);
            else IncidentMinTech[def.defName] = value.Value;
        }
        public TechLevel? GetQuestMinTech(QuestScriptDef def)
        {
            if (def == null) return null;
            if (QuestMinTech.TryGetValue(def.defName, out var v)) return v;
            var ext = def.GetModExtension<TechGateExtension>();
            return ext?.minTech;
        }
        public void SetQuestMinTech(QuestScriptDef def, TechLevel? value)
        {
            if (def == null) return;
            if (value == null) QuestMinTech.Remove(def.defName);
            else QuestMinTech[def.defName] = value.Value;
        }
        public TechLevel GetCurrentColonyTech()
        {
            switch (TechMode)
            {
                case ColonyTechMode.AutoPreferTechAdvancing:
                    if (TechAdvancingBridge.Available && TechAdvancingBridge.TryGet(out var taLvl))
                        return taLvl;
                    return Faction.OfPlayer?.def?.techLevel ?? TechLevel.Neolithic;
                case ColonyTechMode.MaxResearched:
                    return Util.MaxResearchedTechLevel();
                case ColonyTechMode.ManualOverride:
                    return ManualColonyTech;
                case ColonyTechMode.FactionTech:
                default:
                    return Faction.OfPlayer?.def?.techLevel ?? TechLevel.Neolithic;
            }
        }
    }
}