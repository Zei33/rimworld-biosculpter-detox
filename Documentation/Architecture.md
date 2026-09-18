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
| `1.6/ModEntry.cs` | `BiosculpterDetoxMod : Mod`. Creates the Harmony instance `com.zei33.biosculpterdetox` and calls `PatchAll()`, owns the settings, and pushes a duration change to built pods from `WriteSettings`. |
| `1.6/Core/BiosculpterDetoxSettings.cs` | The two settings, their defaults and clamping, and the settings window. |
| `1.6/Patches/BiosculpterPatches.cs` | Every Harmony patch, one nested class each. Postfixes only. |
| `1.6/Core/CompProperties_BiosculpterPod_DetoxCycle.cs` | The properties class, which only sets `compClass`. |
| `1.6/Core/CompBiosculpterPod_DetoxCycle.cs` | The cycle itself. Overrides `CycleCompleted` and `Description`, and answers `CanUseOn`. |
| `1.6/Core/DetoxCycle.cs` | The decisions: what counts as treatable, and the treatment. No UI, no patching. |
| `1.6/Core/DetoxCommandGate.cs` | Decides when a biotuned pod's detox button has to be disabled, and disables it. It takes plain values, so the test project reaches all of it. |
| `1.6/Languages/Russian/WordInfo/case.txt` | The Russian declension of the cycle label. Vanilla's Russian pod strings put the cycle name through a case lookup, and the game's own table has no entry for this mod's label. |

There is no `Defs` folder. The mod ships none, and does not need one.

## Harmony surface

Four patches, all postfixes, all on `RimWorld.CompBiosculpterPod`.

| Target | What it does |
|---|---|
| `PostSpawnSetup` | Appends the detox cycle component to the pod. |
| `CannotUseNowPawnCycleReason(Pawn, Pawn, CompBiosculpterPod_Cycle, bool)` | Returns a reason when the chosen pawn has nothing to treat. |
| `PostExposeData` | Rewrites this mod's old, unnamespaced cycle key when an older save loads. |
| `CompGetGizmosExtra` | Disables the detox button on a biotuned pod when the pawn it would send in is refused. |

The second one names its argument types because that method has two public overloads and an
attribute without them does not resolve. The four-parameter one is the real body.

The `CompGetGizmosExtra` patch exists because the second does not reach the cycle button. Vanilla
enables a cycle button whenever any pawn has a row in the menu it opens, and a refused row still
counts. On a pod biotuned to a pawn the button opens no menu: it runs that pawn's row directly,
and a refused row has no action to run, so the click threw. Every completed cycle biotunes the pod
to its occupant, so after a successful detox this was the normal state. The patch finds the detox
button by the icon vanilla copies onto it from the cycle, never by its translated label, and
disables it with the reason the row would show.

The table is in the order the patches are applied, and the last place is deliberate. `PatchAll`
applies the patch classes in the order they are declared and stops at the first one that throws.
The gizmo patch has the most ways to throw at patch time, and losing it costs one button, where
losing the key migration would trap a pod's occupant, so it is declared last. A test pins that.

## Deciding what to treat

`DetoxCycle` holds three predicates and nothing else decides anything.

- An addiction is `hediff is Hediff_Addiction && (setting || hediff.def.everCurableByItem)`. This
  is vanilla's own test, from `HealthUtility.FindAddiction`, and `everCurableByItem` is what the
  vanilla healing cycle gates on; the permanent-addiction setting relaxes the flag and nothing else.
  Both halves are needed: the flag alone is a general medical flag that most non-drug conditions
  also use, and the type alone would cure luciferium. Biotech's `Hediff_ChemicalDependency` fails
  both, so no setting reaches it. Removing it would not kill the pawn, since the gene recreates it
  only when the pawn next takes the drug, but it would silently suspend the dependency until then.
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

`BiosculpterDetoxSettings` holds two settings, under Options > Mod options > Biosculpter Detox.

- Treat permanent addictions, off by default. It relaxes the `everCurableByItem` half of the
  addiction test and nothing else, and it is read when a cycle completes, not when it starts.
- Cycle duration, 12 days by default, 1 to 30 in whole days. `BiosculpterDetoxMod.WriteSettings`
  pushes a change to the detox cycle on every colony pod when the window closes. A running cycle
  keeps the length it started with, because the pod copied it into its own countdown at entry.

The predicates never read these. They take the setting as a parameter, and the static is read only
at the call boundary, so the decisions stay reachable from the test project.

The cycle properties are built per pod rather than once into a static. That is deliberate: the
label and description are translated at construction, and a static built once would keep whatever
language was active when the first pod spawned.
