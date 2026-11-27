using System;
using System.Linq;
using HarmonyLib;

namespace LFModMenu
{
    /// <summary>
    /// The pause menu info block ("Level / Seed / Mods / Env") – "Mods" there are the game's
    /// own level modifiers, so we leave it alone and add a separate "Plugins:" line.
    /// </summary>
    [HarmonyPatch(typeof(PauseMenu), nameof(PauseMenu.UpdateDebugTextMesh))]
    internal static class PauseInfo
    {
        [HarmonyPostfix]
        private static void Postfix(PauseMenu __instance)
        {
            try
            {
                if (Plugin.PluginLine.Value == PluginLineMode.Off || __instance.debugTextMesh == null) return;
                var plugins = BepInEx.Bootstrap.Chainloader.PluginInfos.Values
                    .Select(p => System.IO.Path.GetFileNameWithoutExtension(p.Location ?? p.Metadata.Name))
                    .Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
                string line = Plugin.PluginLine.Value == PluginLineMode.Count
                    ? $"Plugins: {plugins.Count}"
                    : "Plugins: " + (plugins.Count == 0 ? "None" : string.Join(", ", plugins));
                __instance.debugTextMesh.text = __instance.debugTextMesh.text.TrimEnd('\n', '\r') + "\n" + line + "\n";
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Plugin line: " + e.Message);
            }
        }
    }

    public enum PluginLineMode { Off, Count, Names }
}
