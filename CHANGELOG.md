# Changelog

## 0.2.0 – Calmer screen, bigger pause view, more facts

Tested against LethalFantasy beta 1.0.3. LF Mod Menu is unchanged (0.1.0).

**Less on screen**

- The bottom-right panel shows only while you look at a giantess, then fades
  out (`[HUD] CornerPanelOnLook`). It follows the one you look at.
- The bottom-left panel shows only when your state changes, while a giantess
  warms or blows on you, or when it gets dangerous (`[Feel] OnlyOnChange`).
- Bottom-right: names in their dialogue-box colours, and no name when she is
  the only giantess on the map.

**New**

- Growth milestones: "SOLANA is now taller than the Eiffel Tower!"
- Changed examples in the bottom-right panel pop as she grows.
- Pause menu: **+** opens a big two-column view; faint row bands for easier
  reading; buttons styled like the pause menu.
- Shrinking (shrink house, enchantments) is followed: "you to her", your
  weight, and your body heat – a small body chills and warms much faster.
- Shoe level: you stand under a giant hovering foot – shade, warmth radiating
  from her sole, and the real size of her sweat drops.
- LIMITS: the largest size her body shape could just about work at.
- Many new fun facts: bigger cells or atoms instead of more, Mars gravity,
  her own gravity in space, eyes and stars, smell, touch.
- Optional everyday breeze (`[Feel] BaseBreeze`, off by default).
- [docs/SCIENCE.md](docs/SCIENCE.md): the calculations and sources.

**Fixed**

- "Can she spot you?" uses one realistic answer (her bigger eye sees sharper,
  up to the limit of air shimmer).
- Tooltips stay on screen; rounded corners on the HUD boxes.
- War-related comparisons replaced with neutral ones.
- Several robustness fixes (spawning at height 0, one error no longer turns
  off all HUD panels).

## 0.1.0 – First release

Tested against LethalFantasy beta 1.0.3.

**RealityCheck**

- Her weight on the size bar.
- Bottom-right panel: the nearest giantess in comparisons.
- Bottom-left panel: air, altitude, oxygen, wind, felt temperature and your body.
- Fun-fact popup.
- Pause menu panel with 12 tabs (SIZE … LIMITS) plus THIS MAP for the
  Rain, Shoe, Sock and Stomach environments.
- Real (R) and fantasy (F) values side by side; raw numbers in tooltips.

**LF Mod Menu**

- MODS settings tab for every BepInEx plugin.
- `Plugins:` line in the pause menu.
