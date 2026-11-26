# Working on this repo

## The default branch is the release

The owner installs and updates the SloppyMods mods straight from GitHub: RimSort
clones this repository's default branch (`main`) into RimWorld's Mods
folder and pulls it in place. There is no separate release step, so **every
commit pushed to `main` is what the game loads next**.

- Push finished work to `main`. Nothing half-done: each commit must load
  and play as-is.
- **Snapshot every build on its own branch.** Every commit carries a build
  number (`0.9.<commit count>`, stamped below). Push the same commit to
  `main` AND to a branch named after that build, so any build can be
  restored:

      git push origin HEAD:main HEAD:refs/heads/build/0.9.N

  Build branches are restore points: never move or delete one. To roll back,
  reset `main` to an earlier `build/...` branch - only when the owner asks.
  The release workflow only runs on `main`, so build branches publish nothing.
- **Every update carries its change notes** as BBCode (the Steam Workshop
  change-notes format), in `Changelog/<build>.bbcode`, committed in the same
  commit as the change. Write them for players: what was added, changed and
  fixed, not how. Format:

      [h2]Build 0.9.N[/h2]
      [h3]Added[/h3]
      [list]
      [*]...
      [/list]

  Use only the sections that apply (Added, Changed, Fixed, Removed).
- **Commit the compiled assembly** `Assemblies/AgriculturalStatistics.dll`
  (`.gitignore` un-ignores exactly that file). RimWorld loads the DLL, not the
  source. Rebuild and commit it in the same commit as any C# change.
- **Stamp the build number before every commit**: `<modVersion>` in
  `About/About.xml`, and the `Build x.y` line at the top of its description,
  set to `0.9.<commit count after this commit>` (`git rev-list --count HEAD` + 1;
  needs a full, not shallow, clone). Never leave a placeholder.
- Keep `packageId` (`sloppymod.agriculturalstatistics`) unchanged: saves and
  RimSort key on it. The display `<name>` is "SloppyMods Agricultural Statistics".
- Def names carry the `AgriStats_` prefix; translation keys too. The C#
  namespace is `AgriculturalStatistics`.
- Everything in the repository root lands in the Mods folder, so keep anything
  RimWorld might try to load deliberate; `Source/` and docs are ignored by the game.

## Building

`bash .github/release.sh --no-release` compiles the assembly from source with
mcs against Krafs' RimWorld 1.6 reference assemblies (zip in `dist/`), the same
way the release workflow does on every push to `main`. The script writes the
DLL into `Assemblies/` itself.

- mcs is stricter than Roslyn: no pattern matching beyond `as`, no
  expression-bodied members, `out` variables declared beforehand.
- The assembly targets RimWorld 1.6 and runs unchanged on 1.7, including the
  owner's 1.7 Waylith build (no Unity; UnityEngine types come from UnityStubs).
  When touching game API, check the members still match the 1.7
  Assembly-CSharp: same names and signatures, since a changed return type is a
  MissingMethodException at run time.

## This mod

- One main tab (`MainTabWindow_Farming`), one calculator (`FarmCalc`), mod
  settings. No Harmony, no dependencies, nothing saved in games.
- The numbers come from the game's own data (plant defs, recipes, the nutrient
  paste dispenser, pawns' hunger rates); keep it that way so modded crops, meals
  and races work without patches.
- Nothing here has automated tests. Say plainly when a change compiles but has
  not been run in game.

## Standing preferences

- Licence is CC0 for every SloppyMods mod.
- Replies to the owner: concise summaries. Don't attach build zips: the owner
  installs from GitHub (RimSort), so a pushed build with its release is enough.
