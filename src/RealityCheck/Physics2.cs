using System;

namespace RealityCheck
{
    /// <summary>Atmosphere, air drag, breath, seismic and body-part estimates.</summary>
    public sealed partial class SizeStats
    {
        public const double SeaLevelDensity = 1.225;   // kg/m³
        public const double SeaLevelO2KPa = 21.2;
        public const double WaterDensity = 1000;

        // --- Atmosphere (ISA-like) ---------------------------------------------------------
        public static double AirTemperatureC(double h)
        {
            if (h < 11000) return 15 - 0.0065 * h;
            if (h < 20000) return -56.5;
            if (h < 32000) return -56.5 + 0.001 * (h - 20000);
            if (h < 47000) return -44.5 + 0.0028 * (h - 32000);
            if (h < 51000) return -2.5;
            if (h < 71000) return -2.5 - 0.0028 * (h - 51000);
            return Math.Max(-86, -58.5 - 0.002 * (h - 71000));
        }

        public static double PressureRatio(double h) => Math.Exp(-h / ScaleHeight);
        public static double Density(double h) => SeaLevelDensity * PressureRatio(h);

        /// <summary>0 normal … 5 space, by the altitude of her head.</summary>
        public static int OxygenLevel(double h) =>
            h < 2500 ? 0 : h < 5500 ? 1 : h < 8000 ? 2 : h < 19000 ? 3 : h < 100000 ? 4 : 5;

        public double HeadTempC => AirTemperatureC(EyeHeight);
        public double HeadO2KPa => SeaLevelO2KPa * PressureRatio(EyeHeight);

        /// <summary>
        /// Convective cooling of her body vs. a normal woman: thinner air (∝ √ρ) but colder air.
        /// Evaluated at mid-body height, skin 34 °C, normal air 15 °C.
        /// </summary>
        public double AirCoolingFactor
        {
            get
            {
                double mid = Height / 2;
                double dT = Math.Max(0, 34 - AirTemperatureC(mid));
                return Math.Sqrt(PressureRatio(mid)) * dT / (34 - 15);
            }
        }

        // --- Ball flight with air drag ---------------------------------------------------------
        /// <summary>Best range over launch angles, quadratic drag, density falling with altitude.</summary>
        // From Earth's surface: orbit, escape Earth (then circles the Sun), escape the Solar System, escape the galaxy.
        public const double OrbitalSpeed = 7900, EscapeSpeed = 11200, SolarEscape = 16700, GalaxyEscape = 550_000;

        /// <summary>Where a throw this fast ends up (no air).</summary>
        public static string Fate(double v0) =>
            v0 >= GalaxyEscape ? "leaves the Milky Way" :
            v0 >= SolarEscape ? "leaves the Solar System" :
            v0 >= EscapeSpeed ? "orbits the Sun" :
            v0 >= OrbitalSpeed ? "orbits Earth" : null;

        /// <summary>Result with air: a distance, or – if it left the air – where it goes from there.</summary>
        public static string DragResult((double range, double exit) r, WeightUnits u) =>
            double.IsInfinity(r.range) ? (Fate(r.exit) ?? "space, falls back") : Fmt.Length(r.range, u);

        /// <summary>Range text that handles throws fast enough to orbit or escape.</summary>
        public static string ThrowResult(double v0, double range, WeightUnits u) => Fate(v0) ?? Fmt.Length(range, u);

        public const double SpaceEdge = 100_000;

        public static double ThrowRangeWithDrag(double v0, double diameter, double mass, double releaseHeight) =>
            ThrowWithDrag(v0, diameter, mass, releaseHeight).range;

        /// <summary>
        /// Best range with air drag; if the ball climbs above 100 km it is out of the air:
        /// range = ∞ and exit = its speed there (then Fate(exit) says orbit / Sun / leaves).
        /// </summary>
        public static (double range, double exit) ThrowWithDrag(double v0, double diameter, double mass, double releaseHeight)
        {
            double area = Math.PI * diameter * diameter / 4;
            double best = 0, exit = 0;
            for (double deg = 15; deg <= 45; deg += 2.5)
            {
                double a = deg * Math.PI / 180;
                double vx = v0 * Math.Cos(a), vy = v0 * Math.Sin(a), x = 0, y = 0;
                double dtBase = Math.Max(1e-3, 2 * vy / G / 600);
                for (int i = 0; i < 40000 && y > -releaseHeight; i++)
                {
                    double v = Math.Sqrt(vx * vx + vy * vy);
                    if (releaseHeight + y > SpaceEdge) { x = double.PositiveInfinity; exit = Math.Max(exit, v); break; }
                    double dt = Math.Min(dtBase, 200 / Math.Max(v, 1));   // at most ~200 m per step
                    double cd = v > SpeedOfSound ? 0.9 : 0.35;
                    double kd = 0.5 * Density(releaseHeight + y) * cd * area / mass;
                    double damp = 1 / (1 + kd * v * dt);   // semi-implicit drag: stable at any speed
                    vx *= damp;
                    vy = (vy - G * dt) * damp;
                    x += vx * dt;
                    y += vy * dt;
                }
                best = Math.Max(best, x);
            }
            return (best, exit);
        }

        // --- Throwing the (normal-sized) player ---------------------------------------------------
        public const double PlayerArea = 0.7, PlayerCd = 1.0;      // tumbling human
        /// <summary>Terminal speed of a tumbling 75 kg human at sea level (~45 m/s).</summary>
        public static double HumanTerminal => Math.Sqrt(2 * 75 * G / (SeaLevelDensity * PlayerCd * PlayerArea));

        /// <summary>Range, landing speed and flight time when she throws you at speed v0.</summary>
        public (double range, double impact, double time) ThrowPlayer(double v0)
        {
            double m = Plugin.PlayerMass.Value, kdBase = 0.5 * PlayerCd * PlayerArea / m;
            double release = ReleaseHeight;
            (double range, double impact, double time) best = (0, 0, 0);
            for (double deg = 10; deg <= 45; deg += 2.5)
            {
                double a = deg * Math.PI / 180, vx = v0 * Math.Cos(a), vy = v0 * Math.Sin(a), x = 0, y = 0, t = 0;
                double dtBase = Math.Max(1e-3, Math.Min(0.05, (vy + 50) / G / 400));
                bool escaped = false;
                for (int i = 0; i < 200000 && y > -release; i++)
                {
                    double v = Math.Sqrt(vx * vx + vy * vy);
                    if (release + y > SpaceEdge) { escaped = true; break; }   // out of the air (if you survived the heat)
                    double dt = Math.Min(dtBase, 200 / Math.Max(v, 1));
                    double kd = kdBase * Density(release + y);
                    double damp = 1 / (1 + kd * v * dt);   // semi-implicit drag: stable at any speed
                    vx *= damp;
                    vy = (vy - G * dt) * damp;
                    x += vx * dt; y += vy * dt; t += dt;
                }
                if (escaped) return (double.PositiveInfinity, Math.Sqrt(vx * vx + vy * vy), t);
                if (x > best.range) best = (x, Math.Sqrt(vx * vx + vy * vy), t);
            }
            return best;
        }

        /// <summary>Average acceleration in her hand: speeds up over ~2/3 of her height (arm swing).</summary>
        public double ThrowG(double v0) => v0 * v0 / (2 * 0.66 * Height) / G;

        /// <summary>Overhand release point: the hand is about at the top of her head. Air density starts there.</summary>
        public double ReleaseHeight => 1.0 * Height;

        public double ThrowRangeRealDrag => ThrowRangeWithDrag(ThrowSpeedReal, BallSize, BallKg, ReleaseHeight);
        public double ThrowRangeFantasyDrag => ThrowRangeWithDrag(ThrowSpeedFantasy, BallSize, BallKg, ReleaseHeight);
        public static double PlayerThrowRangeDrag => ThrowRangeWithDrag(PlayerThrowSpeed, BallDiameter, BallMass, 1.6);

        // --- Breath and blowing -----------------------------------------------------------------
        public const double LungCapacityL = 4.2;   // total lung capacity, adult woman
        public const double TidalL = 0.5;          // one calm breath
        public const double BreathsPerMin = 14;
        public const double BlowJet = 30;          // m/s at the lips, strong blow
        public double LungVolumeM3 => LungCapacityL / 1000 * K * K * K;
        public double BreathM3 => TidalL / 1000 * K * K * K;
        public double BreathRateReal => BreathsPerMin * Math.Pow(K, -0.75); // Kleiber-like slowdown

        /// <summary>Wind on you when she blows at arm's length (round jet: v ∝ D/x).</summary>
        public double BlowWindReal => Math.Min(BlowJet, BlowJet * 6 * (0.02 * K) / (0.4 * Height));
        public double BlowWindFantasy => BlowWindReal * K;

        // --- Water ----------------------------------------------------------------------------------
        public const double SeaWaterDensity = 1025;
        public double WaterDisplacedM3 => Volume;       // fully submerged
        /// <summary>Floating in the sea she displaces exactly her own weight of sea water.</summary>
        public double SeaFloatDisplacedM3 => Mass / SeaWaterDensity;
        /// <summary>Share of her body above the sea surface while floating.</summary>
        public double SeaFreeboard => 1 - BodyDensity / SeaWaterDensity;
        public const double DrinkL = 1.5;           // what a woman can drink in one go
        public double DrinkM3 => DrinkL / 1000 * K * K * K;
        public bool Floats => BodyDensity < WaterDensity;

        // --- Heat, sweat, water, clouds ------------------------------------------------------------
        public const double LatentHeat = 2.43e6;      // J/kg, sweat evaporation at skin temperature
        public const double NonEvapWm2 = 100;         // convection + radiation at 15 °C air
        public const double MaxEvapWm2 = 675;         // ≈ 1 L/h per m² of skin, the human maximum
        public const double DailyWaterL = 2;          // normal daily drinking
        public const double CloudPowerW = 1e9;        // ~ a large wildfire builds its own cumulus
        public const double CloudBaseM = 1500;        // typical cumulus cloud base

        public double BaseHeatW => ActiveHeatFluxWm2 * SurfaceArea / (K * K);
        /// <summary>Walking heat if every cell worked like a human cell (∝ k³).</summary>
        public double HeatRealW => BaseHeatW * K * K * K;
        /// <summary>Walking heat with a whale-like slow metabolism (Kleiber, ∝ M^0.75).</summary>
        public double HeatKleiberW => BaseHeatW * Math.Pow(K, 2.25);
        public double NonEvapW => NonEvapWm2 * SurfaceArea * Math.Max(0, AirCoolingFactor);
        public double MaxEvapW => MaxEvapWm2 * SurfaceArea;

        /// <summary>Sweat (litres per hour) that must evaporate to get rid of the heat.</summary>
        public double SweatLph(double heatW) => Math.Max(0, NetHeat(heatW) - NonEvapW) / LatentHeat * 3600;

        /// <summary>
        /// Share of the heat breathed out: warming the inhaled air and saturating it with water vapour.
        /// ~13% for a walking human at 15 °C; colder air at her head takes a bit more.
        /// Breathing volume follows the metabolism, so the share does not change with size.
        /// </summary>
        public double RespiratoryShare => 0.13 * (0.75 + 0.25 * Math.Max(0, NormalSkinC - AirTemperatureC(EyeHeight)) / (NormalSkinC - 15));
        public const double BreathC = 34;               // exhaled air at the mouth
        public double RespiratoryW(double heatW) => heatW * RespiratoryShare;
        /// <summary>Heat left for the skin after breathing.</summary>
        public double NetHeat(double heatW) => heatW - RespiratoryW(heatW);
        public double MaxSweatLph => MaxEvapW / LatentHeat * 3600;

        /// <summary>Daily drinking need in m³: normal need ×k³ plus the sweat of 8 active hours.</summary>
        public double WaterPerDayM3(double heatW) => (DailyWaterL * K * K * K + SweatLph(heatW) * 8) / 1000;
        /// <summary>Real: water turnover follows the metabolism (∝ M^0.75 like the food), plus its sweat.</summary>
        public double WaterPerDayRealM3 => (DailyWaterL * Math.Pow(K, 2.25) + SweatLph(HeatKleiberW) * 8) / 1000;

        public const double BodyHeatCapacity = 3500;    // J/(kg·K)
        public const double RestShare = 0.35;           // resting heat ≈ 35% of walking heat

        /// <summary>Heat that sweat and air cannot remove (W).</summary>
        public double ExcessHeatW(double heatW) => Math.Max(0, NetHeat(heatW) - NonEvapW - MaxEvapW);

        /// <summary>Seconds until her core rises by dT (37 → 40 °C heatstroke: 3 K, → 42 °C deadly: 5 K).</summary>
        public double OverheatSeconds(double heatW, double dT)
        {
            double excess = ExcessHeatW(heatW);
            return excess <= 0 ? double.PositiveInfinity : BodyHeatCapacity * Mass * dT / excess;
        }

        // --- In the sea ---------------------------------------------------------------------------
        public const double SeaC = 15;                  // sea temperature
        public const double SwimShare = 2.3;            // swimming heat ≈ 2.3× walking heat
        public const double WettedShare = 0.9;          // head stays out
        /// <summary>Water-to-skin heat transfer (W/m²K) at natural size; drops slowly with size (∝ k^-0.2).</summary>
        public double WaterH(bool swimming) => (swimming ? 500 : 200) * Math.Pow(K, -0.2);
        public double SeaCoolingW(bool swimming) =>
            WaterH(swimming) * SurfaceArea * WettedShare * (NormalSkinC - SeaC);

        // --- Under a waterfall -------------------------------------------------------------------
        public const double RiverC = 8;                 // cold mountain river
        public const double FallContact = 0.3;          // falling water warms only ~30% of the way to skin temperature
        public static readonly (string name, double flow, double width)[] Waterfalls =
        {
            ("Mountain fall", 1, 5),
            ("Rhine Falls", 600, 150),
            ("Victoria Falls", 1088, 1708),
            ("Niagara Falls", 2400, 1200),
        };

        public double ShoulderWidth => 0.26 * Height;
        /// <summary>Cooling (W) from standing under a waterfall: only the part of it that falls on her counts.</summary>
        public double WaterfallCoolingW(double flow, double width) =>
            flow * Math.Min(1, ShoulderWidth / width) * WaterDensity * 4186 * (NormalSkinC - RiverC) * FallContact;
        /// <summary>Cold water (m³/s) that must pour over her to remove this much heat.</summary>
        public static double FlowNeeded(double heatW) => heatW / (WaterDensity * 4186 * (NormalSkinC - RiverC) * FallContact);

        // --- At the South Pole -----------------------------------------------------------------------
        public const double PoleC = -50;
        /// <summary>
        /// Cooling in −50 °C, dry, windy air: convection/radiation scale with the temperature gap
        /// (×1.5 for the polar wind), plus full sweat evaporation (dry air).
        /// </summary>
        public double PoleCoolingW
        {
            get
            {
                double mid = Height / 2;
                double air = PoleC + (AirTemperatureC(mid) - AirTemperatureC(0));
                double dry = NonEvapWm2 * SurfaceArea * Math.Sqrt(PressureRatio(mid)) * (NormalSkinC - air) / (NormalSkinC - 15) * 1.5;
                return dry + MaxEvapW;
            }
        }

        public bool BreathFogs => AirTemperatureC(EyeHeight) < 7;
        public double CloudHeight => BaseHeight * Math.Pow(CloudPowerW / BaseHeatW, 1.0 / 3);

        // --- Radiant heat felt by a normal person -----------------------------------------------
        public const double Sigma = 5.67e-8;
        public const double SkinK = 307, AmbientK = 288;
        public const double FeltRadiantWm2 = 10;      // smallest extra radiant heat people notice

        /// <summary>Extra radiant heat (W/m²) on a person facing her at distance d, compared with open air.</summary>
        public double RadiantWm2(double d) => RadiantWm2(d, NormalSkinC);

        /// <summary>Extra radiant heat from her skin at skinC °C, at distance d.</summary>
        public double RadiantWm2(double d, double skinC)
        {
            double full = 0.97 * Sigma * (Math.Pow(skinC + 273.15, 4) - Math.Pow(AmbientK, 4)); // ~110 W/m² at 34 °C
            double area = 0.15 * Height * Height;   // her front silhouette
            double view = 0.5 * area / (area + Math.PI * d * d);
            return full * view;
        }

        public double RadiantMaxWm2 => RadiantWm2(0);
        /// <summary>Distance where her warmth is just noticeable (≈ 0.46 × her height).</summary>
        public double RadiantFeltDistance => FeltDistanceFor(NormalSkinC);

        public double FeltDistanceFor(double skinC)
        {
            double full = 0.97 * Sigma * (Math.Pow(skinC + 273.15, 4) - Math.Pow(AmbientK, 4));
            double r = 0.5 * full / FeltRadiantWm2 - 1;
            return r <= 0 ? 0 : Math.Sqrt(0.15 * Height * Height * r / Math.PI);
        }

        /// <summary>
        /// Her skin temperature: 34 °C while she can lose her heat; feverish 40 °C when she overheats,
        /// 45 °C when her heat is more than 3× what she can lose.
        /// </summary>
        public double SkinC(double heatW)
        {
            double excess = ExcessHeatW(heatW);
            if (excess <= 0) return NormalSkinC;
            return excess > 2 * (NonEvapW + MaxEvapW) ? 45 : 40;
        }

        // --- Touch and hug --------------------------------------------------------------------------
        public const double NormalSkinC = 34;
        public const double HandBreadthRatio = 0.046; // palm width / height

        /// <summary>Can her skin get rid of the walking heat (sweat + air) and stay at ~34 °C?</summary>
        public bool SkinOverheats(double heatW) => SweatLph(heatW) > MaxSweatLph;

        public double PalmArea => 0.8 * Parts[1].length * Height * HandBreadthRatio * Height;
        /// <summary>Share of your front (0.15 × h²) her palm can cover, capped at 100%.</summary>
        public double PalmCoverage(double playerHeight) => Math.Min(1, PalmArea / (0.15 * playerHeight * playerHeight));

        // Resting on her: heartbeat and breathing seen from up close.
        public const double HeartRateBpm = 70;
        public const double ChestRise = 0.015;          // m per calm breath at natural size
        public double HeartRateReal => HeartRateBpm * Math.Pow(K, -0.75);   // big animals beat slower
        public double ChestRiseM => ChestRise * K;

        /// <summary>Weight pressing on you (kg) if she stands normally with you under one sole (your top area ~0.5 m²).</summary>
        public double UnderSoleKg => Math.Min(Mass / 2, GroundPressurePa * 0.5 / G);

        // --- Food -----------------------------------------------------------------------------------
        public const double DailyKcal = 2000;           // active young woman
        public const double BigMacKcal = 550;
        public const double BigMacUsd = 5.69;           // US price, Big Mac Index 2024
        public const double FoodKcalPerKg = 2000;       // mixed meals ~2 kcal/g
        public const double HumanBodyKcal = 126_000;    // energy content of a 66 kg human body (Cole 2017)
        public const double BlueWhaleKcal = 3e8;        // ~150 t blue whale at ~2000 kcal/kg

        public double KcalReal => DailyKcal * K * K * K;           // human cells
        public double KcalWhale => DailyKcal * Math.Pow(K, 2.25);  // Kleiber, whale-like

        // --- Stomp --------------------------------------------------------------------------------
        // Foot lifted ~0.2 h and driven down (gravity + muscle ≈ 1.5× free-fall speed);
        // ~30% of her mass (leg + body behind it) hits the ground.
        public const double StompMassShare = 0.3, StompLift = 0.2, StompBoost = 1.5, StompSeismicEff = 5e-3;
        public double StompSpeedReal => StompBoost * Math.Sqrt(2 * G * StompLift * Height);            // ∝ √k
        public double StompSpeedFantasy => StompBoost * Math.Sqrt(2 * G * StompLift * BaseHeight) * K; // ∝ k
        public double StompEnergy(double v) => 0.5 * StompMassShare * Mass * v * v;
        public static double Magnitude(double seismicJ) => (Math.Log10(Math.Max(seismicJ, 1e-9)) - 4.8) / 1.5;
        public static double FeltDistance(double magnitude) => 1000 * Math.Pow(10, 0.5 * magnitude - 0.3);
        /// <summary>Peak force: the momentum stops within ~0.05 s × √k (bigger bodies are softer and slower).</summary>
        public double StompForce(double v) => StompMassShare * Mass * v / (0.05 * Math.Sqrt(K));
        public double StompPressure(double v) => StompForce(v) / SoleArea;

        // --- Jump and jump stomp ---------------------------------------------------------------------
        // Borelli's law: muscle energy ∝ mass, so real jump height stays the same at any size.
        public const double JumpHeight = 0.35;                      // m, centre-of-mass rise of a woman
        public double JumpReal => JumpHeight;
        public double JumpFantasy => JumpHeight * K;
        public static double AirTime(double h) => 2 * Math.Sqrt(2 * h / G);
        /// <summary>Landing energy (J) of her whole body after a jump of height h.</summary>
        public double JumpStompEnergy(double h) => Mass * G * h;

        // --- Footstep tremor ------------------------------------------------------------------------
        /// <summary>Richter-like magnitude of one step: ~0.1% of the centre-of-mass drop energy becomes seismic.</summary>
        public double StepMagnitude
        {
            get
            {
                double e = Mass * G * 0.04 * Height * 1e-3;
                return (Math.Log10(Math.Max(e, 1e-9)) - 4.8) / 1.5;
            }
        }

        /// <summary>Rough distance where the step is still felt by a person.</summary>
        public double StepFeltDistance => 1000 * Math.Pow(10, 0.5 * StepMagnitude - 0.3);

        // --- Body parts (female segment data, de Leva 1996 / Drillis–Contini) ---------------
        public static readonly (string en, double length, double mass)[] Parts =
        {
            ("Head", 0.130, 0.0668),
            ("Hand", 0.108, 0.0056),
            ("Arm", 0.440, 0.0449),
            ("Leg", 0.491, 0.2088),
            ("Foot", 0.152, 0.0129),
        };

        public double PartLength(int i) => Parts[i].length * Height;
        public double PartMass(int i) => Parts[i].mass * Mass;
    }
}
