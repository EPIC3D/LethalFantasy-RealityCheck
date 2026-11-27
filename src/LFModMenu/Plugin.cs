using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace LFModMenu
{
    /// <summary>
    /// Adds a "MODS" tab to the game's settings window. Every loaded BepInEx plugin's
    /// config entries (bool, enum, numbers with an AcceptableValueRange) are listed there
    /// automatically – other mods need no code to show up.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.epicopy.lfmodmenu";
        public const string Name = "LF Mod Menu";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<string> TabTitle;
        internal static ConfigEntry<string> HiddenPlugins;
        internal static ConfigEntry<bool> ShowOwnSettings;
        internal static ConfigEntry<PluginLineMode> PluginLine;

        private void Awake()
        {
            Log = Logger;
            TabTitle = Config.Bind("General", "TabTitle", "MODS", "Title of the settings tab.");
            HiddenPlugins = Config.Bind("General", "HiddenPlugins", "",
                "Comma separated plugin GUIDs that should not appear in the MODS tab.");
            ShowOwnSettings = Config.Bind("General", "ShowOwnSettings", false,
                "List the Mod Menu's own settings in the tab too.");
            PluginLine = Config.Bind("General", "PauseMenuPluginLine", PluginLineMode.Names,
                "Extra line under the pause menu's Level/Seed/Mods/Env info listing the installed BepInEx plugins.");
            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{Name} {Version} loaded");
        }

        /// <summary>Plugins (in load order) whose entries are shown in the tab.</summary>
        internal static IEnumerable<PluginInfo> VisiblePlugins()
        {
            var hidden = new HashSet<string>(
                HiddenPlugins.Value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
            foreach (var info in BepInEx.Bootstrap.Chainloader.PluginInfos.Values
                         .OrderBy(p => p.Metadata.Name, StringComparer.OrdinalIgnoreCase))
            {
                // The game destroys plugin GameObjects on scene load, so Unity's "== null" is true here,
                // but the managed object and its ConfigFile are still alive – use a reference check.
                if ((object)info.Instance == null) continue;
                if (hidden.Contains(info.Metadata.GUID)) continue;
                if (info.Metadata.GUID == Guid && !ShowOwnSettings.Value) continue;
                yield return info;
            }
        }
    }
}
