using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace StarjackAstrogeneWhitelist
{
    public class WhitelistSettings : ModSettings
    {
        // Only the player's departures from the default lists are stored, so a later change to a default list
        // still reaches them. defNames, so an entry survives its mod being temporarily unloaded.
        public HashSet<string> forcedOn = new HashSet<string>();
        public HashSet<string> forcedOff = new HashSet<string>();

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref forcedOn, "forcedOn", LookMode.Value);
            Scribe_Collections.Look(ref forcedOff, "forcedOff", LookMode.Value);
            if (forcedOn == null)
            {
                forcedOn = new HashSet<string>();
            }
            if (forcedOff == null)
            {
                forcedOff = new HashSet<string>();
            }
        }

        public bool Enabled(string gene, bool inDefault)
        {
            return forcedOn.Contains(gene) || (inDefault && !forcedOff.Contains(gene));
        }

        public void Set(string gene, bool inDefault, bool enabled)
        {
            forcedOn.Remove(gene);
            forcedOff.Remove(gene);
            if (enabled && !inDefault)
            {
                forcedOn.Add(gene);
            }
            else if (!enabled && inDefault)
            {
                forcedOff.Add(gene);
            }
        }
    }

    public class WhitelistMod : Mod
    {
        public static WhitelistSettings settings;

        private const float RowHeight = 30f;

        private class Row
        {
            public Candidate candidate;
            public string label;
            public string info;
            public string searchText;
            public string tip;
        }

        private enum Show
        {
            All,
            Enabled,
            Disabled
        }

        private List<Row> rows;
        private readonly List<Row> filtered = new List<Row>();
        private string search = "";
        private string appliedSearch;
        private Show show;
        private Show appliedShow;
        private Vector2 scroll;
        // Recounted only when a tick box changes.
        private bool countsDirty = true;
        private int enabledCount;
        private int pendingCount;

        public WhitelistMod(ModContentPack content) : base(content)
        {
            // Both must be in place before defs are generated, which is when the filter runs.
            settings = GetSettings<WhitelistSettings>();
            new Harmony("DanZinagri.Starjack.AstrogeneWhitelist").PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory()
        {
            return "Starjack - Astrogene Whitelist";
        }

        private static bool IsEnabled(Candidate candidate)
        {
            return settings.Enabled(candidate.gene.defName, candidate.inDefault);
        }

        private void SetEnabled(Candidate candidate, bool enabled)
        {
            settings.Set(candidate.gene.defName, candidate.inDefault, enabled);
            countsDirty = true;
        }

        private void BuildRows()
        {
            rows = new List<Row>();
            foreach (Candidate candidate in AstrogeneFilter.candidates)
            {
                GeneDef gene = candidate.gene;
                string label = gene.LabelCap;
                string modName = gene.modContentPack?.Name ?? "?";
                string info = modName;
                if (!candidate.starjackPick)
                {
                    info = "outside Starjack's criteria  -  " + info;
                }
                if (candidate.inDefault)
                {
                    info = "default  -  " + info;
                }
                rows.Add(new Row
                {
                    candidate = candidate,
                    label = label,
                    info = info,
                    searchText = (label + " " + gene.defName + " " + info).ToLowerInvariant(),
                    tip = label + " (" + gene.defName + ")\n\n" + gene.description
                });
            }
            rows.Sort((a, b) => string.CompareOrdinal(a.searchText, b.searchText));
        }

        // The list is rebuilt when the search text or the Show mode changes, not on every tick box,
        // so a row does not vanish from under the cursor the moment it is toggled.
        private void ApplyFilter()
        {
            appliedSearch = search;
            appliedShow = show;
            filtered.Clear();
            string needle = search.ToLowerInvariant();
            foreach (Row row in rows)
            {
                if (needle.Length != 0 && !row.searchText.Contains(needle))
                {
                    continue;
                }
                if (show != Show.All && IsEnabled(row.candidate) != (show == Show.Enabled))
                {
                    continue;
                }
                filtered.Add(row);
            }
        }

        private void Recount()
        {
            countsDirty = false;
            enabledCount = 0;
            pendingCount = 0;
            foreach (Row row in rows)
            {
                bool enabled = IsEnabled(row.candidate);
                if (enabled)
                {
                    enabledCount++;
                }
                if (enabled != row.candidate.applied)
                {
                    pendingCount++;
                }
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (rows == null)
            {
                BuildRows();
            }
            if (appliedSearch != search || appliedShow != show)
            {
                ApplyFilter();
            }
            if (countsDirty)
            {
                Recount();
            }

            Text.Font = GameFont.Small;
            Rect header = new Rect(inRect.x, inRect.y, inRect.width, 66f);
            Widgets.Label(header, "Ticked genes get an astrogene version that Starjacks can be born with. Unticked ones are not created at all.\n"
                + "Changes only take effect after restarting the game. Pawns in an existing save lose any astrogene you untick.");
            Rect status = new Rect(inRect.x, header.yMax - 22f, inRect.width, 22f);
            Widgets.Label(status, enabledCount + " of " + rows.Count + " enabled.");
            if (pendingCount > 0)
            {
                GUI.color = Color.yellow;
                Text.Anchor = TextAnchor.UpperRight;
                Widgets.Label(status, pendingCount + (pendingCount == 1 ? " change needs" : " changes need") + " a restart to apply.");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            Rect bar = new Rect(inRect.x, header.yMax + 4f, inRect.width, 28f);
            search = Widgets.TextField(new Rect(bar.x, bar.y, 220f, bar.height), search);
            if (Widgets.ButtonText(new Rect(bar.x + 226f, bar.y, 140f, bar.height), "Show: " + show.ToString().ToLowerInvariant()))
            {
                show = (Show)(((int)show + 1) % 3);
            }
            if (Widgets.ButtonText(new Rect(bar.xMax - 370f, bar.y, 110f, bar.height), "Enable shown"))
            {
                foreach (Row row in filtered)
                {
                    SetEnabled(row.candidate, true);
                }
            }
            if (Widgets.ButtonText(new Rect(bar.xMax - 256f, bar.y, 110f, bar.height), "Disable shown"))
            {
                foreach (Row row in filtered)
                {
                    SetEnabled(row.candidate, false);
                }
            }
            if (Widgets.ButtonText(new Rect(bar.xMax - 142f, bar.y, 142f, bar.height), "Reset to defaults"))
            {
                settings.forcedOn.Clear();
                settings.forcedOff.Clear();
                countsDirty = true;
                appliedSearch = null;
            }

            Rect outRect = new Rect(inRect.x, bar.yMax + 6f, inRect.width, inRect.yMax - bar.yMax - 6f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, filtered.Count * RowHeight);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            // Only the rows on screen are drawn.
            int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
            int last = Mathf.Min(filtered.Count - 1, Mathf.CeilToInt((scroll.y + outRect.height) / RowHeight));
            for (int i = first; i <= last; i++)
            {
                Row row = filtered[i];
                Rect rect = new Rect(0f, i * RowHeight, viewRect.width, RowHeight);
                if (i % 2 == 1)
                {
                    Widgets.DrawLightHighlight(rect);
                }
                Widgets.DrawHighlightIfMouseover(rect);

                bool enabled = IsEnabled(row.candidate);
                Widgets.CheckboxDraw(rect.x + 4f, rect.y + 3f, enabled, false);
                Widgets.DefIcon(new Rect(rect.x + 34f, rect.y + 1f, RowHeight - 2f, RowHeight - 2f), row.candidate.gene);
                GUI.color = Color.white;

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(rect.x + 70f, rect.y, 250f, RowHeight), row.label);
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x + 326f, rect.y, rect.width - 330f, RowHeight), row.info);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (Mouse.IsOver(rect))
                {
                    TooltipHandler.TipRegion(rect, row.tip);
                }
                // The whole row is the tick box.
                if (Widgets.ButtonInvisible(rect))
                {
                    SetEnabled(row.candidate, !enabled);
                }
            }
            Widgets.EndScrollView();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            if (rows == null)
            {
                return;
            }
            Recount();
            if (pendingCount > 0)
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "Your astrogene changes take effect the next time the game starts.",
                    "Restart now", GenCommandLine.Restart, "Later"));
            }
        }
    }
}
