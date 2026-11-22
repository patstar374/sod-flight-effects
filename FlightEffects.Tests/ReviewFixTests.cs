using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlightEffects.Core;
using Xunit;

namespace FlightEffects.Tests
{
    /// <summary>Fixes from the final review (0.1.1).</summary>
    public class SettingsMigrationTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "fe_mig_" + Guid.NewGuid().ToString("N"));

        public SettingsMigrationTests() => Directory.CreateDirectory(_dir);

        public void Dispose() => Directory.Delete(_dir, true);

        private string Legacy => Path.Combine(_dir, "client", "presets.txt");
        private string Stable => Path.Combine(_dir, "appdata", "FlightEffects", "presets.txt");

        private static PresetFile With(EffectType type)
        {
            PresetFile f = PresetFile.WithBuiltIns();
            f.Active = EffectSettings.Defaults(type);
            return f;
        }

        [Fact]
        public void SettingsStore_MigratesTheOldFile()
        {
            new SettingsStore(Legacy).Save(With(EffectType.Hearts));
            var store = new SettingsStore(Stable, Legacy);
            Assert.Equal(EffectType.Hearts, store.Load().Active.Type);
            Assert.True(File.Exists(Stable));
        }

        [Fact]
        public void SettingsStore_NewFileWinsOverTheOldOne()
        {
            new SettingsStore(Legacy).Save(With(EffectType.Hearts));
            new SettingsStore(Stable).Save(With(EffectType.Fire));
            Assert.Equal(EffectType.Fire, new SettingsStore(Stable, Legacy).Load().Active.Type);
        }

        [Fact]
        public void SettingsStore_Save_KeepsThePreviousVersionAsBak()
        {
            var store = new SettingsStore(Stable);
            store.Save(With(EffectType.Hearts));
            store.Save(With(EffectType.Fire));
            Assert.Equal(EffectType.Hearts, PresetCodec.Parse(File.ReadAllText(Stable + ".bak")).Active.Type);
            Assert.Equal(EffectType.Fire, store.Load().Active.Type);
        }
    }

    public class PresetNamingTests
    {
        [Fact]
        public void EffectNames_AreGermanAndDistinct()
        {
            Assert.Equal("Herzen", EffectNames.Label(EffectType.Hearts));
            Assert.Equal("Kondensstreifen", EffectNames.Label(EffectType.Contrail));
            string[] all = Enum.GetValues(typeof(EffectType)).Cast<EffectType>().Select(EffectNames.Label).ToArray();
            Assert.Equal(6, all.Distinct().Count());
        }

        [Fact]
        public void NextName_CountsUpPerType()
        {
            var f = new PresetFile();
            Assert.Equal("Herzen 1", f.NextName(EffectType.Hearts));
            f.Presets.Add(new KeyValuePair<string, EffectSettings>("Herzen 1", new EffectSettings()));
            f.Presets.Add(new KeyValuePair<string, EffectSettings>("herzen 2", new EffectSettings()));
            Assert.Equal("Herzen 3", f.NextName(EffectType.Hearts));
            Assert.Equal("Feuer 1", f.NextName(EffectType.Fire));
        }
    }

    public class BuildAttemptsTests
    {
        [Fact]
        public void BuildAttempts_OnlyFailuresCount()
        {
            var b = new BuildAttempts(5, 2f);
            float now = 0f;
            for (int i = 0; i < 10; i++)
            {
                Assert.True(b.CanTry(7, now));
                b.Succeeded(7);
                now += 3f;
            }
        }

        [Fact]
        public void BuildAttempts_GiveUpAfterFiveFailures()
        {
            var b = new BuildAttempts(5, 2f);
            float now = 0f;
            bool gaveUp = false;
            for (int i = 0; i < 5; i++)
            {
                Assert.True(b.CanTry(7, now));
                gaveUp = b.Failed(7, now);
                now += 3f;
            }
            Assert.True(gaveUp);
            Assert.False(b.CanTry(7, now));
            Assert.True(b.CanTry(8, now));
        }

        [Fact]
        public void BuildAttempts_WaitBetweenTries()
        {
            var b = new BuildAttempts(5, 2f);
            b.Failed(7, 0f);
            Assert.False(b.CanTry(7, 1f));
            Assert.True(b.CanTry(7, 2.1f));
        }
    }

    public class MotionTests
    {
        [Fact]
        public void IsJump_ThresholdScalesWithSpanButNeverBelowTwentyMetres()
        {
            var origin = new Vec3(0f, 0f, 0f);
            Assert.False(Motion.IsJump(origin, new Vec3(40f, 0f, 0f), 8.5f));
            Assert.True(Motion.IsJump(origin, new Vec3(0f, 0f, 43f), 8.5f));
            Assert.False(Motion.IsJump(origin, new Vec3(19f, 0f, 0f), 2f));
            Assert.True(Motion.IsJump(origin, new Vec3(21f, 0f, 0f), 2f));
        }
    }

    public class ParticleSpinTests
    {
        [Fact]
        public void Hearts_StayUpright()
        {
            Assert.InRange(StyleTable.For(EffectSettings.Defaults(EffectType.Hearts), 0f).ParticleSpin, 0f, 0.35f);
        }

        [Fact]
        public void Stars_SpinFreely()
        {
            Assert.True(StyleTable.For(EffectSettings.Defaults(EffectType.Sparkle), 0f).ParticleSpin >= 3.14f);
        }
    }

    public class WindowFitTests
    {
        [Fact]
        public void Scale_FollowsScreenHeightWithinLimits()
        {
            Assert.Equal(1f, WindowFit.Scale(1080f), 3);
            Assert.Equal(720f / 1080f, WindowFit.Scale(720f), 3);
            Assert.Equal(2f, WindowFit.Scale(2160f), 3);
            Assert.Equal(WindowFit.MinScale, WindowFit.Scale(300f), 3);
        }

        [Fact]
        public void Clamp_KeepsTheWindowOnScreen()
        {
            float x = 1500f, y = 900f, w = 410f, h = 640f;
            WindowFit.Clamp(ref x, ref y, ref w, ref h, 1280f, 720f, WindowFit.Scale(720f));
            Assert.Equal(410f, w);
            Assert.Equal(640f, h);
            Assert.True(x + w <= 1920f && y + h <= 1080f && x >= 0f && y >= 0f, $"{x},{y}");
        }

        [Fact]
        public void Clamp_ShrinksAWindowTallerThanTheScreen()
        {
            float x = 0f, y = 0f, w = 410f, h = 900f;
            WindowFit.Clamp(ref x, ref y, ref w, ref h, 1000f, 800f, 1f);
            Assert.True(h <= 800f - 2 * WindowFit.Margin && y >= 0f && y + h <= 800f);
        }
    }
}
