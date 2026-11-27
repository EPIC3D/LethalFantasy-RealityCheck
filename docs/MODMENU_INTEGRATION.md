# LF Mod Menu – integration handoff

For: anyone (human or Claude session) working on a LethalFantasy BepInEx mod –
`LethalFantasy-HandleWithCare`, `LethalFantasy-TTS`, `LethalFantasy-SimHub-Haptics` or a new one –
who wants its settings to show up nicely in the shared **MODS** settings tab.

Source: `src/LFModMenu` (`LFModMenu.dll`, GUID `com.epicopy.lfmodmenu`).
Installed at `BepInEx\plugins\LFModMenu\LFModMenu.dll`. Tested against game beta 1.0.3.

## TL;DR

- **No code dependency, no API, no reference to LFModMenu.dll.** The tab reads every loaded
  plugin's BepInEx `ConfigFile` and builds the rows itself.
- A setting appears if it is a `bool`, an `enum`, or an `int` / `float` / `double` **with an
  `AcceptableValueRange`**. Everything else stays in the `.cfg` file only.
- Changing a value in the tab sets `ConfigEntry.Value` immediately → **react to
  `SettingChanged`** (or read `.Value` when you need it), don't cache values at `Awake`.

## What the tab shows

```
HANDLE WITH CARE          ← BepInPlugin Name, upper-cased, once per mod
  General                 ← section names as small sub-headings (only if the mod has 2+ sections)
  [ENABLED]          ☒    ← key, "PrettyCase" → "PRETTY CASE"
  Debug
  [LOG CLICKS]       ☒
REALITY CHECK             ← next mod (mods sorted by Name, A→Z)
```

| Config type | Control | Notes |
|---|---|---|
| `bool` | toggle | |
| any `enum` | dropdown | option text = enum member name, prettified (`BottomLeft` → `BOTTOM LEFT`) |
| `int` with `AcceptableValueRange<int>` | slider, whole numbers | value shown on the right |
| `float` / `double` with `AcceptableValueRange<…>` | slider | range < 10 → 2 decimals (snaps to 0.01); < 100 → 1 decimal; else 0 |
| `string`, `KeyboardShortcut`, `Color`, unranged numbers, `AcceptableValueList` … | **not shown** | still editable in the `.cfg` |

- **Labels** come from the config **key**: `_` and `.` become spaces, CamelCase is split,
  everything upper-cased (`TipInterval` → `TIP INTERVAL`, `HUDScale` → `HUD SCALE`).
  The description text is **not** shown (the game's (i) tooltips need localisation keys).
- **Mod header** = `BepInPlugin` *Name* (not the GUID). Section sub-headings = section names.
- Entries keep the order of `ConfigFile` enumeration, grouped by section in first-seen order.
  BepInEx does not guarantee bind order – don't rely on exact row order.

## Checklist to make a mod "menu-ready"

1. **Give the plugin a readable `Name`** in `[BepInPlugin(GUID, Name, Version)]` – it is the header
   (and also appears in the pause menu's `Plugins:` line with the version).
2. **Choose keys that read well as labels**: short CamelCase nouns/verbs
   (`ShowWeight`, `TipPosition`, `Volume`), no prefixes that repeat the section name.
3. **Use sections as groups** a player understands (`General`, `Audio`, `Debug` …). One section →
   no sub-heading at all.
4. **Add ranges to every number the player may tune**:

   ```csharp
   Volume = Config.Bind("Audio", "Volume", 0.8f,
       new ConfigDescription("Master volume of the voices.", new AcceptableValueRange<float>(0f, 1f)));
   ```

   Pick sensible bounds – they become the slider ends.
5. **Use enums instead of magic strings/ints** for choices:

   ```csharp
   public enum TipPosition { UnderSizeBar, TopRight, BottomLeft, BottomRight }
   Pos = Config.Bind("HUD", "TipPosition", TipPosition.UnderSizeBar, "Where the tips scroll.");
   ```

6. **Apply changes live.** The tab writes `ConfigEntry.Value` the moment a toggle/slider/dropdown
   changes (BepInEx saves the `.cfg` automatically). Either read `.Value` each time you need it,
   or subscribe:

   ```csharp
   Volume.SettingChanged += (_, __) => ApplyVolume();
   // or once for the whole file:
   Config.SettingChanged += (_, e) => OnSettingChanged(e.ChangedSetting);
   ```

   A slider fires many changes while dragging – keep the handler cheap (or debounce).
7. **Hide developer-only settings** from players by making them a type the tab skips (e.g. a
   `string`), or let the user list the plugin in `HiddenPlugins` (see below). There is no
   per-entry "hidden" flag yet.

## Things that bit us (good to know for any mod in this game)

- **The game destroys plugin GameObjects on scene load.** `Plugin.Update()` stops after the first
  scene change, and Unity's `== null` says the plugin is gone. Its managed object and `Config` stay
  alive – that is why the tab uses `(object)info.Instance == null`. Drive per-frame work from game
  methods via Harmony (e.g. `GameUI.Update`, `PauseMenu.Update`) instead of your own `Update`.
- The MODS tab is built in a postfix on `Settings.Start`, by cloning the first settings tab and the
  game's own header/toggle/slider/dropdown prefabs – so it always looks like the game's UI.
- Values edited by hand in the `.cfg` while the game runs are picked up by BepInEx only if the mod
  reloads the file; the tab shows `ConfigEntry` values as of when the settings window was built.

## LF Mod Menu's own settings (`BepInEx\config\com.epicopy.lfmodmenu.cfg`)

| Key | Default | Meaning |
|---|---|---|
| `TabTitle` | `MODS` | Text of the tab button |
| `HiddenPlugins` | *(empty)* | Comma-separated **GUIDs** to leave out of the tab |
| `ShowOwnSettings` | `false` | List the Mod Menu's own settings in the tab too |
| `PauseMenuPluginLine` | `Names` | `Off` / `Count` / `Names` – the `Plugins:` line under the pause menu's Level/Seed/Mods/Env info |

## Ideas not built yet (ask before relying on them)

- Showing the config **description** as a hover tooltip.
- Text fields for `string` settings and key-binding capture for `KeyboardShortcut`.
- A per-entry "hidden"/"advanced" marker (e.g. via a tag object in `ConfigDescription`).
- Refreshing the rows when a value changes from code while the tab is open.
