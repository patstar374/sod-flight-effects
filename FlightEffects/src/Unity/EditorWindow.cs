using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace FlightEffects
{
    using FlightEffects.Core;

    /// <summary>
    /// In-game effect editor (IMGUI, German labels). Changes apply at once and are saved 1 s later (and on quit). No text
    /// input: the game reads raw keys, so presets are named automatically ("Herzen 2"). While the pointer is over the
    /// window, an InputShield keeps clicks away from the game.
    /// </summary>
    internal sealed class EditorWindow : MonoBehaviour
    {
        private const int WindowId = 0x46784564;
        private const float WindowWidth = 410f, WindowHeight = 640f;
        private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
        private static readonly Color[] Swatches =
        {
            new Color(1f, 1f, 1f), new Color(0.75f, 0.88f, 1f), new Color(0.3f, 0.6f, 1f), new Color(0.2f, 0.9f, 1f),
            new Color(0.3f, 1f, 0.5f), new Color(0.75f, 1f, 0.2f), new Color(1f, 0.92f, 0.3f), new Color(1f, 0.6f, 0.1f),
            new Color(1f, 0.25f, 0.1f), new Color(1f, 0.35f, 0.65f), new Color(0.75f, 0.35f, 1f), new Color(0.45f, 0.25f, 0.9f),
        };

        private readonly InputShield _shield = new InputShield();
        private string[] _typeLabels;
        private FlightFxController _fx;
        private SettingsStore _store;
        private PresetFile _file;
        private ConfigEntry<KeyboardShortcut> _key;
        private bool _open;
        private bool _placed;
        private Rect _rect;
        private float _scale = 1f;
        private Vector2 _scroll;
        private int _slot;
        private float _h, _s, _v;
        private bool _hsvValid;
        private string _note;
        private int _confirmDelete = -1;
        private float _saveAt = -1f;
        private bool _cursorVisible;
        private CursorLockMode _cursorLock;

        public void Init(FlightFxController fx, SettingsStore store, PresetFile file, ConfigEntry<KeyboardShortcut> key)
        {
            _fx = fx;
            _store = store;
            _file = file;
            _key = key;
            _typeLabels = Enum.GetValues(typeof(EffectType)).Cast<EffectType>().Select(EffectNames.Label).ToArray();
        }

        private void Update()
        {
            if (_key != null && _key.Value.IsDown())
                Toggle();
            if (_open)
            {
                if (Cursor.lockState != CursorLockMode.None)
                    Cursor.lockState = CursorLockMode.None;
                if (UICursorManager.IsCursorHidden() && !Cursor.visible)
                    Cursor.visible = true; // the game hides its own cursor right now: show the system one
                Vector3 m = Input.mousePosition;
                _shield.Update(_rect.Contains(new Vector2(m.x / _scale, (Screen.height - m.y) / _scale)));
            }
            if (_saveAt > 0f && Time.unscaledTime >= _saveAt)
                Save();
        }

        private void OnApplicationQuit() => FlushSave();

        private void OnDisable()
        {
            _shield.Release();
            FlushSave();
        }

        private void Toggle()
        {
            _open = !_open;
            if (_open)
            {
                _cursorVisible = Cursor.visible;
                _cursorLock = Cursor.lockState;
                _hsvValid = false;
                _confirmDelete = -1;
                _note = null;
            }
            else
            {
                _shield.Release();
                Cursor.visible = _cursorVisible;
                Cursor.lockState = _cursorLock;
                Save();
            }
        }

        private void OnGUI()
        {
            if (!_open || _fx == null)
                return;
            _scale = WindowFit.Scale(Screen.height);
            if (!_placed)
            {
                _rect = new Rect(Screen.width / _scale - WindowWidth - 140f, 160f, WindowWidth, WindowHeight);
                _placed = true;
            }
            float x = _rect.x, y = _rect.y, w = WindowWidth, h = WindowHeight;
            WindowFit.Clamp(ref x, ref y, ref w, ref h, Screen.width, Screen.height, _scale);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            _rect = GUILayout.Window(WindowId, new Rect(x, y, w, h), DrawWindow, $"Flugeffekte ({_key.Value})",
                GUILayout.Width(w), GUILayout.Height(h));
            GUI.matrix = old;
        }

        private void DrawWindow(int id)
        {
            EffectSettings s = _fx.Settings;
            bool changed = false, saveNew = false;
            int load = -1, remove = -1, confirm = _confirmDelete;
            _scroll = GUILayout.BeginScrollView(_scroll);

            changed |= Toggle(ref s.Enabled, "Effekte an");

            Header("Effekt");
            int type = GUILayout.SelectionGrid((int)s.Type, _typeLabels, 3);
            if (type != (int)s.Type)
            {
                s.Type = (EffectType)type;
                EffectSettings.DefaultColors(s.Type, out s.Color1, out s.Color2);
                _hsvValid = false;
                changed = true;
            }

            if (s.Type == EffectType.Rainbow)
                GUILayout.Label("Regenbogen nutzt alle Farben.");
            else
                changed |= ColorEditor(s);

            Header("Aussehen");
            changed |= Slider("Länge", ref s.Length, EffectSettings.MinLength, EffectSettings.MaxLength, v => v.ToString("0.0", De) + " s");
            changed |= Slider("Breite", ref s.Width, EffectSettings.MinWidth, EffectSettings.MaxWidth, v => v.ToString("0.0", De));
            changed |= Slider("Leuchten", ref s.Glow, EffectSettings.MinGlow, EffectSettings.MaxGlow, v => (v * 100f).ToString("0", De) + " %");
            if (StyleTable.For(s, 0f).Particle != SpriteKind.None)
                changed |= Slider("Partikel", ref s.Particles, 0f, EffectSettings.MaxParticles, v => (v * 100f).ToString("0", De) + " %");

            Header("Wann");
            bool always = s.Mode == EmitMode.WhileFlying;
            if (GUILayout.Toggle(always, " immer beim Fliegen") && !always)
            {
                s.Mode = EmitMode.WhileFlying;
                changed = true;
            }
            if (GUILayout.Toggle(!always, " nur beim Beschleunigen") && always)
            {
                s.Mode = EmitMode.WhileAccelerating;
                changed = true;
            }

            Header("Wo");
            GUILayout.BeginHorizontal();
            changed |= Toggle(ref s.Wingtips, "Flügelspitzen");
            changed |= Toggle(ref s.Tail, "Schwanz");
            GUILayout.EndHorizontal();
            changed |= Toggle(ref s.PreviewOnGround, "Vorschau am Boden (Streifen beim Laufen)");

            Header("Vorlagen");
            if (GUILayout.Button("Aktuellen Effekt als Vorlage speichern"))
                saveNew = true;
            if (_note != null)
                GUILayout.Label(_note);
            for (int i = 0; i < _file.Presets.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_file.Presets[i].Key);
                if (_confirmDelete == i)
                {
                    GUILayout.Label("Löschen?", GUILayout.Width(62f));
                    if (GUILayout.Button("Ja", GUILayout.Width(40f)))
                        remove = i;
                    if (GUILayout.Button("Nein", GUILayout.Width(50f)))
                        confirm = -1;
                }
                else
                {
                    if (GUILayout.Button("Laden", GUILayout.Width(64f)))
                        load = i;
                    if (GUILayout.Button("X", GUILayout.Width(28f)))
                        confirm = i;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
            GUILayout.Label("Drache: " + (_fx.RigDescription ?? "keiner gefunden"));
            GUILayout.EndScrollView();
            if (GUILayout.Button("Schließen"))
                Toggle();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));

            if (changed)
            {
                s.Clamp();
                _fx.MarkDirty();
                _saveAt = Time.unscaledTime + 1f;
            }
            _confirmDelete = confirm;
            if (saveNew)
                SaveAsNewPreset();
            else if (load >= 0)
                LoadPreset(load);
            else if (remove >= 0)
                DeletePreset(remove);
        }

        private bool ColorEditor(EffectSettings s)
        {
            bool changed = false;
            int slot = GUILayout.Toolbar(_slot, new[] { "Farbe 1", "Farbe 2" });
            if (slot != _slot)
            {
                _slot = slot;
                _hsvValid = false;
            }
            Rgba current = _slot == 0 ? s.Color1 : s.Color2;
            Rgba picked = current;
            for (int row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < 6; col++)
                {
                    Color sw = Swatches[row * 6 + col];
                    Rect r = GUILayoutUtility.GetRect(30f, 22f, GUILayout.Width(30f), GUILayout.Height(22f));
                    DrawColor(r, sw);
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                    {
                        picked = new Rgba(sw.r, sw.g, sw.b);
                        _hsvValid = false;
                    }
                }
                GUILayout.EndHorizontal();
            }
            if (!_hsvValid)
            {
                ColorMath.RgbToHsv(picked, out _h, out _s, out _v);
                _hsvValid = true;
            }
            bool hsv = false;
            hsv |= Slider("Farbton", ref _h, 0f, 1f, v => (v * 360f).ToString("0", De) + "°");
            hsv |= Slider("Sättigung", ref _s, 0f, 1f, v => (v * 100f).ToString("0", De) + " %");
            hsv |= Slider("Helligkeit", ref _v, 0f, 1f, v => (v * 100f).ToString("0", De) + " %");
            if (hsv)
                picked = ColorMath.HsvToRgb(_h, _s, _v);
            DrawColor(GUILayoutUtility.GetRect(10f, 16f, GUILayout.ExpandWidth(true)), new Color(picked.R, picked.G, picked.B));
            if (!picked.Equals(current))
            {
                if (_slot == 0)
                    s.Color1 = picked;
                else
                    s.Color2 = picked;
                changed = true;
            }
            return changed;
        }

        private void SaveAsNewPreset()
        {
            string name = _file.NextName(_fx.Settings.Type);
            _file.Presets.Add(new KeyValuePair<string, EffectSettings>(name, _fx.Settings.Clone()));
            _note = $"Gespeichert als „{name}“.";
            Save();
        }

        private void LoadPreset(int index)
        {
            _file.Active = _file.Presets[index].Value.Clone();
            _fx.Settings = _file.Active;
            _fx.MarkDirty();
            _hsvValid = false;
            _note = $"„{_file.Presets[index].Key}“ geladen.";
            Save();
        }

        private void DeletePreset(int index)
        {
            _note = $"„{_file.Presets[index].Key}“ gelöscht.";
            _file.Presets.RemoveAt(index);
            _confirmDelete = -1;
            Save();
        }

        private void FlushSave()
        {
            if (_saveAt > 0f)
                Save();
        }

        private void Save()
        {
            _saveAt = -1f;
            try
            {
                _store.Save(_file);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"FlightEffects: could not save {_store.Path}: {e.Message}");
            }
        }

        private static void Header(string text)
        {
            GUILayout.Space(6f);
            GUILayout.Label("<b>" + text + "</b>");
        }

        private static bool Toggle(ref bool value, string label)
        {
            bool v = GUILayout.Toggle(value, " " + label);
            if (v == value)
                return false;
            value = v;
            return true;
        }

        private static bool Slider(string label, ref float value, float min, float max, Func<float, string> format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ": " + format(value), GUILayout.Width(150f));
            float v = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            if (Mathf.Approximately(v, value))
                return false;
            value = v;
            return true;
        }

        private static void DrawColor(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
