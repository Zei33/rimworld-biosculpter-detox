# Biosculpter Detox

Adds a "detox" cycle to every biosculpter pod. A pawn who finishes it comes out with every drug
addiction hediff and every drug tolerance hediff stripped, and the player gets a letter either way.
The cycle runs 12 days by default (players see roughly 11 after the pod's speed stat), adjustable
from 1 to 30 in the mod options, and is gated behind Ideology's Bioregeneration research.
Permanently incurable addictions are excluded unless the player turns them on, which is Luciferium
in vanilla, decided by the game's own `everCurableByItem` flag rather than by name.

**All seven issues were closed on 2026-09-18.** Most of what the trap list below used to warn about
is gone, and what is left is marked. Read the source before acting on anything here.

Released, v1.0.1 live on the Workshop (3540117812, 1706 subscribers, last updated 22 Aug 2025).
`About.xml` says 1.1.0, unreleased on `main`. Full audit with
evidence: `/Users/matthewscott/Programming/rimworld/docs/recon/2026-09-17-recon-dossier.md` (grep the repo name).

## Architecture

| File | Role |
|---|---|
| `1.6/ModEntry.cs` | `BiosculpterDetoxMod : Mod`. Harmony `com.zei33.biosculpterdetox`, `PatchAll()`, one log line. 31 lines. |
| `1.6/Patches/BiosculpterPatches.cs` | Both Harmony patches, as two nested classes. Postfixes only, no prefixes or transpilers. |
| `1.6/Core/CompProperties_BiosculpterPod_DetoxCycle.cs` | `: CompProperties_BiosculpterPod_BaseCycle`, sets `compClass`. 19 lines. |
| `1.6/Core/CompBiosculpterPod_DetoxCycle.cs` | `: CompBiosculpterPod_Cycle`. Overrides `CycleCompleted(Pawn)` and `Description(Pawn)`. `CanUseOn` is live: the `CannotUseNowPawnCycleReason` patch calls it. Must stay unsealed. |
| `1.6/Core/DetoxCycle.cs` | All real logic, and **no defName matching of any kind**. Three predicates (`IsCurableAddiction`, `IsDrugTolerance`, `IsDetoxifiable`) that take the chemical list as a parameter so they are testable, plus `DetoxifiableConditions`, `HasDetoxifiableConditions`, `GetDetoxifiableConditionNames` and `PerformDetox`, which returns what it removed and says nothing to the player. |
| `1.6/Languages/*/Keyed/BiosculpterDetox_Keys.xml` | 9 languages, 10 keys each, parity verified. No orphans. |

Flow: the spawn postfix builds a `CompProperties_BiosculpterPod_DetoxCycle` in C# and appends a live comp
to `parent.AllComps`. Vanilla `CompBiosculpterPod.SetupCycleCaches()` picks it up through
`AllComps.OfType<CompBiosculpterPod_Cycle>()` and registers `cycleLookup["detox"]`. When the timer expires,
`CompBiosculpterPod.CycleCompleted()` calls `SetBiotuned`, then the mod's `CycleCompleted(occupant)`, then
`EjectContents`, so mod code runs while the pawn is still inside `innerContainer`.

| Harmony target | Patch | Notes |
|---|---|---|
| `CompBiosculpterPod.PostSpawnSetup` | Postfix | Load-bearing. Adds the comp. Mutates `AllComps` during `ThingWithComps.SpawnSetup`'s own comps loop, which is safe only because that loop is index-based and re-reads `comps.Count`. |
| `CompBiosculpterPod.CannotUseNowPawnCycleReason(Pawn, Pawn, CompBiosculpterPod_Cycle, bool)` | Postfix | Gives the reason when the pawn has nothing to treat. **Argument types are mandatory in the attribute**: there are two public overloads and a name-only patch does not resolve. Defers to a reason vanilla already gave. |
| `CompBiosculpterPod.PostExposeData` | Postfix | Migrates the legacy cycle key `"detox"` to the namespaced one on `PostLoadInit`, copying vanilla's own `"healing"` to `"medic"` rename. |

Both are wrapped in `try`/`catch` + `Log.Error`. The only texture is `1.6/Textures/UI/Commands/Detox.png`.

## Invariants and traps

- **The cycle is not a def.** There is no `BiosculpterPodCycleDef` type in 1.6; cycles are comps.
  The mod ships no `Defs` folder at all now: the stub that used to be there claimed the game
  required one, which is false, and Chrono Save ships none either.
- The comp is never serialised. `ThingWithComps.ExposeData` calls `InitializeComps()` on
  `LoadingVars`, rebuilding `comps` from `def.comps` only, so the detox comp is destroyed on every
  load and re-added by the spawn postfix. It holds no state and does not override `PostExposeData`.
  **Keep it stateless.**
- **The comp must stay unsealed, and this is load-bearing rather than stylistic.** `AllComps.Add`
  does not populate `compsByType`, and `GetComp<T>` consults that dictionary on any thing with three
  or more comps, which every biosculpter pod is. It still finds the comp only because `GetComp` falls
  through to a linear scan for an unsealed `T`. Sealing it returns null, and the spawn postfix uses
  that exact lookup to decide whether it has already added the comp, so every pod would accumulate a
  duplicate cycle on every spawn. There is a test.
- **Nothing matches a defName any more, and nothing should start again.** An addiction is
  `hediff is Hediff_Addiction && hediff.def.everCurableByItem`, which is vanilla's own predicate from
  `HealthUtility.FindAddiction` and the flag the vanilla healing cycle gates on. Both halves are
  needed: the flag alone is a general medical flag that 14 of its 15 shipped users apply to non-drug
  conditions, and the type alone would catch Biotech's `Hediff_ChemicalDependency`, whose removal
  kills the pawn because the gene re-adds it. A test asserts the three old lists cannot come back.
- **Withdrawal is not a hediff.** It is stage index 1 of the addiction hediff, chosen at runtime from
  `Need_Chemical.CurCategory`. There is no withdrawal `HediffDef` in the game. Curing the addiction
  removes the stage, removes the chemical need (declared `onlyIfCausedByHediff`) and flips the
  withdrawal thought to inactive. **Do not add anything that looks for a withdrawal hediff.**
- **Tolerance has no class to type-test.** Every shipped tolerance inherits `DrugToleranceBase`, whose
  `hediffClass` is the plain `HediffWithComps`. The structural marker is the comp,
  `HediffCompProperties_DrugEffectFactor` (namespace `Verse`), which carries the chemical it dampens.
  The mod also checks `ChemicalDef.toleranceHediff`, for a modded tolerance that skips the comp.
  Enumerate all chemicals, not the addictive ones: Odyssey's Psilocap has a tolerance and cannot be
  addictive. `GoJuiceTolerance` and `WakeUpTolerance` are orphan defs in 1.6 that no chemical points
  at, which is why the comp clause is the one that matters.
- **The predicates take the chemical list as a parameter on purpose.** The harness cannot touch
  `DefDatabase` and naming any `DefOf` member there throws `TypeInitializationException`. That
  parameter is the whole reason the decisions in this mod are testable at all. Do not "simplify" it
  by reading the database inside them.
- `pawn.Map` is null inside `CycleCompleted`: the pawn is despawned inside `innerContainer` and the
  eject happens on the next line of the caller. `MoteMaker.ThrowText` **throws** on a null map rather
  than no-opping, so anything positional must use `parent`, the pod, which is spawned throughout.
  The two motes that used to be here were unreachable and are deleted.
- **`CannotUseNowPawnCycleReason` has two public overloads.** A `[HarmonyPatch]` naming it without
  argument types does not resolve, the same trap the workspace records for `GenConstruct.CanConstruct`.
  The four-parameter one is the real body; the three-parameter one delegates to it.
- **Never identify a cycle or a gizmo by its label.** `Command.defaultLabel` is translated. The old
  gate matched `defaultLabel.ToLower().Contains("detox")`, which is false in all eight non-English
  languages: Polish is "detoks". A test walks every source file to keep it that way.
- **Renaming the cycle key needs a migration, always.** `CurrentCycle` resolves through
  `cycleLookup[key]`, so a saved key that no longer resolves throws out of the pod's tick every tick
  and traps the occupant. The key is `Zei33.BiosculpterDetox.Detox` and `PostExposeData` rewrites the
  legacy `"detox"` on load, copying vanilla's own `"healing"` to `"medic"` rename. Nothing can help a
  player who removes the mod mid-cycle, because the fixing code leaves with the mod.
- **`CompProperties` are built per pod on purpose, not hoisted to a static.** The label is translated
  at construction, and a static built once keeps whatever language was active when the first pod
  spawned. Changing language reloads play data, so every pod respawns and rebuilds correctly.
- **The settings are threaded as parameters, never read from a static inside a predicate.**
  `DetoxCycle`'s predicates take `cureIncurableAddictions` the same way they take the chemical list,
  and the static is read only at the call boundary, in the one-line `CureIncurableAddictions()`
  beside `AllChemicals()`. This is not tidiness. `LoadedModManager.GetMod<T>()` returns **null** in
  the test harness rather than throwing, so a predicate reading the static does not fail loudly, it
  throws a `NullReferenceException` from somewhere unrelated and takes every predicate test with it.
- **A pod that is uninstalled and put back down keeps this mod's comp**, because
  `MinifyUtility.MakeMinified` despawns the existing `Thing` and hands the minified wrapper that same
  instance. So the spawn postfix's "already has the comp" guard fires on a reinstall, and it must
  refresh the duration on that path rather than returning, or a settings change made while the pod
  sat minified never reaches it. Only a save and load rebuilds comps from `def.comps`.
- **`Scribe_Values.Look` forwards its `defaultValue` only when the node is ABSENT.** A node that is
  present but will not parse, or that carries `IsNull`, goes through
  `ScribeExtractor.ValueFromNode`, which returns `default(T)`: zero for a float, not the default you
  passed. That is why `ClampDuration` sends anything at or below zero to the default rather than to
  the minimum. The parse-failure branch also calls `Log.Error` first, and `Log.Error` throws
  `MissingMethodException` in the harness, so that one case is not testable here.
- **The field initialiser and the `Scribe` default are read by different players and must agree.**
  Somebody with no settings file gets `new T()` and the initialiser, because `ReadModSettings` never
  calls `ExposeData` on it; somebody whose file predates a key gets the `defaultValue`. If the two
  disagree the same build hands two different defaults to two different players and nothing reports
  it.
- **The pod snapshots the cycle duration but draws the progress bar from the live value.**
  `CompBiosculpterPod` captures `Props.durationDays` into the scribed `currentCycleTicksRemaining`
  when the pawn enters, so a duration change cannot retime or strand a running cycle. The progress
  mote recomputes its denominator every tick, so shortening the duration mid-cycle pins the bar at
  empty for the whole of the difference. Numbers stay right; only the drawing is wrong.
- **Never write a unit into a duration string.** `"{0} days"` renders "1 days" at the minimum, and
  worse where the noun inflects. `ToStringTicksToDays("F0")` picks between the game's own
  `Period1Day` and `PeriodDays` keys, already translated everywhere; the Russian `PeriodDays` is
  `{0_numCase ? день : дня : дней}`, so deferring to it buys three-way Slavic agreement for free.
  The format must be `"F0"`, because the singular branch tests the formatted text against `"1"` and
  `"1.0"` never matches.
- **Known coverage gap.** No test pins the settings to the behaviour. Both settings can be unwired
  from the game (`CureIncurableAddictions()` replaced by `false`, `CurrentDurationDays()` by `12f`)
  and the whole suite still passes, which was measured rather than assumed. The predicates and the
  serialisation are covered; the two lines joining them to the game are not. Simple Improve closes
  the same gap with IL reading (`Tests/ILCalls.cs`); porting it here is the fix if this ever bites.
- **`CompProperties` are built per pod on purpose, not hoisted to a static**, and `BuildProps()` is
  the single place that builds them so the spawn path and the settings push cannot disagree.
  The live Workshop description still claims the time depends on the severity of the addiction;
  nothing implements that, and the repo's generated pages state the real default.

## Defect register

**Empty.** All seven issues were closed on 2026-09-18; see the closing comments on each for what was
done and what was deliberately not. The four that were confirmed high are all structural rather than
patched over: the dead allow-lists are deleted, the Luciferium exclusion reads the game's own flag,
the substring matching is gone entirely, and the three copies of the match test are one predicate.

**Settings landed 2026-09-18**, which closes the two standing feature requests. `BiosculpterDetoxSettings`
adds a permanent-addiction toggle (default off, so existing subscribers are unaffected) and a cycle
duration slider (default 12, range 1 to 30). Both were decided by Matthew.

## Open user reports

Nothing has shipped since 22 Aug 2025, so everything below is still live. None of it was answered.

- IQ250, 23 Aug 2025: "这种事情不要啊" ("Please don't do that!"), objecting to the Luciferium cure
  being removed in answer to his own question the day before. He wanted it, he was not reporting an exploit.
- 深空—星魂, 5 Dec 2025: "或许可以加个设置来决定能否治疗？" asks for a setting controlling whether
  Luciferium can be cured. **Done 2026-09-18**, as the permanent-addiction toggle, off by default.
  Still unanswered on the Workshop, and worth answering when the pages are repasted.
- Moonsnow, 10 Dec 2025: posts an addiction-then-cure mood sequence that nets out to zero. Not a code
  defect. The mod adds no thought and no hediff, so any mood movement is vanilla reacting to the removed
  addiction. It is a balance point about combat drug use once a pod exists.
- 懒光, 28 Dec 2025: "可否加速治疗?11天确实有点长了" asks whether treatment can be sped up.
  **Done 2026-09-18**, as the duration slider. Severity scaling is still not implemented and was not
  asked for; the slider is a flat duration.
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
- Tests: `dotnet test Tests/BiosculpterDetox.Tests.csproj`, 27 passing as of 2026-09-18. Outside the
  sln, and `Compile Remove="Tests/**"` keeps them out of the shipped DLL. See `Tests/README.md`.
- **The decisions are testable now, and that was the point of the rewrite.** The three predicates in
  `DetoxCycle` take the chemical list as a parameter instead of reading `DefDatabase`, so they run in
  the harness. Everything that takes a `Pawn` is still out of reach.
  `Tests/DetoxPredicateTests.cs` covers the cases the shipped game cannot demonstrate: a modded
  `PainToleranceImplant` that the old `Contains("Tolerance")` would have eaten, Anomaly's
  `CubeWithdrawal` that `EndsWith("Withdrawal")` would have removed, and a modded permanent addiction
  excluded by the flag rather than by name.
- **A defName-validation test here must resolve against `HediffDef` specifically**, never the global
  defName set. Six of the eight old withdrawal names exist as `ThoughtDef`s, so an untyped check
  passes on a dead list and certifies the bug as healthy. `Tests/DefValidationTests.cs` keeps that
  case as a test of its own, because it is the trap rather than a detail.
- The old defName lists are gone, and `Tests/DefValidationTests.cs` keeps their evidence as literals
  with the history attached: none of the nine addiction names nor the eight withdrawal names has ever
  been a `HediffDef`. One test asserts the three lists cannot come back. Another asserts the premise
  the Luciferium exclusion rests on, that the shipped def really does set `everCurableByItem` false,
  so Ludeon dropping that flag surfaces as a decision rather than a silent balance change.
- `Tests/PatchTargetTests.cs` checks that every member the Harmony patches name still exists,
  including that `CannotUseNowPawnCycleReason` still has exactly two overloads and that the private
  `currentCycleKey` the migration injects by string is still called that. Harmony cannot patch on
  this runtime at all, so a patch whose target stops resolving is otherwise silent.
- A dev-mode `[DebugAction]` printing what `PerformDetox` would remove from the selected pawn,
  without removing it, is still the only way to check the behaviour itself.
- An in-game check needs Ideology, the Bioregeneration research, a pod and a genuinely addicted pawn.
  Detox is always last in the gizmo bar, since the comp is appended after the four vanilla cycle comps.

## Repo conventions

Workspace defaults apply (packageId `Zei33.BiosculpterDetox`, Harmony `com.zei33.biosculpterdetox`, nine
languages, XML doc comments on public members). Two differences: namespaces here are `BiosculpterDetox`,
`.Core` and `.Patches` only, with `Core` holding domain logic rather than defs. The one `Log.Message`
is the load line in `ModEntry`, gated on `Prefs.DevMode` and reading `pack.ModMetaData.ModVersion`, so
`About.xml` is the only place the version is written down. `Log.Error` is left ungated.
