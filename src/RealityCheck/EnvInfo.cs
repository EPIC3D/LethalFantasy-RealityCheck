using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static RealityCheck.PausePanel;

namespace RealityCheck
{
    /// <summary>
    /// Extra facts for the level environments (Rain, Shoe, Sock, Stomach). The village and you are
    /// shrunk together, so everything of hers – raindrops, her foot, her sock, her stomach – is k× bigger.
    /// </summary>
    internal static class EnvInfo
    {
        public enum Kind { None, Rain, Shoe, Sock, Stomach }

        public static Kind Current()
        {
            string env;
            try { env = SceneLoader.InstanceData?.GetChosenEnvironmentString ?? "None"; }
            catch { return Kind.None; }
            if (env.Contains("Stomach")) return Kind.Stomach;
            if (env.Contains("Sock")) return Kind.Sock;
            if (env.Contains("Shoe")) return Kind.Shoe;
            if (env.Contains("Rain")) return Kind.Rain;
            return Kind.None;
        }

        // --- Rain ------------------------------------------------------------------------------------
        private const double DropMm = 2.5;            // a typical raindrop at her size
        private const double KnockDownN = 300;        // a hard shove knocks a person over

        /// <summary>Her raindrop seen by you: diameter (m), mass (kg), falling speed (m/s), impact force (N).</summary>
        public static (double d, double kg, double v, double force) RainDrop(SizeStats s)
        {
            double d = DropMm / 1000 * s.K;
            double kg = 1000 * Math.PI * d * d * d / 6;
            double v = Math.Sqrt(4.0 / 3 * SizeStats.G * d * 1000 / (SizeStats.SeaLevelDensity * 0.5));
            double force = kg * v * v / d;            // stopped within its own size
            return (d, kg, v, force);
        }

        // --- Shoe ------------------------------------------------------------------------------------
        private const double FootSweatLPerDay = 0.25; // one foot

        /// <summary>Sweat dripping from the foot above you, as rain in mm per hour.</summary>
        public static double FootSweatRainMmH(SizeStats s) =>
            FootSweatLPerDay / 1000 * s.K * s.K * s.K / 24 / s.SoleArea * 1000;

        /// <summary>
        /// Extra warmth from the sole above (W/m²): skin at 29 °C replaces the clear sky (~ −20 °C radiant),
        /// covering ~70% of the sky.
        /// </summary>
        public const double SoleCover = 0.7;
        public static double SoleRadiantWm2 =>
            SoleCover * 0.97 * SizeStats.Sigma * (Math.Pow(29 + 273.15, 4) - Math.Pow(-20 + 273.15, 4));

        // Real sweat drops of the level, measured on screen (largest seen this scene, metres).
        private static double sweatMaxM;
        private static int sweatScene = -1;
        private static float nextSweatSample;

        /// <summary>Once per second: measure the falling sweat drops (SweatProjectile) of the Shoe level.</summary>
        public static void SampleSweat()
        {
            if (UnityEngine.Time.time < nextSweatSample) return;
            nextSweatSample = UnityEngine.Time.time + 1f;
            int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if (scene != sweatScene) { sweatScene = scene; sweatMaxM = 0; }
            if (Current() != Kind.Shoe) return;
            double mpu = Plugin.MetresPerUnit();
            if (mpu <= 0) return;
            foreach (var drop in UnityEngine.Object.FindObjectsOfType<SweatProjectile>())
                foreach (var r in drop.GetComponentsInChildren<Renderer>())
                {
                    var b = r.bounds.size;
                    double d = Math.Min(b.x, b.z) * mpu;   // width, not the stretched fall length
                    if (d > sweatMaxM) sweatMaxM = d;
                }
        }

        /// <summary>A sweat drop: measured if one was seen, else estimated from her size.</summary>
        private static (double d, double kg, double v, double force, bool seen) SweatDrop(SizeStats s)
        {
            var est = RainDrop(s);
            if (sweatMaxM <= 0) return (est.d, est.kg, est.v, est.force, false);
            double d = sweatMaxM, kg = 1000 * Math.PI * d * d * d / 6;
            double v = Math.Sqrt(4.0 / 3 * SizeStats.G * d * 1000 / (SizeStats.SeaLevelDensity * 0.5));
            return (d, kg, v, kg * v * v / d, true);
        }

        // --- Sock ------------------------------------------------------------------------------------
        private const double FibreM = 15e-6, YarnM = 0.4e-3;

        // --- Stomach ---------------------------------------------------------------------------------
        private const double StomachRestL = 1, StomachFullL = 4, GastricJuiceLPerDay = 2, ChurnEveryS = 20;

        // --- The environment's own giant ---------------------------------------------------------------
        // Shoe, sock and stomach belong to a much bigger giant than the one on the map. Their size is
        // measured once per scene from the rendered object and converted with the map's metres per unit.
        private static int measuredScene = -1;
        private static SizeStats giant;
        private static double gapAbove;
        private static float nextMeasure;

        private static GameObject EnvObject(Kind kind)
        {
            switch (kind)
            {
                case Kind.Shoe: var shoe = UnityEngine.Object.FindObjectOfType<LevelEnvironmentShoe>(); return shoe != null ? shoe.shoeObject : null;
                case Kind.Sock: var sock = UnityEngine.Object.FindObjectOfType<LevelEnvironmentSock>(); return sock != null ? sock.sockObject : null;
                case Kind.Stomach: var st = UnityEngine.Object.FindObjectOfType<LevelEnvironmentStomach>(); return st != null ? st.stomachObject : null;
                default: return null;
            }
        }

        /// <summary>The environment's giant (null if not measurable) and how far her shoe/sock/stomach wall is above you (m).</summary>
        public static (SizeStats s, double above) Giant(Kind kind)
        {
            int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if (scene == measuredScene) return (giant, gapAbove);
            if (UnityEngine.Time.unscaledTime < nextMeasure) return (null, 0);   // not loaded yet: retry at most once a second
            nextMeasure = UnityEngine.Time.unscaledTime + 1f;
            measuredScene = scene;
            giant = null;
            gapAbove = 0;
            try
            {
                var go = EnvObject(kind);
                double mpu = Plugin.MetresPerUnit();
                if (mpu <= 0) { measuredScene = -1; return (null, 0); } // no giantess yet – try again later
                if (go == null) { measuredScene = -1; return (null, 0); }   // not loaded yet – retry later
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) { measuredScene = -1; return (null, 0); }
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                double longest = Math.Max(b.size.x, Math.Max(b.size.y, b.size.z)) * mpu;
                // foot (15.2% of height); sock ≈ foot + ankle; stomach ≈ 30 cm on a 1.62 m woman
                double baseH = Plugin.AkipoHeight.Value;   // her proportions: a normal woman's
                double height = kind == Kind.Stomach ? longest / (0.30 / baseH)
                    : kind == Kind.Sock ? longest / (0.152 * 1.4)
                    : longest / 0.152;   // the bare foot itself
                giant = SizeStats.Compute(baseH, Plugin.AkipoMass.Value, height);
                var player = PlayerDriver.LocalPlayer;
                if (player != null) gapAbove = Math.Max(0, (b.max.y - player.transform.position.y) * mpu);
                Plugin.Log.LogDebug($"{kind}: object {longest:0} m → its giant is {height / 1000:0.0} km tall");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Environment size: " + e.Message);
            }
            return (giant, gapAbove);
        }

        public static void Build(StringBuilder sb, SizeStats s, WeightUnits u)
        {
            var kind = Current();
            if (kind == Kind.None) return;
            if (kind != Kind.Rain)
            {
                var (g, above) = Giant(kind);
                if (g != null)
                {
                    s = g;
                    Section(sb, "THE UNKNOWN GIANT OF THIS MAP");
                    Line(sb, "Estimated height", $"{Fmt.Length(g.Height, u)} <color={Dim}>(×{Fmt.Num(g.K)} a normal woman)</color>");
                    if (above > 0) Line(sb, "Her " + (kind == Kind.Stomach ? "stomach wall" : kind == Kind.Sock ? "sock" : "foot") + " above you",
                        $"{Fmt.Length(above, u)} <color={Dim}>~{Fmt.LikeLength(above)}</color>");
                }
            }
            double ph = Plugin.YouHeight;
            switch (kind)
            {
                case Kind.Rain:
                {
                    Section(sb, "THIS MAP: RAIN (her rain, at her size)");
                    var (d, kg, v, f) = RainDrop(s);
                    Line(sb, "A raindrop", $"{Fmt.Length(d, u)} wide, {Fmt.Mass(kg, u)} <color={Dim}>~{Fmt.LikeMass(kg)}</color>");
                    Line(sb, "Falls at", Fmt.Speed(v, u));
                    Line(sb, "Hits you with", Fmt.Force(f, u) + (f > KnockDownN
                        ? $"  <color={Bad}>knocks you over</color>"
                        : $"  <color={Good}>you stay on your feet</color>"));
                    break;
                }
                case Kind.Shoe:
                {
                    Section(sb, "THIS MAP: UNDER A GIANT FOOT");
                    Line(sb, "Foot above you", $"{Fmt.Length(s.FootLength, u)} × {Fmt.Length(s.FootWidth, u)} <color={Dim}>~{Fmt.LikeLength(s.FootLength)}</color> <color={Dim}>hides the sky</color>");
                    Line(sb, "Sole area", $"{Fmt.Area(s.SoleArea, u)} <color={Dim}>= {Fmt.Num(s.SoleArea / (0.45 * 0.3))} people standing</color>");
                    Line(sb, "Foot weighs", $"{Fmt.Mass(s.PartMass(4), u)} <color={Dim}>~{Fmt.LikeMass(s.PartMass(4))}</color>");
                    var (sd, skg, _, sf, seen) = SweatDrop(s);
                    Line(sb, "Biggest sweat drop", $"{Fmt.Length(sd, u)} wide " + "<ex>" + Fmt.LikeLength(sd) + "</ex>" +
                        $" <color={Dim}>{Fmt.Mass(skg, u)} ~{Fmt.LikeMass(skg)}{(seen ? "" : ", estimated")}</color>");
                    Line(sb, "Hits you with", Fmt.Force(sf, u) + (sf > KnockDownN
                        ? $"  <color={Bad}>knocks you over</color>"
                        : $"  <color={Good}>you stay on your feet</color>"));
                    double rain = FootSweatRainMmH(s);
                    Line(sb, "Sweat falling", $"{Fmt.Num(rain)} mm/h <color={Dim}>– {RainWord(rain)} on you</color>");
                    Line(sb, "Shade", $"no direct sun <color={Dim}>air ~1.5 °C cooler</color>");
                    Line(sb, "Her sole radiates", $"+{Fmt.Num(SoleRadiantWm2)} W/m² " + "<ex>like a warm ceiling</ex>" +
                        $" <color={Dim}>29 °C skin instead of the cold sky; distance does not matter, only how much sky it covers</color>");
                    break;
                }
                case Kind.Sock:
                {
                    Section(sb, "THIS MAP: INSIDE HER SOCK");
                    double fibre = FibreM * s.K, yarn = YarnM * s.K;
                    Line(sb, "One cotton fibre", $"{Fmt.Length(fibre, u)} thick <color={Dim}>~{Fmt.LikeLength(fibre)}</color>");
                    Line(sb, "One yarn", $"{Fmt.Length(yarn, u)} thick – {Fmt.Num(yarn / ph * 100)}% of your height");
                    Line(sb, "Gaps in the weave", $"{Fmt.Length(yarn * 0.5, u)} <color={Dim}>– daylight filters through</color>");
                    break;
                }
                case Kind.Stomach:
                {
                    Section(sb, "THIS MAP: INSIDE HER STOMACH");
                    double k3 = s.K * s.K * s.K;
                    double rest = StomachRestL / 1000 * k3, full = StomachFullL / 1000 * k3;
                    Line(sb, "Stomach, resting", $"{Fmt.Volume(rest, u)} <color={Dim}>= {Fmt.Lake(rest)}</color>");
                    Line(sb, "Stomach, full", $"{Fmt.Volume(full, u)} <color={Dim}>= {Fmt.Lake(full)}</color>");
                    double juice = GastricJuiceLPerDay / 1000 * k3;
                    Line(sb, "Acid per day", $"{Fmt.Volume(juice, u)} <color={Dim}>pH 1.5–3.5, as sour as lemon juice</color>");
                    Line(sb, "Churning wave", RealFantasy($"every {Fmt.Time(ChurnEveryS * Math.Sqrt(s.K))}", $"every {Fmt.Time(ChurnEveryS)}"));
                    Line(sb, "Air", $"<color={Bad}>hardly any oxygen</color> <color={Dim}>– swallowed air and CO2, 37 °C, 100% humid</color>");
                    break;
                }
            }
            sb.AppendLine();
        }

        private static string RainWord(double mmh) =>
            mmh < 0.5 ? "a light drizzle" : mmh < 4 ? "light rain" : mmh < 8 ? "steady rain" : mmh < 50 ? "heavy rain" : "a cloudburst";

        /// <summary>Running-text facts for the current environment.</summary>
        public static IEnumerable<string> Tips(SizeStats s, WeightUnits u)
        {
            var kind = Current();
            if (kind != Kind.Rain && kind != Kind.None)
            {
                var (g, _) = Giant(kind);
                if (g != null)
                {
                    s = g;
                    yield return kind == Kind.Shoe
                        ? $"The unknown giant whose foot hovers above you is {Fmt.Length(g.Height, u)} tall."
                        : $"The unknown giant whose {(kind == Kind.Stomach ? "stomach" : "sock")} you are in is {Fmt.Length(g.Height, u)} tall.";
                }
            }
            switch (kind)
            {
                case Kind.Rain:
                    var (d, kg, v, f) = RainDrop(s);
                    yield return $"Her raindrops are {Fmt.Length(d, u)} wide and weigh {Fmt.Mass(kg, u)} – " +
                                 (f > KnockDownN ? "one hit can knock you over." : "they sting, but you stay on your feet.");
                    break;
                case Kind.Shoe:
                    yield return $"Her foot above you is {Fmt.Length(s.FootLength, u)} long – it hides the whole sky.";
                    if (sweatMaxM > 0)
                        yield return $"Her sweat drops are up to {Fmt.Length(sweatMaxM, u)} wide – {Fmt.LikeLength(sweatMaxM)}.";
                    yield return $"Sweat from the foot above falls on you like {RainWord(FootSweatRainMmH(s))} ({Fmt.Num(FootSweatRainMmH(s))} mm/h).";
                    break;
                case Kind.Sock:
                    yield return $"Each yarn of the sock is {Fmt.Length(YarnM * s.K, u)} thick – like a giant rope.";
                    break;
                case Kind.Stomach:
                    yield return $"Her stomach holds {Fmt.Volume(StomachFullL / 1000 * s.K * s.K * s.K, u)} when full – {Fmt.Lake(StomachFullL / 1000 * s.K * s.K * s.K)}.";
                    yield return "There is hardly any oxygen in a stomach – only swallowed air and CO2.";
                    break;
            }
        }
    }
}
