using Dino.Player;
using Dino.Progression;
using Dino.Survival;
using UnityEngine;
using UnityEngine.UI;

namespace Dino.UI
{
    /// <summary>
    /// Kid-readable HUD built in code: four colored meters with big icons (top-left),
    /// level + XP bar and growth bar (top-center), and a message banner for big moments.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        Canvas _canvas;
        Meter _health, _stamina, _food, _water;
        Image _xpFill, _growFill;
        Text _levelText, _stageText, _banner;
        float _bannerTime;
        DinoActor _actor;

        public System.Func<int> GetXp;

        void Awake() => Build();

        public void Bind(DinoActor actor) { _actor = actor; }
        public void SetVisible(bool v) { if (_canvas) _canvas.gameObject.SetActive(v); }

        public void ShowBanner(string text, float seconds = 3f)
        {
            _banner.text = text;
            _banner.transform.parent.gameObject.SetActive(true);
            _bannerTime = seconds;
        }

        void Update()
        {
            if (_bannerTime > 0f)
            {
                _bannerTime -= Time.unscaledDeltaTime;
                if (_bannerTime <= 0f) _banner.transform.parent.gameObject.SetActive(false);
            }

            int xp = GetXp != null ? GetXp() : 0;
            _levelText.text = $"Level {XpCurve.LevelForXp(xp)}";
            _xpFill.fillAmount = XpCurve.ProgressToNext(xp);

            bool has = _actor != null && _actor.Species != null;
            _health.Root.gameObject.SetActive(has);
            _stamina.Root.gameObject.SetActive(has);
            _food.Root.gameObject.SetActive(has);
            _water.Root.gameObject.SetActive(has);
            _growFill.transform.parent.gameObject.SetActive(has);
            if (!has) return;

            var s = _actor.Stats;
            _health.Set(s.Health);
            _stamina.Set(s.Stamina);
            _food.Set(s.Food);
            _water.Set(s.Water);
            var growth = _actor.GetComponent<DinoGrowth>();
            _growFill.fillAmount = growth ? growth.Progress01 : 1f;
            _stageText.text = $"{_actor.Species.displayName} - {_actor.Stage.name}";
        }

        void Build()
        {
            _canvas = UiFactory.CreateCanvas("HUD", 5);
            _canvas.transform.SetParent(transform, false);
            var root = _canvas.transform;
            Vector2 tl = new Vector2(0, 1);

            _health = new Meter(root, "Health", "♥", new Color(0.9f, 0.25f, 0.3f), new Vector2(40, -40));
            _stamina = new Meter(root, "Stamina", "⚡", new Color(0.95f, 0.8f, 0.2f), new Vector2(40, -120));
            _food = new Meter(root, "Food", "❀", new Color(0.35f, 0.75f, 0.3f), new Vector2(40, -200));
            _water = new Meter(root, "Water", "☔", new Color(0.25f, 0.55f, 0.95f), new Vector2(40, -280));

            // Level + XP (top center)
            var top = new Vector2(0.5f, 1);
            var xpBg = UiFactory.Rect("XpBar", root, top, top, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(520, 34));
            UiFactory.Image(xpBg, new Color(0, 0, 0, 0.45f), null, false);
            var xpFillRt = UiFactory.Stretch("Fill", xpBg);
            _xpFill = UiFactory.Image(xpFillRt, new Color(0.7f, 0.5f, 1f), null, false);
            MakeFilled(_xpFill);
            _levelText = UiFactory.Label(xpBg, "Level 1", 26, Color.white);

            var growBg = UiFactory.Rect("GrowBar", root, top, top, new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(420, 26));
            UiFactory.Image(growBg, new Color(0, 0, 0, 0.45f), null, false);
            _growFill = UiFactory.Image(UiFactory.Stretch("Fill", growBg), new Color(1f, 0.65f, 0.25f), null, false);
            MakeFilled(_growFill);
            _stageText = UiFactory.Label(growBg, "", 20, Color.white);

            // Banner
            var bannerRt = UiFactory.Rect("Banner", root, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 120));
            UiFactory.Image(bannerRt, new Color(0, 0, 0, 0.55f), null, false);
            _banner = UiFactory.Label(bannerRt, "", 46, Color.white);
            bannerRt.gameObject.SetActive(false);
        }

        static void MakeFilled(Image img)
        {
            img.sprite = WhiteSprite;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = 1f;
        }

        static Sprite _white;
        static Sprite WhiteSprite
        {
            get
            {
                if (_white) return _white;
                var t = Texture2D.whiteTexture;
                _white = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
                return _white;
            }
        }

        class Meter
        {
            public RectTransform Root;
            readonly Image _fill;
            readonly Color _color;

            public Meter(Transform parent, string name, string icon, Color color, Vector2 pos)
            {
                _color = color;
                var tl = new Vector2(0, 1);
                Root = UiFactory.Rect(name, parent, tl, tl, new Vector2(0, 1), pos, new Vector2(420, 64));
                var iconRt = UiFactory.Rect("Icon", Root, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(64, 64));
                UiFactory.Image(iconRt, color, UiFactory.Circle, false);
                UiFactory.Label(iconRt, icon, 38, Color.white);
                var bg = UiFactory.Rect("Bar", Root, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(76, 0), new Vector2(330, 34));
                UiFactory.Image(bg, new Color(0, 0, 0, 0.45f), null, false);
                _fill = UiFactory.Image(UiFactory.Stretch("Fill", bg), color, null, false);
                MakeFilled(_fill);
            }

            public void Set(Stat s)
            {
                float n = s.Normalized;
                _fill.fillAmount = n;
                // Flash when low so kids notice.
                bool low = n < 0.2f;
                float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f) : 1f;
                _fill.color = Color.Lerp(Color.white, _color, pulse);
            }
        }
    }
}
