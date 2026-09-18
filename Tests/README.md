# Tests

```sh
export FrameworkPathOverride=/opt/homebrew/opt/mono/lib/mono/4.7.2-api
dotnet test Tests/BiosculpterDetox.Tests.csproj
```

net472 NUnit, running against the real `Assembly-CSharp.dll` from the installed game rather than a
stub. `RimWorldDir` must be set; the workspace `.claude/settings.json` and `~/.zshrc` both export it.

This project is deliberately not in `rimworld-biosculpter-detox.sln`, so `dotnet build` on the solution
still builds only the mod and stays at zero warnings. The mod's own `.csproj` removes `Tests/**` from
its compile items, so nothing here can reach the shipped assembly.

## How it is wired

The mod's sources are compiled into the test assembly rather than referenced as a built DLL. That
keeps private and internal members reachable without an `InternalsVisibleTo`, and it avoids a
`ProjectReference` rebuilding the mod into `1.6/Assemblies/net472` in Debug, which is where
`build.sh` expects to stage a Release artefact from.

## What can and cannot be tested

Unity types load fine outside a Unity player. The real boundary is static game state and native
calls. In this mod almost everything routes through a `Pawn`, so the reachable surface is small:

- `DetoxCycle.PerformDetox`, `HasDetoxifiableConditions` and `GetDetoxifiableConditionNames` all
  walk `pawn.health.hediffSet.hediffs`. A `Pawn` is not constructible out here.
- `CompBiosculpterPod_DetoxCycle` and `BiosculpterPatches` need a spawned pod and the gizmo system.
- Harmony cannot patch on this runtime at all, in or out of the game process.

What is reachable, and is where the value turned out to be, is the mod's hardcoded defNames. The
tests read the game's own shipped def XML directly rather than going through `DefDatabase`, which
only a running game populates, and check each name against `HediffDef` **specifically**.

That type restriction is the whole point, and there is a test for the trap itself
(`TheUntypedCheckWouldCertifyTheDeadWithdrawalListAsHealthy`). Six of the eight withdrawal names do
exist in the game's global defName set, as `ThoughtDef`s. A validation test written against every
defName rather than against `HediffDef` would pass on that list and report a dead list as healthy.

## What the def checks found

All 17 hardcoded names across the two detoxifiable lists are dead:

- `DetoxifiableAddictions` spells every entry `Addiction_Alcohol`. The real hediffs are
  `AlcoholAddiction`: suffix, not prefix, and no underscore. Nine entries, none of them match.
- `DetoxifiableWithdrawals` has eight entries. Six exist as `ThoughtDef`s and two
  (`FlakeWithdrawal`, `YayoWithdrawal`) do not exist at all.
- There is no withdrawal `HediffDef` in RimWorld 1.6 at all. Withdrawal is modelled purely as a
  thought, so the `EndsWith("Withdrawal")` fallback cannot fire either and the withdrawal half of
  this mod has never been able to remove anything.

The mod works in spite of this, because `EndsWith("Addiction")` catches all seven real addiction
hediffs, and `NonDetoxifiableAddictions` correctly excludes `LuciferiumAddiction`.

These tests pin that state rather than asserting what ought to be true, so fixing any of it turns
a test red instead of changing nothing visible.

## In-game checks owed after the 2026-09-18 issue sweep

Every issue in this repo was closed on 2026-09-18, and the harness cannot run any of it: every
entry point takes a `Pawn`, and Harmony cannot patch on this runtime at all. What the tests cover
is the predicates and the patch targets. These six are the parts that are still a claim.

1. **A pawn whose only treatable condition is a drug tolerance.** Give a pawn a tolerance with no
   addiction, and confirm the cycle is offered, that its description says it will treat the
   tolerance, and that the completion letter names what was removed. That whole chain was the
   subject of issue #1 and every step of it used to disagree with the others.
2. **A pawn with nothing to treat.** Confirm the detox option is disabled with a reason rather than
   hidden, in the cycle-selection gizmo AND in the right-click float menu, since
   `CannotUseNowPawnCycleReason` feeds both. The old gate could never run at all.
3. **The same, in a non-English language.** The old gate matched a translated label, so it was false
   in eight of the nine. Polish is the sharpest test: its label is "detoks", which does not even
   contain the substring the old code looked for.
4. **A luciferium-addicted pawn.** Confirm the addiction survives the cycle, and that any ordinary
   addiction on the same pawn does not.
5. **A save with a cycle already running, loaded after this update.** This is the migration, and it
   is the one with real downside: the cycle key was renamed, and a save holding the old key that is
   not migrated throws out of the pod's tick every tick and traps the occupant. Start a detox on the
   previous build, save mid-cycle, load on this one, and confirm the cycle continues.
6. **A completion of each kind.** One cycle that removes something and one that removes nothing,
   confirming a letter arrives both times and that the second is neutral rather than bad news.

A dev-mode `[DebugAction]` printing what `PerformDetox` would remove from the selected pawn, without
removing it, would make 1 and 4 cheap. It does not exist yet.

An in-game check needs Ideology, the Bioregeneration research, a pod and a genuinely addicted pawn.
Detox is always last in the gizmo bar, because the comp is appended after the four vanilla ones.
