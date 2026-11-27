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
        private static bool shownSeveral;

        // --- Looking at her --------------------------------------------------------------------
        private const float LookLinger = 2f, FadeOutSeconds = 0.67f;
        private static float lookAlpha;
        private static CanvasGroup lookGroup;
        private static string lastLooked;
        private static float lookUntil;
        private static readonly Dictionary<Giantess, Renderer[]> bodies = new Dictionary<Giantess, Renderer[]>();

        /// <summary>The giantess under the centre of the screen (her body's bounding boxes), or null.</summary>
        private static string LookedAt()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            string best = null;
            float bestD = float.MaxValue;
            foreach (var g in PausePanel.Giantesses())
            {
                if (!bodies.TryGetValue(g, out var rs) || rs.Length == 0 || rs[0] == null)
                {
                    foreach (var dead in bodies.Keys.Where(k => k == null).ToList()) bodies.Remove(dead);
                    rs = g.GetComponentsInChildren<SkinnedMeshRenderer>(false);
                    bodies[g] = rs;
                }
                foreach (var r in rs)
                {
                    if (r == null || !r.enabled) continue;
                    if (r.bounds.IntersectRay(ray, out float d) && d < bestD)
                    {
                        bestD = d;
                        best = Plugin.BodyOf(Plugin.CharacterOf(g)).name;
                    }
                }
            }
            return best;
        }

        // --- Pop of changed examples -------------------------------------------------------------
        private const float PulseSeconds = 0.45f, PulseScale = 0.3f;
        private static string pulsePick;
        private static float pulseStart = -1;
        private static readonly List<(int line, int skip)> pulseLines = new List<(int, int)>();
        private static readonly System.Text.RegularExpressions.Regex Tags = new System.Text.RegularExpressions.Regex("<[^>]+>");

        /// <summary>Remember which lines changed; on each, the part after the label pops.</summary>
        private static void StartPulse(string before, string after)
        {
            var a = (before ?? "").Split('\n');
            var b = (after ?? "").Split('\n');
            pulseLines.Clear();
            for (int i = 0; i < b.Length; i++)
            {
                if (i < a.Length && a[i] == b[i]) continue;
                // Skip the label: "<color=…>Weight</color> Eiffel Tower" → 7 visible characters.
                int end = b[i].IndexOf("</color>");
                int skip = end > 0 ? Tags.Replace(b[i].Substring(0, end), "").Length + 1 : 0;
                pulseLines.Add((i, skip));
            }
            pulseStart = pulseLines.Count > 0 ? Time.unscaledTime : -1;
        }

        private static void Pulse()
        {
            if (pulseStart < 0) return;
            float t = (Time.unscaledTime - pulseStart) / PulseSeconds;
            text.ForceMeshUpdate();
            if (t >= 1) { pulseStart = -1; return; }   // back to the normal mesh
            float scale = 1 + PulseScale * Mathf.Sin(Mathf.PI * t);
            var info = text.textInfo;
            foreach (var (line, skip) in pulseLines)
            {
                if (line >= info.lineCount) continue;
                var li = info.lineInfo[line];
                int first = li.firstCharacterIndex + skip, last = li.lastCharacterIndex;
                if (first > last) continue;
                // Grow around the middle of the example.
                Vector3 lo = info.characterInfo[first].bottomLeft, hi = info.characterInfo[last].topRight;
                Vector3 c = (lo + hi) / 2;
                for (int i = first; i <= last; i++)
                {
                    var ch = info.characterInfo[i];
                    if (!ch.isVisible) continue;
                    var verts = info.meshInfo[ch.materialReferenceIndex].vertices;
                    for (int v = 0; v < 4; v++)
                        verts[ch.vertexIndex + v] = c + (verts[ch.vertexIndex + v] - c) * scale;
                }
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        // --- Growth milestones: "SOLANA is now taller than the Eiffel Tower!" ----------------------
        private static readonly Dictionary<string, double> tallest = new Dictionary<string, double>();
        private static int milestoneScene = -1;
        private static string milestone;   // waiting to be shown (only the newest counts)

        private static void CheckMilestone(string name, double baseH, double baseKg, double h)
        {
            int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if (scene != milestoneScene) { milestoneScene = scene; tallest.Clear(); milestone = null; }
            if (!tallest.TryGetValue(name, out double before)) { tallest[name] = h; return; }   // first sight: no popup
            if (h <= before) return;
            tallest[name] = h;

            // The biggest height example passed since last time (skip "… height" and layers).
            string passed = null;
            foreach (var (m, en) in Fmt.HeightLadder)
                if (m >= 5 && m > before && m <= h && !en.Contains("height") && !en.Contains("layer")) passed = en;
            double kgBefore = baseKg * System.Math.Pow(before / baseH, 3), kg = baseKg * System.Math.Pow(h / baseH, 3);
            string heavier = null;
            foreach (var (m, en) in Fmt.MassLadder)
                if (m >= 1000 && m > kgBefore && m <= kg) heavier = en;

            string who = $"<color={NameColor(name)}>{ShortName(name)}</color>";
            if (passed != null) milestone = $"{who} is now taller than {Fmt.WithArticle(passed)}!";
            else if (heavier != null) milestone = $"{who} now outweighs {Fmt.WithArticle(heavier)}!";
        }

        private static string ShortName(string name) => name.ToUpperInvariant();

        /// <summary>The colour of her dialogue box: Akipo pink, Red red, Blue blue.</summary>
        private static string NameColor(string name) =>
            name == "Solana" ? "#FF4A4A" : name == "Selena" ? "#4A9BFF" : "#FF6EC7";

        private sealed class Entry
        {
            public float BuiltHeight, LastSeen;
            public string Panel, PanelNoName;
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
            string looked = null;
            if (show && Plugin.HudOnLook.Value)
            {
                // Only while you look at her – and a moment after, so it doesn't flicker.
                looked = LookedAt();
                if (looked != null) { lastLooked = looked; lookUntil = Time.unscaledTime + LookLinger; }
                bool wanted = Time.unscaledTime < lookUntil && lastLooked != null && cache.ContainsKey(lastLooked);
                // Quick fade in, slow fade out once you have looked away for a few seconds.
                lookAlpha = Mathf.MoveTowards(lookAlpha, wanted ? 1 : 0, Time.unscaledDeltaTime / (wanted ? 0.25f : FadeOutSeconds));
                show = lookAlpha > 0 && lastLooked != null && cache.ContainsKey(lastLooked);
                looked = lastLooked;
            }
            else lookAlpha = 1;
            if (lookGroup == null) lookGroup = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
            lookGroup.alpha = lookAlpha;
            lookGroup.blocksRaycasts = false;
            if (root.activeSelf != show) root.SetActive(show);
            if (show)
            {
                // The one you look at; otherwise (several in the lobby) the one closest to you.
                string pick = looked != null && cache.ContainsKey(looked) ? looked
                    : nearest != null && cache.ContainsKey(nearest) ? nearest : order[0];
                bool several = order.Count > 1;
                if ((pick != shown || several != shownSeveral) && cache.TryGetValue(pick, out var e))
                {
                    string next = several ? e.Panel : e.PanelNoName;
                    // Same giantess, new comparisons (she grew): the changed examples pop like the size bar.
                    if (pick == pulsePick && text.text != next) StartPulse(text.text, next);
                    pulsePick = pick;
                    shown = pick;
                    shownSeveral = several;
                    text.text = next;
                    Ui.FitToText(root, text, new Vector2(0.992f, 0.02f), new Vector2(1, 0));
                }
                Pulse();
            }
            Ticker(any && Plugin.HudTips.Value);
        }

        /// <summary>Once a second: read heights; rebuild a character's texts when her height has changed and settled.</summary>
        private static double lastPlayerScale = 1;

        private static void CheckHeights()
        {
            // You shrank or grew back (shrink house, enchantment, mods): rebuild "you to her".
            double ps = Plugin.PlayerScale();
            if (System.Math.Abs(ps - lastPlayerScale) > 0.02 * lastPlayerScale) { lastPlayerScale = ps; Invalidate(); }
            var present = new HashSet<string>();
            var heights = new List<(string name, float h)>();
            foreach (var g in PausePanel.Giantesses())
            {
                var body = Plugin.BodyOf(Plugin.CharacterOf(g));
                float h = Plugin.HeightOf(g);
                CheckMilestone(body.name, body.height, body.mass, h);
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
            double ph = Plugin.YouHeight;
            var sb = new StringBuilder();
            double you = ph / s.K;
            double youKg = Plugin.YouMass / (s.K * s.K * s.K);
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
                // Her name in her dialogue-box colour; left out when she is the only giantess on the map.
                Panel = $"<color={NameColor(name)}>{ShortName(name)}</color>\n" + sb,
                PanelNoName = sb.ToString(),
                Tips = Tips(name, s, ph, Plugin.YouMass, u),
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
                if (milestone != null)
                {
                    // A growth milestone goes first, without waiting for the tip interval.
                    tickerText.text = milestone;
                    milestone = null;
                }
                else
                {
                    if (now < nextTicker || order.Count == 0 || !cache.TryGetValue(order[0], out var e) || e.Tips.Count == 0) return;
                    tipIndex = (tipIndex + 1) % e.Tips.Count;
                    tickerText.text = e.Tips[tipIndex];
                }
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
            t.Add($"That ball leaves her hand with {Tnt(s.ImpactRealTnt)} (real) – {Tnt(s.ImpactFantasyTnt)} in fantasy.");
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
            // Fun "what if" facts: growing by bigger cells, or by bigger atoms, instead of more cells.
            double cellR = 5e-6 * s.K, oxygenS = cellR * cellR / (2 * 2e-9);   // O2 diffusion in water, D = 2e-9 m²/s
            if (s.K >= 200)   // below that the cell still breathes fine
                t.Add($"Bigger cells instead of more? Each would be {Fmt.LikeLength(2 * cellR)}-sized – oxygen needs {PausePanel.Duration(oxygenS)} to reach the middle.");
            double natKg = s.Mass / (s.K * s.K * s.K), density = natKg / (s.Mass / SizeStats.BodyDensity);
            // Bigger atoms: same charge spread over a bigger cloud – weaker bonds (1/k), diluted electron
            // clouds (normal atoms slip through, like through real Rydberg atoms), energy levels 1/k² (light passes).
            if (s.K >= 10)
            {
                t.Add($"Bigger atoms instead? She'd weigh just {Fmt.Mass(natKg, u)}, and air and ground would pass through her thin atoms.");
                t.Add($"Atoms {Fmt.Num(s.K)}× bigger bind {Fmt.Num(s.K)}× weaker – she'd evaporate into a see-through ghost.");
                t.Add("Magic atoms with stronger forces? Then she's simply the fantasy (F) giantess.");
            }
            t.Add($"In her body shape, ~{Fmt.Length(PausePanel.MaxWorkingHeight(s.BaseHeight), u)} is the most that could work – limping, and not for long.");
            t.Add($"On Mars (38% gravity) her bones would hold up to ~{Fmt.Length(s.BaseHeight * SizeStats.BoneStrengthMPa / SizeStats.WalkingBoneStressMPa / 0.378, u)} – only her body heat would still stop her.");
            // Her own gravity in space: treat her as a ball of the same volume.
            if (s.K >= 1000)
            {
                double vol = s.Mass / SizeStats.BodyDensity, r = System.Math.Pow(3 * vol / (4 * System.Math.PI), 1.0 / 3);
                double gHer = 6.674e-11 * s.Mass / (r * r), escape = System.Math.Sqrt(2 * gHer * r);
                t.Add($"In space her own gravity would be {Fmt.Num(100 * gHer / SizeStats.G)}% of Earth's – a dropped coin takes {PausePanel.Duration(System.Math.Sqrt(2 / gHer))} to fall 1 m, and a jump at {Fmt.Speed(escape, u)} would fling you off her for good.");
            }
            // Self-gravity squeezing her: centre pressure of a ball of her thickness, P = 2π/3·G·ρ²·R².
            // Soft tissue (~100 kPa) gives way when her body is ~53 km thick (~350 km tall).
            if (s.K >= 1000)
            {
                double rBody = 0.075 * s.Height, rho = SizeStats.BodyDensity;
                double squeeze = 2 * System.Math.PI / 3 * 6.674e-11 * rho * rho * rBody * rBody;
                double ballH = System.Math.Sqrt(1e5 / (2 * System.Math.PI / 3 * 6.674e-11 * rho * rho)) / 0.075;
                t.Add($"Her own gravity squeezes her middle with just {Fmt.Pressure(squeeze, u)} – only above ~{Fmt.Length(ballH, u)} tall would it crush her into a ball, like a small moon.");
            }
            // Night pupil ~7 mm × k: a point-like star is k² brighter to her (telescope-like).
            double pupil = 0.007 * s.K;
            if (pupil >= 1)
                t.Add($"Her pupils open {Fmt.Length(pupil, u)} wide at night – like {Fmt.LikeLength(pupil)}. Every star looks {Fmt.Num(s.K * s.K)}× brighter to her" +
                      (s.EyeHeight > 15000 ? ", and above the air she sees them even at noon." : ". No wonder she loves the starry sky."));
            // Smell: nose lining k² bigger with cells like ours → k² more smell cells (human ~6 million, bloodhound ~300 million).
            double smell = 6e6 * s.K * s.K;
            if (s.K >= 5)
                t.Add($"Her nose has {Fmt.Num(smell)} smell cells – {(smell >= 3e8 ? $"{Fmt.Num(smell / 3e8)}× a bloodhound's" : $"{Fmt.Num(smell / 6e6)}× yours, close to a dog's")}" +
                      (s.EyeHeight > 3000 ? ", but up there the air is too thin and clean to carry smells." : "."));
            // Touch: to her you weigh pm/k³; below ~5 mg (a speck of dust) the palm feels nothing.
            if (pm / (s.K * s.K * s.K) < 5e-6)
                t.Add($"To her you weigh {Fmt.Mass(pm / (s.K * s.K * s.K), u)} – less than a speck of dust. She wouldn't even feel you on her palm.");
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
