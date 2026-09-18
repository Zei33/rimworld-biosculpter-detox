# Biosculpter Detox

A RimWorld 1.6 mod that adds a detox cycle to the biosculpter pod. A pawn who completes it comes
out with every drug addiction and every drug tolerance removed.

![RimWorld Version](https://img.shields.io/badge/RimWorld-1.6-brightgreen.svg)
![License](https://img.shields.io/badge/License-GPL%20v3-blue.svg)

## What it does

- Removes every drug addiction the game considers curable, vanilla or modded.
- Removes every drug tolerance, which is what raises the risk of becoming addicted again.
- Leaves permanent addictions alone. Luciferium is the vanilla example.

Withdrawal is not removed, because withdrawal is not a separate condition. RimWorld models it as a
stage of the addiction hediff, chosen from how long the pawn has gone without the drug, so curing
the addiction ends the withdrawal and the mood penalty that goes with it.

The mod identifies what to treat by asking the game, not by matching names. An addiction is
anything the game classes as a drug addiction and does not flag as permanently incurable, which is
the same test the vanilla healing cycle uses. A tolerance is anything carrying the game's own
drug-effect comp, or anything a chemical names as its tolerance. Mods that add drugs through the
normal def structure are therefore supported without this mod knowing anything about them, and
mods whose hediffs merely happen to be named like addictions are left alone.

## Requirements

- RimWorld 1.6.
- The Ideology expansion. The biosculpter pod is an Ideology building, so the mod does nothing
  without it.
- The [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) mod. It is not
  bundled: the copy in this repository is a compile-time reference and is deliberately stripped
  from the built mod folder.

## Using it

1. Build a biosculpter pod and complete the Bioregeneration research, both from Ideology.
2. Select the pod and choose the detox cycle.
3. Pick a pawn.
4. The cycle takes 12 days.

The cycle is offered for every pawn, like every other biosculpter cycle. A pawn with nothing to
treat gets a disabled option saying so, rather than the option being hidden, which is how vanilla
handles a cycle a pawn cannot use.

The cycle description lists what the cycle would treat for the selected pawn before it starts, and
the completion letter lists what was actually removed. A cycle that finds nothing says so.

## Limitations

- The duration is fixed at 12 days. There is no setting for it and no scaling by how bad the
  addiction is.
- There is no mod settings screen at all, so there is nowhere to make the Luciferium exclusion or
  the duration configurable. Both have been asked for.
- Removing the mod while a pawn is inside a pod running the detox cycle leaves the pod holding a
  cycle it can no longer resolve. Finish or cancel a cycle before removing the mod.

## Building

```sh
export FrameworkPathOverride=/opt/homebrew/opt/mono/lib/mono/4.7.2-api
dotnet build rimworld-biosculpter-detox.sln -c Release
```

`./build.sh` builds Release, stages the mod and replaces the copy in the RimWorld install. It
deletes that folder first, so do not run it to check something.

Tests: `dotnet test Tests/BiosculpterDetox.Tests.csproj`. They cover the predicates that decide
what a cycle treats, and they check the mod's assumptions about the shipped game by reading the
game's own def XML. See `Tests/README.md` for what is and is not reachable.

## Licence

GPL-3.0. See `LICENSE`.
