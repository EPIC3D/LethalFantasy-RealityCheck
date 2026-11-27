using System;

namespace RealityCheck
{
    /// <summary>
    /// Voice loudness, range, intelligibility and conversation delay, with the atmosphere's layers:
    /// thin air at her mouth radiates less sound, the speed of sound follows the temperature
    /// layers, and air absorbs higher frequencies. Refraction (sound bending in layers) is ignored.
    /// </summary>
    public sealed partial class SizeStats
    {
        public const double SpeechW = 1e-5;        // acoustic power of normal speech (≈ 60 dB at 1 m)
        public const double ShoutW = 3e-3;         // shouting (≈ 84 dB at 1 m)
        public const double QuietAmbientDb = 30;   // quiet countryside – what a voice must beat to be heard
        public const double UnderstandDb = 45;     // about what you need to follow speech outdoors
        public const double F2Hz = 1500;           // second formant: tells vowels apart
        public const double SpeechBandHz = 500;    // where most speech energy is
        public const double ThinkS = 0.3;          // time to start answering

        public double MouthHeight => 0.9 * Height;

        /// <summary>Her voice power (W): real ∝ k² (bigger lungs and vocal tract, same pressure), fantasy ∝ k³.</summary>
        public double VoiceW(double baseW, bool fantasy) => baseW * (fantasy ? K * K * K : K * K);

        /// <summary>Speed of sound at an altitude (m/s), from the ISA temperature layers.</summary>
        public static double SoundSpeed(double h) => 331.3 * Math.Sqrt(1 + AirTemperatureC(h) / 273.15);

        /// <summary>Air absorption in dB per km at frequency f (≈ 5 dB/km at 1 kHz, ∝ f²).</summary>
        public static double AbsorptionDbPerKm(double f) => 5 * (f / 1000) * (f / 1000);

        /// <summary>Sound level (dB) at distance d from a voice of this power, emitted at height h, at frequency f.</summary>
        public static double Level(double watts, double d, double sourceHeight, double f)
        {
            d = Math.Max(d, 0.1);
            double radiated = watts * PressureRatio(sourceHeight); // thin air radiates less
            double spread = 10 * Math.Log10(radiated / (4 * Math.PI * d * d) / 1e-12);
            return spread - AbsorptionDbPerKm(f) * d / 1000;
        }

        /// <summary>Distance where the level falls to db (binary search, absorption included).</summary>
        public static double HeardFrom(double watts, double db, double sourceHeight, double f)
        {
            double lo = 0.1, hi = 1e7;
            if (Level(watts, lo, sourceHeight, f) < db) return 0;
            for (int i = 0; i < 60; i++)
            {
                double mid = Math.Sqrt(lo * hi);
                if (Level(watts, mid, sourceHeight, f) > db) lo = mid; else hi = mid;
            }
            return lo;
        }

        /// <summary>Travel time of sound along a straight line from height h1 to h2 over a total distance d.</summary>
        public static double SoundTime(double d, double h1, double h2)
        {
            const int n = 50;
            double t = 0;
            for (int i = 0; i < n; i++)
            {
                double h = h1 + (h2 - h1) * (i + 0.5) / n;
                t += d / n / SoundSpeed(Math.Max(0, h));
            }
            return t;
        }

        public double VoiceBandReal => SpeechBandHz / K;
        public double FormantReal => F2Hz / K;
        public double SpeechSlowdown => Math.Sqrt(K);
        public double ThinkReal => ThinkS * Math.Sqrt(K);

        /// <summary>Your voice as she hears it: her ears are tuned k× lower, so you sound k× higher.</summary>
        public double YourPitchToHer => VoiceF0Hz * K;
    }
}
