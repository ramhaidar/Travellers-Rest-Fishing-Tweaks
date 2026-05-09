# Travellers Rest Fishing Tweaks

A BepInEx mod for Traveller's Rest that adds fishing tweaks, quality-of-life options, and optional automation.

## Notes

Thanks to the original projects this update is based on:

* [Skyppid/TravellersRestFishingTweaks](https://github.com/Skyppid/TravellersRestFishingTweaks)
* [DrStalker/TravellersRestFishingTweaks](https://github.com/DrStalker/TravellersRestFishingTweaks)

Latest tested Traveller's Rest version: **v0.7.5.3.0**

Latest tested BepInEx version: **v5.4.23.5**

If you want to build the mod locally yourself, change the **GameInstallDirectory** variable in the `.csproj` file to point to your local copy of the game.

## Current features

The following settings are created in `BepInEx\config\net.bepinex.trvlrest.travellers-rest-fishing-tweaks.cfg` after starting the game once with the mod installed.

| Config key | Default | Description |
| --- | ---: | --- |
| `Quick Bites` | `true` | Reduces time before bites and removes fake bites. Default: `true`. |
| `Instant Catch` | `true` | Instantly completes the catch after a real hook, skipping the fishing minigame. Default: `true`. |
| `Quick Progress` | `true` | Makes fishing minigame progress fill faster while you hold the fishing input. Default: `true`. |
| `Quick Progress Amount` | `0.15` | Progress added per second while `Quick Progress` is enabled. Values below `0` are treated as `0`. Default: `0.15`. |
| `Quick Progress On Miss` | `false` | If enabled, Quick Progress still increases while you hold the fishing input even when the fish is outside the target box. Default: `false`. |
| `No Bar Decrease` | `true` | Prevents fishing minigame progress from decreasing over time. Default: `true`. |
| `Dont use bait` | `false` | Prevents bait from being consumed while fishing. Default: `false`. |
| `Auto Reel` | `false` | Automatically reels in when a real bite occurs. If `Instant Catch` is enabled, the fish is caught immediately. Default: `false`. |
| `Auto Recast Rod` | `false` | Automatically recasts the selected fishing rod after a catch finishes. Cast manually once with the rod selected to start the session. To stop it, switch away from the fishing rod on the action bar. When enabled, the mod also handles reeling so the recast loop can continue. Default: `false`. |
| `Auto Recast Rod Player` | `1` | Player number used by Auto Recast Rod. Use `1` for the local single-player character. Supported values are `1` to `2`. Default: `1`. |
| `Auto Recast Rod Delay` | `1.25` | Seconds Auto Recast Rod waits between recast attempts. Values below `1.25` are treated as `1.25`. Default: `1.25`. |
| `Remove Recast Delay` | `false` | Removes the extra post-catch delay so you can cast again sooner after rewards are granted. Default: `false`. |
| `Debug Logging` | `false` | Writes additional diagnostic information to the BepInEx log and console. Default: `false`. |

## Downloading the mod

Mods are available on [Nexus Mods](https://www.nexusmods.com/travellersrest), or you can download releases from [GitHub](https://github.com/ramhaidar/Travellers-Rest-Fishing-Tweaks/releases).

## How to install mods

* Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) for Windows x64. The latest tested version for this mod is `v5.4.23.5`.
* Start the game, then quit after it finishes loading.
* This creates the BepInEx config file and plugins folder.
* Optional: enable the BepInEx console. See the detailed guide or BepInEx documentation for steps.
* Copy the mod `.dll` to the BepInEx `plugins` directory.

## How to change mod settings

* Install the mod and start the game once.
* BepInEx will create `BepInEx\config\net.bepinex.trvlrest.travellers-rest-fishing-tweaks.cfg` with default settings.
* Exit the game, edit the config file, then restart the game.

## Is this mod safe to add/remove mid play-through?

Yes.

## Traveller's Rest Modding Guide

[DrStalker's notes on modding Traveller's Rest.](https://docs.google.com/document/d/e/2PACX-1vSciLNh4KgUxE4L2h_K0KAxi2hE6Z1rhroX0DJVhZIqNEgz2RvYESqffRl8GFONKKF1MjYIIGI5OKHE/pub)
