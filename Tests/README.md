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
