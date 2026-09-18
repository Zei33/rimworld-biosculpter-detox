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
  walk `pawn.health.hediffSet.hediffs`. A `Pawn` is not constructible out here. The naming both
  lists share, `DetoxCycle.ConditionLabels`, takes hediffs rather than a pawn and is covered by
  `ConditionLabelTests`; nothing holds the two callers to using it.
- `CompBiosculpterPod_DetoxCycle` and `BiosculpterPatches` need a spawned pod and the gizmo system.
  The gizmo postfix is the partial exception. Every decision it makes, including how it splits the
  pod's cycles into the detox cycle and the rest, lives in `DetoxCommandGate`, which takes plain
  values and delegates, so `DetoxCommandGateTests` runs all of it against uninitialised commands and
  textures. What is left to in-game check 14 is the postfix's readings of the pod: the type test
  and the icon it hands the split, the biotuned pawn, and the two methods that build the row's
  reason.
- Harmony cannot patch on this runtime at all, in or out of the game process. What it decides by
  name before patching is reachable, though, and `PatchTargetTests` checks both halves of every
  name: the game's (the target, its overloads, the private fields) and the mod's. It resolves each
  patch class's `[HarmonyPatch]` the way Harmony does, on declared members only, and binds every
  `__instance`, `__result`, `___field` and argument parameter by name. So a renamed parameter, a
  retargeted attribute or a deleted one fails here; in game the first two throw from `PatchAll`
  and the third silently leaves the patch unapplied. It also pins the gizmo patch as the last class
  applied.

What was reachable, and where the value turned out to be, was the mod's hardcoded defNames. The
tests read the game's own shipped def XML directly rather than going through `DefDatabase`, which
only a running game populates, and check each name against `HediffDef` **specifically**.

That type restriction is the whole point, and there is a test for the trap itself
(`TheUntypedCheckWouldCertifyTheDeadWithdrawalListAsHealthy`). Six of the eight withdrawal names do
exist in the game's global defName set, as `ThoughtDef`s. A validation test written against every
defName rather than against `HediffDef` would pass on that list and report a dead list as healthy.

## What the def checks found

All 17 hardcoded names across the two detoxifiable lists were dead:

- `DetoxifiableAddictions` spelled every entry `Addiction_Alcohol`. The real hediffs are
  `AlcoholAddiction`: suffix, not prefix, and no underscore. Nine entries, none of them matched.
- `DetoxifiableWithdrawals` had eight entries. Six exist as `ThoughtDef`s and two
  (`FlakeWithdrawal`, `YayoWithdrawal`) do not exist at all.
- There is no withdrawal `HediffDef` in RimWorld 1.6 at all. Withdrawal is modelled purely as a
  thought, so the `EndsWith("Withdrawal")` fallback could not fire either, and the withdrawal half
  of the mod never removed anything.

The mod worked in spite of this, because `EndsWith("Addiction")` caught all seven real addiction
hediffs, and `NonDetoxifiableAddictions` correctly excluded `LuciferiumAddiction`.

Those tests pinned that state rather than asserting what ought to be true, so that fixing it would
turn a test red instead of changing nothing visible, and it did. The three lists and every defName
match were then deleted: an addiction is now `Hediff_Addiction` plus the game's own
`everCurableByItem` flag, and a tolerance is known by its `DrugEffectFactor` comp or by a chemical
naming it. `DefValidationTests` keeps the old names as history. It asserts that none of them has
ever been a `HediffDef`, that the three lists cannot come back
(`TheDefNameListsAreGoneAndMustStayGone`), and that the shipped `LuciferiumAddiction` still sets
`everCurableByItem` false, which is the premise the luciferium exclusion rests on.

## In-game checks owed after the 2026-09-18 issue sweep

Every issue in this repo was closed on 2026-09-18, and the harness cannot run any of it: every
entry point takes a `Pawn`, and Harmony cannot patch on this runtime at all. What the tests cover
is the predicates and the patch targets. The checks below are the parts that are still a claim.

**Results.** Checks 1 to 13 were run in game on 2026-09-18 against `21c7756` (RimWorld 1.6.4871,
dev and god mode) and all thirteen passed, including 3, 7 and 8 in Polish, German and Russian, and 5
against a save written by the August 2025 Workshop build. Check 14's premise was found that day as
a live `NullReferenceException`. On 2026-09-19, against the build that ships (`2158317` installed by
`build.sh`, in a fresh session so the shared `Log.ErrorOnce` key was unspent), check 14 passed: the
refilled biotuned button was greyed with the refusal, a click logged nothing, and a tolerance
re-enabled it while paused, in English and Polish. Its two optional bullets were not run. Check 15
passed: the Russian strings read "цикл детоксикации", and a female colonist's letters in Russian
and Polish carried no masculine verb. The reworded refusal and the bracket-free "Cured:" line were
read in the same session.

### Setting up

Every procedure below assumes this. It was read out of the decompiled pod and checked against the
game's shipped English strings.

- Run the local copy, not the Workshop subscription. `ModsConfig.xml` activates the bare package
  id, which resolves to the copy in `Mods/`; the Workshop one is suffixed `_steam`. With dev mode
  on, the log's `[BiosculpterDetox] Loaded version` line names the build. The Workshop 1.0.1 build
  fails several of these checks in ways that look like real defects.
- Turn on dev mode and god mode. The pod's DEV buttons and the Health tab's red X ("DEV: Remove
  hediff") need both, since `DebugSettings.ShowDevGizmos` is `Prefs.DevMode && godMode`, and god
  mode also finishes a placed building at once.
- Debug actions > General > "Finish all research". Without Bioregeneration the detox cycle reports
  "Missing required research Bioregeneration".
- Build the pod (Architect > Ideology > biosculpter pod) outdoors if the numbers matter. Outdoors
  its speed factor is always 0.7, so the "Begin detox cycle" tooltip reads "Duration: 17.1 days"
  unbiotuned and 13.7 biotuned. Indoors the factor follows room cleanliness, and every completed
  cycle drops pod slime that moves it again. Only checks 9 and 10 depend on a pod's duration, and
  both compare against their own baseline. Power the pod from a wood-fired generator within six
  cells, fuelled with the generator's "DEV: Set fuel to max", or from a conduit with the debug
  window's "Unlimited Power" setting on. The countdown only runs while the pod has power.
- Press "DEV: fill nutrition and cycle ingredients" on the pod before every cycle and before every
  right-click test, and look for "Ready for cycle selection" in its pane. Entering and ejecting both
  empty it. An empty pod answers "Nutrition not loaded", and since this mod's reason yields to any
  reason vanilla already gave, that hides the one under test.
- Add a condition with Debug actions > Pawns > "Add hediff": pick the entry, click the pawn, then
  "(no body part)". The Health tab's "Dev tool..." button has the same list, and also "Hediff debug
  tooltips" and "Show hidden Hediffs". Clear a test pawn's existing drug conditions with the red X
  first, because starting colonists can roll one. A tolerance arrives at severity 0.5, its "(large)"
  stage, and drops to "(small)" within 200 ticks. That bracket is the Health tab's; the "Will
  treat:" line and the "Cured:" letter name the condition without it.
- In English, this mod's refusal reads "Nothing this cycle can treat". Its nothing-detected line,
  which the tooltip of a pod biotuned to a pawn with nothing to treat shows, reads "There is nothing
  this cycle can treat, so it cannot be started." The checks below call these the refusal and the
  nothing-detected line. The letters are the blue "Detox Treatment Complete" and the grey "Detox
  Complete: Nothing to Treat".
- "Begin detox cycle" is the last of the pod's five cycle buttons, after medic, bioregeneration, age
  reversal and pleasure, because the comp is appended after the four vanilla cycle comps. It is not
  the last gizmo: "Interrupt cycle" (while occupied), "Auto load nutrition", the copy and paste
  buttons, "Select <pawn>" and the DEV buttons all follow it.
- Whether the pod is biotuned changes what you see. Every completed cycle biotunes it to the
  occupant: the pane shows "Biotuned to: <pawn> (1.3 years)", every cycle button gains " (<pawn>)",
  and anyone else gets "Biotuned to someone else.". Unbiotuned, a cycle button opens a menu with one
  row per free colonist. Biotuned, it sends that pawn straight in, with the message "<pawn> will
  enter biosculpter and begin a detox cycle.". The per-pawn preview, a "Will treat:" line or the
  nothing-detected line, exists only on a biotuned pod, because vanilla builds the tooltip from
  `Description(biotunedTo)`. "DEV: complete biotune timer" clears the tuning within about ten
  powered ticks.
- To finish a cycle, wait for "Contains: <pawn>" in the pane, select the pod (entering reselects a
  selected pawn inside it), keep the game unpaused and press "DEV: complete cycle", which ends it
  within about a hundred ticks. "DEV: advance cycle +1 day" takes one day off instead. Both work
  for this mod's cycle, because vanilla owns the countdown and hands completion to the comp. Never
  use "Interrupt cycle", which skips completion and adds biosculpting sickness. "Increment time"
  does not tick the pod at all.
- Open the settings from the on-screen Menu button rather than Esc, which only deselects while
  anything is selected: Menu > Options > Mod options > Biosculpter Detox. Closing the window by any
  route pushes the duration to pods already built.
- One order reuses state well: 2 on a fresh pod, then the cycle in 1, then 14, the preview in 1,
  and 6, all on the same pod and pawn. Restart RimWorld before any session that includes 14.

### From the issue sweep

1. **A pawn whose only treatable condition is a drug tolerance.** Confirm the cycle is offered, that
   its description says it will treat the tolerance, and that the completion letter names what was
   removed. That whole chain was the subject of issue #1 and every step of it used to disagree with
   the others. Pawn X gets "Psychite tolerance" and no addiction: an addiction would let every step
   pass for the wrong reason, and a dev-added tolerance is all the check needs.
   - Offered. On an unbiotuned, filled pod, click "Begin detox cycle". X's row is clickable, and any
     colonist with nothing to treat is greyed as "<name>: <the refusal>". Select X alone and
     right-click the pod: "Enter biosculpter pod for detox cycle (<n> days)". Take it and finish
     the cycle.
   - Named. A blue "Detox Treatment Complete" letter reading "Cured: Psychite tolerance", with no
     stage in brackets, and X's Health tab without it, nothing else changed.
   - Described. The preview needs a pod biotuned to X, which that cycle leaves behind. Give X the
     tolerance again, refill, and hover "Begin detox cycle (<X>)": its tooltip reads "Will treat:
     Psychite tolerance", named exactly as the letter named it. Carry straight on into check 6.

   It fails if X is refused, if the biotuned tooltip shows the nothing-detected line while X has the
   tolerance, if the letter is the grey one or never arrives, if the "Cured:" line is empty or names
   something else, or if either list carries a stage or a percentage in brackets. An unbiotuned
   tooltip with no "Will treat:" line is expected.
2. **A pawn with nothing to treat.** Confirm the detox option is disabled with a reason rather than
   hidden, in the menu the "Begin detox cycle" button opens AND in the right-click float menu, since
   `CannotUseNowPawnCycleReason` feeds both. Use an unbiotuned, filled pod and a pawn A with no
   addiction, tolerance or "Luciferium need"; a Biotech "... dependency" row is fine, because it is
   never treatable. Click "Begin detox cycle": A's row is greyed as "<A's name>: <the refusal>".
   Select A alone and right-click the pod: a greyed "Cannot start detox cycle: <the refusal>". The
   button itself stays enabled, and that is vanilla rather than this mod:
   `SelectPawnsForCycleOptions` adds a refused pawn's greyed row and counts it as eligible, so the
   button greys only for a pod-wide reason or when no free colonist is on the map. Check 14 covers
   the biotuned button. The old gate could never run at all.
3. **The same, in a non-English language.** The old gate matched a translated label, so it was false
   in eight of the nine. Polish is the sharpest test: its label is "detoks", which does not even
   contain the substring the old code looked for.
4. **A luciferium-addicted pawn.** Confirm the addiction survives the cycle, and that any ordinary
   addiction on the same pawn does not. With "Treat permanent addictions" off, give the pawn both
   "Luciferium need" (not "Luciferium", which is the high) and "Alcohol addiction". Luciferium alone
   is refused at the gate, which is right but says nothing about the cycle.
5. **A save with a cycle already running, loaded after this update.** This is the migration, and it
   is the one with real downside: the cycle key was renamed, and a save holding the old key that is
   not migrated throws out of the pod's tick every tick and traps the occupant. Start a detox on the
   previous build, save mid-cycle, load on this one, and confirm the cycle continues.
6. **A completion of each kind.** One cycle that removes something and one that removes nothing,
   confirming a letter arrives both times and that the second is neutral rather than bad news. The
   first is check 1's cycle. The second needs a recipe, because the gate will not start a cycle for
   a pawn with nothing to treat, and waiting in the pod for a tolerance to decay does not work: the
   occupant is suspended and never ticks. On the pod still biotuned to X, with X carrying the
   tolerance from check 1's preview, refill. Pause, and give the order while X still has it, with
   "Begin detox cycle (<X>)" or by right-clicking. Still paused, remove the tolerance with the red
   X on X's Health tab. Unpause: X walks in regardless, because neither the enter job nor the pod
   asks the gate again. Finish the cycle. Expect the grey "Detox Complete: Nothing to Treat", whose
   text says X "had nothing the cycle could treat" (`NeutralEvent`, the plain arrival sound), never the
   yellow `NegativeEvent` with its urgent
   sting, and a normal exit with no biosculpting sickness. If X is already inside by the time you
   reach the Health tab, the pod's "Select <X>" button should reach it; that route is read from the
   code and has not been tried.

### Added 2026-09-18 with the settings window

Nothing in this repo can open a settings window: `DoSettingsWindowContents` needs IMGUI. So the
values and the serialisation are covered by `SettingsTests` and the drawing is not covered at all.

7. **The settings window opens and draws.** Confirm the mod appears in the options list under its
   own name in a non-English language, that the checkbox, the slider and the reset button all
   render inside the window rather than off the bottom, and that both tooltips appear on hover.
   This is the half no test reaches.
8. **The duration slider at its minimum.** Confirm it reads "1 day" and not "1 days". The label
   defers to the game's own `ToStringTicksToDays`, so check one inflecting language too: German
   should say "1 Tag" against "2 Tage", and Russian should give "1 день", "2 дня", "5 дней".
9. **A duration change reaching pods already built.** Change the slider, close the window, and
   confirm an existing pod's float menu reports the new length WITHOUT a save and load. Then
   uninstall a pod, change the duration, reinstall it, and confirm it also picks up the new length:
   that path keeps the comp through the minify, so it is refreshed rather than rebuilt.
10. **A duration change while a pod is running.** Confirm the occupant finishes at the length the
    cycle started with, and that the progress bar misbehaves in the documented way rather than any
    other way. The bar is `1 - Clamp01(ticksLeft / (liveDuration * 60000))`, recomputed every tick
    from the live setting, while the countdown was fixed at entry. So shortening pins the bar at
    empty for as long as the time left exceeds the new length, lengthening jumps it forward, and
    either way it fills when the original countdown ends while "Ends in" never moves. Count presses
    of "DEV: advance cycle +1 day", which takes one day off the countdown whatever the pod's speed:
    the press counts below hold at any speed factor, and only the "Ends in" figures scale. Zoom all
    the way in, since the bar draws only at the closest zoom, select the pod rather than the pawn,
    and unpause briefly after every press and every settings change, because the bar is only
    written on a tick.
    - Shortening. Slider at 12, a pawn with a tolerance, refill, enter. Press 4 times: the bar is
      about a third full. Set 3 and close: the bar drops to empty and "Ends in" stays where it was.
      It stays empty through press 9, reads about a third at press 10 and two thirds at press 11,
      and the pawn is ejected on press 12, the original length.
    - Lengthening. Refill (the completion emptied the pod), give the pawn the tolerance again, slider
      at 6, enter. Press twice: about a third. Set 12 and close: the bar jumps to about two thirds
      and "Ends in" stays where it was. Presses 3, 4 and 5 read about 75, 83 and 92 per cent, and the
      pawn is ejected on press 6.

    It fails if "Ends in" moves with the slider, if the pawn leaves after 3 presses in the first half
    or needs 12 in the second, if the bar keeps its old step after a change (the push missed the pod,
    which is really check 9), or if the bar overfills, runs backwards or logs errors from the pod's
    tick. The forward jump, and the tooltip showing the new length mid-cycle (it describes the next
    cycle), are not failures.
11. **The permanent-addiction toggle, both ways.** With it on, confirm a luciferium addiction is
    cured and that the cycle description says so. With it off, confirm it is not. Check the
    description updates as soon as the setting changes rather than on the next load. The description
    is the "Begin detox cycle" tooltip, and its second paragraph states the policy ("Permanent
    addictions such as luciferium will be treated.", or "will not be treated."). Three things shape
    the procedure. With the toggle off, a pawn whose only condition is luciferium is refused
    outright, so "not cured" shows up as a refusal rather than as a cycle that leaves it alone. The
    "Will treat:" list appears only on a pod biotuned to that pawn. And the setting is read when the
    cycle completes, not when it starts.
    - Off, on a pod biotuned to nobody, so that "Biotuned to someone else." cannot stand in for the
      refusal ("DEV: complete biotune timer" clears it). Pawn A has only "Luciferium need". The
      tooltip says "will not be treated", and A gets the refusal on right-click.
    - On, with no save and load. The sentence flips on the next redraw. A is now offered: send A in
      and finish the cycle. The blue letter's "Cured:" line names the luciferium need, and both the
      Health tab row and the Needs tab's Luciferium bar are gone.
    - Off again, on the pod now biotuned to A. Give A "Luciferium need" and "Alcohol addiction",
      refill, and with the toggle still on hover "Begin detox cycle (<A>)": "Will treat:" names both.
      Turn the toggle off and hover again: the policy sentence and the list both drop luciferium.
      Click it and finish the cycle. The letter cures the alcohol addiction alone, and the luciferium
      need and its bar stay.

    It fails if either tooltip change waits for a save and load or a map change, or if a letter
    disagrees with the setting in force when the cycle ended. Having to reselect the pod after
    closing the options is not a failure.
12. **A pawn with a Biotech chemical dependency, toggle ON.** Confirm the dependency is NOT removed.
    A wrongful removal would not kill the pawn, and nothing would put it back either:
    `Gene_ChemicalDependency` has no tick, and recreates the hediff only when the gene is added, when
    the pawn next takes the drug, or when the gene tracker is reset. So the failure is silent: the
    dependency stops progressing, with no deficiency, no mood penalty and no drug-seeking until the
    next dose, and the pawn keeps the gene's metabolism bonus. The hediff still being there
    afterwards is the whole check.
    - Pawn B must be biologically 13 or older, because the gene does nothing younger. Debug actions >
      Pawns > "Add gene" > "Xenogene" > "ChemicalDependency_GoJuice", then click B. Go-juice because
      a new colony rarely has any, so nothing can recreate the hediff through a dose mid-check. Do
      not use "Add hediff" > "Chemical dependency" instead: with no gene behind it, the game removes
      it by itself.
    - Unpause for about ten seconds, since the hediff starts invisible at severity 0. B's Health tab
      then shows "Go-juice dependency", which is the name the player sees; `GeneticDrugNeed` is only
      its defName. Turn on "Hediff debug tooltips" and note the severity.
    - Turn the toggle on and refill a pod that is not biotuned to another pawn. Select B alone and
      right-click it: the detox cycle gets the refusal, because the dependency is B's only
      condition.
    - Give B "Alcohol addiction", send B in and finish the cycle. The letter's "Cured:" line names
      the alcohol addiction and nothing else. "Go-juice dependency" is still on the Health tab at the
      same severity or a hair above it, and the gene is still on the Genes tab. Turn on "Show hidden
      Hediffs" before concluding the hediff has gone.

    Three unit tests cover the predicate, the class graph and the gene having no tick of its own,
    but none of them runs against the shipped hediff on a real pawn.
13. **A config file with an unparseable duration.** Hand-edit
    `Config/Mod_*_BiosculpterDetoxMod.xml` to something like `<cycleDurationDays>12 days</cycleDurationDays>`
    and confirm the cycle is 12 days rather than 1, with a red Scribe error in the log. The unit
    test cannot reach this: that path calls `Log.Error`, which throws in the harness.

### Added 2026-09-18 after the first in-game pass

14. **The biotuned button when its pawn has nothing to treat.** After a successful detox the pod is
    biotuned to a pawn who has nothing left, so this is the normal state after every success, and it
    used to throw. The button stayed enabled, a click did nothing, and one `NullReferenceException`
    from `CompBiosculpterPod+<>c__DisplayClass91_0.<CompGetGizmosExtra>b__0` reached the log. Restart
    RimWorld before this check: `GizmoGridDrawer` reports a gizmo's exception through
    `Log.ErrorOnce` on one key that every gizmo in the game shares, and `Log.Clear` does not reset
    it, so after the first such error in a session every later one is silent and a clean log proves
    nothing.
    - After the cycle in check 1, refill the pod still biotuned to X, and confirm X has nothing
      treatable. "Begin detox cycle (<X>)" is greyed, and its tooltip shows the nothing-detected
      line and ends in the red "Disabled: <the refusal>", the same reason the right-click menu gives
      X for the detox cycle. Refilling matters: an empty pod greys the button for "Nutrition not
      loaded" and would pass this for the wrong reason. The other four cycle buttons are unaffected
      here and in every step below.
    - Click it. Nothing happens beyond the message "Disabled: <the refusal>" at the top of the
      screen, and no red error appears.
    - Pause, and give X the tolerance again: the button enables at once, without unpausing. Hover it
      for check 1's preview rather than clicking; the click that sends X straight in belongs to
      check 6.
    - Optional, still paused and with the tolerance on X: wall X off from the pod in god mode. The
      button greys again, now with "Disabled: No path", which is what X's own row would say. Before
      this change the same click threw for the same reason. Remove the wall afterwards.
    - Repeat the first two bullets in Polish. The button is found by its icon, never by its label,
      so it must grey the same way there.
    - Optional: select two pods biotuned to X together. Their detox buttons group into one, which
      is greyed, and clicking it does nothing beyond the message.

### Added 2026-09-19 after review

15. **The Russian cycle name, and the Russian and Polish letters.** Vanilla's Russian pod strings
    put the cycle name after "цикл" in the genitive, through the game's word table, and the game
    has no entry for this mod's label. The mod ships one in
    `1.6/Languages/Russian/WordInfo/case.txt`. First confirm the installed copy has
    `Mods/BiosculpterDetox/1.6/Languages/Russian/WordInfo/case.txt`, since the staging sweep
    deletes every other non-XML file under `Languages`.
    - Russian selected, on an unbiotuned, filled pod. The button reads "Начать цикл детоксикации",
      the right-click option "Войти в биоскульптор для цикла детоксикации (...)", and for a pawn
      with nothing to treat "Невозможно начать цикл детоксикации: ...". Judge the detox strings
      only; the other four cycles are vanilla's.
    - With a female pawn, repeat check 1's cycle and check 6's no-op, in Russian and then in Polish.
      Each letter opens "<name>: " and nothing in it agrees with the pawn's gender. Both letters
      were reworded without a native reader, so ask one whether they read naturally.

    It fails if "детоксикация" follows "цикл" in any detox string, if the table is missing from the
    installed copy, or if a letter still carries a masculine verb for a female pawn.

A dev-mode `[DebugAction]` printing what `PerformDetox` would remove from the selected pawn, without
removing it, would make 1, 4, 11 and 12 cheap. It does not exist yet.
