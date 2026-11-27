using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static RealityCheck.PausePanel;

namespace RealityCheck
{
    /// <summary>SOUND tab: her voice, your voice, and the conversation delay at the real distance.</summary>
    internal static class SoundPage
    {
        /// <summary>Per character: distance from your head to her mouth and to her ear (m), and your height above her feet (m).</summary>
        private static readonly Dictionary<string, (double mouth, double ear, double playerHeight)> distances =
            new Dictionary<string, (double, double, double)>();

        /// <summary>Measured distance from your head to her eyes/head (m), or null if not measured.</summary>
        public static double? EyeDistance(string name) =>
            distances.TryGetValue(name, out var d) ? d.ear : (double?)null;

        /// <summary>Measure distances once when the pause menu opens. Unity units are converted to the
        /// game's metres with her own head height, so it matches the size shown on the HUD.</summary>
        public static void Snapshot()
        {
            distances.Clear();
            var player = PlayerDriver.LocalPlayer;
            if (player == null) return;
            Vector3 p = player.transform.position;
            foreach (var g in Giantesses())
            {
                try
                {
                    if (g.headTransform == null) continue;
                    float feetY = g.transform.position.y;
                    Vector3 head = g.headTransform.position;
                    float headUnits = head.y - feetY;
                    if (headUnits <= 0.01f) continue;
                    double metresPerUnit = Plugin.HeightOf(g) * 0.93 / headUnits;
                    string name = Plugin.BodyOf(Plugin.CharacterOf(g)).name;
                    Vector3 mouth = g.mouthTransform != null ? g.mouthTransform.position : head;
                    Vector3 you = Ui.Eye() ?? p + Vector3.up * (float)(1.6 / metresPerUnit); // your eyes (camera), not your feet
                    distances[name] = (Vector3.Distance(you, mouth) * metresPerUnit, Vector3.Distance(you, head) * metresPerUnit,
                        Math.Max(0, (p.y - feetY) * metresPerUnit));
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Distance: " + e.Message);
                }
            }
        }

        private const double PlayerVoiceHz = 150;   // your voice's fundamental
        private const double ConsonantHz = 3000;    // consonants make speech understandable
        private const double AtomM = 1e-10;

        /// <summary>
        /// Eardrum displacement (m). A human eardrum moves ~1e-11 m at 0 dB (×10 per 20 dB). Inside her
        /// hearing range her (bigger) ear is as sensitive as ours; above it the response falls with f².
        /// </summary>
        private static double EardrumMove(double db, double f, double top)
        {
            double x = 1e-11 * Math.Pow(10, Math.Max(0, db) / 20);
            return f <= top ? x : x * (top / f) * (top / f);
        }

        private static string FmtTiny(double m) =>
            m >= 1e-6 ? Fmt.Num(m * 1e6) + " µm" : m >= 1e-9 ? Fmt.Num(m * 1e9) + " nm" : Fmt.Num(m * 1e12) + " pm";

        private static string DbText(double db)
        {
            string what =
                db < 0 ? "silent" :
                db < 30 ? "a whisper" :
                db < 60 ? "a conversation" :
                db < 85 ? "loud, like traffic" :
                db < 100 ? "a lawnmower" :
                db < 120 ? "a rock concert" :
                db < 140 ? "a jet engine – painful" :
                db < 160 ? "eardrums burst" :
                db < 194 ? "a blast wave" :
                "a shock wave – louder than sound can be";
            string color = db < 85 ? Good : db < 120 ? Warn : Bad;
            return $"{Fmt.Num(Math.Max(0, db))} dB " + Ex(what, color);
        }

        public static void Build(StringBuilder sb, string name, SizeStats s, WeightUnits u)
        {
            bool measured = distances.TryGetValue(name, out var dist);
            double d = measured ? dist.mouth : s.Height;      // her mouth → you; fallback: one body length away
            double dEar = measured ? dist.ear : s.Height;     // you → her ear (head)
            double playerH = measured ? dist.playerHeight : 0;
            double mouth = s.MouthHeight;

            Section(sb, "HER VOICE");
            Line(sb, "Her mouth to you", $"{Fmt.Length(d, u)} <color={Dim}>~{Fmt.LikeLength(d)}</color>" + (measured ? "" : $" <color={Dim}>(not measured – one body length)</color>"));

            double speechReal = SizeStats.Level(s.VoiceW(SizeStats.SpeechW, false), d, mouth, s.VoiceBandReal);
            double speechFan = SizeStats.Level(s.VoiceW(SizeStats.SpeechW, true), d, mouth, SizeStats.SpeechBandHz);
            double shoutFan = SizeStats.Level(s.VoiceW(SizeStats.ShoutW, true), d, mouth, SizeStats.SpeechBandHz);
            bool infra = s.VoiceBandReal < 20;   // her real voice is below your hearing
            Line(sb, "Talking, real", infra ? $"{Fmt.Num(s.VoiceBandReal)} Hz " + Ex("felt, not heard", Warn) : DbText(speechReal));
            Line(sb, "Talking, fantasy", DbText(speechFan));
            Line(sb, "Shouting, fantasy", DbText(shoutFan));

            double heardReal = SizeStats.HeardFrom(s.VoiceW(SizeStats.SpeechW, false), SizeStats.QuietAmbientDb, mouth, s.VoiceBandReal);
            double heardFan = SizeStats.HeardFrom(s.VoiceW(SizeStats.SpeechW, true), SizeStats.QuietAmbientDb, mouth, SizeStats.SpeechBandHz);
            double shoutHeard = SizeStats.HeardFrom(s.VoiceW(SizeStats.ShoutW, true), SizeStats.QuietAmbientDb, mouth, SizeStats.SpeechBandHz);
            double understood = SizeStats.HeardFrom(s.VoiceW(SizeStats.SpeechW, true), SizeStats.UnderstandDb, mouth, SizeStats.SpeechBandHz);
            Line(sb, "Talk heard", RF(infra ? "infrasound" : Fmt.Length(heardReal, u), infra ? "never" : Fmt.LikeLength(heardReal), Fmt.Length(heardFan, u), Fmt.LikeLength(heardFan)));
            Line(sb, "Shout heard", $"{Fmt.Length(shoutHeard, u)} <color={Dim}>~{Fmt.LikeLength(shoutHeard)}</color> <color={Dim}>fantasy, quiet countryside</color>");
            Line(sb, "Understood (F)", $"{Fmt.Length(understood, u)} <color={Dim}>~{Fmt.LikeLength(understood)}</color>");

            bool clear = s.FormantReal >= 250;
            Line(sb, "Clarity, real", clear
                ? $"<color={Good}>clear</color>"
                : $"<color={Warn}>vowels blur into a deep boom</color> <color={Dim}>(F2 {Fmt.Num(s.FormantReal)} Hz)</color>");
            Line(sb, "Speech speed", $"{Fmt.Num(s.SpeechSlowdown)}× slower <color={Dim}>– fantasy: normal</color>");
            sb.AppendLine();

            Section(sb, "YOUR VOICE TO HER");
            double yourTalk = SizeStats.Level(SizeStats.SpeechW, dEar, playerH + 1.6, SizeStats.SpeechBandHz);
            double yourShout = SizeStats.Level(SizeStats.ShoutW, dEar, playerH + 1.6, PlayerVoiceHz);
            Line(sb, "Your talk", DbText(yourTalk));
            Line(sb, "Your shout", DbText(yourShout) + (yourShout < SizeStats.QuietAmbientDb ? $" <color={Bad}>she can't hear you</color>" : ""));
            // Talking straight into her ear (half a metre from the canal), and whether you fit inside it.
            double inEar = SizeStats.Level(SizeStats.SpeechW, 0.5, playerH + 1.6, SizeStats.SpeechBandHz);
            Line(sb, "Into her ear", PlayerVoiceHz <= 20000 / s.K ? DbText(inEar) : $"{Fmt.Num(PlayerVoiceHz * s.K / 1000)} kHz to her " + Ex("ultrasound", Bad));
            double canal = 0.007 * s.K;
            Line(sb, "Fit in ear canal?", canal >= 0.45 ? $"<color={Good}>yes</color> <color={Dim}>{Fmt.Length(canal, u)} wide</color>" : $"<color={Bad}>no</color> <color={Dim}>{Fmt.Length(canal, u)} wide</color>");
            // Real hearing: her ears are k× bigger, so her hearing range slides down to 20/k … 20 000/k Hz.
            double top = 20000 / s.K;
            bool voiceInRange = PlayerVoiceHz <= top, consonantsInRange = ConsonantHz <= top;
            Line(sb, "Her hearing (real)", $"{Fmt.Num(20 / s.K)} – {Fmt.Num(top)} Hz <color={Dim}>(yours: 20 – 20 000 Hz)</color>");

            // At this distance: the voice must beat the quiet background, the consonants must be loud
            // enough to follow words – air absorbs 3 kHz consonants much faster than the 150 Hz voice.
            double src = playerH + 1.6;
            double voiceAtEar = SizeStats.Level(SizeStats.ShoutW, dEar, src, PlayerVoiceHz);
            double consonantsAtEar = SizeStats.Level(SizeStats.ShoutW, dEar, src, ConsonantHz);
            bool hears = voiceInRange && voiceAtEar >= SizeStats.QuietAmbientDb;
            bool understands = consonantsInRange && consonantsAtEar >= SizeStats.UnderstandDb;
            string why =
                !voiceInRange ? $"even your voice ({Fmt.Num(PlayerVoiceHz)} Hz) is ultrasound to her" :
                !hears ? "you're too far – your shout fades into the background" :
                !consonantsInRange ? $"your consonants ({Fmt.Num(ConsonantHz)} Hz) are ultrasound to her" :
                !understands ? "too far – the air swallows your consonants" : "";
            Line(sb, "She gets it?", understands
                ? $"<color={Good}>yes, she understands you</color>"
                : hears
                    ? $"<color={Warn}>hears a muffled hum</color> <color={Dim}>– {why}</color>"
                    : $"<color={Bad}>no</color> <color={Dim}>– {why}</color>");

            double drum = EardrumMove(yourShout, PlayerVoiceHz, top);
            Line(sb, "Her eardrum", $"{FmtTiny(drum)} <color={Dim}>= {Fmt.Num(drum / AtomM)}× an atom</color>");
            double heardRange = voiceInRange ? SizeStats.HeardFrom(SizeStats.ShoutW, SizeStats.QuietAmbientDb, src, PlayerVoiceHz) : 0;
            double understoodRange = consonantsInRange ? SizeStats.HeardFrom(SizeStats.ShoutW, SizeStats.UnderstandDb, src, ConsonantHz) : 0;
            Line(sb, "Shout heard", voiceInRange ? $"{Fmt.Length(heardRange, u)} <color={Dim}>~{Fmt.LikeLength(heardRange)}</color>" : $"<color={Bad}>never</color>");
            Line(sb, "Understood", consonantsInRange ? $"{Fmt.Length(understoodRange, u)} <color={Dim}>~{Fmt.LikeLength(understoodRange)}</color>" : $"<color={Bad}>never</color>");
            Note(sb, $"you are {Fmt.Length(dEar, u)} from her ear, in a quiet countryside");
            sb.AppendLine();

            Section(sb, "CONVERSATION (round trip)");
            double toHer = SizeStats.SoundTime(dEar, playerH + 1.6, s.EyeHeight);
            double toYou = SizeStats.SoundTime(d, mouth, playerH + 1.6);
            Line(sb, "To her", Fmt.Time(toHer));
            Line(sb, "She thinks", RealFantasy(Fmt.Time(s.ThinkReal), Fmt.Time(SizeStats.ThinkS)));
            Line(sb, "Her answer", Fmt.Time(toYou));
            double totalReal = toHer + s.ThinkReal + toYou, totalFan = toHer + SizeStats.ThinkS + toYou;
            Line(sb, "Total", RealFantasy(Fmt.Time(totalReal), Fmt.Time(totalFan)));
            Note(sb, Lag(totalFan));
        }
    }
}
