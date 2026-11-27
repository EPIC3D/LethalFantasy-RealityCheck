using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RealityCheck
{
    public enum WeightUnits { Metric, Imperial, Both }
    public enum TipPosition { UnderSizeBar, TopRight, BottomLeft, BottomRight, AboveHotbar }

    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.epicopy.realitycheck";
        public const string Name = "LethalFantasy RealityCheck";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> HudWeight;
        internal static ConfigEntry<WeightUnits> Units;
        internal static ConfigEntry<bool> HudPanelEnabled, HudTips;
        internal static ConfigEntry<float> TickerInterval;
        internal static ConfigEntry<TipPosition> TipPosition;
        internal static ConfigEntry<bool> FeelEnabled;
        internal static ConfigEntry<float> AmbientOffsetC, BodyTimeScale, BaseWind;
        internal static ConfigEntry<bool> PanelEnabled;
        internal static ConfigEntry<float> AkipoHeight, AkipoMass, RedHeight, RedMass, BlueHeight, BlueMass;
        internal static ConfigEntry<float> PlayerHeight, PlayerMass;
        internal static ConfigEntry<float> AkipoBust, RedBust, BlueBust;

        private void Awake()
        {
            Log = Logger;
            HudWeight = Config.Bind("HUD", "ShowWeight", true, "Show the giantess' weight next to her height on the size bar.");
            Units = Config.Bind("General", "Units", WeightUnits.Metric, "Units everywhere: Metric (m, kg), Imperial (ft, lb) or Both.");
            HudPanelEnabled = Config.Bind("HUD", "ShowCornerPanel", true, "Small comparison panel in the bottom-right corner during gameplay.");
            HudTips = Config.Bind("HUD", "ShowTips", true, "A fun-fact popup now and then.");
            FeelEnabled = Config.Bind("Feel", "Show", true, "Bottom-left: how warm it feels to you and how your body copes.");
            BodyTimeScale = Range("Feel", "BodyTimeScale", 5f, 1f, 100f,
                "How fast your body heats up and chills compared to real time (the game day is ~80× faster).");
            BaseWind = Range("Feel", "BaseBreeze", 0f, 0f, 15f, "Everyday breeze in m/s (stronger higher up and in rain). 0 = still air.");
            AmbientOffsetC = Range("Feel", "AirTemperatureOffset", 0f, -20f, 20f, "Shift the map's air temperature (°C). The base follows the map and the time of day.");
            TipPosition = Config.Bind("HUD", "TipPosition", RealityCheck.TipPosition.TopRight, "Where the fun-fact popup appears.");
            TickerInterval = Range("HUD", "TipInterval", 30f, 5f, 300f, "Seconds between two scrolling fun facts.");

            PanelEnabled = Config.Bind("PausePanel", "Show", true, "Show the comparison panel in the pause menu.");

            // Natural (k = 1) body of each character. The game does not state them – edit to taste.
            AkipoHeight = Range("Bodies", "AkipoHeight", 1.62f, 1.3f, 2.2f, "Akipo's natural height in metres.");
            AkipoMass = Range("Bodies", "AkipoMass", 54f, 35f, 120f, "Akipo's natural weight in kg.");
            RedHeight = Range("Bodies", "RedTwinHeight", 1.72f, 1.3f, 2.2f, "Red Twin's natural height in metres.");
            RedMass = Range("Bodies", "RedTwinMass", 66f, 35f, 120f, "Red Twin's natural weight in kg.");
            BlueHeight = Range("Bodies", "BlueTwinHeight", 1.70f, 1.3f, 2.2f, "Blue Twin's natural height in metres.");
            BlueMass = Range("Bodies", "BlueTwinMass", 64f, 35f, 120f, "Blue Twin's natural weight in kg.");
            AkipoBust = Range("Bodies", "AkipoBustKg", 0.6f, 0.1f, 5f, "Weight of one breast in kg at natural size (anime proportions).");
            RedBust = Range("Bodies", "RedTwinBustKg", 1.5f, 0.1f, 5f, "Weight of one breast in kg at natural size.");
            BlueBust = Range("Bodies", "BlueTwinBustKg", 1.4f, 0.1f, 5f, "Weight of one breast in kg at natural size.");
            PlayerHeight = Range("Bodies", "PlayerHeight", 1.75f, 1.3f, 2.2f, "Your height in metres (the comparison reference).");
            PlayerMass = Range("Bodies", "PlayerMass", 75f, 35f, 160f, "Your weight in kg.");

            Config.SettingChanged += (_, __) => { HudPanel.Invalidate(); HudPatch.Clear(); };
            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{Name} {Version} loaded");
        }

        private ConfigEntry<float> Range(string section, string key, float def, float min, float max, string desc) =>
            Config.Bind(section, key, def, new ConfigDescription(desc, new AcceptableValueRange<float>(min, max)));

        internal static (float height, float mass, string name) BodyOf(CharacterType c) => c switch
        {
            CharacterType.RedTwin => (RedHeight.Value, RedMass.Value, "Solana"),
            CharacterType.BlueTwin => (BlueHeight.Value, BlueMass.Value, "Selena"),
            _ => (AkipoHeight.Value, AkipoMass.Value, "Akipo"),
        };

        internal static float BustOf(string name) =>
            name == "Solana" ? RedBust.Value : name == "Selena" ? BlueBust.Value : AkipoBust.Value;

        /// <summary>Height in metres as the game shows it; the lobby has no LootZoneManager, so use its default 30 m per scale unit.</summary>
        internal static float HeightOf(Giantess g)
        {
            float h = LootZoneManager.Instance != null ? g.GetScaleInMeters : g.scale * 30f;
            return float.IsNaN(h) || h < 0.01f ? 0.01f : h;   // never 0: spawning or not yet scaled
        }

        /// <summary>Game metres per Unity unit, from the first giantess's head height (0 if none).</summary>
        internal static double MetresPerUnit()
        {
            foreach (var g in PausePanel.Giantesses())
            {
                if (g.headTransform == null) continue;
                float units = g.headTransform.position.y - g.transform.position.y;
                if (units > 0.01f) return HeightOf(g) * 0.93 / units;
            }
            return 0;
        }

        internal static CharacterType CharacterOf(Giantess g)
        {
            var d = g.dialogue;
            return d != null ? d.character : CharacterType.Akipo;
        }
    }
}
