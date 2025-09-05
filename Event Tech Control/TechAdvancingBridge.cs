using System;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
namespace TechGate
{
    public static class TechAdvancingBridge
    {
        private static Func<TechLevel> _getter;
        public static bool Available { get; private set; }
        private static bool _logged;
        static TechAdvancingBridge()
        {
            try
            {
                // Без PackageIdNonUnique — используем только PackageId и Name
                var hasTAInList = false;
                try
                {
                    foreach (var m in LoadedModManager.RunningModsListForReading)
                    {
                        var id = (m?.PackageId ?? "").ToLowerInvariant();
                        var name = (m?.Name ?? "").ToLowerInvariant();
                        if (id.Contains("techadvancing") || name.Contains("tech advancing"))
                        {
                            hasTAInList = true;
                            break;
                        }
                    }
                }
                catch
                {
                    hasTAInList = false;
                }
                // Пробуем найти сборку напрямую
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name.IndexOf("TechAdvancing", StringComparison.OrdinalIgnoreCase) >= 0);
                // Считаем доступным, если мод в списке или сборка найдена
                Available = hasTAInList || asm != null;
                if (asm == null)
                {
                    // Сборка не найдена — используем fallback (tech фракции/исследования)
                    LogOnce("Tech Advancing: pack not finded, using fallback (tech faction/research).", true);
                    return;
                }
                // Поиск API
                var candidateTypeNames = new[]
                {
                "TechAdvancing.TechAdvancingManager",
                "TechAdvancing.TechTracker",
                "TechAdvancing.TechLevelTracker",
                "TechAdvancing.TA_GameComponent"
            };
                Type type = null;
                foreach (var tn in candidateTypeNames)
                {
                    type = asm.GetType(tn, throwOnError: false);
                    if (type != null) break;
                }
                if (type == null)
                {
                    type = asm.GetTypes().FirstOrDefault(t =>
                        t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                         .Any(m => m.Name.IndexOf("Tech", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                   m.Name.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                   m.Name.IndexOf("Current", StringComparison.OrdinalIgnoreCase) >= 0));
                }
                if (type == null)
                {
                    LogOnce("Tech Advancing API not found — fallback on tech faction/research.", true);
                    return;
                }
                var prop = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(p => p.Name.IndexOf("Tech", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                         p.Name.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                         p.Name.IndexOf("Current", StringComparison.OrdinalIgnoreCase) >= 0);
                if (prop != null)
                {
                    _getter = () => ConvertToTechLevel(prop.GetValue(null));
                    LogOnce($"Tech Advancing API Found: {type.FullName}.{prop.Name}", false);
                    return;
                }
                var meth = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.GetParameters().Length == 0 &&
                                         m.Name.IndexOf("Tech", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                         m.Name.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                         m.Name.IndexOf("Current", StringComparison.OrdinalIgnoreCase) >= 0);
                if (meth != null)
                {
                    _getter = () => ConvertToTechLevel(meth.Invoke(null, null));
                    LogOnce($"Tech Advancing API Found: {type.FullName}.{meth.Name}()", false);
                    return;
                }
                LogOnce("Tech Advancing API not found — fallback on tech faction/research.", true);
            }
            catch
            {
                Available = false;
                _getter = null;
                LogOnce("error integration with Tech Advancing — disabled. Will used fallback.", true);
            }
        }
        public static bool TryGet(out TechLevel level)
        {
            try
            {
                if (_getter != null)
                {
                    level = _getter();
                    return true;
                }
            }
            catch
            {
                // игнор — используем запасной вариант ниже
            }
            level = Faction.OfPlayer?.def?.techLevel ?? TechLevel.Neolithic;
            return true;
        }
        private static TechLevel ConvertToTechLevel(object val)
        {
            if (val is TechLevel tl) return tl;
            if (val is int i) return (TechLevel)i;
            return (TechLevel)Convert.ToInt32(val);
        }
        private static void LogOnce(string msg, bool warn)
        {
            if (_logged) return;
            _logged = true;
            if (warn) Log.Warning($"[TechGate] {msg}");
            else Log.Message($"[TechGate] {msg}");
        }
    }
}