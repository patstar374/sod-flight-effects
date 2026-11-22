using System;
using System.IO;
using System.Text;

namespace FlightEffects.Core
{
    /// <summary>
    /// Loads and saves the preset file. Never throws on load; saving replaces the file atomically and keeps the
    /// previous version as &lt;path&gt;.bak. An older file at legacyPath (inside the game client, which the launcher wipes
    /// on every payload change) is copied over once when the stable file does not exist yet.
    /// </summary>
    public sealed class SettingsStore
    {
        private readonly string _legacyPath;

        public SettingsStore(string path, string legacyPath = null)
        {
            Path = path;
            _legacyPath = legacyPath;
        }

        public string Path { get; }

        /// <summary>Missing file -> start presets. A file that is no preset file is kept as .broken, then start presets.</summary>
        public PresetFile Load()
        {
            MigrateLegacy();
            if (!File.Exists(Path))
                return PresetFile.WithBuiltIns();
            string text;
            try
            {
                text = File.ReadAllText(Path, Encoding.UTF8);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return PresetFile.WithBuiltIns();
            }
            PresetFile file = PresetCodec.Parse(text, out bool valid);
            if (valid)
                return file;
            try
            {
                File.Copy(Path, Path + ".broken", true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // keep going with the start presets even if the backup fails
            }
            return PresetFile.WithBuiltIns();
        }

        public void Save(PresetFile file)
        {
            string dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            string tmp = Path + ".tmp";
            File.WriteAllText(tmp, PresetCodec.Write(file), new UTF8Encoding(false));
            if (File.Exists(Path))
                File.Replace(tmp, Path, Path + ".bak");
            else
                File.Move(tmp, Path);
        }

        private void MigrateLegacy()
        {
            if (string.IsNullOrEmpty(_legacyPath) || File.Exists(Path) || !File.Exists(_legacyPath))
                return;
            try
            {
                string dir = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.Copy(_legacyPath, Path, false);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // the old file stays where it is; Load falls back to the start presets
            }
        }
    }
}
