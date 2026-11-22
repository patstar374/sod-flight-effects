using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace FlightEffects
{
    using FlightEffects.Core;

    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "de.httyd.flighteffects";
        public const string Name = "FlightEffects";
        public const string Version = "0.1.1";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            ConfigEntry<bool> enabled = Config.Bind("General", "Enabled", true,
                "Show flight effects at the wing tips of your dragon (restart the game after changing this).");
            ConfigEntry<KeyboardShortcut> key = Config.Bind("General", "EditorKey", new KeyboardShortcut(KeyCode.F7),
                "Opens and closes the effect editor.");
            if (!enabled.Value)
            {
                Log.LogInfo($"{Name} {Version}: disabled in config");
                return;
            }
            // presets live outside the game client: the launcher rebuilds the client (and wipes BepInEx/config) on
            // every payload change, e.g. a mod update; the old location is migrated once
            string stable = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "FlightEffects", "presets.txt");
            var store = new SettingsStore(stable, Path.Combine(Paths.ConfigPath, Guid + ".presets.txt"));
            PresetFile file = store.Load();
            var fx = gameObject.AddComponent<FlightFxController>();
            fx.Settings = file.Active;
            gameObject.AddComponent<EditorWindow>().Init(fx, store, file, key);
            Log.LogInfo($"{Name} {Version} loaded; editor key {key.Value}; presets {store.Path}; shaders {ShaderLib.Describe()}; " +
                        $"{GameAccess.Describe()}; {InputShield.Describe()}");
        }
    }
}
