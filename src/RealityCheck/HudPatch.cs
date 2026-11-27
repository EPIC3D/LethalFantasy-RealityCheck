using System;
using HarmonyLib;

namespace RealityCheck
{
    /// <summary>
    /// The size bar text ("90m") comes from Giantess.GetSizeUIParameters(sizeValue), called every
    /// frame with the bar's smoothed height in metres. Append the matching weight.
    /// </summary>
    [HarmonyPatch(typeof(Giantess), nameof(Giantess.GetSizeUIParameters))]
    internal static class HudPatch
    {
        private static bool failed;
        private static readonly System.Collections.Generic.Dictionary<int, (float next, string text)> cache =
            new System.Collections.Generic.Dictionary<int, (float, string)>();

        internal static void Clear() => cache.Clear();

        [HarmonyPostfix]
        private static void Postfix(Giantess __instance, float sizeValue, ref ValueTuple<string, float, float> __result)
        {
            if (failed || !Plugin.HudWeight.Value || sizeValue <= 0f) return;
            try
            {
                // The game asks every frame; the weight text is rebuilt at most once a second per giantess.
                int id = __instance.GetInstanceID();
                float now = UnityEngine.Time.unscaledTime;
                if (!cache.TryGetValue(id, out var c) || now >= c.next)
                {
                    if (cache.Count > 32) cache.Clear();   // old giantesses from earlier scenes
                    var body = Plugin.BodyOf(Plugin.CharacterOf(__instance));
                    double k = sizeValue / body.height;
                    double kg = body.mass * k * k * k;
                    c = (now + 1f, "  <size=75%>" + Fmt.Mass(kg, Plugin.Units.Value) + "</size>");
                    cache[id] = c;
                }
                __result.Item1 += c.text;
            }
            catch (Exception e)
            {
                failed = true; // never break the game's HUD
                Plugin.Log.LogError("HUD weight disabled: " + e);
            }
        }
    }
}
