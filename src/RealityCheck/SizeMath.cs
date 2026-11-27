using System;

namespace RealityCheck
{
    /// <summary>
    /// Square–cube law estimates for a uniformly scaled human body.
    /// k = current height / natural height. Lengths scale with k, areas with k², volume and
    /// mass with k³. All values are rough, order-of-magnitude figures for fun – not science.
    /// Pure C#, no Unity dependency.
    /// </summary>
    public sealed partial class SizeStats
    {
        // --- Constants ---------------------------------------------------------------
        public const double G = 9.81;
        public const double BodyDensity = 985;        // kg/m³ (human body, lungs included)
        public const double BloodDensity = 1060;      // kg/m³
        public const double MmHg = 133.322;           // Pa
        public const double EarthRadius = 6_371_000; // m
        public const double ScaleHeight = 8_434;      // m, isothermal atmosphere

        // Reference values at natural size (k = 1).
        public const double FootLengthRatio = 0.152;  // foot length / height
        public const double FootWidthRatio = 0.38;    // foot width / foot length
        public const double SoleFill = 0.72;          // footprint area / (length × width)
        public const double HeartToBrainRatio = 0.19; // vertical heart–brain distance / height
        public const double BaseDiastolicDriveMmHg = 90; // pressure the brain needs on top of the column
        public const double ActiveHeatFluxWm2 = 130;  // walking heat output per skin area
        public const double MaxCoolingWm2 = 700;      // best case sweating/evaporation
        public const double WalkingBoneStressMPa = 25;// peak femur stress while walking
        public const double BoneStrengthMPa = 170;    // cortical bone, compression
        public const double SoilBearingKPa = 200;     // firm soil before the foot sinks in
        public const double GraniteStrengthMPa = 150; // rock is crushed above this
        public const double NerveSpeed = 60;          // m/s, fast motor/sensory fibres
        public const double VoiceF0Hz = 210;          // average adult female voice
        public const double BaseLiftKg = 40;          // what she can lift at natural size
        public const double FingerWidthRatio = 0.0105;// index finger width / height
        public const double DeathZoneM = 8000;
        public const double ThrowSpeed = 18;          // m/s, average woman throwing a baseball
        public const double PlayerThrowSpeed = 25;    // m/s, average adult throwing a baseball
        public const double PlayerLiftKg = 60;        // what you can lift
        public const double SpeedOfSound = 343;
        public const double BallDiameter = 0.074;     // m, baseball
        public const double BallMass = 0.145;         // kg
        public const double TntJoulePerTon = 4.184e9;

        // --- Inputs --------------------------------------------------------------------
        public double BaseHeight, BaseMass, Height;

        // --- Results -----------------------------------------------------------------------
        public double K, Mass, Volume, SurfaceArea;
        public double FootLength, FootWidth, SoleArea, FingerWidth, StepLength, EyeHeight;
        public double GroundPressurePa;
        public double LiftRealKg, LiftFantasyKg;
        public double WalkSpeedReal, WalkSpeedFantasy, SlowMotion;
        public double BloodPressureMmHg, HeatFluxWm2, HeatFluxKleiberWm2, BoneStressMPa;
        public double NerveDelay, VoiceHz, HeadAirPressure, HorizonKm;
        public double BallSize, BallKg, ImpactRealTnt, ImpactFantasyTnt;
        public double ThrowSpeedReal, ThrowSpeedFantasy, ThrowRangeReal, ThrowRangeFantasy;

        public static SizeStats Compute(double baseHeight, double baseMass, double height)
        {
            var s = new SizeStats { BaseHeight = baseHeight, BaseMass = baseMass, Height = height };
            double k = s.K = height / baseHeight;
            double k2 = k * k, k3 = k2 * k;

            s.Mass = baseMass * k3;
            s.Volume = s.Mass / BodyDensity;
            // Mosteller body surface area at natural size, scaled by k².
            s.SurfaceArea = Math.Sqrt(baseHeight * 100 * baseMass / 3600) * k2;

            s.FootLength = FootLengthRatio * height;
            s.FootWidth = FootWidthRatio * s.FootLength;
            s.SoleArea = SoleFill * s.FootLength * s.FootWidth;
            s.FingerWidth = FingerWidthRatio * height;
            s.StepLength = 0.413 * height;
            s.EyeHeight = 0.935 * height;
            s.GroundPressurePa = s.Mass * G / (2 * s.SoleArea);

            s.LiftRealKg = BaseLiftKg * k2;      // muscle force ∝ cross-section
            s.LiftFantasyKg = BaseLiftKg * k3;   // "everything scales" fantasy

            // Walking speed from the Froude number (Fr ≈ 0.25, leg ≈ 0.53 h): v ∝ √k.
            s.WalkSpeedReal = Math.Sqrt(0.25 * G * 0.53 * height);
            double baseWalk = Math.Sqrt(0.25 * G * 0.53 * baseHeight);
            s.WalkSpeedFantasy = baseWalk * k;
            s.SlowMotion = Math.Sqrt(k);         // pendulum time ∝ √length

            double column = BloodDensity * G * HeartToBrainRatio * height / MmHg;
            s.BloodPressureMmHg = BaseDiastolicDriveMmHg + column;

            s.HeatFluxWm2 = ActiveHeatFluxWm2 * k;               // human cells: heat ∝ k³, skin ∝ k²
            s.HeatFluxKleiberWm2 = ActiveHeatFluxWm2 * Math.Pow(k, 0.25); // heat ∝ M^0.75
            s.BoneStressMPa = WalkingBoneStressMPa * k;

            s.NerveDelay = 0.9 * height / NerveSpeed;
            s.VoiceHz = VoiceF0Hz / k;
            s.HeadAirPressure = Math.Exp(-height / ScaleHeight);
            s.HorizonKm = Math.Sqrt(2 * EarthRadius * s.EyeHeight) / 1000;
            // Baseball throw, 45°, no air drag. Real: limb tip speed ∝ √k. Fantasy: ∝ k.
            s.ThrowSpeedReal = ThrowSpeed * Math.Sqrt(k);
            s.ThrowSpeedFantasy = ThrowSpeed * k;
            s.ThrowRangeReal = RangeNoAir(s.ThrowSpeedReal, height);
            s.ThrowRangeFantasy = RangeNoAir(s.ThrowSpeedFantasy, height);
            // Her ball grows with her (×k size, ×k³ mass).
            s.BallSize = BallDiameter * k;
            s.BallKg = BallMass * k3;
            s.ImpactRealTnt = 0.5 * s.BallKg * s.ThrowSpeedReal * s.ThrowSpeedReal / TntJoulePerTon;
            s.ImpactFantasyTnt = 0.5 * s.BallKg * s.ThrowSpeedFantasy * s.ThrowSpeedFantasy / TntJoulePerTon;
            return s;
        }

        /// <summary>Best range without air from release height h (same start as the drag simulation).</summary>
        public static double RangeNoAir(double v, double h)
        {
            double best = 0;
            for (double deg = 5; deg <= 45; deg += 1)
            {
                double a = deg * Math.PI / 180, vs = v * Math.Sin(a);
                best = Math.Max(best, v * Math.Cos(a) * (vs + Math.Sqrt(vs * vs + 2 * G * h)) / G);
            }
            return best;
        }

        public static double PlayerThrowRange => PlayerThrowSpeed * PlayerThrowSpeed / G;

        /// <summary>Simplified ground profile: (bottom depth m, allowable bearing Pa, en, hu).</summary>
        public static readonly (double depth, double bearing, string en)[] Ground =
        {
            (0.3, 50e3, "topsoil"),
            (3, 200e3, "firm clay"),
            (20, 600e3, "dense gravel"),
            (60, 3e6, "weathered rock"),
            (double.PositiveInfinity, 150e6, "granite bedrock"),
        };

        // Terzaghi bearing capacity of a medium-dense soil (φ ≈ 30°): it grows with the width of the
        // foot (more soil carries it) and with the depth it has sunk (soil above holds it down).
        public const double SoilWeight = 18_000;   // N/m³
        public const double NGamma = 15.7, Nq = 18.4;
        public const double BedrockDepth = 60;

        /// <summary>
        /// How deep a foot of this width sinks at this pressure (continuous), and the layer it stops in.
        /// Layer -1 = even the granite bedrock is crushed.
        /// </summary>
        public static (double depth, int layer) SinkDepth(double pressurePa, double footWidth)
        {
            double widthPart = 0.5 * SoilWeight * footWidth * NGamma;
            double depth = Math.Max(0, (pressurePa - widthPart) / (SoilWeight * Nq));
            if (depth >= BedrockDepth)
            {
                if (pressurePa > GraniteStrengthMPa * 1e6) return (double.PositiveInfinity, -1);
                return (BedrockDepth, Ground.Length - 1);   // stopped on the bedrock
            }
            int layer = 0;
            while (layer < Ground.Length - 1 && depth > Ground[layer].depth) layer++;
            return (depth, layer);
        }

        public (double depth, int layer) SinkDepth() => SinkDepth(GroundPressurePa, FootWidth);

        public double AnkleHeight => 0.039 * Height;
        public double KneeHeight => 0.285 * Height;

        /// <summary>Height (k) at which a quantity that grows linearly with k reaches a limit.</summary>
        public double HeightWhere(double valueAtK1, double limit) => BaseHeight * limit / valueAtK1;
    }
}
