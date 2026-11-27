# LethalFantasy – RealityCheck

*What would it really be like?*

A BepInEx plugin for **LethalFantasy** that puts the giantesses' size into
real-world terms. How heavy is she, how hot, how loud, how far could she throw
you – and where would real physics stop her? Every number is shown as a
well-known comparison first (a jumbo jet, the Eiffel Tower, an Olympic pool);
the raw values are one hover away.

Comes with **LF Mod Menu**, a small companion plugin that adds a **MODS** tab to
the game's settings for every installed BepInEx plugin.

## What you get

**During gameplay**

- **Size bar:** her weight next to her height (`90m  8.92 kt`).
- **Bottom right:** the nearest giantess – her weight, your size to her, her
  height and how warm it feels to you, each as a comparison.
- **Bottom left – how you feel:** air temperature (map, time of day, height),
  your altitude and oxygen, her breath, the wind from her movement, the felt
  temperature with her body warmth, and how your body copes (sweating, panting,
  heatstroke, or getting cold).
- **Fun-fact popup** now and then (top right by default).

**In the pause menu** – a tabbed panel, one page per topic, with a character
selector (Akipo, Red, Blue):

| Tab | About |
| --- | --- |
| SIZE | Height, weight, volume, body parts; you from her point of view |
| BODY | Strength, walking speed, ground pressure, sinking into the ground |
| FOOD | What she eats and drinks per day |
| THROW | How far she throws a ball – or you – with air drag and G-forces |
| STOMP | Stomp and jump energy, earthquake strength, how far it is felt |
| SOUND | How loud she is, how far she is heard, can she hear you |
| AIR | Thin air at her head, her breath, clouds |
| HEAT | Body heat, sweat, overheating |
| COOL | Cooling off in the sea, under waterfalls, at the South Pole |
| TOUCH | Her warmth on you, her hand, a hug, her lap |
| SKIN | Her skin up close: ridges, pores, hair |
| LIMITS | Where real physics would stop her: bones, ground, blood, heat, air |
| THIS MAP | Extras for the Rain, Shoe, Sock and Stomach environments |

**(R) and (F):** many values come in two versions. *(R)eal* scales her body like
a real animal of her size would work (slower metabolism, weaker per mass);
*(F)antasy* keeps every cell working like ours, just more of them. The tooltips
explain each.

All numbers are square–cube law estimates with rough reference values – fun,
not science. The formulas are written from scratch for this plugin.

Supports **LethalFantasy beta 1.0.3** (Akipo, Red twin, Blue twin).

**LethalFantasy** is made by **LadyAlice** – support the original game on
[Patreon](https://www.patreon.com/cw/LadyAliceR18). This is an unofficial
fan-made mod and is not affiliated with or endorsed by the game's creator.

> **Disclaimer:** This software is provided "as is", without warranty of any
> kind. Use it at your own risk. The author accepts no responsibility for any
> damage, data loss or game issues resulting from its use. See
> [LICENSE](LICENSE) for details.

## LF Mod Menu

- Adds a **MODS** tab to the settings window, built from the game's own UI.
- Lists the settings of **every** installed BepInEx plugin automatically:
  on/off → toggle, choices → dropdown, numbers with a range → slider.
  Other mods need no code; see
  [docs/MODMENU_INTEGRATION.md](docs/MODMENU_INTEGRATION.md).
- Adds a `Plugins:` line under the pause menu's Level/Seed/Mods/Env info.

RealityCheck works without it – its settings are then only in the config file.

## Download

Get the ready-to-use package from the [Releases](../../releases) page. It
contains two DLLs:

- `BepInEx\plugins\RealityCheck\realitycheck.dll` – the mod
- `BepInEx\plugins\LFModMenu\LFModMenu.dll` – the settings tab (optional)

## Installation

1. Install **BepInEx 5 (x64)** into the LethalFantasy folder and start the game once.
2. Unzip the package into the LethalFantasy folder.
3. Start the game. Settings: *Settings → MODS*.

## Configuration

Settings are in the MODS tab, or in `<LethalFantasy>\BepInEx\config\`:

- `com.epicopy.realitycheck.cfg`
  - `[General] Units` – Metric, Imperial or Both
  - `[HUD]` – weight on the size bar, corner panel, fun facts and their position/interval
  - `[PausePanel] Show`
  - `[Feel]` – the bottom-left panel; `BodyTimeScale` (how fast you heat up or
    chill), `AirTemperatureOffset`, `BaseBreeze` (0 = still air)
  - `[Bodies]` – the natural height and weight of each character and of you
    (Akipo 1.62 m / 54 kg, Red 1.72 m / 66 kg, Blue 1.70 m / 64 kg – assumptions)
- `com.epicopy.lfmodmenu.cfg` – tab title, hidden plugins, the `Plugins:` line

## Building from source

Requirements: .NET SDK (any version that can build `netstandard2.1`) and a local
LethalFantasy install **with BepInEx 5** – the build references the game's own
`Assembly-CSharp.dll`, Unity and BepInEx assemblies, which are not part of this
repository.

```powershell
# 1. Tell the build where your game is
Copy-Item GamePath.props.example GamePath.props
#    then edit GamePath.props -> <LethalFantasyDir>

# 2. Build
dotnet build src\RealityCheck\RealityCheck.csproj -c Release
dotnet build src\LFModMenu\LFModMenu.csproj -c Release
```

### Repository layout

```
src/RealityCheck/Plugin.cs      Settings, character bodies
src/RealityCheck/SizeMath.cs    Square–cube scaling: mass, area, strength, pressure
src/RealityCheck/Physics2.cs    Air, throws, heat and sweat, cooling, stomps, food
src/RealityCheck/Sound.cs       Loudness, hearing range, voice pitch
src/RealityCheck/Format.cs      Units and the comparison ladders
src/RealityCheck/PausePanel.cs  Pause menu tabs
src/RealityCheck/SoundPage.cs   SOUND tab
src/RealityCheck/EnvInfo.cs     Rain / Shoe / Sock / Stomach environments
src/RealityCheck/HudPanel.cs    Bottom-right panel and fun-fact popup
src/RealityCheck/FeelPanel.cs   Bottom-left: how you feel
src/RealityCheck/Climate.cs     Air temperature per map, time and environment
src/RealityCheck/Wind.cs        Wind from her movement and breath, her warmth
src/RealityCheck/HudPatch.cs    Weight on the size bar
src/RealityCheck/Ui.cs          Panels, tooltips (HoverTip.cs), game hooks
src/LFModMenu/                  MODS settings tab and the Plugins: line
docs/MODMENU_INTEGRATION.md     How other mods show up in the MODS tab
```

## Notes

- This is an unofficial fan mod and is not affiliated with the LethalFantasy developers.
- No game content is included or redistributed: no game, Unity, BepInEx or
  Harmony binaries, no game art, audio or text.

## License

[MIT](LICENSE)

## Support

If you enjoy the game, please support its author, LadyAlice, first:
<https://www.patreon.com/cw/LadyAliceR18>.

If you'd also like to support this mod:

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/E6Z42AUOBQ)
