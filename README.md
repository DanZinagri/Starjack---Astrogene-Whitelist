# Starjack - Astrogene Whitelist

Choose which genes Vanilla Races Expanded - Starjack turns into astrogenes. RimWorld 1.7 only.

**Upstream behaviour.** VRE - Starjack postfixes the vanilla implied-gene generator and clones a free `<gene>_Astrogene`
for every loaded gene with metabolism -1 to -5, complexity at most 2, no archite, no prerequisite, not extraction-disabled
and not already a Starjack gene. With a large gene-mod list that is hundreds of astrogenes.

**What this does.** A Harmony postfix runs right after Starjack's and removes every astrogene that is not enabled, both
from the generated defs and from Starjack's own pool. A gene is enabled when it is on a default list or the player ticked
it, and the player has not unticked it. Listed genes that Starjack's criteria reject are built with Starjack's own
(private) astrogene builder, so nothing of Starjack's is reimplemented here.

**Default lists.** `1.7/Defs/AstrogeneWhitelist.xml` holds a `StarjackAstrogeneWhitelist.AstrogeneWhitelistDef` with gene
defNames. All loaded defs of that type are merged, so another mod can add its own list; patch the def to change or
replace it. Genes that are not loaded are skipped.

**Settings.** Options > Mod settings lists every gene Starjack could build plus everything on a default list, with its
icon, a search box (name, defName or mod), a Show all/enabled/disabled switch, and Enable shown / Disable shown / Reset to
defaults. Only departures from the defaults are saved, so later changes to a default list still apply. Astrogenes are
created while the game loads, so changes need a restart; closing the settings with pending changes offers one. Pawns in
an existing save lose any astrogene that is switched off.

| Path | Contents |
|---|---|
| `1.7/Assemblies/StarjackAstrogeneWhitelist.dll` | net10.0 build |
| `1.7/Defs/AstrogeneWhitelist.xml` | the default list (Too Many Mods' original, about 75 genes; not yet checked against current gene mods) |
| `Source/` | `AstrogeneFilter.cs` (def, filter patch), `WhitelistMod.cs` (settings and menu). Not deployed. |

**Build.** `dotnet build -c Release` in `Source`. VRE - Starjack has no 1.7 build yet, so the csproj references its 1.6
DLL; repoint and rebuild once it ships.

**Origin.** Split out of Too Many Mods - Compats and Rebalances by Warachia, where a Harmony prefix replaced Starjack's
generator with a fixed whitelist behind a setting that shipped off. Rewritten 2026-10-13 as a filter with a settings menu.
Untested in-game.
