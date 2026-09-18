# Architecture

How Biosculpter Detox is put together, and why.

## The problem it solves structurally

RimWorld biosculpter cycles are not defs. There is no `BiosculpterPodCycleDef` type. A cycle is a
`CompBiosculpterPod_Cycle` component sitting on the pod's thing alongside the pod's own
`CompBiosculpterPod`, and vanilla finds them with `AllComps.OfType<CompBiosculpterPod_Cycle>()`.

That means a mod adding a cycle has to get a component onto the pod. This mod does it by patching
`CompBiosculpterPod.PostSpawnSetup` and appending one.

## Components

| File | Role |
|---|---|
| `1.6/ModEntry.cs` | `BiosculpterDetoxMod : Mod`. Creates the Harmony instance `com.zei33.biosculpterdetox` and calls `PatchAll()`. |
| `1.6/Patches/BiosculpterPatches.cs` | All three Harmony patches, as nested classes. Postfixes only. |
| `1.6/Core/CompProperties_BiosculpterPod_DetoxCycle.cs` | The properties class, which only sets `compClass`. |
| `1.6/Core/CompBiosculpterPod_DetoxCycle.cs` | The cycle itself. Overrides `CycleCompleted` and `Description`, and answers `CanUseOn`. |
| `1.6/Core/DetoxCycle.cs` | The decisions: what counts as treatable, and the treatment. No UI, no patching. |

There is no `Defs` folder. The mod ships none, and does not need one.

## Harmony surface

Three patches, all postfixes, all on `RimWorld.CompBiosculpterPod`.

| Target | What it does |
|---|---|
| `PostSpawnSetup` | Appends the detox cycle component to the pod. |
| `CannotUseNowPawnCycleReason(Pawn, Pawn, CompBiosculpterPod_Cycle, bool)` | Returns a reason when the chosen pawn has nothing to treat. |
| `PostExposeData` | Rewrites this mod's old, unnamespaced cycle key when an older save loads. |

The second one names its argument types because that method has two public overloads and an
attribute without them does not resolve. The four-parameter one is the real body.

## Deciding what to treat

`DetoxCycle` holds three predicates and nothing else decides anything.

- An addiction is `hediff is Hediff_Addiction && hediff.def.everCurableByItem`. This is vanilla's
  own test, from `HealthUtility.FindAddiction`, and `everCurableByItem` is what the vanilla healing
  cycle gates on. Both halves are needed: the flag alone is a general medical flag that most
  non-drug conditions also use, and the type alone would catch Biotech's
  `Hediff_ChemicalDependency`, whose removal kills the pawn.
- A tolerance is a def carrying `HediffCompProperties_DrugEffectFactor`, or a def that some
  `ChemicalDef` names as its `toleranceHediff`. Tolerance has no distinguishing class to test, so
  the comp is the only structural marker.
- Withdrawal is not tested for, because it is not a hediff. It is stage index 1 of the addiction
  hediff, chosen from the pawn's chemical need level, so curing the addiction ends it.

The predicates take the chemical list as a parameter rather than reading `DefDatabase`. That is
what makes them reachable from the test project, which cannot touch the database.

Removal goes through `HealthUtility.Cure`, which honours `cureAllAtOnceIfCuredByItem`. Hediffs are
re-checked as they are cured, because curing one can cascade.

## Completion

`CompBiosculpterPod.CycleCompleted` calls the cycle's `CycleCompleted(occupant)` and then ejects
the pawn, in that order, so mod code runs while the pawn is still despawned inside the pod's
container. Two consequences:

- `occupant.Map` is null. Anything positional, a mote for example, has to use the pod.
- A letter's look target still works, because target validity does not require the thing to be
  spawned, and by the time the player clicks it the pawn is out.

Both outcomes send a letter. That diverges from vanilla, which uses `Messages.Message` for its own
cycles; the reasons are the 12-day length and that the success letter is the shipped behaviour.

## State

The component is never serialised. `ThingWithComps.ExposeData` rebuilds `comps` strictly from
`def.comps` on load, so a runtime-added component is destroyed on every load and re-added by the
spawn postfix. The component therefore holds no state and must not start holding any.

The cycle key is persisted, though, by vanilla, inside `CompBiosculpterPod.currentCycleKey`. That
is why renaming it needed the migration patch: a saved key that no longer resolves throws out of
the pod's tick and traps the occupant.

`AllComps.Add` does not populate `compsByType`, so `GetComp<CompBiosculpterPod_DetoxCycle>()` falls
through to a linear scan over the pod's comps. That is a cost, not a fault, and it stays a cost
only while the component is unsealed: `GetComp<T>` returns null early for a sealed `T` that the
dictionary does not contain. Do not seal it.

## Configuration

There is none. The mod ships no `ModSettings` class, and `BiosculpterDetoxMod` overrides neither
`SettingsCategory()` nor `DoSettingsWindowContents(Rect)`, so it has no entry in the mod options.
The 12-day duration is a literal in the spawn postfix.

The cycle properties are built per pod rather than once into a static. That is deliberate: the
label and description are translated at construction, and a static built once would keep whatever
language was active when the first pod spawned.
