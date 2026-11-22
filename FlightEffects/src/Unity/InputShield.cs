using System.Reflection;
using UnityEngine;

namespace FlightEffects
{
    /// <summary>
    /// While the pointer is over the editor window the game must not react to clicks there. The game's HUD (NGUI) is
    /// paused with UICamera.ignoreAllEvents, and KAUI's "mouse is over a UI item" (the check behind click-to-walk,
    /// camera dragging and clickable objects) reports a hidden dummy widget. Both are restored as soon as the pointer
    /// leaves the window or the editor closes. No game code is patched.
    /// </summary>
    internal sealed class InputShield
    {
        private static readonly FieldInfo HoverItem =
            typeof(KAUI).GetField("mGlobalMouseOverItem", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private KAWidget _dummy;
        private bool _active;
        private bool _ignoreBefore;

        public static string Describe() => $"mGlobalMouseOverItem {(HoverItem != null ? "ok" : "missing")}";

        public void Update(bool pointerOverWindow)
        {
            if (pointerOverWindow)
                Engage();
            else
                Release();
        }

        public void Release()
        {
            if (!_active)
                return;
            UICamera.ignoreAllEvents = _ignoreBefore;
            if (HoverItem != null && _dummy != null && ReferenceEquals(HoverItem.GetValue(null), _dummy))
                HoverItem.SetValue(null, null);
            _active = false;
        }

        private void Engage()
        {
            if (!_active)
            {
                _ignoreBefore = UICamera.ignoreAllEvents;
                UICamera.ignoreAllEvents = true;
                _active = true;
            }
            if (HoverItem != null && HoverItem.GetValue(null) as Object == null)
                HoverItem.SetValue(null, Dummy());
        }

        private KAWidget Dummy()
        {
            if (_dummy == null)
            {
                var go = new GameObject("FlightFx_InputShield");
                go.SetActive(false); // inactive: the widget never initialises or draws
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                _dummy = go.AddComponent<KAWidget>();
            }
            return _dummy;
        }
    }
}
