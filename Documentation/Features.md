# Features

What the detox cycle does, stated against what the code does.

## The cycle

A detox cycle is offered on every biosculpter pod, alongside the vanilla cycles, once the
Bioregeneration research is complete. It runs for 12 days by default, and the mod options allow
anything from 1 to 30.

For reference, the vanilla cycles are 4 days for pleasure, 6 for medic, 8 for age reversal and 25
for bioregeneration. At the default, detox is the second longest thing a pod can do.

Power and nutrition are the pod's, not the cycle's. A biosculpter pod draws 200W and consumes 5
nutrition to sustain a pawn, for every cycle equally. Nothing in this mod changes either, and
neither is a property a cycle can set.

## What it removes

**Drug addictions.** Every addiction the game classes as curable. That is all seven vanilla ones,
alcohol, ambrosia, go-juice, psychite, smokeleaf and wake-up among them, and any modded addiction
declared through the game's normal structure.

**Drug tolerances.** Every drug tolerance, including the ones no vanilla chemical points at any
more. Tolerance is what makes the next addiction easier to acquire, so leaving it behind would
undercut the cure.

**Not luciferium, and not anything else the game calls permanent, unless the player asks.** The
exclusion is the game's own `everCurableByItem` flag rather than a name this mod knows, so a modded
permanent addiction is excluded too. "Treat permanent addictions" in the mod options lifts it for
addictions only; a permanent condition that is not an addiction stays out of reach either way.

**Not withdrawal, because there is nothing to remove.** RimWorld has no withdrawal hediff.
Withdrawal is a stage of the addiction hediff, selected from how long the pawn has gone without the
drug. Curing the addiction removes the stage, removes the chemical need and ends the mood penalty.
The end result is what the old copy promised; the mechanism is not what it described.

## What the player sees

**Before starting.** The cycle description lists the conditions it would treat for the pawn the pod
is tuned to, or says there are none. A pawn with nothing to treat gets a disabled option giving
that as the reason, which is how vanilla handles a cycle a pawn cannot use. The option is not
hidden. On a pod biotuned to that pawn, the cycle button itself is disabled with the same reason,
because there the button sends the pawn straight in rather than opening a menu.

**On completion.** A letter, naming the pawn and listing what was cured. A cycle that removed
nothing sends a neutral letter saying so, rather than saying nothing at all.

**During the cycle.** Standard biosculpter progress. This mod adds no inspect-pane text of its own.

## Settings

Two, under Options > Mod options > Biosculpter Detox. "Treat permanent addictions" is off by
default, and it is read when a cycle finishes rather than when it starts. "Cycle duration" is 12
days by default, from 1 to 30 in whole days. A change reaches pods already built when the window
closes, and a cycle already running keeps the length it started with.

## What it does not do

- No mood buff, hediff or thought is applied on completion. Mood moves only because vanilla reacts
  to the addiction being gone.
- No severity scaling. Every cycle runs for the set duration, however addicted the pawn is.
- No Architect menu entry, no designator, no batch operation.

## Compatibility

Drug mods that declare addictions and tolerances through the game's own def structure work without
this mod knowing about them, because nothing here matches on names.

The reverse also holds and is the more important half: a modded hediff that merely happens to be
named like an addiction or a tolerance is left alone. Earlier versions matched defName substrings
and would remove, for example, any hediff whose name contained "Tolerance" anywhere, from any mod.

Biotech's genetic chemical dependency is deliberately untouched, whatever the permanent-addiction
setting says. It is a different type from an addiction, and the game flags it incurable. Removing
it would not kill the pawn or end the dependency: the next dose of the drug brings it back, and
until then the pawn would carry none of the gene's drawbacks.

Removing the mod while a cycle is running leaves the pod holding a cycle key it cannot resolve.
Finish or cancel the cycle first.
