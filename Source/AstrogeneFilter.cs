using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using VanillaRacesExpandedStarjack;
using Verse;

namespace StarjackAstrogeneWhitelist
{
    // The default selection: genes that get an astrogene unless the player turns them off.
    // Any number of these defs may be loaded (another mod can add its own, or patch this mod's); their lists are merged.
    public class AstrogeneWhitelistDef : Def
    {
        // defNames, not def references: an entry whose gene is not loaded is skipped without an error,
        // and MayRequire works on each entry.
        public List<string> genes = new List<string>();
    }

    // One row of the settings list.
    public class Candidate
    {
        public GeneDef gene;
        // Listed in an AstrogeneWhitelistDef.
        public bool inDefault;
        // Starjack's own criteria accept this gene. False for genes that are only here because a list names them.
        public bool starjackPick;
    }

    // Starjack builds an astrogene for every gene that fits its criteria, inside its own postfix on
    // GeneDefGenerator.ImpliedGeneDefs. This runs right after that postfix and drops the ones that are switched off,
    // so Starjack's criteria and its astrogene construction stay Starjack's. Runs once, while defs are generated.
    [HarmonyPatch(typeof(VanillaRacesExpandedStarjack_GeneDefGenerator_ImpliedGeneDefs_Patch), "Postfix")]
    public static class AstrogeneFilter
    {
        private const string Suffix = "_Astrogene";

        public static readonly List<Candidate> candidates = new List<Candidate>();

        // Starjack's private astrogene builder, used for listed genes its criteria reject.
        private static readonly Func<GeneDef, GeneDef> MakeAstrogene = FindBuilder();

        private static Func<GeneDef, GeneDef> FindBuilder()
        {
            MethodInfo method = AccessTools.Method(typeof(VanillaRacesExpandedStarjack_GeneDefGenerator_ImpliedGeneDefs_Patch), "GetAstrogene", new[] { typeof(GeneDef) });
            return method == null ? null : AccessTools.MethodDelegate<Func<GeneDef, GeneDef>>(method);
        }

        [HarmonyPostfix]
        public static void FilterResult(ref IEnumerable<GeneDef> __result)
        {
            if (__result == null)
            {
                return;
            }
            List<GeneDef> list = __result as List<GeneDef> ?? __result.ToList();
            WhitelistSettings settings = WhitelistMod.settings;

            HashSet<string> defaults = new HashSet<string>();
            foreach (AstrogeneWhitelistDef def in DefDatabase<AstrogeneWhitelistDef>.AllDefsListForReading)
            {
                if (def.genes != null)
                {
                    defaults.UnionWith(def.genes);
                }
            }

            candidates.Clear();
            HashSet<string> seen = new HashSet<string>();
            int enabledCount = 0;

            // Starjack's picks: keep the enabled ones, remove the rest from the result and from Starjack's own pool.
            for (int i = list.Count - 1; i >= 0; i--)
            {
                GeneDef astrogene = list[i];
                if (!StaticCollections.allAstrogenes.Contains(astrogene) || !astrogene.defName.EndsWith(Suffix))
                {
                    continue;
                }
                string name = astrogene.defName.Substring(0, astrogene.defName.Length - Suffix.Length);
                GeneDef source = DefDatabase<GeneDef>.GetNamedSilentFail(name);
                if (source == null || !seen.Add(name))
                {
                    continue;
                }
                bool inDefault = defaults.Contains(name);
                bool enabled = settings.Enabled(name, inDefault);
                if (enabled)
                {
                    enabledCount++;
                }
                else
                {
                    list.RemoveAt(i);
                    StaticCollections.allAstrogenes.Remove(astrogene);
                }
                candidates.Add(new Candidate { gene = source, inDefault = inDefault, starjackPick = true });
            }

            // Listed genes Starjack would not pick itself (wrong metabolism, archite, ...): built with Starjack's builder.
            bool builderMissing = false;
            foreach (string name in defaults.Concat(settings.forcedOn).ToList())
            {
                if (!seen.Add(name))
                {
                    continue;
                }
                GeneDef source = DefDatabase<GeneDef>.GetNamedSilentFail(name);
                if (source == null)
                {
                    continue;
                }
                bool inDefault = defaults.Contains(name);
                if (settings.Enabled(name, inDefault))
                {
                    if (MakeAstrogene != null)
                    {
                        list.Add(MakeAstrogene(source));
                        enabledCount++;
                    }
                    else
                    {
                        builderMissing = true;
                    }
                }
                candidates.Add(new Candidate { gene = source, inDefault = inDefault, starjackPick = false });
            }

            if (builderMissing)
            {
                Log.Warning("[Starjack - Astrogene Whitelist] Could not find Starjack's astrogene builder, so listed genes outside Starjack's own criteria were skipped. Starjack may have been updated.");
            }
            Log.Message("[Starjack - Astrogene Whitelist] " + enabledCount + " of " + candidates.Count + " possible astrogenes enabled.");
            __result = list;
        }
    }
}
