# The science behind RealityCheck – calculations and sources

How RealityCheck gets its numbers. Everything is an estimate: well-known scaling
laws applied to the game's characters, with rough reference values. Good for the
order of magnitude, not for engineering.

## Scale factor

`k = height / natural height` (Akipo 1.62 m, Red 1.72 m, Blue 1.70 m by default).

| Quantity | Scales with | Example at k = 15 (24 m) |
| --- | --- | --- |
| Lengths (foot, finger, step) | k | ×15 |
| Areas (skin, sole) | k² | ×225 |
| Volume, mass | k³ | ×3375 |

Body surface at natural size: Mosteller, `√(height_cm · mass_kg / 3600)` m².

## Real (R) and fantasy (F)

Both keep her cells the size of ours – there are just more of them.

| | (R) real | (F) fantasy |
| --- | --- | --- |
| Metabolism, heat, food | Kleiber: ∝ M^0.75 → k^2.25 | every cell like ours: k³ |
| Muscle force, lifting | ∝ cross-section: k² | k³ |
| Throw / limb tip speed | √k | k |
| Walking speed | Froude number: `v = √(Fr · g · leg)`, Fr ≈ 0.25 → √k | k |

## Limits

| Limit | Formula | Where it fails (Akipo, Earth) |
| --- | --- | --- |
| Bones | walking femur stress 25 MPa · k, cortical bone ~170 MPa | ~11 m |
| Ground | pressure `M·g / (2 · sole area)` ∝ k, against layered soil (topsoil → clay → rock) | ~10 m (sinks > 5 cm) |
| Heart → brain | `90 mmHg + ρ_blood · g · 0.19 · height` (giraffe ~300 mmHg) | ~14 m |
| Heat (F) | walking 130 W/m² · k vs. best-case cooling ~800 W/m² (dry + sweat) | ~11 m |
| Heat (R) | 130 W/m² · k^0.25 | ~8.6 km |

Bone, ground and blood loads are proportional to `g · k`, so in lower gravity the
limits rise by `1/g` (Mars, 0.38 g: ×2.6). The load feels Earth-like when
`g · k = 1`.

The "largest possible" size shown in game is 90% of the bone limit.

## Air, heat and sound

- **Atmosphere:** International Standard Atmosphere (ISO 2533) for temperature
  and pressure with height; oxygen partial pressure for breathing.
- **Heat balance:** heat made vs. convection/radiation (~100 W/m² at 15 °C,
  thinner with height) plus sweat evaporation.
- **Radiant warmth:** Stefan–Boltzmann, `q = ε·σ·(T_skin⁴ − T_air⁴)`, with a
  view factor `0.5 · (r/x)²` for distance x.
- **Air warmed by her heat:** `ΔT = P / (ρ · c_p · v · A)`, A = her front area.
- **Sound:** spherical spreading (−6 dB per doubling of distance), air
  absorption, thinner air at height; voice pitch ∝ 1/k.
- **Throws:** quadratic air drag, integrated step by step with air density
  changing with height.

## Fun facts (not used in the calculations)

- **Bigger cells instead of more:** oxygen diffusion time `t = r² / (2D)`,
  D ≈ 2·10⁻⁹ m²/s, cell radius 5 µm · k.
- **Bigger atoms:** same charge spread over a bigger cloud → bonds ∝ 1/k, energy
  levels ∝ 1/k². Real-world analogue: Rydberg atoms.
- **Own gravity:** her volume treated as a ball, `g = G·M / r²`, escape speed
  `√(2·g·r)`. Centre pressure `P = (2π/3) · G · ρ² · R²`; soft tissue
  (~100 kPa) gives way at ~53 km thickness (~360 km tall).
- **Eyes:** same f-number → same brightness per cell; point-like stars ∝ pupil
  area (k²); sharpness up to k×, limited by air shimmer (~1 arcsec).
- **Smell:** ~6 million smell cells (human) · k², bloodhound ~300 million.
- **Touch:** your weight to her is `M_you / k³`; below ~5 mg it is not felt.

## Further reading

- Galileo Galilei, *Two New Sciences* (1638) – the square–cube law and why
  giants would need thicker bones.
- J. B. S. Haldane, "On Being the Right Size" (1926) – a short, classic essay.
- M. Kleiber, "Body size and metabolism", *Hilgardia* (1932) – the ¾-power law.
- T. McMahon & J. T. Bonner, *On Size and Life* (1983).
- K. Schmidt-Nielsen, *Scaling: Why Is Animal Size So Important?* (1984).
- A. A. Biewener, "Biomechanics of mammalian terrestrial locomotion",
  *Science* (1990) – how bone stress limits large animals.
- R. McN. Alexander – papers and books on animal gaits and the Froude number.
- P. M. Sander et al., "Biology of the sauropod dinosaurs: the evolution of
  gigantism", *Biological Reviews* (2011).
