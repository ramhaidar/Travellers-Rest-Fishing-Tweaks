# Travellers Rest Modding How-To

Published using Google Docs

[Report abuse](https://docs.google.com/document/u/0/d/e/2PACX-1vSciLNh4KgUxE4L2h_K0KAxi2hE6Z1rhroX0DJVhZIqNEgz2RvYESqffRl8GFONKKF1MjYIIGI5OKHE/abuse) [Learn more](https://support.google.com/docs/answer/183965 "Learn more")

Travellers Rest Modding How-To

Updated automatically every 5 minutes

## Modding Travellers rest

> Note: this is a work-in-progress document. This is how I made things work; there are lots of different ways to do things so don’t feel forced to follow everything in here exactly.

### Who am I?

Someone who dislikes tedious repetition in games and enjoys modding as an additional way to interact with a game. [Github](https://www.google.com/url?q=https://github.com/DrStalker&sa=D&source=editors&ust=1778246260456570&usg=AOvVaw3cZq4Wj6ysuPynegsC6sen), [Steam Workshop](https://www.google.com/url?q=https://steamcommunity.com/id/drstalker/myworkshopfiles/&sa=D&source=editors&ust=1778246260456845&usg=AOvVaw0P5NBwwtSIHMhwOyqFaqZE), [Nexus Mods](https://www.google.com/url?q=https://next.nexusmods.com/profile/drstalker/mods&sa=D&source=editors&ust=1778246260456989&usg=AOvVaw3JZLRO1AN9xA4Ko6aIszBh).

## Overview

Traveller’s Rest has no official modding support, but it was made in Unity which is a well known game engine that has several generic modding tools available. This document describes how to use Bepinex, a general purpose Unity modding framework, to create and load mods in Traveller’s Rest.

## Backup your saves first!

Saves are located in:

```text
%userprofile%\appdata\locallow\Louqou\TravellersRest\
```

Make a copy of the GameSaves folder before playing with mods. You can also use the “duplicate” feature on the load game screen.

## How to install mods

1. Install Bepinex
2. Start the game, quit the game after it finishes loading
3. This will create a Bepinex config file and a plugins folder that you can put additional mods in
4. Enable the Bepinx Console
5. Copy additional mods to the plugins directory.

### Setup: Install BepinEx

I used version 5.4.23.2, the current latest stable version.

- [Project page including download instructions](https://www.google.com/url?q=https://github.com/BepInEx/BepInEx/&sa=D&source=editors&ust=1778246260459239&usg=AOvVaw1HeMhtUf5spfkcmdXPL_W_)
- [5.4.23.2 release](https://www.google.com/url?q=https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2&sa=D&source=editors&ust=1778246260459478&usg=AOvVaw2Akbgp05cXCOo93lmxg0aL) (this was the latest stable version as of July 2024)
- [Direct link to download for Windows](https://www.google.com/url?q=https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.2/BepInEx_win_x64_5.4.23.2.zip&sa=D&source=editors&ust=1778246260459853&usg=AOvVaw3QsUnGKV9cOLofmmDeJ8kV) (this was the latest as of July 2024)

### What if I am not playing on PC?

[This guide](https://www.google.com/url?q=https://steamcommunity.com/sharedfiles/filedetails/?id%3D3122526585&sa=D&source=editors&ust=1778246260460232&usg=AOvVaw0VVcUAERkS0ZRzLvo38E7r) covers how to set up Bepinex on a Steam Deck for a different game and several user have confirmed it worked for Traveller’s Rest as well. Note that you should use the exact version of Bepinex listed in the guide.

#### Install Instructions

[https://docs.bepinex.dev/articles/user\_guide/installation/index.html](https://www.google.com/url?q=https://docs.bepinex.dev/articles/user_guide/installation/index.html&sa=D&source=editors&ust=1778246260460947&usg=AOvVaw2ll6xQYy1T0AtSHCNegHaT)

Open the zip file and put all the files in:

```text
C:\Games\Steam\steamapps\common\Travellers Rest\Windows
```

or equivalent path if you have the game installed elsewhere.

#### Enable the Bepinex Console

Edit:

```text
<game folder>\BepInEx\config\BepInEx.cfg
```

Search for `Logging.Concole` and change `Enabled` to `True`.

```ini
[Logging.Console]

## Enables showing a console for log output.
# Setting type: Boolean
# Default value: false
Enabled = true
```

This isn’t strictly required, but it will let you see what is going on if you need to troubleshoot the game.

### Install additional mods

Copy the mods to:

```text
<game folder>\BepInEx\plugins\
```

Some mods are a dingle .dll file, some are a folder of multipel files.

Start the game, and once it has finished loading if the mod has a config file it will be created with default values in:

```text
<game folder>\BepInEx\config\
```

## If you just want to use mods, you can stop reading now.

The rest of this document is about how to configure a build environment and make mods.

### Install dotNet SDK

[https://dotnet.microsoft.com/en-us/download](https://www.google.com/url?q=https://dotnet.microsoft.com/en-us/download&sa=D&source=editors&ust=1778246260463674&usg=AOvVaw16fiNzp4Lub0YELLAkH30w)

### Install Bepinex templates

```powershell
dotnet new -i BepInEx.Templates --nuget-source https://nuget.bepinex.dev/v3/index.json
```

```text
D:\TR>dotnet new -i BepInEx.Templates --nuget-source https://nuget.bepinex.dev/v3/index.json

The following template packages will be installed:

    BepInEx.Templates

BepInEx.Templates is already installed, version: 2.0.0-be.1, it will be replaced with version .
BepInEx.Templates::2.0.0-be.1 was successfully uninstalled.
Success: BepInEx.Templates::1.4.0 installed the following templates:

Template Name                                 Short Name             Language  Tags
---------------------------------------       --------------------   --------  --------------------------------------
BepInEx 5 Plugin Template                     bepinex5plugin         [C#]      BepInEx/BepInEx 5/Plugin
BepInEx 6 .NET Launcher Plugin Template       bep6plugin_netfx       [C#]      BepInEx/BepInEx 6/Plugin/.NET Launcher
BepInEx 6 Il2Cpp Plugin Template              bep6plugin_il2cpp      [C#]      BepInEx/BepInEx 6/Plugin/Il2Cpp
BepInEx 6 Unity Mono Plugin Template          bep6plugin_unitymono   [C#]      BepInEx/BepInEx 6/Plugin/Unity Mono
```

### Create a project

```powershell
dotnet new bepinex5plugin -n MyFirstPlugin -T netstandard2.1 -U 2020.3.17
dotnet restore MyFirstPlugin
```

### Put some details in the `<pluginname>.csprojfile`

Make sure the AssemblyName you use is unique and will not conflict with any other mods.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
        <TargetFramework>netstandard2.1</TargetFramework>
        <AssemblyName>net.mynickname.bepinex.trtest2</AssemblyName>
        <Description>Travellers Rest Test Plugin 2</Description>
        <Version>1.0.0</Version>
        <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
        <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
        <PackageReference Include="BepInEx.Analyzers" Version="1.*" PrivateAssets="all" />
        <PackageReference Include="BepInEx.Unity" Version="6.0.0-*" IncludeAssets="compile" />
        <PackageReference Include="BepInEx.PluginInfoProps" Version="1.*" />
        <PackageReference Include="UnityEngine.Modules" Version="2020.3.17" IncludeAssets="compile" />
  </ItemGroup>

  <ItemGroup Condition="'$(TargetFramework.TrimEnd(`0123456789`))' == 'net'">
        <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.2" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

### Open the project in Visual Studio (or editor of your choice)

I’m using Visual Studio 2022, but you can use any IDE you like; or be a complete masochist and use notepad and the windows command line.

### Add a reference to the Traveller’s Rest Code

Before writing your own code make sure to reference the necessary assembly files in your mod project, the most important assembly being Assembly-CSharp.dll

![](https://docs.google.com/u/0/docs-images-rt/ABaEjg1veDBcJEg41efL8pMRAlPPQpfMYFP3umtZHKbqZhOpv_MR-s8w8_HH8jKvmp-Vr9Ei7JlwWD4pPYKLOmdhKV0TCW9lvEzm8fsJSEcDYQFNMyXAgP3XuOtpmRrMqpLTfFJVSrBjXW_w_c_48DvBM7RWXzmepWkmoTs=s2048)

![](https://docs.google.com/u/0/docs-images-rt/ABaEjg3urMokm0BBF05r-2EiOA2BUDMvnahIyndKXKiQutHVjbhQWb79z-4cj16-I-joQLJVC2hkpdVpds__M_uDUfQg0PsnIyOH8Cm6Ix2nn4kKvS8szsKhbSjAR_Ecm8sVCoq56nXSmnF948VV2qDoreG0Opt2jKsLLfz7eg=s2048)

### Compile

The template has code that will print "Plugin <modname> is loaded!" and does nothing else, which is enough to test that the build environment is working. So hit compile, and if all goes well you’ll see output like this:

![](https://docs.google.com/u/0/docs-images-rt/ABaEjg0nLZJIWNyxwVZGGPQWmvXoPiNw0hMLQF70qd14hO0z61AcLcll5AnLd_jlzs2WTwZLoQSzIufJkcuma8VWgHFL1WxZ4E2LA0PMbmMBknjgbRUNySah-_8rE-D6SQ-EcA5_UYXznn1qeG1a9SwDGKAfYecke0Lkrw0=s2048)

### Install your mod

Copy `net.nep.bepinex.trtest3.dll` to:

```text
<game folder>\BepInEx\plugins
```

Run raveller’s Rest and look at the Bepinex Console output

![](https://docs.google.com/u/0/docs-images-rt/ABaEjg2o3inR5IuFXQh0_zQheBlp-hVKIjXgwOZpxhetKMDe_Ob7GgaFFFL_Ek2X0wc8lk4n56KcRvHZXQn_MLhMHkfgln4rCEPQ5cokqpmISyj-Db1wtUKzcvMcr9AVgpITh4Oefo0ftQlqPgBnK8mjcLtIojk2sYnql64=s2048)

### Congratulations!

You made a functional Traveller’s Rest Mod!

### What if I want my mod to do more than print a message when it loads?

Add more code until it does whatever you want it to do.

## Modding tips and tricks

And also bits of code I copy/paste a lot.

### Why are nearly all function names and properties strings of random characters instead of useful names?

Looks like the compiler has been “helpful” and broken up the original code into smaller pieces (using random names) and moved any variables that would have been used in more than one piece into class fields. This mean that the random method/field names will change with every update, which makes modding lot trickier because 1) you have to slog through a bunch of randomly named method/fields (many of which are not actually used) to figure out what is happening and 2) those random strings change with every build, meaning every minor patch update completely changes them.

> Important: If you use any of the randomly named fields/methods your mod will break with every minor update!

This makes modding more difficult and you will need to get creative, and know how to use reflection to access private fields that are only exposed by randomly named methods.

![](https://docs.google.com/u/0/docs-images-rt/ABaEjg2djFeyt4Np6b9QFUw8DO_jcMT5j_nnAxqoaexY6l_FZ67rRgkNTv6w-dVn-UjCFWzMsCViAbahsrSVhjiD-vaON_vE4njydmx_xP1hLpgOWso_DFd-5gvNGfAL6XjdfEL5vwkHhPMxJioEwmfF3YBcnJvtx3k3r5Pm4g=s2048)

### How do I patch a method with Bepinex?

Bepinex Uses [HarmonyX](https://www.google.com/url?q=https://github.com/BepInEx/HarmonyX&sa=D&source=editors&ust=1778246260474370&usg=AOvVaw24ANYbEQCKeSDaeRIuiisJ), refer to the [HarmonyX docs](https://www.google.com/url?q=https://github.com/BepInEx/HarmonyX/wiki&sa=D&source=editors&ust=1778246260474565&usg=AOvVaw31DqqS1KXXLUYm0nlB1quB) or have a look at the example mods on github (links later in this doc)

### How do I use reflection to access a private field from within a patch?

Putting this here because I can never remember the syntax for this when I need it:

```csharp
[HarmonyPatch(typeof(FishingUI), "LateUpdate")]
[HarmonyPrefix]
static bool LateUpdatePrefix(FishingUI __instance)
{
        // Get the private slider object
        Slider reflectedSlider = Traverse.Create(__instance).Field("progress").GetValue<Slider>();

        // Can now use it to do stuff
        reflectedSlider.value = 1.0f;
```

- If the target is a method replace Field() with Method() - simple explanation [here](https://www.google.com/url?q=https://docs.vtolvr-mods.com/documentation/harmony/Traverse-private-fields.html&sa=D&source=editors&ust=1778246260476263&usg=AOvVaw1YPg5AwzKMBdVynO0aDhXY)
- GetValue() needs an explicit casting to <type>
- [Harmony Class Traverse](https://www.google.com/url?q=https://harmony.pardeike.net/api/HarmonyLib.Traverse.html&sa=D&source=editors&ust=1778246260476724&usg=AOvVaw3xTRoDyd5Tez_mYULG089d)
- [Harmony Access Tools](https://www.google.com/url?q=https://harmony.pardeike.net/articles/utilities.html&sa=D&source=editors&ust=1778246260476927&usg=AOvVaw28-u4BZ3TLSEiazEfMliVd)
- There is also using [three underscores](https://www.google.com/url?q=https://harmony.pardeike.net/articles/patching-injections.html&sa=D&source=editors&ust=1778246260477165&usg=AOvVaw1UDvKXPjRfTTLOuwlfawYy) e.g. `___privateField` as a function arg to access private field `__instance.privateField`, but I’ve only just found out about this and have not tried it.

### How Do I find a field when I know the type but not the name?

For example, you know there is only one field of type `Slot[]` but the name is randomised:

```csharp
Slot[] reflectedSlots = null;

FieldInfo[] piFieldInfo = pi.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance); //all private fields.

foreach (FieldInfo fi in piFieldInfo) // now look for the one of type Slot[]
{
        if (fi.FieldType == typeof(Slot[]))
        {
                reflectedSlots = (Slot[])fi.GetValue(pi);
                break;
        }
}
```

If the field is not private use different BindingFlags. If there is more than one field with that type you’ll need to figure out how to tell which one is the one you want.

### How to access c# instances attached to Unity objects that aren’t created via C# assemblies?

Some classes are only created as part of Unity objects (at least the classes exist and are attached to unity objects but I can find no reference to their creation in the c#)which means there is no name to access them in the C# code. For example, the WorldTime class has an instance that is a property of the Management Unity game object:

![](https://docs.google.com/u/0/docs-images-rt/ABaEjg3HZeMU0sV8cqZTO4iNIzDKy37IxwdD89RGSfYzgaXlIawFeDNzI-7p_YpPcKWYHUfUPgbNzQ5VmDQ8ChlnFY6wPEVb6w8gwX95J4Ty5sOY-dSxGiglJOYgDyV2obqctD49iXUcSdBcY4P70RBWa30kB6VD1qyQ7tBZig=s2048)

```csharp
UnityEngine.Object.FindObjectOfType<WorldTime>().currentGameDate.min
```

You can also find all objects of a given type instead:

```csharp
ContainerUI targetContainer = null;

foreach (ContainerUI cUI in UnityEngine.Object.FindObjectsOfType<ContainerUI>())
{
        if (cUI.IsOpen()) {targetContainer = cUI; break;}
}
```

This is a pre-packaged use of reflection, with all the usual performance issues that come with searching through every object for a matching one (i.e.: don’t do this on every Update());

Sometimes you can attach a patch to a function in the class that will trigger when needed and save you looking for the object constantly. All the usual reflection tricks are possible, i.e. be ready to get very confused and end up copy-pasting the one thing you found that will work.

### Easy way to add a message popup over character

Note that if a message is being displayed a second message will be ignored.

```csharp
MainUI.ShowErrorText(1,"Hello World!");
```

![](https://docs.google.com/u/0/docs-images-rt/ABaEjg04geUSc9rq9XgUjjLXSoBRp_PjF7npd3Nr1aHismAj1bej12S_idmDov9lG_pP7H8a5RmbzB8QnBsCd43nQtkr9KsihO5ofK5pEaInAWz6jg6lsaXlTL4WHn0dMeRORwen_j34L6iYVn4uqVglPJZs6omDeOSbWVIu1Q=s2048)

### Creating Unity objects using c#

```csharp
        public void CatSpawn()
    {
        if (this.catNPC == null)
        {
            UnityEngine.Object.Instantiate<GameObject>(this.catPrefab, TravelZonesManager.GDNODPKDDGA.GetTravelZone(Location.Tavern, Location.Road, -1).transform.position, Quaternion.identity).GetComponent<CatNPC>().Spawn();
        }
    }
```

### Using the localization system

```csharp
Item x= ItemDatabaseAccessor.GetItem(3439);
string y = LocalisationSystem.Get("Items/item_name_" + x.id.ToString());
string z = LocalisationSystem.Get("Items/item_description_" + x.id.ToString());
Log (String.Format("{0}, {1}, {2}, {3}",x.id, x.translationByID, y, z));
```

## Useful Stuff

### Unity Explorer

This Bepinex mod gives you a way to explore and edit objects and their properties while the game is running. It can be really helpful for exploring C# objects live to figure out what exactly you need to change with your mod.

[https://github.com/sinai-dev/UnityExplorer?tab=readme-ov-file](https://www.google.com/url?q=https://github.com/sinai-dev/UnityExplorer?tab%3Dreadme-ov-file&sa=D&source=editors&ust=1778246260485894&usg=AOvVaw0crL_7-7DoO95M3h_J9vEW)

Download the BepInEx 5.X Mono version.

### Configuration Manager

[https://github.com/BepInEx/BepInEx.ConfigurationManager](https://www.google.com/url?q=https://github.com/BepInEx/BepInEx.ConfigurationManager&sa=D&source=editors&ust=1778246260486442&usg=AOvVaw1PB5DJKSd7TeqzkHHeBXPK)

This is a Bepinex mod that lets you easily adjust mod settings while the game is running.

### DnSpy

There are other .dot .NET debuggers, this is the one I use.

[https://github.com/dnSpy/dnSpy](https://www.google.com/url?q=https://github.com/dnSpy/dnSpy&sa=D&source=editors&ust=1778246260487066&usg=AOvVaw3PArbSXbWqIxgLkdA1BmFd)

### Source code for some example mods

- [Add One Gold - Proof of Concept mod for Traveller’s Rest](https://www.google.com/url?q=https://github.com/DrStalker/TravellersRest-AddOneGold&sa=D&source=editors&ust=1778246260487464&usg=AOvVaw1lmHmlEwRVoY5xAlhhmbaj)
- [Travellers Rest Fishing Tweaks](https://www.google.com/url?q=https://github.com/DrStalker/TravellersRestFishingTweaks&sa=D&source=editors&ust=1778246260487671&usg=AOvVaw3uIJ9ij2h2SHJnSo9UrwA5)
- [Better Clock](https://www.google.com/url?q=https://github.com/DrStalker/TravellersRest-BetterClock&sa=D&source=editors&ust=1778246260487896&usg=AOvVaw0hrEEM9hTmlDB7EOln-m0V)
- [More Harvests](https://www.google.com/url?q=http://moreharvests&sa=D&source=editors&ust=1778246260488068&usg=AOvVaw34JKwxsqbOpdGZKQTOvbwD)
