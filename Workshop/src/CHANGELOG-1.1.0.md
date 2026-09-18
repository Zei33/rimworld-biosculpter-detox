# Biosculpter Detox 1.1.0

Not a store page. This is the text to post as a Workshop comment when 1.1.0 goes up,
because the in-game uploader hardcodes its own change note and cannot write one.

---

**Biosculpter Detox 1.1.0**

The first update since August 2025.

New

- The cycle length is now a setting. It is still 12 days by default and can be set anywhere
  from 1 to 30 days. A cycle already running keeps the length it started with.
- An option to treat permanent addictions, luciferium among them. It is off by default, and
  luciferium is the only permanent addiction the base game has.

Fixed

- The mod decided what counted as a drug addiction by matching names, which was wrong in both
  directions. It could remove things that were not drug addictions at all, including Anomaly's
  cube withdrawal, and it relied on other mods happening to name their hediffs the way the base
  game does. It now asks the game, so any mod's drug addiction is covered and nothing else is
  touched.
- The detox cycle is now offered where the game asks whether a cycle is available, rather than
  where its button is drawn.
- After a detox finished, the pod's detox button for that same colonist did nothing when
  clicked and logged an error. It is greyed out now, with the reason.
- The reason given when there is nothing to treat said "No addictions to treat", which was
  wrong for a colonist whose only addiction is one the mod deliberately leaves alone. It
  reads "Nothing this cycle can treat" now.
- The list of what will be treated, and the letter afterwards, named each condition with its
  live recovery percentage or stage attached, so the same thing could be named two different
  ways in the same cycle. Both use the plain name now.
- The Russian strings now decline the cycle's name the way the game's own do, and the Russian
  and Polish letters no longer assume the colonist is male.

Corrected

- The store page has said since August 2025 that time in the pod depends on the severity of the
  addiction. It never did. The cycle has always been a fixed length, and it is now a setting.
- The page also said luciferium tolerance was left alone. There is no such thing in RimWorld;
  only five drugs have a tolerance and luciferium is not one of them.
- The page said anything that is not a drug "dependency" is untouched. A dependency is what
  Biotech's genes give a pawn, and those are deliberately never treated, because removing one
  kills the pawn. So the sentence promised the one thing the cycle refuses to do. Six of the
  nine languages had it. It names drug addictions and tolerances now, which is what the cycle
  actually removes, and no page mentioned tolerance at all before.
