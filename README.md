# Rim Languages - Late Factions

Compatibility patch for [Cybranian - Rim Languages](https://steamcommunity.com/sharedfiles/filedetails/?id=3450332059) (`DimonSever000.FactionLanguages`), RimWorld 1.7.

## Problem

Rim Languages gives every faction a language dialect **once**, when the world is created. A faction that appears later never gets one: Hegemony splinters ("Freed Slaves of X", "X Rebels"), quest and raid expeditions, and any faction added by a mod after the save was started. The base mod has no UI to assign a dialect, and its author lists mid-game faction changes as unsupported.

## What this does

Gives those factions a dialect, using Rim Languages' own public API, so the saved data is the same as the mod would have written:

1. **On load** it finds every faction without a dialect and assigns one.
2. **When a faction is added** (a Harmony postfix on `FactionManager.Add`) it flags it, and the next tick assigns one.

The dialect is picked in this order:

1. **Parent by name.** The longest other faction name contained in the new faction's name (`Freed Slaves of Comboa Treaty` takes Comboa Treaty's dialect; `Rasyttbar expedition` takes Rasyttbar's).
2. **Same faction def.** Another faction of the same `FactionDef` that already has one.
3. **A new dialect** (`Human_Standard` or the alien equivalent) under an existing language of the same race and tech level.

Factions with no speaking race (hive, mechanoids, entities, abstract factions) are left alone, as in the base mod. Each assignment is logged as `[LateFactions] <faction> -> <dialect> (<how>)`.

It also repairs dialects an earlier build saved without a parent language, which would crash the faction tab.

## Requirements

- RimWorld 1.7
- Harmony
- Cybranian - Rim Languages

Load after both. Remove it any time: the dialect data it wrote is ordinary Rim Languages data.

## Build

Needs the .NET 10 SDK and the game plus Rim Languages and Harmony installed (paths in `Source/LateFactions.csproj`):

```
cd Source
dotnet build -c Release -v q
```

The DLL is written to `1.7/Assemblies/LateFactions.dll` and is tracked in git.

## Layout

```
About/About.xml
About/Preview.png
LoadFolders.xml
1.7/Assemblies/LateFactions.dll
Source/LateFactionAllocator.cs   GameComponent: load pass, repair pass, dialect choice
Source/Hook.cs                   Harmony postfix on FactionManager.Add
Source/LateFactions.csproj
```

## Limits

- Not tested outside one large modded save.
- New dialects get generated names; you can't pick one.
- Creating a dialect writes private fields of Rim Languages' `Dialect` by reflection, so a Rim Languages update that renames them will need this updated.
- The first load after removing this mod logs one harmless `Could not find class LateFactions.LateFactionAllocator` error.

## AI disclosure

This mod was created using AI tooling.

## License

MIT, see `LICENSE`.
