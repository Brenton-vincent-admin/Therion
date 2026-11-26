using System;
using System.Collections.Generic;
using Dino.Core;
using Dino.Progression;
using Dino.Species;
using UnityEngine;
using UnityEngine.UI;

namespace Dino.UI
{
    /// <summary>
    /// "Pick your dino" screen: one big button per species (locked ones show the level needed),
    /// and a mode row (Survival / Hardcore / Explorer). Built in code.
    /// </summary>
    public class SpeciesSelectScreen : MonoBehaviour
    {
        public event Action<SpeciesDefinition> SpeciesChosen;
        public event Action<GameMode> ModeChanged;
        /// <summary>Parent/testing: the "all dinos" switch was flipped (tap the corner button 3 times).</summary>
        public event Action<bool> UnlockAllChanged;

        Canvas _canvas;
        RectTransform _grid;
        Text _modeText, _header;
        readonly List<GameObject> _buttons = new List<GameObject>();
        GameMode _mode;
        SpeciesCatalog _catalog; ProgressionService _progression;
        Text _unlockAllText; int _unlockTaps; float _lastUnlockTap;

        static readonly Color Herb = new Color(0.3f, 0.65f, 0.3f);
        static readonly Color Omni = new Color(0.85f, 0.6f, 0.2f);
        static readonly Color Carn = new Color(0.8f, 0.3f, 0.3f);

        void Awake() => Build();

        public bool IsOpen => _canvas && _canvas.gameObject.activeSelf;

        public void Open(SpeciesCatalog catalog, ProgressionService progression, GameMode mode)
        {
            _mode = mode; _catalog = catalog; _progression = progression;
            RefreshUnlockAll();
            _canvas.gameObject.SetActive(true);
            foreach (var b in _buttons) Destroy(b);
            _buttons.Clear();
            RefreshMode();

            _header.text = $"Pick your dino!   (Level {progression.Level})";
            if (catalog == null || catalog.species.Count == 0)
            {
                _header.text = "No dinos yet: add species to Assets/_Game/Resources/SpeciesCatalog.asset";
                return;
            }

            int i = 0;
            foreach (var s in catalog.Sorted())
            {
                bool unlocked = progression.IsUnlocked(s);
                Color c = s.diet == Diet.Herbivore ? Herb : s.diet == Diet.Omnivore ? Omni : Carn;
                if (!unlocked) c = Color.Lerp(c, Color.gray, 0.75f);
                int col = i % 5, row = i / 5;
                var pos = new Vector2(-760 + col * 380, 220 - row * 210);
                string label = unlocked ? (progression.IsEarned(s) ? s.displayName : $"{s.displayName}\n(test)") : $"{s.displayName}\nLevel {XpCurve.LevelForXp(s.unlockXp)}";
                var species = s;
                var btn = UiFactory.Button(_grid, s.id, label, new Vector2(0.5f, 0.5f), pos, new Vector2(350, 180), c,
                    unlocked ? () => SpeciesChosen?.Invoke(species) : (Action)null);
                btn.interactable = unlocked;
                if (s.icon)
                {
                    var iconRt = UiFactory.Rect("Icon", btn.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(90, 90));
                    UiFactory.Image(iconRt, unlocked ? Color.white : new Color(0, 0, 0, 0.6f), s.icon, false);
                }
                _buttons.Add(btn.gameObject);
                i++;
            }
        }

        public void Close() => _canvas.gameObject.SetActive(false);

        void Build()
        {
            _canvas = UiFactory.CreateCanvas("SpeciesSelect", 20);
            _canvas.transform.SetParent(transform, false);
            var bg = UiFactory.Stretch("Background", _canvas.transform);
            UiFactory.Image(bg, new Color(0.08f, 0.14f, 0.12f, 0.96f));
            var headerRt = UiFactory.Rect("Header", bg, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(1600, 90));
            _header = UiFactory.Label(headerRt, "Pick your dino!", 60, Color.white);
            _grid = UiFactory.Rect("Grid", bg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1900, 800));

            var bottom = new Vector2(0.5f, 0);
            UiFactory.Button(bg, "Survival", "Survival", bottom, new Vector2(-420, 90), new Vector2(300, 100), new Color(0.25f, 0.5f, 0.8f), () => SetMode(GameMode.Survival));
            UiFactory.Button(bg, "Hardcore", "Hardcore", bottom, new Vector2(-100, 90), new Vector2(300, 100), new Color(0.6f, 0.25f, 0.3f), () => SetMode(GameMode.Hardcore));
            UiFactory.Button(bg, "Explorer", "Explorer", bottom, new Vector2(220, 90), new Vector2(300, 100), new Color(0.3f, 0.6f, 0.4f), () => SetMode(GameMode.Explorer));
            var modeRt = UiFactory.Rect("ModeText", bg, bottom, bottom, new Vector2(0.5f, 0.5f), new Vector2(640, 90), new Vector2(560, 100));
            _modeText = UiFactory.Label(modeRt, "", 26, Color.white, TextAnchor.MiddleLeft);
            // Parent/testing switch, top right. Needs 3 quick taps so a child doesn't flip it by accident.
            var corner = new Vector2(1, 1);
            var unlockBtn = UiFactory.Button(bg, "UnlockAll", "", corner, new Vector2(-190, -60), new Vector2(340, 80), new Color(0.2f, 0.25f, 0.3f), OnUnlockAllTap);
            _unlockAllText = unlockBtn.GetComponentInChildren<Text>();
            if (_unlockAllText) _unlockAllText.fontSize = 22;
            _canvas.gameObject.SetActive(false);
        }

        void OnUnlockAllTap()
        {
            if (_progression == null) return;
            _unlockTaps = Time.unscaledTime - _lastUnlockTap < 1.5f ? _unlockTaps + 1 : 1;
            _lastUnlockTap = Time.unscaledTime;
            if (_unlockTaps < 3) return;
            _unlockTaps = 0;
            _progression.UnlockAll = !_progression.UnlockAll;
            UnlockAllChanged?.Invoke(_progression.UnlockAll);
            Open(_catalog, _progression, _mode);
        }

        void RefreshUnlockAll()
        {
            if (_unlockAllText == null) return;
            bool on = _progression != null && _progression.UnlockAll;
            _unlockAllText.text = on ? "Parent: all dinos ON\n(tap 3x to turn off)" : "Parent: all dinos\n(tap 3x)";
        }

        void SetMode(GameMode m) { _mode = m; RefreshMode(); ModeChanged?.Invoke(m); }

        void RefreshMode()
        {
            _modeText.text = _mode switch
            {
                GameMode.Survival => "Survival: if you faint, you wake up at your nest.",
                GameMode.Hardcore => "Hardcore: if you faint, start over as a baby. You keep your XP!",
                _ => "Explorer: no hunger, no danger. Just explore!",
            };
        }
    }
}
