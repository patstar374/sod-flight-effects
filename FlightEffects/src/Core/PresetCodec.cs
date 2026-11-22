using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FlightEffects.Core
{
    /// <summary>The active settings plus the player's named presets (in their order).</summary>
    public sealed class PresetFile
    {
        public EffectSettings Active = new EffectSettings();
        public List<KeyValuePair<string, EffectSettings>> Presets = new List<KeyValuePair<string, EffectSettings>>();

        /// <summary>Next free preset name for a type: "Herzen 1", "Herzen 2", ... (no typing needed in game).</summary>
        public string NextName(EffectType type)
        {
            string label = EffectNames.Label(type);
            for (int n = 1; ; n++)
            {
                string name = label + " " + n;
                if (!Presets.Exists(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)))
                    return name;
            }
        }

        public static PresetFile WithBuiltIns()
        {
            var f = new PresetFile { Active = PresetCodec.BuiltIns[0].Value.Clone() };
            foreach (KeyValuePair<string, EffectSettings> p in PresetCodec.BuiltIns)
                f.Presets.Add(new KeyValuePair<string, EffectSettings>(p.Key, p.Value.Clone()));
            return f;
        }
    }

    /// <summary>
    /// Readable text format (UTF-8): "# FlightEffects presets v1", sections [active] and [preset Name], lines key=value.
    /// Parsing never throws: unknown keys and sections are ignored, bad values keep their defaults, all values are
    /// clamped; decimal commas are accepted for hand-edited files.
    /// </summary>
    public static class PresetCodec
    {
        public const string Header = "# FlightEffects presets v1";
        private const string PresetPrefix = "preset ";

        public static readonly IReadOnlyList<KeyValuePair<string, EffectSettings>> BuiltIns = CreateBuiltIns();

        public static string Write(PresetFile file)
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n').Append("[active]\n");
            WriteSettings(sb, file.Active);
            foreach (KeyValuePair<string, EffectSettings> p in file.Presets)
            {
                sb.Append('\n').Append('[').Append(PresetPrefix).Append(SanitizeName(p.Key)).Append("]\n");
                WriteSettings(sb, p.Value);
            }
            return sb.ToString();
        }

        public static PresetFile Parse(string text) => Parse(text, out _);

        /// <param name="valid">true when the text has an [active] section (otherwise it is not a preset file).</param>
        public static PresetFile Parse(string text, out bool valid)
        {
            var file = new PresetFile();
            valid = false;
            EffectSettings current = null;
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    string section = line.Substring(1, line.Length - 2);
                    current = null;
                    if (section.Equals("active", StringComparison.OrdinalIgnoreCase))
                    {
                        current = file.Active;
                        valid = true;
                    }
                    else if (section.StartsWith(PresetPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        current = new EffectSettings();
                        file.Presets.Add(new KeyValuePair<string, EffectSettings>(
                            SanitizeName(section.Substring(PresetPrefix.Length)), current));
                    }
                    continue;
                }
                int eq = line.IndexOf('=');
                if (current != null && eq > 0)
                    Apply(current, line.Substring(0, eq).Trim().ToLowerInvariant(), line.Substring(eq + 1).Trim());
            }
            file.Active.Clamp();
            foreach (KeyValuePair<string, EffectSettings> p in file.Presets)
                p.Value.Clamp();
            return file;
        }

        public static string SanitizeName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? "")
                sb.Append(c == '[' || c == ']' || c == '\r' || c == '\n' ? ' ' : c);
            string s = sb.ToString().Trim();
            return s.Length == 0 ? "Vorlage" : s;
        }

        private static void WriteSettings(StringBuilder sb, EffectSettings s)
        {
            sb.Append("enabled=").Append(Bool(s.Enabled)).Append('\n');
            sb.Append("type=").Append(s.Type).Append('\n');
            sb.Append("color1=").Append(ColorMath.ToHex(s.Color1)).Append('\n');
            sb.Append("color2=").Append(ColorMath.ToHex(s.Color2)).Append('\n');
            sb.Append("length=").Append(Num(s.Length)).Append('\n');
            sb.Append("width=").Append(Num(s.Width)).Append('\n');
            sb.Append("glow=").Append(Num(s.Glow)).Append('\n');
            sb.Append("particles=").Append(Num(s.Particles)).Append('\n');
            sb.Append("mode=").Append(s.Mode).Append('\n');
            sb.Append("wingtips=").Append(Bool(s.Wingtips)).Append('\n');
            sb.Append("tail=").Append(Bool(s.Tail)).Append('\n');
            sb.Append("preview=").Append(Bool(s.PreviewOnGround)).Append('\n');
        }

        private static void Apply(EffectSettings s, string key, string value)
        {
            switch (key)
            {
                case "enabled": s.Enabled = ParseBool(value, s.Enabled); break;
                case "type":
                    if (Enum.TryParse(value, true, out EffectType t) && Enum.IsDefined(typeof(EffectType), t)) s.Type = t;
                    break;
                case "color1": if (ColorMath.TryParseHex(value, out Rgba c1)) s.Color1 = c1; break;
                case "color2": if (ColorMath.TryParseHex(value, out Rgba c2)) s.Color2 = c2; break;
                case "length": s.Length = ParseFloat(value, s.Length); break;
                case "width": s.Width = ParseFloat(value, s.Width); break;
                case "glow": s.Glow = ParseFloat(value, s.Glow); break;
                case "particles": s.Particles = ParseFloat(value, s.Particles); break;
                case "mode":
                    if (Enum.TryParse(value, true, out EmitMode m) && Enum.IsDefined(typeof(EmitMode), m)) s.Mode = m;
                    break;
                case "wingtips": s.Wingtips = ParseBool(value, s.Wingtips); break;
                case "tail": s.Tail = ParseBool(value, s.Tail); break;
                case "preview": s.PreviewOnGround = ParseBool(value, s.PreviewOnGround); break;
            }
        }

        private static string Bool(bool b) => b ? "true" : "false";

        private static string Num(float f) => f.ToString("0.###", CultureInfo.InvariantCulture);

        private static bool ParseBool(string v, bool fallback)
        {
            if (bool.TryParse(v, out bool b)) return b;
            if (v == "1") return true;
            if (v == "0") return false;
            return fallback;
        }

        private static float ParseFloat(string v, float fallback) =>
            float.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : fallback;

        private static IReadOnlyList<KeyValuePair<string, EffectSettings>> CreateBuiltIns()
        {
            EffectSettings fire = EffectSettings.Defaults(EffectType.Fire);
            fire.Width = 1.3f;
            EffectSettings stardust = EffectSettings.Defaults(EffectType.Stardust);
            stardust.Length = 1.8f;
            return new List<KeyValuePair<string, EffectSettings>>
            {
                new KeyValuePair<string, EffectSettings>("Klassisch", EffectSettings.Defaults(EffectType.Contrail)),
                new KeyValuePair<string, EffectSettings>("Regenbogen", EffectSettings.Defaults(EffectType.Rainbow)),
                new KeyValuePair<string, EffectSettings>("Feuervogel", fire),
                new KeyValuePair<string, EffectSettings>("Glitzerfee", EffectSettings.Defaults(EffectType.Sparkle)),
                new KeyValuePair<string, EffectSettings>("Sternenstaub", stardust),
                new KeyValuePair<string, EffectSettings>("Herzchen", EffectSettings.Defaults(EffectType.Hearts)),
            };
        }
    }
}
