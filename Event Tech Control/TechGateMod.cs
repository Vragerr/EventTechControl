using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace TechGate
{
    internal static class TechGateGUI
    {
        public const float TopGap = 36f;   // опустить зону табов, чтобы не перекрывалось заголовком окна
        public const float HeaderH1 = 28f; // строка 1: счётчик/поиск/сортировка
        public const float HeaderH2 = 28f; // строка 2: массовые операции
        public const float RowHeight = 44f; // увеличенная высота строки
        public const float RowHalf = RowHeight / 2f;
    }
    public class TechGateMod : Mod
    {
        public static TechGateSettings Settings;
        private Vector2 _scrollLocal = Vector2.zero;
        private Vector2 _scrollWorld = Vector2.zero;
        private Vector2 _scrollQuest = Vector2.zero;
        // 0 = Локальные, 1 = Мировые, 2 = Квесты, 3 = Настройки
        private int _activeTab = 0;
        private string _searchLocal = "";
        private string _searchWorld = "";
        private string _searchQuest = "";
        private enum SortKey
        {
            Label,
            ModName,
            DefName
        }
        private SortKey _localSortKey = SortKey.Label;
        private bool _localSortAsc = true;
        private SortKey _worldSortKey = SortKey.Label;
        private bool _worldSortAsc = true;
        private SortKey _questSortKey = SortKey.Label;
        private bool _questSortAsc = true;
        public TechGateMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<TechGateSettings>();
            var harmony = new Harmony("yourname.techgateincidents");
            harmony.PatchAll();
        }
        public override string SettingsCategory() => "Tech Gate (Incidents)";
        private string SortKeyLabel(SortKey key)
        {
            switch (key)
            {
                case SortKey.Label: return "Name";
                case SortKey.ModName: return "Mod";
                case SortKey.DefName: return "DefName";
                default: return key.ToString();
            }
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Сместим секцию вниз, чтобы заголовок окна настроек не наезжал на табы
            var tabsRect = new Rect(inRect.x, inRect.y + TechGateGUI.TopGap, inRect.width, inRect.height - TechGateGUI.TopGap);
            Widgets.DrawMenuSection(tabsRect);
            var tabs = new List<TabRecord>
        {
            new TabRecord("Local", () => _activeTab = 0, _activeTab == 0),
            new TabRecord("World", () => _activeTab = 1, _activeTab == 1),
            new TabRecord("Quests", () => _activeTab = 2, _activeTab == 2),
            new TabRecord("Settings", () => _activeTab = 3, _activeTab == 3),
        };
            TabDrawer.DrawTabs(tabsRect, tabs);
            var inner = tabsRect.ContractedBy(10f);
            inner.yMin += 32f; // место под закладки
            if (_activeTab == 3)
            {
                DrawSettings(inner);
            }
            else if (_activeTab == 2)
            {
                DrawQuestList(inner, ref _scrollQuest, ref _searchQuest, _questSortKey, _questSortAsc, (k, asc) => { _questSortKey = k; _questSortAsc = asc; });
            }
            else
            {
                var isWorld = _activeTab == 1;
                if (isWorld)
                    DrawIncidentList(inner, isWorld: true, ref _scrollWorld, ref _searchWorld, _worldSortKey, _worldSortAsc, (k, asc) => { _worldSortKey = k; _worldSortAsc = asc; });
                else
                    DrawIncidentList(inner, isWorld: false, ref _scrollLocal, ref _searchLocal, _localSortKey, _localSortAsc, (k, asc) => { _localSortKey = k; _localSortAsc = asc; });
            }
        }
        private void DrawSettings(Rect rect)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);
            listing.Label("Source TechLevel settlements:");
            if (listing.RadioButton(
                    TechAdvancingBridge.Available ? "Auto (prefer Tech Advancing)" : "Auto (if avaible Tech Advancing)",
                    Settings.TechMode == ColonyTechMode.AutoPreferTechAdvancing))
                Settings.TechMode = ColonyTechMode.AutoPreferTechAdvancing;
            if (listing.RadioButton("Faction player(vanille)", Settings.TechMode == ColonyTechMode.FactionTech))
                Settings.TechMode = ColonyTechMode.FactionTech;
            if (listing.RadioButton("The highest among completed researches", Settings.TechMode == ColonyTechMode.MaxResearched))
                Settings.TechMode = ColonyTechMode.MaxResearched;
            if (listing.RadioButton("Manual selection", Settings.TechMode == ColonyTechMode.ManualOverride))
                Settings.TechMode = ColonyTechMode.ManualOverride;
            if (Settings.TechMode == ColonyTechMode.ManualOverride)
            {
                var tech = Settings.ManualColonyTech;
                if (listing.ButtonText($"Current manual: {tech}"))
                {
                    var options = Enum.GetValues(typeof(TechLevel)).Cast<TechLevel>()
                        .Where(t => t != TechLevel.Undefined && t != TechLevel.Animal)
                        .Select(t => new FloatMenuOption(t.ToString(), () => Settings.ManualColonyTech = t))
                        .ToList();
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }
            listing.CheckboxLabeled("Block even forced incidents", ref Settings.BlockForced);
            listing.GapLine();
            var currentTech = Settings.GetCurrentColonyTech();
            var taFlag = TechAdvancingBridge.Available ? " (found Tech Advancing)" : "";
            listing.Label($"Current calculated TechLevel colonie: {currentTech}{taFlag}");
            listing.End();
        }
        private void DrawIncidentList(
    Rect rect,
    bool isWorld,
    ref Vector2 scroll,
    ref string search,
    SortKey sortKey,
    bool sortAsc,
    Action<SortKey, bool> setSort)
        {
            var all = DefDatabase<IncidentDef>.AllDefsListForReading;
            var list = all.Where(d => Util.IsWorldIncident(d) == isWorld).ToList();
            // Шапка: строка 1 — счетчик, поиск, сортировка
            var header1 = new Rect(rect.x, rect.y, rect.width, TechGateGUI.HeaderH1);
            Widgets.Label(header1.LeftPartPixels(200f), $"Count: {list.Count}");
            var searchLblRect = new Rect(header1.x + 210f, header1.y, 55f, TechGateGUI.HeaderH1);
            Widgets.Label(searchLblRect, "Search:");
            var searchRect = new Rect(searchLblRect.xMax + 4f, header1.y, 260f, TechGateGUI.HeaderH1);
            search = Widgets.TextField(searchRect, search ?? "");
            var sortBtnRect = new Rect(searchRect.xMax + 8f, header1.y, 180f, TechGateGUI.HeaderH1);
            if (Widgets.ButtonText(sortBtnRect, "Sort: " + SortKeyLabel(sortKey)))
            {
                var opts = new List<FloatMenuOption>
            {
                new FloatMenuOption("Name", () => setSort(SortKey.Label, sortAsc)),
                new FloatMenuOption("Mod", () => setSort(SortKey.ModName, sortAsc)),
                new FloatMenuOption("DefName", () => setSort(SortKey.DefName, sortAsc)),
            };
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            var dirBtnRect = new Rect(sortBtnRect.xMax + 4f, header1.y, 120f, TechGateGUI.HeaderH1);
            if (Widgets.ButtonText(dirBtnRect, sortAsc ? "Up." : "Down."))
            {
                setSort(sortKey, !sortAsc);
            }
            // Шапка: строка 2 — Массовые операции
            var header2 = new Rect(rect.x, header1.yMax, rect.width, TechGateGUI.HeaderH2);
            var massBtnRect = new Rect(header2.x, header2.y, 220f, TechGateGUI.HeaderH2);
            if (Widgets.ButtonText(massBtnRect, "Mass operations"))
            {
                var opts = new List<FloatMenuOption>
            {
                new FloatMenuOption("Reset all to Default", () =>
                {
                    foreach (var d in list) Settings.SetIncidentMinTech(d, null);
                })
            };
                var lvls = Enum.GetValues(typeof(TechLevel)).Cast<TechLevel>()
                    .Where(t => t != TechLevel.Undefined && t != TechLevel.Animal).ToList();
                foreach (var t in lvls)
                {
                    var tCopy = t;
                    opts.Add(new FloatMenuOption($"Sett all: not lower {tCopy}", () =>
                    {
                        foreach (var d in list) Settings.SetIncidentMinTech(d, tCopy);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            // Фильтр по поиску
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                list = list.Where(d =>
                    (d.label ?? "").ToLowerInvariant().Contains(s) ||
                    d.defName.ToLowerInvariant().Contains(s) ||
                    ModNameOf(d).ToLowerInvariant().Contains(s)
                ).ToList();
            }
            // Сортировка
            IOrderedEnumerable<IncidentDef> ordered;
            switch (sortKey)
            {
                case SortKey.ModName:
                    ordered = sortAsc
                        ? list.OrderBy(d => ModNameOf(d), StringComparer.CurrentCultureIgnoreCase).ThenBy(d => LabelOf(d), StringComparer.CurrentCultureIgnoreCase)
                        : list.OrderByDescending(d => ModNameOf(d), StringComparer.CurrentCultureIgnoreCase).ThenByDescending(d => LabelOf(d), StringComparer.CurrentCultureIgnoreCase);
                    break;
                case SortKey.DefName:
                    ordered = sortAsc
                        ? list.OrderBy(d => d.defName, StringComparer.OrdinalIgnoreCase)
                        : list.OrderByDescending(d => d.defName, StringComparer.OrdinalIgnoreCase);
                    break;
                case SortKey.Label:
                default:
                    ordered = sortAsc
                        ? list.OrderBy(d => LabelOf(d), StringComparer.CurrentCultureIgnoreCase)
                        : list.OrderByDescending(d => LabelOf(d), StringComparer.CurrentCultureIgnoreCase);
                    break;
            }
            var sorted = ordered.ToList();
            // Список
            var startY = header2.yMax + 8f;
            var scrollRect = new Rect(rect.x, startY, rect.width, rect.height - (startY - rect.y));
            var rowHeight = TechGateGUI.RowHeight;
            var viewHeight = sorted.Count * rowHeight + 8f;
            var view = new Rect(0f, 0f, scrollRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(scrollRect, ref scroll, view);
            float y = 0f;
            foreach (var def in sorted)
            {
                var row = new Rect(0f, y, view.width, rowHeight);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                // Левая колонка: Имя (верх) + DefName (низ)
                var left = row.LeftPartPixels(view.width * 0.50f);
                var labelRect = new Rect(left.x + 4f, left.y + 2f, left.width - 8f, TechGateGUI.RowHalf - 2f);
                var defNameRect = new Rect(left.x + 4f, left.y + TechGateGUI.RowHalf - 2f, left.width - 8f, TechGateGUI.RowHalf - 2f);
                Widgets.Label(labelRect, LabelOf(def));
                var oldColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.65f);
                Widgets.Label(defNameRect, def.defName);
                GUI.color = oldColor;
                // Колонка мод
                var modRect = new Rect(view.width * 0.52f, y, view.width * 0.20f, rowHeight);
                Widgets.Label(modRect, ModNameOf(def));
                // Кнопка min tech
                var current = Settings.GetIncidentMinTech(def);
                var btnRect = new Rect(view.width * 0.74f, y + 6f, 220f, rowHeight - 12f);
                if (Widgets.ButtonText(btnRect, current?.ToString() ?? "Default (withoutIfs)"))
                {
                    var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("Default (withoutIfs)", () => Settings.SetIncidentMinTech(def, null))
                };
                    foreach (var t in Enum.GetValues(typeof(TechLevel)))
                    {
                        var tv = (TechLevel)t;
                        if (tv == TechLevel.Undefined || tv == TechLevel.Animal) continue;
                        options.Add(new FloatMenuOption(tv.ToString(), () => Settings.SetIncidentMinTech(def, tv)));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                y += rowHeight;
            }
            Widgets.EndScrollView();
        }
        private void DrawQuestList(
    Rect rect,
    ref Vector2 scroll,
    ref string search,
    SortKey sortKey,
    bool sortAsc,
    Action<SortKey, bool> setSort)
        {
            var all = DefDatabase<QuestScriptDef>.AllDefsListForReading;
            var list = all.ToList();
            // Шапка: строка 1 — счетчик, поиск, сортировка
            var header1 = new Rect(rect.x, rect.y, rect.width, TechGateGUI.HeaderH1);
            Widgets.Label(header1.LeftPartPixels(200f), $"Count: {list.Count}");
            var searchLblRect = new Rect(header1.x + 210f, header1.y, 55f, TechGateGUI.HeaderH1);
            Widgets.Label(searchLblRect, "Search:");
            var searchRect = new Rect(searchLblRect.xMax + 4f, header1.y, 260f, TechGateGUI.HeaderH1);
            search = Widgets.TextField(searchRect, search ?? "");
            var sortBtnRect = new Rect(searchRect.xMax + 8f, header1.y, 180f, TechGateGUI.HeaderH1);
            if (Widgets.ButtonText(sortBtnRect, "Sort: " + SortKeyLabel(sortKey)))
            {
                var opts = new List<FloatMenuOption>
            {
                new FloatMenuOption("Name", () => setSort(SortKey.Label, sortAsc)),
                new FloatMenuOption("Mod", () => setSort(SortKey.ModName, sortAsc)),
                new FloatMenuOption("DefName", () => setSort(SortKey.DefName, sortAsc)),
            };
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            var dirBtnRect = new Rect(sortBtnRect.xMax + 4f, header1.y, 120f, TechGateGUI.HeaderH1);
            if (Widgets.ButtonText(dirBtnRect, sortAsc ? "Up." : "Down."))
            {
                setSort(sortKey, !sortAsc);
            }
            // Шапка: строка 2 — Массовые операции
            var header2 = new Rect(rect.x, header1.yMax, rect.width, TechGateGUI.HeaderH2);
            var massBtnRect = new Rect(header2.x, header2.y, 220f, TechGateGUI.HeaderH2);
            if (Widgets.ButtonText(massBtnRect, "Mass operations"))
            {
                var opts = new List<FloatMenuOption>
            {
                new FloatMenuOption("reset all to Default", () =>
                {
                    foreach (var d in list) Settings.SetQuestMinTech(d, null);
                })
            };
                var lvls = Enum.GetValues(typeof(TechLevel)).Cast<TechLevel>()
                    .Where(t => t != TechLevel.Undefined && t != TechLevel.Animal).ToList();
                foreach (var t in lvls)
                {
                    var tCopy = t;
                    opts.Add(new FloatMenuOption($"Set all: not lowwer {tCopy}", () =>
                    {
                        foreach (var d in list) Settings.SetQuestMinTech(d, tCopy);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            // Фильтр по поиску
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                list = list.Where(d =>
                    (d.label ?? "").ToLowerInvariant().Contains(s) ||
                    d.defName.ToLowerInvariant().Contains(s) ||
                    ModNameOf(d).ToLowerInvariant().Contains(s)
                ).ToList();
            }
            // Сортировка
            IOrderedEnumerable<QuestScriptDef> ordered;
            switch (sortKey)
            {
                case SortKey.ModName:
                    ordered = sortAsc
                        ? list.OrderBy(d => ModNameOf(d), StringComparer.CurrentCultureIgnoreCase).ThenBy(d => LabelOf(d), StringComparer.CurrentCultureIgnoreCase)
                        : list.OrderByDescending(d => ModNameOf(d), StringComparer.CurrentCultureIgnoreCase).ThenByDescending(d => LabelOf(d), StringComparer.CurrentCultureIgnoreCase);
                    break;
                case SortKey.DefName:
                    ordered = sortAsc
                        ? list.OrderBy(d => d.defName, StringComparer.OrdinalIgnoreCase)
                        : list.OrderByDescending(d => d.defName, StringComparer.OrdinalIgnoreCase);
                    break;
                case SortKey.Label:
                default:
                    ordered = sortAsc
                        ? list.OrderBy(d => LabelOf(d), StringComparer.CurrentCultureIgnoreCase)
                        : list.OrderByDescending(d => LabelOf(d), StringComparer.CurrentCultureIgnoreCase);
                    break;
            }
            var sorted = ordered.ToList();
            // Список
            var startY = header2.yMax + 8f;
            var scrollRect = new Rect(rect.x, startY, rect.width, rect.height - (startY - rect.y));
            var rowHeight = TechGateGUI.RowHeight;
            var viewHeight = sorted.Count * rowHeight + 8f;
            var view = new Rect(0f, 0f, scrollRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(scrollRect, ref scroll, view);
            float y = 0f;
            foreach (var def in sorted)
            {
                var row = new Rect(0f, y, view.width, rowHeight);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                // Левая колонка: Имя (верх) + DefName (низ)
                var left = row.LeftPartPixels(view.width * 0.50f);
                var labelRect = new Rect(left.x + 4f, left.y + 2f, left.width - 8f, TechGateGUI.RowHalf - 2f);
                var defNameRect = new Rect(left.x + 4f, left.y + TechGateGUI.RowHalf - 2f, left.width - 8f, TechGateGUI.RowHalf - 2f);
                Widgets.Label(labelRect, LabelOf(def));
                var oldColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.65f);
                Widgets.Label(defNameRect, def.defName);
                GUI.color = oldColor;
                // Колонка мод
                var modRect = new Rect(view.width * 0.52f, y, view.width * 0.20f, rowHeight);
                Widgets.Label(modRect, ModNameOf(def));
                // Кнопка min tech
                var current = Settings.GetQuestMinTech(def);
                var btnRect = new Rect(view.width * 0.74f, y + 6f, 220f, rowHeight - 12f);
                if (Widgets.ButtonText(btnRect, current?.ToString() ?? "Default (withoutIfs)"))
                {
                    var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("Default (withoutIfs)", () => Settings.SetQuestMinTech(def, null))
                };
                    foreach (var t in Enum.GetValues(typeof(TechLevel)))
                    {
                        var tv = (TechLevel)t;
                        if (tv == TechLevel.Undefined || tv == TechLevel.Animal) continue;
                        options.Add(new FloatMenuOption(tv.ToString(), () => Settings.SetQuestMinTech(def, tv)));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                y += rowHeight;
            }
            Widgets.EndScrollView();
        }
        private static string LabelOf(Def def)
        {
            return def?.label?.CapitalizeFirst() ?? def?.defName ?? "";
        }
        private static string ModNameOf(Def def)
        {
            return def?.modContentPack?.Name ?? "Core";
        }
        public override void WriteSettings()
        {
            base.WriteSettings();
        }
    }
}
