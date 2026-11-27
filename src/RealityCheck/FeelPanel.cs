using System;
using TMPro;
using UnityEngine;

namespace RealityCheck
{
    /// <summary>
    /// Bottom-left gameplay readout: how warm the air feels to YOU and how your body copes.
    /// Felt temperature = air temperature + her radiant warmth (or her skin when you touch her).
    /// Your core temperature follows a simple heat balance, so heat builds up over time.
    /// </summary>
    internal static class FeelPanel
    {
        private static GameUI owner;
        private static GameObject root;
        private static TextMeshProUGUI text;
        private static float nextUpdate, lastUpdate;
        private static double core = 37.0;
        private static Giantess windG;
        private static double windMpu, windHeight;
        private static double sweatLevel;              // 0 … 1 of what your skin can sweat right now
        private static double feltSmooth = double.NaN, windSmooth;
        private const double SweatOnsetSeconds = 120, SweatFadeSeconds = 300, FeelSeconds = 30;   // FeelSeconds is real time (display smoothing)

        // Your body (normal adult).
        private const double BodyHeatW = 150;          // moving around
        private const double SkinAreaM2 = 1.9;
        private const double ExchangeW_m2K = 9;        // radiation + convection
        private const double SweatMaxW = 400;          // evaporative cooling in open air
        private const double SweatEnclosedW = 50;      // in her hand / hug: humid, little airflow
        private const double PlayerMassKg = 75, HeatCapacity = 3500;
        private const double RecoverSeconds = 300;     // core drifts back to 37 °C when you can cool
        private const double ExhaleShare = 0.4;        // part of each breathing cycle spent exhaling
        private const double ClothingFactor = 0.3;     // clothes cut the extra cold loss to ~30%
        private const double ChillLossW = 100;         // net loss above this (cold and/or wind) chills you

        private const float ShowSeconds = 6f;   // how long a new state stays on screen
        private static string lastState;
        private static float stateChangedAt = -100, feelAlpha;
        private static bool herEffect, danger;
        private static CanvasGroup feelGroup;

        public static void Tick(GameUI ui)
        {
            if (owner != ui || root == null)
            {
                owner = ui;
                var font = ui.GetComponentInChildren<TextMeshProUGUI>(true);
                (root, text) = Ui.CreatePanel(ui.canvas, font,
                    new Vector2(0.01f, 0.02f), new Vector2(0.42f, 0.30f), "RealityCheck_FeelPanel", 20, background: false);
                text.alignment = TextAlignmentOptions.BottomLeft;
                text.enableAutoSizing = false; // fixed size: lines appearing or disappearing must not shrink the text
                text.fontSize = 22;
                text.enableWordWrapping = false;
                core = 37.0;
                sweatLevel = 0;
                feltSmooth = double.NaN;
                windSmooth = 0;
                Wind.Reset();
                lastUpdate = Time.time;
            }

            var ls = LevelSelect.Instance;
            bool levelSelect = ls != null && ls.canvas != null && ls.canvas.enabled;
            bool show = Plugin.FeelEnabled.Value && ui.canvas != null && ui.canvas.enabled && !PauseMenu.IsPaused && !levelSelect
                        && PlayerDriver.LocalPlayer != null && !Ui.CraftingOpen();
            if (root.activeSelf != show) root.SetActive(show);
            if (!show) { Wind.Reset(); return; }   // no stale positions after a pause or menu

            // Only when something happens: fade in fast, fade out after a few seconds of nothing new.
            bool wanted = !Plugin.FeelOnChange.Value || herEffect || danger || Time.unscaledTime - stateChangedAt < ShowSeconds;
            feelAlpha = Mathf.MoveTowards(feelAlpha, wanted ? 1 : 0, Time.unscaledDeltaTime / (wanted ? 0.25f : 1f));
            if (feelGroup == null) feelGroup = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
            feelGroup.alpha = feelAlpha;
            feelGroup.blocksRaycasts = false;
            // Body-part speeds are sampled every frame (a swinging foot must not cancel out); the panel reads the peak.
            if (windG != null && Time.deltaTime > 0)
                Wind.SampleFrame(windG, Ui.Eye() ?? PlayerDriver.LocalPlayer.transform.position, windMpu, windHeight, Time.deltaTime);
            if (Time.time < nextUpdate) return;

            float now = Time.time;               // game time: frozen while paused
            double dt = Math.Min(5, now - lastUpdate);
            lastUpdate = now;
            nextUpdate = now + 1f; // at most once a second
            text.text = Update(dt * Plugin.BodyTimeScale.Value);   // your body runs on (sped-up) game time
            Ui.FitToText(root, text, new Vector2(0.01f, 0.02f), new Vector2(0, 0));
        }

        /// <summary>Closest giantess: distance from you to her body and to her mouth (m), her stats, and your height above her feet (m).</summary>
        private static (double distance, SizeStats s, double altitude, double mouth, Giantess g, double mPerUnit, double facing) Nearest()
        {
            Vector3 bodyPos = PlayerDriver.LocalPlayer.transform.position;
            Vector3? eye = Ui.Eye();
            double best = double.PositiveInfinity, altitude = 0, mouthDist = double.PositiveInfinity, bestMpu = 1, facing = 0;
            Giantess bestG = null;
            SizeStats bestStats = null;
            foreach (var g in PausePanel.Giantesses())
            {
                if (g.headTransform == null) continue;
                Vector3 feet = g.transform.position, head = g.headTransform.position;
                float headUnits = head.y - feet.y;
                if (headUnits <= 0.01f) continue;
                var body = Plugin.BodyOf(Plugin.CharacterOf(g));
                float h = Plugin.HeightOf(g);
                double mPerUnit = h * 0.93 / headUnits;
                // You: your eyes (camera) and the feet under them; without a camera, the body position.
                Vector3 yourHead = eye ?? bodyPos + Vector3.up * (float)(1.6 / mPerUnit);
                Vector3 p = yourHead - Vector3.up * (float)(1.6 / mPerUnit);
                // Her body as a vertical line from feet to head, ~12% of her height in radius.
                Vector3 axis = head - feet;
                float t = Mathf.Clamp01(Vector3.Dot(p - feet, axis) / axis.sqrMagnitude);
                double d = Vector3.Distance(p, feet + axis * t) * mPerUnit - 0.12 * h;
                if (d < best)
                {
                    best = Math.Max(0, d);
                    bestStats = SizeStats.Compute(body.height, body.mass, h);
                    altitude = Math.Max(0, (p.y - feet.y) * mPerUnit);   // climbing her lifts you into thinner, colder air
                    Vector3 mouth = g.mouthTransform != null ? g.mouthTransform.position : head;
                    mouthDist = Vector3.Distance(yourHead, mouth) * mPerUnit;
                    facing = BreathFacing(g, head, mouth, yourHead);
                    bestG = g;
                    bestMpu = mPerUnit;
                }
            }
            return (best, bestStats, altitude, mouthDist, bestG, bestMpu, facing);
        }

        /// <summary>
        /// 1 when you are in the jet of her breath, falling to 0 outside it. The jet leaves her face forward
        /// (headFront direction) tilted ~25° down (breathing out through the nose); the cone is ~20° wide.
        /// </summary>
        private static double BreathFacing(Giantess g, Vector3 head, Vector3 mouth, Vector3 you)
        {
            Vector3 forward = g.headFrontTransform != null ? (g.headFrontTransform.position - head) : g.headTransform.forward;
            if (forward.sqrMagnitude < 1e-6f) return 1;
            forward.Normalize();
            // Keep her head's pitch (she may look down at you); tilt the jet 25° further down, towards her chin.
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            Vector3 chin = Vector3.Cross(right.normalized, forward).normalized;
            Vector3 jet = (forward * Mathf.Cos(25f * Mathf.Deg2Rad) + chin * Mathf.Sin(25f * Mathf.Deg2Rad)).normalized;
            Vector3 toYou = you - mouth;
            if (toYou.sqrMagnitude < 1e-6f) return 1;
            double angle = Vector3.Angle(jet, toYou);
            return Math.Exp(-(angle / 20) * (angle / 20));
        }

        /// <summary>
        /// Contact during her interactions (user's mapping): pet = hand, hug = chest + hand + breath,
        /// lap = body/thigh + hand, foot massage = sole, carried = hand.
        /// cover = share of you her skin covers; enclosed = humid, little airflow (poor sweating).
        /// </summary>
        private static (string name, double skinC, double cover, bool enclosed, bool breath) Interaction(Giantess g)
        {
            try
            {
                var b = g != null && g.aiLogic != null ? g.aiLogic.currentBehaviour : null;
                if (b == null) return (null, 0, 0, false, false);
                string t = b.GetType().Name;
                if (t.Contains("Hug")) return ("chest & hands", 34.5, 0.9, true, true);   // chest, breath, left hand under you, right hand on top
                if (t.Contains("Lap")) return ("lap & left hand", 33, 0.7, true, false);
                if (t.Contains("FootMassage")) return ("left sole", 29, 0.4, false, false);
                if (t.Contains("Pet")) return ("right hand", 34, 0.5, false, false);
                if (t.Contains("GrabPlayer")) return ("hand", 34, 0.8, true, false);
            }
            catch { }
            return (null, 0, 0, false, false);
        }

        private static string OxygenFor(double alt)
        {
            int lvl = SizeStats.OxygenLevel(alt);
            string pct = Fmt.Num(SizeStats.PressureRatio(alt) * 100) + "%";
            return lvl switch
            {
                1 => $"<color={PausePanel.Warn}>{pct} · thin air, you get short of breath</color>",
                2 => $"<color={PausePanel.Warn}>{pct} · hypoxia, headache and dizziness</color>",
                3 => $"<color={PausePanel.Bad}>{pct} · death zone, minutes to pass out</color>",
                4 => $"<color={PausePanel.Bad}>{pct} · your blood would boil</color>",
                _ => $"<color={PausePanel.Bad}>{pct} · space</color>",
            };
        }

        private static string Update(double dt)
        {
            var u = Plugin.Units.Value;
            var (d, s, alt, mouthDist, nearG, mpu, facing) = Nearest();
            // Air around you: map + time of day + environment, cooled by the atmosphere's layers at your height
            // (not inside her sock or stomach – there her body sets the temperature).
            var climate = Climate.Current();
            bool inside = climate.where.StartsWith("inside");
            double air = inside ? climate.tempC : climate.tempC + (SizeStats.AirTemperatureC(alt) - SizeStats.AirTemperatureC(0));
            double ambientHumidity = climate.humidity;

            // Warmth and touch from her nearest body parts (foot, shin, hand …), not from her body as a whole.
            double radiant = 0;
            string warmPart = null;
            if (nearG != null)
            {
                Vector3 you = Ui.Eye() ?? PlayerDriver.LocalPlayer.transform.position;
                var (rad, surface, part) = Wind.Warmth(nearG, you, mpu, s.Height);
                if (part != null) { radiant = rad; d = surface; warmPart = part; }
                else radiant = s.RadiantWm2(d);

                // Every other giantess adds her own warmth (e.g. the Twins on both sides of you).
                int warmers = radiant >= SizeStats.FeltRadiantWm2 ? 1 : 0;
                foreach (var other in PausePanel.Giantesses())
                {
                    if (other == nearG || other.headTransform == null) continue;
                    float units = other.headTransform.position.y - other.transform.position.y;
                    if (units <= 0.01f) continue;
                    float h = Plugin.HeightOf(other);
                    var (r2, _, p2) = Wind.Warmth(other, you, h * 0.93 / units, h);
                    if (p2 == null) continue;
                    radiant += r2;
                    if (r2 >= SizeStats.FeltRadiantWm2) warmers++;
                }
                double full = 0.97 * SizeStats.Sigma * (Math.Pow(SizeStats.SkinK, 4) - Math.Pow(SizeStats.AmbientK, 4));
                radiant = Math.Min(radiant, full);   // at most: warm skin all around you
                if (warmers > 1) warmPart = "both of them";
            }
            // Shoe level: the giant sole above covers most of the sky – a 29 °C ceiling instead of the cold sky.
            double soleRadiant = !inside && EnvInfo.Current() == EnvInfo.Kind.Shoe ? EnvInfo.SoleRadiantWm2 : 0;
            radiant += soleRadiant;
            bool touching = s != null && d < 0.5;   // her skin within half a metre: in her hand, on her foot, hugged
            double feel = touching ? Math.Max(air, SizeStats.NormalSkinC) : air + radiant / ExchangeW_m2K;

            // Interactions: the game only moves the camera, so the contact comes from what she is doing.
            var act = Interaction(nearG);
            if (act.name != null)
            {
                touching = act.enclosed || touching;
                double skin = Math.Max(act.skinC, Math.Min(air, 37));  // in hot air her skin warms up too (max ~37 °C)
                feel = air + (skin - air) * act.cover;                   // how much of you her skin covers
                warmPart = act.name;
            }

            // Her breath: a warm (34 °C), fully humid jet. It mixes with the air like a round jet:
            // the share of her breath left at your distance ≈ 6 × mouth opening / distance.
            double mix = 0, gustC = 0, every = 0, humidity = ambientHumidity;
            if (s != null && !double.IsInfinity(mouthDist))
            {
                double opening = 0.012 * s.Height;
                mix = Math.Min(1, 6 * opening / Math.Max(mouthDist, 0.01)) * facing; // only inside the cone of her breath
                if (act.breath) mix = Math.Max(mix, 0.6);                           // hugged: she breathes right on you
                if (mix > 0.05)
                {
                    gustC = air + (Math.Max(SizeStats.BreathC, Math.Min(air, 37)) - air) * mix;   // exhale: 34 °C, up to 37 in hot air
                    feel = Math.Max(feel, feel + (gustC - feel) * ExhaleShare); // averaged over the breathing cycle
                    every = 60 / s.BreathRateReal;
                    humidity = Math.Max(humidity, ambientHumidity + (100 - ambientHumidity) * mix);
                }
            }

            // Wind on you: her moving body (measured from the animation) and her breath.
            double motionWind = 0, breathWind = 0;
            string windFrom = null;
            if (nearG != null)
            {
                windG = nearG; windMpu = mpu; windHeight = s.Height;
                var (mw, part) = Wind.Peak;
                motionWind = mw;
                windFrom = part;
                if (mix > 0.05) breathWind = Wind.FromBreath(s, mouthDist) * facing;
            }
            // Optional base breeze: slowly gusting, stronger higher up (none inside her sock or stomach).
            double breeze = inside ? 0 : Plugin.BaseWind.Value * (climate.where.Contains("rain") ? 2 : 1)
                * (0.7 + 0.6 * Mathf.PerlinNoise(Time.time * 0.05f, 0.3f))
                * Math.Min(20, Math.Pow(Math.Max(alt, 2) / 2, 0.14));
            double windNow = Math.Sqrt(breeze * breeze + motionWind * motionWind + breathWind * breathWind);
            windSmooth += (windNow - windSmooth) * Math.Min(1, dt / Plugin.BodyTimeScale.Value / 3); // ~3 s real-time average
            double wind = windSmooth;
            if (breathWind > motionWind) windFrom = "breath";
            if (Math.Max(motionWind, breathWind) < Math.Max(0.05, breeze)) windFrom = null;   // still air or just the breeze
            double exchange = ExchangeW_m2K + 3 * Math.Min(wind, 15);   // wind strips heat from (or brings it to) your skin

            // Your heat balance. Humid air (her breath, a hug) makes your sweat evaporate poorly.
            // Your skin does not jump to a new temperature: the felt value follows in ~20 s.
            feltSmooth = double.IsNaN(feltSmooth) ? feel : feltSmooth + (feel - feltSmooth) * Math.Min(1, dt / Plugin.BodyTimeScale.Value / FeelSeconds); // ~30 s real-time average
            feel = feltSmooth;

            // Shrunk: you make less heat (k^2.25) but lose it over relatively more skin (k²), and your
            // small body warms and chills much faster (heat capacity k³) – like a mouse.
            double sc = Plugin.PlayerScale(), sc2 = sc * sc;
            double bodyHeat = BodyHeatW * Math.Pow(sc, 2.25), skinArea = SkinAreaM2 * sc2;
            double sweatCap = sc2 * (touching ? SweatEnclosedW : SweatMaxW) * (1 - 0.8 * Math.Max(0, humidity - Climate.DefaultHumidity) / (100 - Climate.DefaultHumidity));
            double dry = exchange * skinArea * (34 - feel);       // negative when the air is warmer than your skin
            if (dry > bodyHeat) dry = bodyHeat + (dry - bodyHeat) * ClothingFactor; // clothes hold heat in the cold
            double needSweat = Math.Max(0, bodyHeat - dry);
            // Sweating builds up over ~2 min and fades over ~5 min (you stay damp for a while).
            double demand = sweatCap > 0 ? Math.Min(1, needSweat / sweatCap) : 1;
            double tau = demand > sweatLevel ? SweatOnsetSeconds : SweatFadeSeconds;
            sweatLevel += (demand - sweatLevel) * Math.Min(1, dt / tau);
            double net = bodyHeat - dry - sweatLevel * sweatCap;     // > 0: you heat up
            double change = net * dt / (PlayerMassKg * sc2 * sc * HeatCapacity);
            if (net > 0)
            {
                // Gaining heat: back up to 37 °C quickly when chilled, then heat up from there.
                core = core < 37 ? Math.Min(37, core + Math.Max(change, (37 - core) * Math.Min(1, dt / RecoverSeconds))) : core + change;
            }
            else if (core > 37)
            {
                core += Math.Max(37.0 - core, change);
                core += (37.0 - core) * Math.Min(1, dt / RecoverSeconds);
            }
            else if (net < -ChillLossW * sc2)
            {
                core = Math.Max(25, core + change);   // clearly losing more heat than you make (cold, wind): you chill
            }
            else if (core < 37)
            {
                core += (37.0 - core) * Math.Min(1, dt / RecoverSeconds); // mild air: you warm back up
            }

            string feelColor = feel < 10 ? PausePanel.Dim : feel < 26 ? PausePanel.Good : feel < 32 ? PausePanel.Warn : PausePanel.Bad;
            string coreColor = core < 33 ? PausePanel.Bad : core < 35.5 ? PausePanel.Warn : core < 37.6 ? PausePanel.Good : core < 39 ? PausePanel.Warn : PausePanel.Bad;

            string state =
                core < 33 ? $"<color={PausePanel.Bad}>SEVERE HYPOTHERMIA! Get warm now</color>" :
                core < 35 ? $"<color={PausePanel.Warn}>Hypothermia – find warmth</color>" :
                core >= 40 ? $"<color={PausePanel.Bad}>HEATSTROKE! Get away from her and cool down</color>" :
                core >= 39 ? $"<color={PausePanel.Bad}>You are overheating – dizzy and weak</color>" :
                core >= 38 ? $"<color={PausePanel.Warn}>You are panting to vent the heat</color>" :
                sweatLevel > 0.7 && demand >= sweatLevel - 0.05 ? $"<color={PausePanel.Warn}>You are sweating heavily</color>" :
                sweatLevel > 0.25 && demand >= sweatLevel - 0.05 ? $"<color={PausePanel.Warn}>You are sweating</color>" :
                demand > 0.1 && demand > sweatLevel ? $"<color={PausePanel.Warn}>You are getting warm</color>" :
                sweatLevel > 0.1 && feel >= 14 ? $"<color={PausePanel.Good}>You are cooling down – still damp</color>" :
                feel < -25 ? $"<color={PausePanel.Bad}>Deadly cold – frostbite</color>" :
                feel < -10 ? $"<color={PausePanel.Warn}>Bitter cold</color>" :
                feel < 0 ? $"<color={PausePanel.Dim}>Freezing</color>" :
                feel < 5 ? $"<color={PausePanel.Dim}>You are shivering</color>" :
                feel < 14 ? $"<color={PausePanel.Dim}>You feel cold</color>" :
                feel < 19 ? $"<color={PausePanel.Dim}>You feel cool</color>" :
                feel < 26 ? $"<color={PausePanel.Good}>You feel comfortable</color>" :
                $"<color={PausePanel.Warn}>You feel warm</color>";

            // When to show the panel: the state changed, she is warming / breathing / blowing on you, or it is dangerous.
            string stateKey = System.Text.RegularExpressions.Regex.Replace(state, "<[^>]+>", "");
            if (stateKey != lastState) { lastState = stateKey; stateChangedAt = Time.unscaledTime; }
            herEffect = act.name != null || touching || radiant >= SizeStats.FeltRadiantWm2 || mix > 0.05 || (windFrom != null && wind >= 0.5);
            danger = core < 35 || core >= 39 || feel < -10;

            string source = act.name != null || touching ? " <color={0}>– GTS warmth</color>" :
                soleRadiant > 0 && radiant - soleRadiant < SizeStats.FeltRadiantWm2 ? $" <color={{0}}>– her sole +{Fmt.Num(radiant / ExchangeW_m2K)} °</color>" : radiant >= SizeStats.FeltRadiantWm2 ? $" <color={{0}}>– GTS +{Fmt.Num(radiant / ExchangeW_m2K)} °</color>" : "";
            source = string.Format(source, PausePanel.Dim);

            string breath = mix > 0.05
                ? $"<color={PausePanel.Dim}>HER BREATH</color> <color={(gustC >= 30 ? PausePanel.Warn : PausePanel.Good)}>{Fmt.Temp(gustC, u)}</color>" +
                  $" <color={PausePanel.Dim}>{Fmt.Num(humidity)}% humid, every {Fmt.Num(every)} s</color>\n"
                : "";
            string windLine = wind >= 0.5
                ? $"<color={PausePanel.Dim}>WIND</color> <color={(wind < 10 ? PausePanel.Good : wind < 33 ? PausePanel.Warn : PausePanel.Bad)}>{Fmt.Speed(wind, u)}</color>" +
                  $" <color={PausePanel.Dim}>{Fmt.Wind(wind)}{(windFrom != null ? " – her " + windFrom : "")}</color>\n"
                : "";
            string height = alt >= 10 ?$"<color={PausePanel.Dim}>YOU ARE</color> {Fmt.Length(alt, u)} up" +
                (SizeStats.OxygenLevel(alt) > 0 ? $"  <color={PausePanel.Dim}>O2</color> {OxygenFor(alt)}" : "") + "\n" : "";
            string airLine = $"<color={PausePanel.Dim}>AIR</color> {Fmt.Temp(air, u)} <color={PausePanel.Dim}>{Fmt.Num(ambientHumidity)}% · {climate.where}</color>\n";
            if (climate.where.Contains("stomach"))
                airLine += $"<color={PausePanel.Dim}>O2</color> <color={PausePanel.Bad}>very low – stomach gas, you get short of breath</color>\n";
            if (sc < 0.95 || sc > 1.05)
                airLine += $"<color={PausePanel.Dim}>YOU</color> {Fmt.Length(Plugin.YouHeight, u)} <color={PausePanel.Dim}>(×{Fmt.Num(sc)}){(sc < 0.95 ? " – you chill and warm faster" : "")}</color>\n";
            return airLine + height + breath + windLine + $"<color={PausePanel.Dim}>FEELS LIKE</color> <color={feelColor}>{Fmt.Temp(feel, u)}</color>{source}\n" +
                   $"<color={PausePanel.Dim}>BODY</color> <color={coreColor}>{Fmt.Temp1(core, u)}</color>  {state}";
        }
    }
}
