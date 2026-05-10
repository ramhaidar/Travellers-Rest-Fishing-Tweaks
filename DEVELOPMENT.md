# Development Guide

This document is for maintaining and building **Travellers Rest Fishing Tweaks**.

The public mod name is `Travellers Rest Fishing Tweaks`, and the internal plugin identity is `net.bepinex.trvlrest.travellers-rest-fishing-tweaks`.

## Project overview

- Game: **Traveller's Rest**
- Latest tested game version: **v0.7.5.3.0**
- Latest tested BepInEx version: **v5.4.23.5**
- Target framework: **netstandard2.1**
- Unity API package: **UnityEngine.Modules 2020.3.17**
- Solution file: `TravellersRestFishingTweaks.slnx`
- Project file: `TravellersRestFishingTweaks.csproj`
- Main plugin source: `TravellersRestFishingTweaks.cs`
- Runtime config file: `BepInEx\config\net.bepinex.trvlrest.travellers-rest-fishing-tweaks.cfg`
- Built plugin DLL: `net.bepinex.trvlrest.travellers-rest-fishing-tweaks.dll`

## Repository layout

```text
.
├── TravellersRestFishingTweaks.slnx      # Solution containing TravellersRestFishingTweaks.csproj
├── TravellersRestFishingTweaks.csproj    # Build configuration and game assembly references
├── TravellersRestFishingTweaks.cs         # Plugin implementation and Harmony patches
├── README.md                 # User-facing install/config documentation
├── DEVELOPMENT.md            # Developer notes for this repository
├── Modding-How-To.md         # DrStalker's Traveller's Rest modding notes snapshot
├── NuGet.Config              # NuGet feeds, including BepInEx feed
└── .gitignore                # Visual Studio/.NET ignores
```

Generated build folders such as `bin/` and `obj/` should stay untracked.

## Prerequisites

Install:

- .NET SDK
- Traveller's Rest on Steam/GOG/etc.
- BepInEx 5 for Windows x64 installed into the game folder
- An editor such as Visual Studio 2022, Rider, or VS Code

The project references Traveller's Rest assemblies directly from your local game install. The key assembly is `Assembly-CSharp.dll`; this contains the game's C# types such as `FishingController`, `FishingUI`, `Rod`, `PlayerInventory`, and related classes.

## Local game path

`TravellersRestFishingTweaks.csproj` contains a `GameInstallDirectory` property:

```xml
<GameInstallDirectory>E:\SteamLibrary\steamapps\common\Travellers Rest</GameInstallDirectory>
```

Change this value if your game is installed somewhere else.

The project currently derives these paths from `GameInstallDirectory`:

```xml
<GameFilesPath>$(GameInstallDirectory)\Windows\TravellersRest_Data\Managed</GameFilesPath>
<BepInExPath>$(GameInstallDirectory)\Windows\BepInEx\plugins</BepInExPath>
```

Some Traveller's Rest/BepInEx installs use this layout instead:

```xml
<GameFilesPath>$(GameInstallDirectory)\TravellersRest_Data\Managed</GameFilesPath>
<BepInExPath>$(GameInstallDirectory)\BepInEx\plugins</BepInExPath>
```

If the build cannot find game types such as `FishingUI` or `FishingController`, verify the actual location of `Assembly-CSharp.dll`, `Sirenix.Serialization.dll`, and `UnityEngine.UI.dll` in your game install and update `GameFilesPath` accordingly.

## Restore and build

From the repository root:

```powershell
dotnet restore "TravellersRestFishingTweaks.csproj"
dotnet build "TravellersRestFishingTweaks.csproj" -c Release --nologo
```

Or build the solution:

```powershell
dotnet build "TravellersRestFishingTweaks.slnx" -c Release --nologo
```

On successful build, the `CopyToPluginDir` target copies the built DLL to:

```text
$(BepInExPath)\net.bepinex.trvlrest.travellers-rest-fishing-tweaks.dll
```

If you do not want builds to deploy directly into the game folder, temporarily remove or disable the `CopyToPluginDir` target in `TravellersRestFishingTweaks.csproj` while doing local experiments.

## Running in game

1. Build the project in `Release`.
2. Confirm `net.bepinex.trvlrest.travellers-rest-fishing-tweaks.dll` exists in the game's `BepInEx\plugins` folder.
3. Start Traveller's Rest.
4. Check the BepInEx console or log for plugin startup output.
5. Start or load a save and test fishing behavior.

Useful log locations:

```text
<game folder>\BepInEx\LogOutput.log
<game folder>\BepInEx\config\net.bepinex.trvlrest.travellers-rest-fishing-tweaks.cfg
```

The plugin logs a startup line like:

```text
TravellersRestFishingTweaks: Plugin net.bepinex.trvlrest.travellers-rest-fishing-tweaks is loaded!
```

Enable `Debug Logging` in the config file for additional patch-resolution and runtime diagnostics.

## Configuration compatibility

Do not rename existing config sections or keys unless you are intentionally making a breaking release.

Current config keys are:

```text
[General]
Quick Bites
Instant Catch
Quick Progress
Quick Progress Amount
Quick Progress On Miss
No Bar Decrease
Dont use bait
Auto Reel
Auto Recast Rod
Auto Recast Rod Delay
Remove Recast Delay

[Debug]
Debug Logging
```

Notes:

- Keep `Dont use bait` as-is, including the missing apostrophe, to avoid resetting existing user configs.
- Keep the plugin GUID/config file identity as `net.bepinex.trvlrest.travellers-rest-fishing-tweaks`.
- Reordering `Config.Bind(...)` calls and improving descriptions is safe; renaming keys is not.
- If a future version needs cleaner names, add migration code or document it as a breaking config change.

## Code structure notes

`TravellersRestFishingTweaks.cs` is a single-file BepInEx plugin using Harmony patches and reflection.

Important areas:

- `Plugin` constructor: binds BepInEx config entries.
- `Awake()`: initializes logging, resolves game methods/fields, and installs Harmony patches.
- `PatchWithLogging(...)`: applies patches while logging missing targets safely.
- `ResolveMethod(...)`: resolves decompiled/obfuscated method names.
- Fishing minigame patches: quick progress, no decrease, quick bites, instant catch.
- Bait protection/refund logic: supports `Dont use bait`.
- Auto reel/recast logic: drives optional automation around bites, catches, and rod recasting.

Traveller's Rest contains compiler-generated or obfuscated method/field names that can change between game versions. Prefer resilient discovery by signature/type when possible, and keep debug logging useful for patch-target failures.

## Harmony and reflection guidelines

- Patch named methods when stable public names exist.
- For obfuscated aliases, use `ResolveMethod(...)` and tolerate missing targets.
- Avoid hard failing when a patch target is missing; a game update may remove or rename aliases.
- Prefer type/signature-based field discovery for compiler-generated fields.
- Avoid expensive reflection in per-frame paths unless cached.
- Keep prefix patches conservative: only return `false` when intentionally skipping the original method.
- Log enough context to diagnose game-update breakage, but guard noisy logs behind `Debug Logging`.

## Manual testing checklist

After code changes, test with a real game session when possible:

- Plugin loads without BepInEx errors.
- Config file is created/updated at `BepInEx\config\net.bepinex.trvlrest.travellers-rest-fishing-tweaks.cfg`.
- `Quick Bites` reduces bite wait and avoids fake bites.
- `Instant Catch` completes a catch after a real hook.
- `Quick Progress` fills progress faster during the minigame.
- `Quick Progress On Miss` behaves according to the config value.
- `No Bar Decrease` prevents progress decay.
- `Dont use bait` prevents bait loss or refunds bait correctly.
- `Auto Reel` reels in on real bites.
- `Auto Recast Rod` starts after manually casting once with a rod selected.
- `Auto Recast Rod` stops when switching the equipped action-bar slot away from the fishing rod.
- `Remove Recast Delay` lets the player cast again sooner after rewards are granted.

When testing automation, use a safe save and keep BepInEx logs open. Traveller's Rest does not have official mod support, so runtime verification matters more than compile success alone.

## Release checklist

Before publishing a release:

1. Confirm `README.md` matches current features, defaults, and tested versions.
2. Confirm `TravellersRestFishingTweaks.csproj` version is correct.
3. Build in `Release`.
4. Launch the game and verify the plugin loads.
5. Test the changed fishing behavior in-game.
6. Confirm the release DLL is named `net.bepinex.trvlrest.travellers-rest-fishing-tweaks.dll`.
7. Package only the DLL and any intended release notes; do not include `bin/`, `obj/`, logs, or local config files.

## Release script

Use `Release.ps1` from the repository root to build, zip, checksum, and publish a GitHub release.

The script prompts for a release version. Press Enter to use the default `1.0.0`.

```powershell
.\Release.ps1
```

You can also pass the version directly:

```powershell
.\Release.ps1 -Version 1.0.0
```

The script:

1. Builds `TravellersRestFishingTweaks.csproj` in `Release` mode.
2. Skips copying the DLL into the game plugin folder during release builds.
3. Zips `net.bepinex.trvlrest.travellers-rest-fishing-tweaks.dll` into `artifacts\TravellersRestFishingTweaks-v<version>.zip`.
4. Computes MD5 and SHA256 checksums for the DLL.
5. Creates a GitHub release with the zip attached and the checksum values in the release notes.

Requirements:

- `dotnet`
- GitHub CLI: `gh`
- Authenticated GitHub CLI session: `gh auth login`
- Local Traveller's Rest managed assemblies available through `TravellersRestFishingTweaks.csproj`'s `GameFilesPath`, or passed explicitly:

```powershell
.\Release.ps1 -Version 1.0.0 -GameFilesPath "E:\SteamLibrary\steamapps\common\Travellers Rest\TravellersRest_Data\Managed"
```

## Troubleshooting

### Build cannot find `FishingUI`, `FishingController`, or `UnityEngine.UI`

Verify `GameFilesPath` points to the folder containing:

```text
Assembly-CSharp.dll
Sirenix.Serialization.dll
UnityEngine.UI.dll
```

Then rebuild.

### Build succeeds but DLL is not copied to the game

Verify `BepInExPath` points to the actual game plugin folder. Depending on the install layout, it may be under either:

```text
<game folder>\Windows\BepInEx\plugins
```

or:

```text
<game folder>\BepInEx\plugins
```

### Plugin does not load

Check `BepInEx\LogOutput.log` for loader errors. Confirm the DLL is in the correct `plugins` folder and BepInEx 5 is installed for Windows x64.

### A feature stops working after a Traveller's Rest update

Enable `Debug Logging`, start the game, and inspect patch target resolution in `LogOutput.log`. Game updates may rename obfuscated methods or change method signatures, requiring new aliases or reflection logic.

## Related resources

- `README.md` for user-facing install and config instructions.
- `Modding-How-To.md` for DrStalker's Traveller's Rest modding notes.
- [BepInEx releases](https://github.com/BepInEx/BepInEx/releases)
- [HarmonyX documentation](https://github.com/BepInEx/HarmonyX/wiki)
