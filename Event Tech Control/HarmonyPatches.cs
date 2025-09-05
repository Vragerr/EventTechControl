using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using Verse;
namespace TechGate
{
    [HarmonyPatch]
    public static class HarmonyPatches
    {
        // Перехватываем CanFireNow и CanFireNowSub на любых воркерах
        [HarmonyPatch]
        public static class Incident_CanFireNow_Any_Patch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                var t = typeof(IncidentWorker);
                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if ((m.Name == "CanFireNow" || m.Name == "CanFireNowSub") &&
                    m.ReturnType == typeof(bool) &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(IncidentParms))
                    {
                        yield return m;
                    }
                }
            }
            static void Postfix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
            {
                if (!__result) return;
                var def = __instance?.def;
                var settings = TechGateMod.Settings;
                if (def == null || settings == null) return;
                var minTech = settings.GetIncidentMinTech(def);
                if (minTech == null) return;
                var colonyTech = settings.GetCurrentColonyTech();
                if (Util.TechRank(minTech.Value) > Util.TechRank(colonyTech))
                    __result = false;
            }
        }
        // Перехватываем выполнение TryExecute и TryExecuteWorker до оригинала
        [HarmonyPatch]
        public static class Incident_TryExecute_Any_Patch
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                var t = typeof(IncidentWorker);
                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if ((m.Name == "TryExecute" || m.Name == "TryExecuteWorker") &&
                        m.ReturnType == typeof(bool) &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(IncidentParms))
                    {
                        yield return m;
                    }
                }
            }
            static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
            {
                var def = __instance?.def;
                var settings = TechGateMod.Settings;
                if (def == null || settings == null) return true;
                var minTech = settings.GetIncidentMinTech(def);
                if (minTech == null) return true;
                var colonyTech = settings.GetCurrentColonyTech();
                if (Util.TechRank(minTech.Value) > Util.TechRank(colonyTech))
                {
                    // блокируем всегда, если не forced; если forced — по настройке
                    if (!parms.forced || settings.BlockForced)
                    {
                        __result = false;
                        return false;
                    }
                }
                return true;
            }
        }
    }
    // КВЕСТЫ: все перегрузки CanRun(...)
    [HarmonyPatch]
    public static class QuestScriptDef_CanRun_Patch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var t = typeof(QuestScriptDef);
            return t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "CanRun" && m.ReturnType == typeof(bool));
        }
        static void Postfix(QuestScriptDef __instance, ref bool __result)
        {
            if (!__result) return;
            var settings = TechGateMod.Settings; if (settings == null) return;
            var minTech = settings.GetQuestMinTech(__instance); if (minTech == null) return;
            var colonyTech = settings.GetCurrentColonyTech();
            if (Util.TechRank(minTech.Value) > Util.TechRank(colonyTech))
                __result = false;
        }
    }
    // Барьер на уровне генерации квеста (QuestGen.Generate* -> Quest)
    [HarmonyPatch]
    public static class QuestGen_Generate_Patch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var t = AccessTools.TypeByName("RimWorld.QuestGen.QuestGen");
            if (t == null) yield break;
            foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (m.ReturnType == typeof(Quest) && m.GetParameters().Any(p => p.ParameterType == typeof(QuestScriptDef)))
                    yield return m;
        }
        static bool Prefix(MethodBase __originalMethod, object[] __args)
        {
            var settings = TechGateMod.Settings; if (settings == null) return true;
            var ps = __originalMethod.GetParameters();
            QuestScriptDef qdef = null;
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].ParameterType == typeof(QuestScriptDef)) { qdef = __args[i] as QuestScriptDef; break; }
            if (qdef == null) return true;
            var minTech = settings.GetQuestMinTech(qdef);
            if (minTech == null) return true;
            var colonyTech = settings.GetCurrentColonyTech();
            return Util.TechRank(minTech.Value) <= Util.TechRank(colonyTech);
        }
    }
    // Барьер перед публикацией (часто через фреймворки)
    [HarmonyPatch]
    public static class QuestUtility_GenerateMakeAvailable_Patch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var t = AccessTools.TypeByName("RimWorld.QuestUtility");
            if (t == null) yield break;
            foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.GetParameters().Any(p => p.ParameterType == typeof(QuestScriptDef)) &&
                    (m.Name.IndexOf("Generate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     m.Name.IndexOf("TryGenerate", StringComparison.OrdinalIgnoreCase) >= 0) &&
                    m.Name.IndexOf("MakeAvailable", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    yield return m;
                }
            }
        }
        static bool Prefix(MethodBase __originalMethod, object[] __args)
        {
            var settings = TechGateMod.Settings; if (settings == null) return true;
            var ps = __originalMethod.GetParameters();
            QuestScriptDef qdef = null;
            for (int i = 0; i < ps.Length; i++)
                if (ps[i].ParameterType == typeof(QuestScriptDef)) { qdef = __args[i] as QuestScriptDef; break; }
            if (qdef == null) return true;
            var minTech = settings.GetQuestMinTech(qdef);
            if (minTech == null) return true;
            var colonyTech = settings.GetCurrentColonyTech();
            return Util.TechRank(minTech.Value) <= Util.TechRank(colonyTech);
        }
    }
    // Последний барьер — не добавлять квест в менеджер
    [HarmonyPatch]
    public static class QuestManager_Add_Patch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var t = typeof(QuestManager);
            return t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "Add"
                             && m.GetParameters().Length >= 1
                             && m.GetParameters()[0].ParameterType == typeof(Quest));
        }
        static bool Prefix(object[] __args)
        {
            var settings = TechGateMod.Settings; if (settings == null) return true;
            var quest = __args[0] as Quest;
            var qdef = quest?.root;
            if (qdef == null) return true;
            var minTech = settings.GetQuestMinTech(qdef);
            if (minTech == null) return true;
            var colonyTech = settings.GetCurrentColonyTech();
            return Util.TechRank(minTech.Value) <= Util.TechRank(colonyTech);
        }
    }
}