#!/bin/bash

export FrameworkPathOverride=/opt/homebrew/opt/mono/lib/mono/4.7.2-api

rm -Rf release
dotnet build rimworld-biosculpter-detox.sln -c Release
mkdir -p release

cp -r About release/About
cp -r 1.6 release
rm release/1.6/Assemblies/net472/Unity*
rm release/1.6/Assemblies/net472/Assembly*
rm release/1.6/Libraries/0Harmony.dll

rm -Rf "${RimWorldDir}/Mods/BiosculpterDetox"
cp -r release "${RimWorldDir}/Mods/BiosculpterDetox"
rm -Rf release