using Dino.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dino.Input
{
    /// <summary>
    /// On-screen phone controls, built in code:
    ///  - left half: floating move stick (touch anywhere, drag). Pushing to the edge for a moment starts running.
    ///  - right half: drag to turn the camera
    ///  - big round buttons bottom-right: Eat/Drink (hold), Bite, Jump, Power (species ability), Sneak (toggle), Run (toggle), Roar
    /// Big targets, icons + one-word labels, no menus: designed for a pre-reader.
    /// </summary>
    public class TouchControls : MonoBehaviour, IDinoInputSource
    {
        public float lookSensitivity = 0.12f;
        [Tooltip("Show on desktop too (for testing). Always shown on phones.")]
        public bool showInEditor = true;

        Canvas _canvas;
        StickZone _stick;
        LookZone _look;
        HoldButton _action, _attack, _jump, _call, _ability;
        ToggleButton _run, _sneak;
        Text _actionLabel;
        float _edgeTime;

        public string ActionLabel { set { if (_actionLabel) _actionLabel.text = value; } }
        public bool ActionAvailable { set { if (_action) _action.SetHighlight(value); } }

        void Awake()
        {
            bool show = Application.isMobilePlatform || (Application.isEditor && showInEditor);
            Build();
            _canvas.gameObject.SetActive(show);
        }

        void OnEnable() { if (PlayerInputRouter.Instance) PlayerInputRouter.Instance.AddSource(this); }
        void Start() { if (PlayerInputRouter.Instance) PlayerInputRouter.Instance.AddSource(this); }
        void OnDisable() { if (PlayerInputRouter.Instance) PlayerInputRouter.Instance.RemoveSource(this); }

        public void SetVisible(bool visible) { if (_canvas) _canvas.gameObject.SetActive(visible); }

        public DinoInputState Read()
        {
            var s = new DinoInputState();
            if (_canvas == null || !_canvas.gameObject.activeInHierarchy) return s;
            s.Move = _stick.Value;
            s.Look = _look.ConsumeDelta() * lookSensitivity;

            // Auto-run: stick held at the edge for 0.4s.
            if (s.Move.magnitude > 0.95f) _edgeTime += Time.deltaTime; else _edgeTime = 0f;
            if (s.Move.sqrMagnitude < 0.01f && _run.On) _run.Set(false);
            s.Run = _run.On || _edgeTime > 0.4f;
            s.Sneak = _sneak.On && !s.Run;
            s.Action = _action.Held;
            s.Attack = _attack.ConsumePress();
            s.Jump = _jump.ConsumePress();
            s.Call = _call.ConsumePress();
            s.Ability = _ability.ConsumePress();
            return s;
        }

        void Build()
        {
            _canvas = UiFactory.CreateCanvas("TouchControls", 10);
            _canvas.transform.SetParent(transform, false);
            var root = _canvas.transform;

            // Look zone first (behind everything), right 55% of the screen.
            var lookRt = UiFactory.Rect("LookZone", root, new Vector2(0.45f, 0f), Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UiFactory.Image(lookRt, new Color(0, 0, 0, 0));
            _look = lookRt.gameObject.AddComponent<LookZone>();

            // Move stick zone: left 45%, bottom 75%.
            var stickRt = UiFactory.Rect("StickZone", root, Vector2.zero, new Vector2(0.45f, 0.75f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UiFactory.Image(stickRt, new Color(0, 0, 0, 0));
            _stick = stickRt.gameObject.AddComponent<StickZone>();
            _stick.Build(new Vector2(320, 300));

            // Buttons, bottom-right cluster.
            Vector2 br = new Vector2(1, 0);
            _action = MakeHold(root, "Action", "Eat", br, new Vector2(-200, 200), 230, new Color(0.3f, 0.75f, 0.3f, 0.85f), out _actionLabel);
            _attack = MakeHold(root, "Bite", "Bite", br, new Vector2(-430, 150), 150, new Color(0.85f, 0.35f, 0.3f, 0.85f), out _);
            _jump = MakeHold(root, "Jump", "Jump", br, new Vector2(-180, 440), 150, new Color(0.3f, 0.55f, 0.9f, 0.85f), out _);
            _call = MakeHold(root, "Call", "Roar", br, new Vector2(-400, 360), 120, new Color(0.7f, 0.5f, 0.9f, 0.85f), out _);
            _ability = MakeHold(root, "Ability", "Power", br, new Vector2(-620, 230), 140, new Color(0.95f, 0.55f, 0.15f, 0.85f), out _);
            _run = MakeToggle(root, "Run", "Run", new Vector2(0, 0), new Vector2(150, 520), 140, new Color(0.95f, 0.75f, 0.2f, 0.85f));
            _sneak = MakeToggle(root, "Sneak", "Sneak", new Vector2(0, 0), new Vector2(320, 560), 120, new Color(0.4f, 0.4f, 0.45f, 0.85f));
        }

        static HoldButton MakeHold(Transform root, string name, string label, Vector2 anchor, Vector2 pos, float size, Color c, out Text text)
        {
            var rt = UiFactory.Rect(name, root, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var img = UiFactory.Image(rt, c, UiFactory.Circle);
            text = UiFactory.Label(rt, label, Mathf.RoundToInt(size * 0.2f), Color.white);
            var b = rt.gameObject.AddComponent<HoldButton>();
            b.Init(img);
            return b;
        }

        static ToggleButton MakeToggle(Transform root, string name, string label, Vector2 anchor, Vector2 pos, float size, Color c)
        {
            var rt = UiFactory.Rect(name, root, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var img = UiFactory.Image(rt, c, UiFactory.Circle);
            UiFactory.Label(rt, label, Mathf.RoundToInt(size * 0.2f), Color.white);
            var b = rt.gameObject.AddComponent<ToggleButton>();
            b.Init(img);
            return b;
        }

        // ---------- widgets ----------

        public class StickZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            public Vector2 Value { get; private set; }
            RectTransform _base, _knob, _zone;
            Vector2 _restPos;
            int _pointer = -99;
            const float Radius = 130f;

            public void Build(Vector2 restPos)
            {
                _zone = (RectTransform)transform;
                _restPos = restPos;
                _base = UiFactory.Rect("StickBase", transform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), restPos, new Vector2(Radius * 2, Radius * 2));
                UiFactory.Image(_base, new Color(1, 1, 1, 0.18f), UiFactory.Circle, false);
                _knob = UiFactory.Rect("Knob", _base, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120));
                UiFactory.Image(_knob, new Color(1, 1, 1, 0.55f), UiFactory.Circle, false);
            }

            Vector2 Local(PointerEventData e)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_zone, e.position, e.pressEventCamera, out var lp);
                return lp - _zone.rect.min; // bottom-left origin to match the base's anchor
            }

            public void OnPointerDown(PointerEventData e)
            {
                if (_pointer != -99) return;
                _pointer = e.pointerId;
                _base.anchoredPosition = Local(e);
                OnDrag(e);
            }

            public void OnDrag(PointerEventData e)
            {
                if (e.pointerId != _pointer) return;
                Vector2 d = Local(e) - _base.anchoredPosition;
                d = Vector2.ClampMagnitude(d, Radius);
                _knob.anchoredPosition = d;
                Vector2 v = d / Radius;
                Value = v.magnitude < 0.12f ? Vector2.zero : v; // dead zone
            }

            public void OnPointerUp(PointerEventData e)
            {
                if (e.pointerId != _pointer) return;
                _pointer = -99;
                Value = Vector2.zero;
                _knob.anchoredPosition = Vector2.zero;
                _base.anchoredPosition = _restPos;
            }

            void OnDisable() { _pointer = -99; Value = Vector2.zero; }
        }

        public class LookZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            Vector2 _delta;
            int _pointer = -99;
            public Vector2 ConsumeDelta() { var d = _delta; _delta = Vector2.zero; return d; }
            public void OnPointerDown(PointerEventData e) { if (_pointer == -99) _pointer = e.pointerId; }
            public void OnDrag(PointerEventData e) { if (e.pointerId == _pointer) _delta += e.delta; }
            public void OnPointerUp(PointerEventData e) { if (e.pointerId == _pointer) _pointer = -99; }
            void OnDisable() { _pointer = -99; _delta = Vector2.zero; }
        }

        public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public bool Held { get; private set; }
            bool _pressed;
            Image _img; Color _color;
            public void Init(Image img) { _img = img; _color = img.color; }
            public bool ConsumePress() { bool p = _pressed; _pressed = false; return p; }
            public void SetHighlight(bool on) { if (_img) _img.color = on || Held ? _color : new Color(_color.r, _color.g, _color.b, _color.a * 0.45f); }
            public void OnPointerDown(PointerEventData e) { Held = true; _pressed = true; transform.localScale = Vector3.one * 0.92f; }
            public void OnPointerUp(PointerEventData e) { Held = false; transform.localScale = Vector3.one; }
            public void OnPointerExit(PointerEventData e) { Held = false; transform.localScale = Vector3.one; }
            void OnDisable() { Held = false; _pressed = false; }
        }

        public class ToggleButton : MonoBehaviour, IPointerDownHandler
        {
            public bool On { get; private set; }
            Image _img; Color _color;
            public void Init(Image img) { _img = img; _color = img.color; Refresh(); }
            public void Set(bool on) { On = on; Refresh(); }
            public void OnPointerDown(PointerEventData e) => Set(!On);
            void Refresh() { if (_img) _img.color = On ? Color.Lerp(_color, Color.white, 0.35f) : new Color(_color.r, _color.g, _color.b, _color.a * 0.6f); transform.localScale = Vector3.one * (On ? 1.08f : 1f); }
        }
    }
}
