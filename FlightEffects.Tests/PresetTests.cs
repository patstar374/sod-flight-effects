using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlightEffects.Core;
using Xunit;

namespace FlightEffects.Tests
{
    public class PresetCodecTests
    {
        internal static void AssertSameSettings(EffectSettings a, EffectSettings b)
        {
            Assert.Equal(a.Enabled, b.Enabled);
            Assert.Equal(a.Type, b.Type);
            Assert.Equal(ColorMath.ToHex(a.Color1), ColorMath.ToHex(b.Color1));
            Assert.Equal(ColorMath.ToHex(a.Color2), ColorMath.ToHex(b.Color2));
            Assert.Equal(a.Length, b.Length, 3);
            Assert.Equal(a.Width, b.Width, 3);
            Assert.Equal(a.Glow, b.Glow, 3);
            Assert.Equal(a.Particles, b.Particles, 3);
            Assert.Equal(a.Mode, b.Mode);
            Assert.Equal(a.Wingtips, b.Wingtips);
            Assert.Equal(a.Tail, b.Tail);
            Assert.Equal(a.PreviewOnGround, b.PreviewOnGround);
        }

        [Fact]
        public void RoundTrip_PreservesActiveAndPresets()
        {
            var f = new PresetFile { Active = EffectSettings.Defaults(EffectType.Fire) };
            f.Active.Length = 2.5f;
            f.Active.Tail = true;
            f.Active.Mode = EmitMode.WhileAccelerating;
            f.Active.PreviewOnGround = true;
            f.Active.Enabled = false;
            f.Active.Color1 = new Rgba(0.2f, 0.4f, 0.6f, 0.8f);
            f.Presets.Add(new KeyValuePair<string, EffectSettings>("Mein Feuer", EffectSettings.Defaults(EffectType.Fire)));
            f.Presets.Add(new KeyValuePair<string, EffectSettings>("Herz ♥ Ä", EffectSettings.Defaults(EffectType.Hearts)));
            PresetFile back = PresetCodec.Parse(PresetCodec.Write(f));
            AssertSameSettings(f.Active, back.Active);
            Assert.Equal(new[] { "Mein Feuer", "Herz ♥ Ä" }, back.Presets.Select(p => p.Key));
            AssertSameSettings(f.Presets[1].Value, back.Presets[1].Value);
        }

        [Fact]
        public void Parse_IgnoresUnknownKeysAndBadValues()
        {
            PresetFile f = PresetCodec.Parse("[active]\ntype=Hearts\nlength=abc\nfoo=bar\ncolor1=nope\n[weird]\ntype=Fire\n");
            Assert.Equal(EffectType.Hearts, f.Active.Type);
            Assert.Equal(1.2f, f.Active.Length);
            Assert.Equal("#FFFFFFFF", ColorMath.ToHex(f.Active.Color1));
            Assert.Empty(f.Presets);
        }

        [Fact]
        public void Parse_AcceptsDecimalComma()
        {
            Assert.Equal(1.5f, PresetCodec.Parse("[active]\nlength=1,5\n").Active.Length, 3);
        }

        [Fact]
        public void Parse_ClampsValues()
        {
            Assert.Equal(4f, PresetCodec.Parse("[active]\nlength=99\n").Active.Length);
        }

        [Fact]
        public void Parse_Empty_GivesDefaultsWithoutPresets()
        {
            foreach (string text in new[] { "", null })
            {
                PresetFile f = PresetCodec.Parse(text, out bool valid);
                Assert.False(valid);
                AssertSameSettings(new EffectSettings(), f.Active);
                Assert.Empty(f.Presets);
            }
        }

        [Fact]
        public void Write_SanitizesPresetNames()
        {
            var f = new PresetFile();
            f.Presets.Add(new KeyValuePair<string, EffectSettings>("A]b\nc[", new EffectSettings()));
            f.Presets.Add(new KeyValuePair<string, EffectSettings>("  ", new EffectSettings()));
            PresetFile back = PresetCodec.Parse(PresetCodec.Write(f));
            Assert.Equal(new[] { "A b c", "Vorlage" }, back.Presets.Select(p => p.Key));
        }

        [Fact]
        public void BuiltIns_SixNamedPresetsCoverAllTypes()
        {
            Assert.Equal(new[] { "Klassisch", "Regenbogen", "Feuervogel", "Glitzerfee", "Sternenstaub", "Herzchen" },
                PresetCodec.BuiltIns.Select(p => p.Key));
            Assert.Equal(Enum.GetValues(typeof(EffectType)).Cast<EffectType>().OrderBy(t => t),
                PresetCodec.BuiltIns.Select(p => p.Value.Type).OrderBy(t => t));
        }
    }

    public class SettingsStoreTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "fe_tests_" + Guid.NewGuid().ToString("N"));

        public SettingsStoreTests() => Directory.CreateDirectory(_dir);

        public void Dispose() => Directory.Delete(_dir, true);

        private string FilePath => Path.Combine(_dir, "presets.txt");

        [Fact]
        public void SettingsStore_MissingFile_GivesBuiltIns()
        {
            PresetFile f = new SettingsStore(FilePath).Load();
            Assert.Equal(6, f.Presets.Count);
            Assert.Equal(EffectType.Contrail, f.Active.Type);
        }

        [Fact]
        public void SettingsStore_SaveThenLoad_RoundTrips()
        {
            var store = new SettingsStore(FilePath);
            PresetFile f = PresetFile.WithBuiltIns();
            f.Active = EffectSettings.Defaults(EffectType.Stardust);
            f.Active.Width = 2.25f;
            f.Presets.RemoveAt(0);
            store.Save(f);
            PresetFile back = store.Load();
            PresetCodecTests.AssertSameSettings(f.Active, back.Active);
            Assert.Equal(5, back.Presets.Count);
        }

        [Fact]
        public void SettingsStore_Save_LeavesNoTmp()
        {
            var store = new SettingsStore(FilePath);
            store.Save(PresetFile.WithBuiltIns());
            store.Save(PresetFile.WithBuiltIns());
            Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
            Assert.True(File.Exists(FilePath));
        }

        [Fact]
        public void SettingsStore_CorruptFile_BacksUpAndUsesBuiltIns()
        {
            File.WriteAllText(FilePath, "garbage \0\0 not a preset file");
            PresetFile f = new SettingsStore(FilePath).Load();
            Assert.Equal(6, f.Presets.Count);
            Assert.True(File.Exists(FilePath + ".broken"));
            Assert.Equal("garbage \0\0 not a preset file", File.ReadAllText(FilePath + ".broken"));
        }
    }
}
