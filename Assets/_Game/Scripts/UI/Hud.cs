using System;
using Dino.Player;
using Dino.Progression;
using Dino.Survival;
using UnityEngine;
using UnityEngine.UI;

namespace Dino.UI
{
    /// <summary>
    /// Kid-readable HUD built in code: four meters with picture icons (top-left), level + XP and
    /// growth bars (top-center), a pause button (top-right) and a banner for big moments.
    /// Everything sits inside the phone's safe area.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        Canvas _canvas;
        Meter _health, _stamina, _food, _water;
        Bar _xp, _grow;
        Text _levelText, _stageText, _bannerText;
        CanvasGroup _banner;
        RectTransform _bannerRt, _topCenter;
        float _bannerTime, _bannerAge;
        int _lastLevel = -1;
        float _levelPop;
        DinoActor _actor;
        DinoGrowth _growth;

        public Func<int> GetXp;
        /// <summary>Raised when the pause button is tapped.</summary>
        public event Action PauseClicked;

        void Awake() => Build();

        public void Bind(DinoActor actor)
        {
            _actor = actor;
            _growth = actor ? actor.GetComponent<DinoGrowth>() : null;
            _health.Snap(); _stamina.Snap(); _food.Snap(); _water.Snap();
        }

        public void SetVisible(bool v) { if (_canvas) _canvas.gameObject.SetActive(v); }

        public void ShowBanner(string text, float seconds = 3f)
        {
            _bannerText.text = text;
            _banner.gameObject.SetActive(true);
            _bannerTime = seconds;
            _bannerAge = 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateBanner(dt);

            int xp = GetXp != null ? GetXp() : 0;
            int level = XpCurve.LevelForXp(xp);
            if (_lastLevel >= 0 && level > _lastLevel) _levelPop = 1f;
            _lastLevel = level;
            _levelText.text = level.ToString();
            _xp.Set(XpCurve.ProgressToNext(xp), dt);
            _levelPop = Mathf.MoveTowards(_levelPop, 0f, dt * 2f);
            _levelText.transform.parent.localScale = Vector3.one * (1f + 0.35f * _levelPop);

            bool has = _actor != null && _actor.Species != null;
            _health.Root.gameObject.SetActive(has);
            _stamina.Root.gameObject.SetActive(has);
            _food.Root.gameObject.SetActive(has);
            _water.Root.gameObject.SetActive(has);
            _grow.Root.gameObject.SetActive(has);
            if (!has) return;

            var s = _actor.Stats;
            _health.Set(s.Health, dt, false);
            _stamina.Set(s.Stamina, dt, s.IsExhausted);
            _food.Set(s.Food, dt, false);
            _water.Set(s.Water, dt, false);
            if (_growth == null) _growth = _actor.GetComponent<DinoGrowth>();
            _grow.Set(_growth ? _growth.Progress01 : 1f, dt);
            _stageText.text = _actor.IsAdult
                ? $"{_actor.Species.displayName}  •  {_actor.Stage.name}"
                : $"{_actor.Stage.name} {_actor.Species.displayName}";
        }

        void UpdateBanner(float dt)
        {
            if (!_banner.gameObject.activeSelf) return;
            _bannerAge += dt;
            _bannerTime -= dt;
            float fadeIn = Mathf.Clamp01(_bannerAge / 0.2f);
            float fadeOut = Mathf.Clamp01(_bannerTime / 0.4f);
            _banner.alpha = Mathf.Min(fadeIn, fadeOut);
            // Little pop on entry.
            float pop = 1f + 0.12f * Mathf.Clamp01(1f - _bannerAge / 0.25f);
            _bannerRt.localScale = Vector3.one * pop;
            if (_bannerTime <= 0f) _banner.gameObject.SetActive(false);
        }

        void Build()
        {
            _canvas = UiFactory.CreateCanvas("HUD", 5);
            _canvas.transform.SetParent(transform, false);
            var root = UiFactory.SafeArea(_canvas.transform);

            // ----- meters, top-left -----
            var tl = new Vector2(0, 1);
            var panel = UiFactory.Rect("Meters", root, tl, tl, tl, new Vector2(28, -28), new Vector2(430, 292));
            UiFactory.Panel(panel, UiTheme.Panel, 28);
            _health = new Meter(panel, "Health", UiSprites.Heart, UiTheme.Health, 0);
            _stamina = new Meter(panel, "Stamina", UiSprites.Bolt, UiTheme.Stamina, 1);
            _food = new Meter(panel, "Food", UiSprites.Leaf, UiTheme.Food, 2);
            _water = new Meter(panel, "Water", UiSprites.Drop, UiTheme.Water, 3);

            // ----- level, XP and growth, top-center -----
            var tc = new Vector2(0.5f, 1);
            _topCenter = UiFactory.Rect("Progress", root, tc, tc, tc, new Vector2(0, -28), new Vector2(600, 118));
            UiFactory.Panel(_topCenter, UiTheme.Panel, 28);

            var badge = UiFactory.Rect("LevelBadge", _topCenter, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(62, 0), new Vector2(92, 92));
            UiFactory.Image(badge, UiTheme.Xp, UiSprites.Star, false);
            _levelText = UiFactory.Label(badge, "1", 34, UiTheme.Ink);
            Destroy(_levelText.GetComponent<Shadow>());

            _xp = new Bar(_topCenter, "XpBar", UiTheme.Xp, new Vector2(124, -22), new Vector2(452, 26));
            _grow = new Bar(_topCenter, "GrowBar", UiTheme.Growth, new Vector2(124, -64), new Vector2(452, 34));
            _stageText = UiFactory.Label(_grow.Root, "", 22, UiTheme.Text);

            // ----- pause, top-right -----
            var tr = new Vector2(1, 1);
            var pauseRt = UiFactory.Rect("Pause", root, tr, tr, tr, new Vector2(-28, -28), new Vector2(104, 104));
            var pauseImg = UiFactory.Panel(pauseRt, UiTheme.Panel, 28);
            pauseImg.raycastTarget = true;
            var pauseBtn = pauseRt.gameObject.AddComponent<Button>();
            pauseBtn.targetGraphic = pauseImg;
            pauseBtn.onClick.AddListener(() => PauseClicked?.Invoke());
            pauseRt.gameObject.AddComponent<PressFeedback>();
            var pauseIcon = UiFactory.Rect("Icon", pauseRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
            UiFactory.Image(pauseIcon, UiTheme.Text, UiSprites.Pause, false);

            // ----- banner -----
            var mid = new Vector2(0.5f, 0.7f);
            _bannerRt = UiFactory.Rect("Banner", root, mid, mid, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 124));
            UiFactory.Panel(_bannerRt, UiTheme.Panel, 36);
            _banner = _bannerRt.gameObject.AddComponent<CanvasGroup>();
            _banner.blocksRaycasts = false;
            _banner.interactable = false;
            _bannerText = UiFactory.Label(_bannerRt, "", 46, UiTheme.Text);
            var brt = (RectTransform)_bannerText.transform;
            brt.offsetMin = new Vector2(30, 10); brt.offsetMax = new Vector2(-30, -10);
            _bannerText.resizeTextForBestFit = true; _bannerText.resizeTextMinSize = 24; _bannerText.resizeTextMaxSize = 46;
            _bannerRt.gameObject.SetActive(false);
        }

        /// <summary>A rounded track with a fill that eases toward its value.</summary>
        class Bar
        {
            public readonly RectTransform Root;
            protected readonly RectTransform Fill;
            protected readonly Image FillImage;
            protected float Shown = 1f;
            const float MinVisible = 0.06f; // keep the rounded cap from collapsing

            public Bar(Transform parent, string name, Color color, Vector2 topLeftPos, Vector2 size)
            {
                var tl = new Vector2(0, 1);
                Root = UiFactory.Rect(name, parent, tl, tl, tl, topLeftPos, size);
                int radius = Mathf.RoundToInt(size.y * 0.5f);
                UiFactory.Panel(Root, UiTheme.Track, radius, shadow: false);
                Fill = UiFactory.Rect("Fill", Root, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
                Fill.offsetMin = new Vector2(3, 3); Fill.offsetMax = new Vector2(-3, -3);
                FillImage = UiFactory.Panel(Fill, color, Mathf.Max(2, radius - 3), shadow: false);
            }

            public void Set(float value, float dt, bool snap = false)
            {
                value = Mathf.Clamp01(value);
                Shown = snap ? value : Mathf.Lerp(Shown, value, 1f - Mathf.Exp(-10f * dt));
                if (Mathf.Abs(Shown - value) < 0.002f) Shown = value;
                FillImage.enabled = Shown > 0.004f;
                Fill.anchorMax = new Vector2(Mathf.Max(MinVisible, Shown), 1f);
            }
        }

        /// <summary>Icon badge + bar. Pulses when low so kids notice before it runs out.</summary>
        class Meter : Bar
        {
            readonly Color _color;
            readonly RectTransform _icon;
            bool _snapNext = true;

            public Meter(Transform parent, string name, Sprite icon, Color color, int row)
                : base(parent, name, color, new Vector2(92, -26 - row * 64), new Vector2(314, 36))
            {
                _color = color;
                var tl = new Vector2(0, 1);
                var badge = UiFactory.Rect(name + "Icon", parent, tl, tl, new Vector2(0.5f, 0.5f), new Vector2(52, -44 - row * 64), new Vector2(54, 54));
                UiFactory.Image(badge, color, icon, false);
                _icon = badge;
            }

            public void Snap() => _snapNext = true;

            public void Set(Stat s, float dt, bool dimmed)
            {
                float n = s.Normalized;
                Set(n, dt, _snapNext);
                _snapNext = false;
                bool low = n < 0.25f;
                float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
                Color c = dimmed ? Color.Lerp(_color, Color.gray, 0.6f) : _color;
                FillImage.color = Color.Lerp(c, Color.white, pulse * 0.6f);
                _icon.localScale = Vector3.one * (1f + 0.18f * pulse);
            }
        }
    }
}
