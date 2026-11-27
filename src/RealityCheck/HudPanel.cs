using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace RealityCheck
{
    /// <summary>Small gameplay panel in the top-right corner: key numbers + a rotating fun fact.</summary>
    internal static class HudPanel
    {
        private static GameUI owner;
        private static GameObject root, band;
        private static TextMeshProUGUI text, tickerText;
        private static CanvasGroup popupGroup;
        private static float popupStart;
        private static float nextCheck, nextTicker;
        private static int tipIndex;
        private static string shown, nearest;

        private const string Dim = PausePanel.Dim, Head = PausePanel.Head, Bad = PausePanel.Bad;

        /// <summary>Cached texts per character; rebuilt only when her height changed and has settled (growth spurt over).</summary>
        private sealed class Entry
        {
            public float BuiltHeight, LastSeen;
            public string Panel;
            public List<string> Tips;
        }

        private static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();
        private static readonly List<string> order = new List<string>();

        /// <summary>Settings changed (units, bodies ...): rebuild everything on the next check.</summary>
        public static void Invalidate()
        {
            cache.Clear();
            shown = null;
            nextCheck = 0;
        }

        public static void Tick(GameUI ui)
        {
            if (owner != ui || root == null || band == null)
            {
                owner = ui;
                var font = ui.GetComponentInChildren<TextMeshProUGUI>(true);
                (root, text) = Ui.CreatePanel(ui.canvas, font,
                    new Vector2(0.70f, 0.02f), new Vector2(0.992f, 0.30f), "RealityCheck_HudPanel", 17, background: false);
                text.alignment = TextAlignmentOptions.BottomRight;
                text.enableAutoSizing = false; // fixed size, no shrinking
                text.fontSize = 30;
                text.enableWordWrapping = false;
                (band, tickerText) = Ui.CreatePanel(ui.canvas, font,
                    new Vector2(0.74f, 0.80f), new Vector2(0.99f, 0.97f), "RealityCheck_TipPopup", 22);
                tickerText.fontSizeMin = 14;
                tickerText.alignment = TextAlignmentOptions.MidlineLeft;
                popupGroup = band.AddComponent<CanvasGroup>();
                popupGroup.blocksRaycasts = false;
                var popupBg = band.GetComponent<UnityEngine.UI.Image>();
                if (popupBg != null) popupBg.color = new Color(0f, 0f, 0f, 0.5f); // like the bottom panels
                var frame = band.GetComponent<UnityEngine.UI.Outline>();
                if (frame != null) Object.Destroy(frame);   // the outline effect copied the box in grey
                Ui.Readable(tickerText);
                nextTicker = Time.unscaledTime + 15f;
                Invalidate();
                Plugin.Log.LogDebug("HUD panel attached");
            }

            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 1f;
                CheckHeights();
            }

            var ls = LevelSelect.Instance;
            bool levelSelect = ls != null && ls.canvas != null && ls.canvas.enabled;
            bool any = order.Count > 0 && ui.canvas != null && ui.canvas.enabled && !PauseMenu.IsPaused && !levelSelect && !Ui.CraftingOpen();
            bool show = any && Plugin.HudPanelEnabled.Value;
            if (root.activeSelf != show) root.SetActive(show);
            if (show)
            {
                // Several characters (lobby): show the one closest to you.
                string pick = nearest != null && cache.ContainsKey(nearest) ? nearest : order[0]; // the giantess closest to you
                if (pick != shown && cache.TryGetValue(pick, out var e))
                {
                    shown = pick;
                    text.text = e.Panel;
                    Ui.FitToText(root, text, new Vector2(0.992f, 0.02f), new Vector2(1, 0));
                }
            }
            Ticker(any && Plugin.HudTips.Value);
        }

        /// <summary>Once a second: read heights; rebuild a character's texts when her height has changed and settled.</summary>
        private static void CheckHeights()
        {
            var present = new HashSet<string>();
            var heights = new List<(string name, float h)>();
            foreach (var g in PausePanel.Giantesses())
            {
                var body = Plugin.BodyOf(Plugin.CharacterOf(g));
                float h = Plugin.HeightOf(g);
                present.Add(body.name);
                heights.Add((body.name, h));
                if (!cache.TryGetValue(body.name, out var e))
                {
                    cache[body.name] = Build(body.name, body.height, body.mass, h);
                    if (shown == body.name) shown = null;
                    continue;
                }
                bool settled = Mathf.Abs(h - e.LastSeen) <= 0.001f * h;
                bool changed = Mathf.Abs(h - e.BuiltHeight) > 0.001f * h;
                e.LastSeen = h;
                if (settled && changed)
                {
                    cache[body.name] = Build(body.name, body.height, body.mass, h);
                    if (shown == body.name) shown = null;
                }
            }
            foreach (var gone in cache.Keys.Where(k => !present.Contains(k)).ToList()) cache.Remove(gone);
            order.Clear();
            order.AddRange(heights.OrderByDescending(x => x.h).Select(x => x.name));

            // Closest giantess to the player.
            nearest = null;
            var player = PlayerDriver.LocalPlayer;
            if (player != null)
            {
                float best = float.MaxValue;
                foreach (var g in PausePanel.Giantesses())
                {
                    float d = Vector3.Distance(player.transform.position, g.transform.position);
                    if (d < best) { best = d; nearest = Plugin.BodyOf(Plugin.CharacterOf(g)).name; }
                }
            }
        }

        private static Entry Build(string name, double baseHeight, double baseMass, float h)
        {
            var s = SizeStats.Compute(baseHeight, baseMass, h);
            var u = Plugin.Units.Value;
            double ph = Plugin.PlayerHeight.Value;
            var sb = new StringBuilder();
            double you = ph / s.K;
            double youKg = Plugin.PlayerMass.Value / (s.K * s.K * s.K);
            sb.Append($"<color={Head}>{name.ToUpperInvariant()}</color> <color={Dim}>{Fmt.Factor(s.K)}</color>\n");
            sb.Append($"<color={Dim}>Weight</color> {Fmt.LikeMass(s.Mass)}\n");
            sb.Append($"<color={Dim}>You to her</color> {Fmt.LikeLength(you)}\n");
            sb.Append($"<color={Dim}>Height</color> {Fmt.LikeHeight(s.Height)}\n");
            double headC = Climate.TempAt(s.EyeHeight);
            // Only how it feels up at her head – no label, no numbers.
            int o2 = SizeStats.OxygenLevel(s.EyeHeight);
            string air = o2 == 0 ? "" : " · " + (o2 == 1 ? $"<color={PausePanel.Warn}>thin air</color>" : o2 == 2 ? $"<color={PausePanel.Warn}>gasping</color>" :
                o2 == 3 ? $"<color={Bad}>death zone</color>" : $"<color={Bad}>no air</color>");
            string word = Fmt.TempWord(headC);
            if (word.StartsWith("a ")) word = word.Substring(2);
            sb.Append($"<color={Dim}>Feels</color> {Fmt.Temp(headC, Plugin.Units.Value)} ({word})" + air + "\n");
            return new Entry
            {
                BuiltHeight = h,
                LastSeen = h,
                Panel = sb.ToString(),
                Tips = Tips(name, s, ph, Plugin.PlayerMass.Value, u),
            };
        }

        private const float PopupSeconds = 9f, FadeSeconds = 0.8f;

        /// <summary>Every TipInterval seconds one cached fun fact pops up for a few seconds, then fades out.</summary>
        private static void Ticker(bool enabled)
        {
            if (!enabled) { if (band.activeSelf) band.SetActive(false); return; }
            float now = Time.unscaledTime;
            if (!band.activeSelf)
            {
                if (now < nextTicker || order.Count == 0 || !cache.TryGetValue(order[0], out var e) || e.Tips.Count == 0) return;
                tipIndex = (tipIndex + 1) % e.Tips.Count;
                tickerText.text = e.Tips[tipIndex];
                Ui.PlacePopup((RectTransform)band.transform, Plugin.TipPosition.Value);
                Ui.FitPopup(band, tickerText, 520f);   // box hugs the text, pinned to its corner
                popupStart = now;
                band.SetActive(true);
            }
            float t = now - popupStart;
            popupGroup.alpha = Mathf.Clamp01(Mathf.Min(t / FadeSeconds, (PopupSeconds - t) / FadeSeconds));
            if (t >= PopupSeconds)
            {
                band.SetActive(false);
                nextTicker = now + Plugin.TickerInterval.Value;
            }
        }

        internal static string Tnt(double tons) => Fmt.Tnt(tons);

        private static List<string> Tips(string name, SizeStats s, double ph, double pm, WeightUnits u)
        {
            var t = new List<string>();
            double b = s.BaseHeight;
            t.Add($"{name} weighs as much as {Fmt.Num(s.Mass / pm)} of you.");
            t.Add($"One of her steps is {Fmt.Length(s.StepLength, u)} – {Fmt.Num(s.StepLength / (0.413 * ph))} of yours.");
            t.Add($"A baseball scaled to her size: {Fmt.Length(s.BallSize, u)}, {Fmt.Mass(s.BallKg, u)} – she could throw it {SizeStats.DragResult(SizeStats.ThrowWithDrag(s.ThrowSpeedReal, s.BallSize, s.BallKg, s.ReleaseHeight), u)} (real) or {SizeStats.DragResult(SizeStats.ThrowWithDrag(s.ThrowSpeedFantasy, s.BallSize, s.BallKg, s.ReleaseHeight), u)} (fantasy). You: {Fmt.Length(SizeStats.PlayerThrowRangeDrag, u)}.");
            t.Add($"That ball leaves her hand with {Tnt(s.ImpactRealTnt)} of TNT (real) – {Tnt(s.ImpactFantasyTnt)} in fantasy.");
            t.Add($"Walking, she makes {PausePanel.Power(s.HeatRealW)} of heat and must drink {Fmt.Volume(s.WaterPerDayM3(s.HeatRealW), u)} a day (fantasy).");
            if (s.HeatRealW >= SizeStats.CloudPowerW)
                t.Add("In fantasy, her body heat would build its own cumulus cloud above her.");
            t.Add($"You can feel her body warmth from {Fmt.Length(s.RadiantFeltDistance, u)} – right next to her it's {PausePanel.Warmth(s.RadiantMaxWm2)}.");
            t.Add($"She'd need {Fmt.Num(s.KcalReal)} kcal a day – {Fmt.Num(s.KcalReal / SizeStats.BigMacKcal)} Big Macs (${Fmt.Num(s.KcalReal / SizeStats.BigMacKcal * SizeStats.BigMacUsd)}) in fantasy – {Fmt.Round(s.KcalWhale / SizeStats.BigMacKcal)} for real.");
            t.Add($"If she threw you, you'd feel {Fmt.Num(s.ThrowG(s.ThrowSpeedReal))} g in her hand (real) – {Fmt.Num(s.ThrowG(s.ThrowSpeedFantasy))} g in fantasy.");
            double stompT = s.StompEnergy(s.StompSpeedReal) / SizeStats.TntJoulePerTon;
            t.Add($"One stomp of hers: {Fmt.Tnt(stompT)} – like {Fmt.LikeEnergy(stompT)}.");
            t.Add($"Real physics: she jumps only {Fmt.Length(s.JumpReal, u)} high, like any woman – in fantasy {Fmt.Length(s.JumpFantasy, u)}, landing with {Fmt.Tnt(s.JumpStompEnergy(s.JumpFantasy) / SizeStats.TntJoulePerTon)}.");
            if (s.ExcessHeatW(s.HeatRealW) > 0)
                t.Add($"In fantasy, walking would give her heatstroke in {PausePanel.Duration(s.OverheatSeconds(s.HeatRealW, 3))}.");
            t.Add(s.NetHeat(s.HeatRealW * SizeStats.RestShare) > s.SeaCoolingW(false)
                ? "In fantasy, even floating in a cold sea she would overheat – the sea can't carry the heat away."
                : "Floating in the sea would keep her cool – water carries heat away much better than air.");
            if (s.BreathFogs)
                t.Add("It's so cold at her head that her breath turns into a little cloud.");
            t.Add($"In one go she could drink {Fmt.Volume(s.DrinkM3, u)} – {Fmt.Lake(s.DrinkM3)}.");
            if (s.ThrowSpeedFantasy > SizeStats.SpeedOfSound)
                t.Add($"Her fantasy throw would fly at Mach {Fmt.Num(s.ThrowSpeedFantasy / SizeStats.SpeedOfSound)} – the ball would glow from air friction.");
            t.Add($"Floating in the sea she'd displace {Fmt.Volume(s.SeaFloatDisplacedM3, u)} – {Fmt.Lake(s.SeaFloatDisplacedM3)}.");
            t.Add($"Her footsteps are like an M{s.StepMagnitude:0.0} quake, felt {Fmt.Length(s.StepFeltDistance, u)} away.");
            t.Add($"If she blew at you: {Fmt.Speed(s.BlowWindFantasy, u)} – {Fmt.Wind(s.BlowWindFantasy)}.");
            t.Add($"One breath of hers: {Fmt.Volume(s.BreathM3, u)} – like {Fmt.LikeVolume(s.BreathM3)}.");
            t.Add($"At her head it's {Fmt.Temp(SizeStats.AirTemperatureC(s.EyeHeight), u)}.");
            t.Add($"Each sole covers {Fmt.Area(s.SoleArea, u)}.");
            t.Add($"Her feet press the ground with {Fmt.Pressure(s.GroundPressurePa, u)}.");
            if (s.BoneStressMPa >= SizeStats.BoneStrengthMPa)
                t.Add($"<color={Bad}>" + "Real bones would shatter at her first step." + "</color>");
            if (s.GroundPressurePa >= SizeStats.SoilBearingKPa * 1000)
                t.Add("Real ground: she'd sink " + PausePanel.Sinking(s, u));
            if (s.BloodPressureMmHg >= 300)
                t.Add($"Her heart would need {Fmt.Num(s.BloodPressureMmHg)} mmHg to reach her brain.");
            if (s.ExcessHeatW(s.HeatRealW) > 0)
                t.Add("She would overheat just by walking.");
            if (s.VoiceHz < 20)
                t.Add("Her real voice would be infrasound – felt, not heard.");
            t.Add($"A touch on her toe reaches her brain after {Fmt.Time(s.NerveDelay)} – {PausePanel.Lag(s.NerveDelay).TrimStart('=', ' ')}.");
            t.Add($"Real physics: she'd move {Fmt.Num(s.SlowMotion)}× slower, like slow motion.");
            t.Add($"From her eyes the horizon is {Fmt.Length(s.HorizonKm * 1000, u)} away.");
            t.AddRange(EnvInfo.Tips(s, u));
            return t;
        }
    }
}
