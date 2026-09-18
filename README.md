# Biosculpter Detox

A RimWorld 1.6 mod that adds a detox cycle to the biosculpter pod. A pawn who completes it comes
out with every drug addiction and every drug tolerance removed.

![RimWorld Version](https://img.shields.io/badge/RimWorld-1.6-brightgreen.svg)
![License](https://img.shields.io/badge/License-GPL%20v3-blue.svg)

## What it does

- Removes every drug addiction the game considers curable, vanilla or modded.
- Removes every drug tolerance, which is what raises the risk of becoming addicted again.
- Leaves permanent addictions alone unless you turn that on in the mod options. Luciferium is the
  vanilla example.

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
4. The cycle takes 12 days by default. Room cleanliness and biotuning change the time the pod
   shows, as they do for every cycle.

The cycle is offered for every pawn, like every other biosculpter cycle. A pawn with nothing to
treat gets a disabled option saying so, rather than the option being hidden, which is how vanilla
handles a cycle a pawn cannot use.

Once a pod is biotuned to a pawn, which any completed cycle does, the cycle description lists what
the cycle would treat for that pawn. The completion letter lists what was actually removed. A cycle
that finds nothing says so.

## Settings

Options > Mod options > Biosculpter Detox has two settings.

- Treat permanent addictions is off by default. Turn it on to let the cycle remove luciferium, and
  any modded addiction the game marks as permanent.
- Cycle duration is 12 days by default and runs from 1 to 30. A change reaches pods already built
  when the window closes. A pawn already in a pod keeps the length their cycle started with.

## Limitations

- There is no scaling by how bad the addiction is. Every cycle runs for the set duration.
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
