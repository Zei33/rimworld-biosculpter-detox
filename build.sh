#!/bin/bash
set -e

export FrameworkPathOverride=/opt/homebrew/opt/mono/lib/mono/4.7.2-api

if [ -z "${RimWorldDir}" ]; then
  echo "RimWorldDir is not set. Export it to your RimWorld install before building." >&2
  exit 1
fi

rm -Rf release
dotnet build rimworld-biosculpter-detox.sln -c Release
mkdir -p release

cp -r About release/About

# Stage 1.6 by naming what ships, rather than copying it wholesale and deleting
# afterwards. `cp -r 1.6 release` put six C# source files and the vendored Harmony
# reference into every subscriber's mod folder, none of which the game reads. An
# allow-list is also the shape that stays correct when a folder is added: a new
# source folder is simply not staged, where a deny-list ships it until somebody
# notices.
mkdir -p release/1.6/Assemblies/net472

# Only our own assembly. Anything else here is a build artefact or a game assembly
# RimWorld already has loaded, and ModAssemblyHandler.ReloadAll loads every .dll at
# any depth, filtered on extension alone, so shipping one is not inert. The vendored
# 0Harmony.dll under Libraries/ is reference-only and is never staged.
cp 1.6/Assemblies/net472/BiosculpterDetox.dll release/1.6/Assemblies/net472/

cp -r 1.6/Languages release/1.6/Languages
cp -r 1.6/Textures release/1.6/Textures

# No Defs folder and no Patches folder, and the second is the one worth stating.
# This mod's 1.6/Patches holds a single C# file, the Harmony patch class. RimWorld
# globs *.xml from a folder named exactly Patches, so there is nothing there for the
# game to read and the folder must not be staged. Simple Improve's build.sh does copy
# its Patches/*.xml, because that mod really does ship PatchOperations; do not copy
# that line across without checking which kind this mod has.

# Both Languages and Textures carry a developer README that has been shipping to
# subscribers. Keep only what the game loads.
find release/1.6/Languages -type f ! -name '*.xml' -delete
find release/1.6/Textures -type f ! -name '*.png' -delete

rm -Rf "${RimWorldDir}/Mods/BiosculpterDetox"
cp -r release "${RimWorldDir}/Mods/BiosculpterDetox"
rm -Rf release
