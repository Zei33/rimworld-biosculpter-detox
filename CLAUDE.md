# Biosculpter Detox

Adds a "detox" cycle to every biosculpter pod. A pawn who finishes it comes out with every drug
addiction hediff and every drug tolerance hediff stripped, and the player gets a positive letter.
The cycle runs a nominal 12 days (players see roughly 11 after the pod's speed stat) and is gated
behind Ideology's Bioregeneration research. Luciferium is deliberately excluded.

Released, v1.0.1, Workshop 3540117812, 1706 subscribers, last Workshop update 22 Aug 2025. Full audit with
evidence: `/Users/matthewscott/Programming/rimworld/docs/recon/2026-09-17-recon-dossier.md` (grep the repo name).

## Architecture

| File | Role |
|---|---|
| `1.6/ModEntry.cs` | `BiosculpterDetoxMod : Mod`. Harmony `com.zei33.biosculpterdetox`, `PatchAll()`, one log line. 31 lines. |
| `1.6/Patches/BiosculpterPatches.cs` | Both Harmony patches, as two nested classes. Postfixes only, no prefixes or transpilers. |
| `1.6/Core/CompProperties_BiosculpterPod_DetoxCycle.cs` | `: CompProperties_BiosculpterPod_BaseCycle`, sets `compClass`. 19 lines. |
| `1.6/Core/CompBiosculpterPod_DetoxCycle.cs` | `: CompBiosculpterPod_Cycle`. Overrides `CycleCompleted(Pawn)` and `Description(Pawn)`. `CanUseOn` at :87 has zero callers. |
| `1.6/Core/DetoxCycle.cs` | All real logic. Three `static readonly List<string>` name lists, plus `PerformDetox`, `RemoveDrugTolerances`, `HasDetoxifiableConditions`, `GetDetoxifiableConditionNames`. |
| `1.6/Core/BiosculpterDetoxDefOf.cs` | Empty class, no `[DefOf]`, no fields, no references. Dead. |
| `1.6/Defs/BiosculpterCycleDefs/Cycles_Detox.xml` | `<Defs>` containing only a comment. Defines nothing. |
| `1.6/Languages/*/Keyed/BiosculpterDetox_Keys.xml` | 9 languages, 10 keys each, all present (verified). `BiosculpterDetox_TreatingConditions` is referenced by no code. |

Flow: the spawn postfix builds a `CompProperties_BiosculpterPod_DetoxCycle` in C# and appends a live comp
to `parent.AllComps`. Vanilla `CompBiosculpterPod.SetupCycleCaches()` picks it up through
`AllComps.OfType<CompBiosculpterPod_Cycle>()` and registers `cycleLookup["detox"]`. When the timer expires,
`CompBiosculpterPod.CycleCompleted()` calls `SetBiotuned`, then the mod's `CycleCompleted(occupant)`, then
`EjectContents`, so mod code runs while the pawn is still inside `innerContainer`.

| Harmony target | Patch | Notes |
|---|---|---|
| `CompBiosculpterPod.PostSpawnSetup` | Postfix, `BiosculpterPatches.cs:20-62` | Load-bearing. Adds the comp. Mutates `AllComps` during `ThingWithComps.SpawnSetup`'s own comps loop, which is safe only because that loop is index-based and re-reads `comps.Count`. |
| `CompBiosculpterPod.CompGetGizmosExtra` | Postfix, `BiosculpterPatches.cs:68-120` | Meant to disable the detox gizmo for unaddicted pawns and append a preview. Never fires. See traps. |

Both are wrapped in `try`/`catch` + `Log.Error`. The only texture is `1.6/Textures/UI/Commands/Detox.png`.

## Invariants and traps

- **The cycle is not a def.** There is no `BiosculpterPodCycleDef` type in 1.6; cycles are comps, and
  `Cycles_Detox.xml` is a stub. Do not look for XML that configures the cycle, and do not "fix" the stub.
- The comp is never serialised. `ThingWithComps.ExposeData` calls `InitializeComps()` on
  `LoadingVars`, rebuilding `comps` from `def.comps` only, so the detox comp is destroyed on every load
  and re-added by the spawn postfix. It holds no state and does not override `PostExposeData`, so this
  round-trips cleanly. Keep it stateless.
- **Every defName in `DetoxifiableAddictions` (`DetoxCycle.cs:18-29`) is wrong.** The real vanilla names,
  verified in `$RimWorldDir/Data/Core/Defs/Drugs/`, are `AlcoholAddiction`, `AmbrosiaAddiction`,
  `GoJuiceAddiction`, `LuciferiumAddiction`, `PsychiteAddiction`, `SmokeleafAddiction`, `WakeUpAddiction`;
  nothing starts with `Addiction_`. The allow-list has never matched a hediff, and every cure actually
  happens through the substring fallback at `DetoxCycle.cs:94-97`.
- **`DetoxifiableWithdrawals` (`DetoxCycle.cs:35-45`) matches nothing, and a naive test will not catch
  it.** RimWorld has no withdrawal `HediffDef` at all; withdrawal is stage index 1 of the addiction
  hediff, selected by `Hediff_Addiction.CurStageIndex`. Six of those eight names *do* exist in the game
  data (`AlcoholWithdrawal`, `AmbrosiaWithdrawal`, `GoJuiceWithdrawal`, `PsychiteWithdrawal`,
  `SmokeleafWithdrawal`, `WakeUpWithdrawal`), but they are `ThoughtDef`s, and the mod only ever walks
  `pawn.health.hediffSet.hediffs`. **Any defName-validation test written for this repo must resolve
  against `HediffDef` specifically, not the global defName set, or it will certify this dead list as
  healthy and lock the bug in.**
- The Luciferium exclusion is a hardcoded defName (`DetoxCycle.cs:51-54`, applied at :97, :104,
  :196, :221). Correct and complete for vanilla, wrong in principle: the game already carries the flag
  vanilla itself uses. `LuciferiumAddiction` sets `<everCurableByItem>false</everCurableByItem>`
  (`Data/Core/Defs/Drugs/Luciferium.xml:124`) and `CompBiosculpterPod_HealingCycle.WillHeal` opens with
  `if (!hediff.def.everCurableByItem) return false;`. Any modded permanent addiction ending in
  "Addiction" is currently cured.
- The gizmo gating in `BiosculpterPatches.cs:91` is dead twice over. It matches on
  `command.defaultLabel.ToLower().Contains("detox")`, a localised string built from
  `BiosculpterDetox_CycleLabel`: only English yields "detox" (Polish "detoks", French "désintoxication",
  Chinese "戒毒"). Independently, `__instance.Occupant` is null at that moment. The getter returns
  `pawnEnteringBiosculpter` when it is non-null, then requires both `currentCycleKey != null` and
  `innerContainer.Count == 1`; `pawnEnteringBiosculpter` is only ever set and cleared inside one
  synchronous `TryAcceptPawn` call, and no cycle is current while the cycle-selection gizmos are
  drawn, so every branch yields null and the `occupant != null` arms never run. Match on the mod's own
  comp type.
- `pawn.Map` is null inside `CycleCompleted`. The pawn is still in `innerContainer` and despawned,
  so `Thing.Map` returns null and both `MoteMaker.ThrowText` calls (`DetoxCycle.cs:123`, :139) are
  unreachable. Use `pawn.MapHeld`, or follow vanilla cycles and use `Messages.Message`.
- **There is no `ModSettings` class anywhere in the repo**, and `BiosculpterDetoxMod` overrides neither
  `SettingsCategory()` nor `DoSettingsWindowContents(Rect)`, so the mod has no entry in Mod Settings.
  This is the structural blocker behind both most-requested features. A second sits behind it:
  `CompProperties` are constructed per pod in the spawn postfix (`BiosculpterPatches.cs:36-45`), so a
  runtime setting change would not reach existing pods until they are hoisted to one shared static.
- `durationDays = 12f` is a hardcoded literal at `BiosculpterPatches.cs:42`, the only occurrence in
  the repo. The live Workshop description claims "Time in the pod is determined by the severity of the
  addiction". Nothing implements that: no severity input, no scaling. `README.md:25` and
  `Documentation/Features.md:29` correctly say a flat 12 days, so the store page is the odd one out. Both
  also claim this matches the pleasure cycle; pleasure is 4, medic 6, ageReversal 8, bioregeneration 25.
- ~~This repo has no `Workshop/` folder.~~ **Fixed 2026-09-18**, commit `7a79092`. It carries nine
  translated description `.md` files now, like Simple Improve and Chrono Save, and unlike either of
  those they are **generated** rather than hand-maintained: edit
  `Workshop/src/body/<Language>.bbcode` and rerun `workshop-content-builder`, never the `.md` itself,
  which the builder refuses to overwrite once hand-edited. So a fix that changes player-visible
  behaviour costs a regeneration rather than a nine-language translation job. The new copy already
  states the real 12 day duration and drops the treatment preview and withdrawal removal claims, so
  the copy half of B-3 is settled ahead of the code half. Matthew still pastes the pages into Steam
  by hand at release; the website is region-blocked from this machine.
- `DefDatabase<ResearchProjectDef>.GetNamed("Bioregeneration")` at `BiosculpterPatches.cs:44` throws
  without Ideology, straight into the swallowing catch, so the mod silently does nothing. `About.xml`
  declares only the Harmony dependency and never declares Ideology.
- `key = "detox"` is not namespaced, and `cycleLookup[key] = cycle` overwrites silently, so a second mod
  using that key produces two gizmos and an ambiguous lookup. Uninstalling mid-cycle leaves an
  unresolvable `currentCycleKey` in the save and NREs the pod every tick.
- `Documentation/Architecture.md` is fiction in places: it names three Harmony patches, two of which do
  not exist, documents power and nutrition as cycle properties (not fields on
  `CompProperties_BiosculpterPod_BaseCycle`), and describes a mood buff the mod never applies.

## Defect register

Confirmed high severity. Evidence, decompiled excerpts and failure scenarios are in the dossier.

| Defect | Location | What breaks |
|---|---|---|
| `DetoxifiableAddictions` allow-list is entirely dead | `1.6/Core/DetoxCycle.cs:18` | Nine defNames that have never existed. All cures run through the substring fallback instead. |
| `DetoxifiableWithdrawals` matches nothing | `1.6/Core/DetoxCycle.cs:35` | No withdrawal hediff exists in any RimWorld version. The advertised "removes withdrawal effects" is kept only as a side effect of removing the addiction. |
| Luciferium exclusion hardcodes a defName instead of reading `everCurableByItem` | `1.6/Core/DetoxCycle.cs:51` | Modded permanent addictions, and any rename or repatch of the luciferium hediff, are cured anyway. |
| Blanket substring matching over an uncontrolled namespace (`likely`, not confirmed) | `1.6/Core/DetoxCycle.cs:94`, `:163` | `EndsWith("Addiction")` and unanchored `Contains("Tolerance")` delete arbitrary modded hediffs. Safe in vanilla only by luck. |

Three confirmed mediums are covered under traps (dead gizmo gating, unreachable motes, `removedAny` never
set by `RemoveDrugTolerances` at `DetoxCycle.cs:117`, so a tolerance-only detox reports failure). One
medium `likely`, fixed in the repo on 2026-09-17 but still live on the Workshop:
`com.rlabrecque.steamworks.net.dll` and `ISharpZipLib.dll` are the game's own assemblies, and the
22 Aug 2025 Workshop file still ships them inside `1.6/Assemblies/net472/` at 451 KB per subscriber.
`build.sh` now deletes everything in the staged output bar `BiosculpterDetox.dll` and every csproj
`<Reference>` is `<Private>false</Private>`, so a build no longer produces them, but the published copy
carries them until the next upload. Nine lows follow in the dossier.

The highest-value refactor collapses the three duplicated matching expressions (`PerformDetox`,
`HasDetoxifiableConditions`, `GetDetoxifiableConditionNames`) into one predicate over `HediffDef`, driven
by `DefDatabase<ChemicalDef>` `addictionHediff` / `toleranceHediff` and `everCurableByItem`. That is the
correctness fix, the testability fix and the thing that makes a settings screen tractable.

## Open user reports

Nothing has shipped since 22 Aug 2025, so everything below is still live. None of it was answered.

- IQ250, 23 Aug 2025: "这种事情不要啊" ("Please don't do that!"), objecting to the Luciferium cure
  being removed in answer to his own question the day before. He wanted it, he was not reporting an exploit.
- 深空—星魂, 5 Dec 2025: "或许可以加个设置来决定能否治疗？" asks for a setting controlling whether
  Luciferium can be cured. There is no settings infrastructure to hang it on, so this means a settings
  window from scratch.
- Moonsnow, 10 Dec 2025: posts an addiction-then-cure mood sequence that nets out to zero. Not a code
  defect. The mod adds no thought and no hediff, so any mood movement is vanilla reacting to the removed
  addiction. It is a balance point about combat drug use once a pod exists.
- 懒光, 28 Dec 2025: "可否加速治疗?11天确实有点长了" asks whether treatment can be sped up.
  `durationDays` is hardcoded with no setting and no severity scaling.
- Arthur GC, 4 Apr 2026: asks what happens with a gene that forces an addiction. Answered correctly by
  another user, Rox, on 18 Aug 2026, never by the author. No defect: Biotech dependency is
  `Hediff_ChemicalDependency` / `GeneticDrugNeed`, which matches none of the mod's four patterns.

## Build and test

```
export FrameworkPathOverride=/opt/homebrew/opt/mono/lib/mono/4.7.2-api
dotnet build rimworld-biosculpter-detox.sln -c Release   # clean, zero warnings
```

- `./build.sh` builds Release then deletes and replaces `$RimWorldDir/Mods/BiosculpterDetox`.
  Use it to stage for testing, never to check something.
- Building writes into `1.6/Assemblies/net472/`, which `.gitignore` now excludes outright.
  `BiosculpterDetox.dll`, `.pdb` and the two game DLLs were removed from the index on 2026-09-17, so a
  build no longer dirties the tree.
- Tests: `dotnet test Tests/BiosculpterDetox.Tests.csproj`, 9 passing as of 2026-09-17. Outside the
  sln, and `Compile Remove="Tests/**"` keeps them out of the shipped DLL. See `Tests/README.md`.
- The mod's behaviour is still not unit-testable: every entry point takes a `Pawn`. What the tests
  cover instead is the hardcoded defNames, read from the game's shipped XML and checked against
  `HediffDef` **specifically**. That type restriction is the whole point and has a test of its own:
  six of the eight withdrawal names exist as `ThoughtDef`s, so an untyped check would pass and
  certify a dead list as healthy.
- **All 17 names in both detoxifiable lists are dead.** `DetoxifiableAddictions` spells every entry
  `Addiction_Alcohol`; the real hediffs are `AlcoholAddiction`, suffix not prefix. Of
  `DetoxifiableWithdrawals`, six are `ThoughtDef`s and `FlakeWithdrawal` and `YayoWithdrawal` do not
  exist at all. Further: **there is no withdrawal `HediffDef` in 1.6 at all**, so the
  `EndsWith("Withdrawal")` fallback cannot fire either and the withdrawal half of this mod has never
  removed anything. The mod works only through `EndsWith("Addiction")`, which catches all seven real
  addiction hediffs, with `LuciferiumAddiction` correctly excluded. Confirm with the test suite
  before acting on issues #4 and #5.
- The three copies of the match test are **not identical**, and that is defect B-1 (#1).
  `HasDetoxifiableConditions` counts tolerances, `PerformDetox` and `GetDetoxifiableConditionNames`
  do not. A tolerance-only pawn therefore passes the eligibility gate, has the tolerance removed, is
  listed as having nothing to treat, and is reported as a failure. Do not unify them as a tidy-up;
  that is the fix, and it belongs to #1.
- A dev-mode `[DebugAction]` printing what `PerformDetox` would remove from the selected pawn,
  without removing it, is still the only way to check the behaviour itself.
- An in-game check needs Ideology, the Bioregeneration research, a pod and a genuinely addicted pawn.
  Detox is always last in the gizmo bar, since the comp is appended after the four vanilla cycle comps.

## Repo conventions

Workspace defaults apply (packageId `Zei33.BiosculpterDetox`, Harmony `com.zei33.biosculpterdetox`, nine
languages, XML doc comments on public members). Two differences: namespaces here are `BiosculpterDetox`,
`.Core` and `.Patches` only, with `Core` holding domain logic rather than defs; and every `Log.Message`
is ungated, where the house pattern (Simple Improve) wraps informational logging in `if (Prefs.DevMode)`
and leaves `Log.Error` alone. `ModEntry.cs:28` also logs "Loaded version 1.0" while `About.xml:5` says
`1.0.1`.
