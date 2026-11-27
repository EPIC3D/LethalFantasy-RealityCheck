using System;
using UnityEngine.SceneManagement;

namespace RealityCheck
{
    /// <summary>
    /// Air around the player by map, time of day and level environment.
    /// Sock and Stomach put you inside her – there the air is warmed by her body.
    /// Shoe: a giant bare foot hovers above you – shade and sweat drops, but open air.
    /// </summary>
    internal static class Climate
    {
        public const double DefaultHumidity = 50;

        private static double MapBaseC(string scene)
        {
            if (scene.Contains("Plains")) return 24;
            if (scene.Contains("Long Day")) return 23;
            if (scene.Contains("Meadow")) return 20;
            if (scene.Contains("Dawn") || scene.Contains("Dusk")) return 17;
            if (scene.Contains("Long Night")) return 14;
            return 18; // Lobby and anything new
        }

        /// <summary>Hour of the in-game clock (0–24), or -1 when the level has no clock.</summary>
        public static double Hour()
        {
            var tod = TimeOfDay.Instance;
            if (tod == null) return -1;
            return tod.time % 1440f / 60f;
        }

        private static string Environment()
        {
            try { return SceneLoader.InstanceData?.GetChosenEnvironmentString ?? "None"; }
            catch { return "None"; }
        }

        /// <summary>Air temperature at a height above the ground (map + time of day + air layers).</summary>
        public static double TempAt(double height)
        {
            var c = Current();
            if (c.where.StartsWith("inside")) return c.tempC;
            return c.tempC + (SizeStats.AirTemperatureC(height) - SizeStats.AirTemperatureC(0));
        }

        /// <summary>Air temperature (°C), relative humidity (%), and a short description.</summary>
        public static (double tempC, double humidity, string where) Current()
        {
            string env = Environment();
            if (env.Contains("Stomach")) return (37, 100, "inside a stomach");
            if (env.Contains("Sock")) return (32, 85, "inside a sock");

            string scene = SceneManager.GetActiveScene().name;
            double t = MapBaseC(scene) + Plugin.AmbientOffsetC.Value;
            double hour = Hour();
            if (hour >= 0) t += 6 * Math.Cos(2 * Math.PI * (hour - 15) / 24); // warmest at 15:00, coldest at 03:00
            double humidity = DefaultHumidity;
            string where = scene;
            if (hour >= 0) where += $", {(int)hour:00}:{(int)(hour * 60 % 60):00}";
            if (env.Contains("Shoe"))
            {
                t -= 1.5;        // her foot blocks the sun (its warm sole is counted as radiant heat)
                humidity = 80;   // her sweat drops evaporate around you
                where += ", under a giant foot";
            }
            if (env.Contains("Rain"))
            {
                t -= 4;
                humidity = 90;
                where += ", rain";
            }
            return (t, humidity, where);
        }
    }
}
