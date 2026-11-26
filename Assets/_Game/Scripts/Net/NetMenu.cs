using System;
using Dino.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dino.Net
{
    /// <summary>
    /// The "Friends" button (top right) and the play-together panel: host on Wi-Fi, host online
    /// (behind a 3-second grown-up hold), join a nearby game with one tap, or type a code.
    /// Built in code like the rest of the UI. Creates itself when the game starts.
    /// </summary>
    public class NetMenu : MonoBehaviour
    {
        NetSession _net;
        Canvas _canvas;
        RectTransform _panel, _content, _toastRoot;
        Text _buttonLabel, _toast;
        float _toastTime;
        bool _open, _dirty;
        string _typedCode = "";
        int _seenNearby = -1;

        readonly Button[] _nearbyButtons = new Button[3];
        readonly Text[] _nearbyLabels = new Text[3];
        readonly string[] _nearbyAddress = new string[3];
        Text _nearbyHint;

        static readonly Color PanelColor = new Color(0.08f, 0.14f, 0.12f, 0.97f);
        static readonly Color Green = new Color(0.3f, 0.65f, 0.3f);
        static readonly Color Blue = new Color(0.25f, 0.5f, 0.8f);
        static readonly Color Purple = new Color(0.55f, 0.4f, 0.8f);
        static readonly Color Red = new Color(0.7f, 0.3f, 0.3f);
        static readonly Color Grey = new Color(0.35f, 0.38f, 0.4f);
        static readonly Color Yellow = new Color(1f, 0.9f, 0.45f);
        static readonly Vector2 Top = new Vector2(0.5f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<NetMenu>() != null) return;
            var go = new GameObject("PlayTogether");
            DontDestroyOnLoad(go);
            go.AddComponent<NetSession>();
            go.AddComponent<NetMenu>();
        }

        void Awake()
        {
            _net = GetComponent<NetSession>();
            if (_net == null) _net = gameObject.AddComponent<NetSession>();
            Build();
            _net.Changed += OnNetChanged;
            _net.Toast += ShowToast;
        }

        void OnDestroy()
        {
            if (_net == null) return;
            _net.Changed -= OnNetChanged;
            _net.Toast -= ShowToast;
        }

        void OnNetChanged() => _dirty = true;

        void Build()
        {
            _canvas = UiFactory.CreateCanvas("PlayTogether", 15);
            _canvas.transform.SetParent(transform, false);
            var root = _canvas.transform;

            var open = UiFactory.Button(root, "FriendsButton", "Friends", new Vector2(1, 1), new Vector2(-275, -80),
                new Vector2(250, 84), Purple, Toggle); // sits left of the pause button
            _buttonLabel = open.GetComponentInChildren<Text>();

            _toastRoot = UiFactory.Rect("Toast", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-28, -150), new Vector2(760, 64));
            UiFactory.Image(_toastRoot, new Color(0, 0, 0, 0.6f), null, false);
            _toast = UiFactory.Label(_toastRoot, "", 28, Color.white);
            _toastRoot.gameObject.SetActive(false);

            _panel = UiFactory.Rect("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1280, 920));
            UiFactory.Image(_panel, PanelColor);
            _content = UiFactory.Stretch("Content", _panel);
            _panel.gameObject.SetActive(false);
        }

        void Toggle()
        {
            _open = !_open;
            _panel.gameObject.SetActive(_open);
            if (_open) Rebuild();
            _net.SetLookingForGames(_open && _net.State == NetState.Offline);
        }

        void Close() { if (_open) Toggle(); }

        void ShowToast(string text)
        {
            _toast.text = text;
            _toastRoot.gameObject.SetActive(true);
            _toastTime = 4f;
        }

        void Update()
        {
            if (_toastTime > 0f)
            {
                _toastTime -= Time.unscaledDeltaTime;
                if (_toastTime <= 0f) _toastRoot.gameObject.SetActive(false);
            }

            _buttonLabel.text = _net.IsActive ? $"Friends ({_net.PlayerCount})" : "Friends";

            if (_dirty)
            {
                _dirty = false;
                _net.SetLookingForGames(_open && _net.State == NetState.Offline);
                if (_open) Rebuild();
            }
            if (_open && _seenNearby != _net.NearbyVersion) RefreshNearby();
        }

        // ------------------------------------------------------------------ panel contents

        void Rebuild()
        {
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            for (int i = 0; i < _nearbyButtons.Length; i++) { _nearbyButtons[i] = null; _nearbyLabels[i] = null; }
            _nearbyHint = null;

            switch (_net.State)
            {
                case NetState.Offline: BuildOffline(); break;
                case NetState.Hosting: BuildHosting(); break;
                case NetState.Joined: BuildJoined(); break;
                default: BuildBusy(); break;
            }
        }

        void BuildOffline()
        {
            Line("Play together", -70, 60, Color.white);
            Line($"You are: {DinoNames.Local}", -150, 36, Color.white);
            Btn("New name", new Vector2(450, -150), new Vector2(230, 64), Grey, () => { DinoNames.Reroll(); Rebuild(); }, 28);

            Btn("Host on home Wi-Fi", new Vector2(-305, -265), new Vector2(570, 120), Green, () => _net.HostWifi());
            var online = Btn("Host online\n(grown-ups: hold 3 seconds)", new Vector2(305, -265), new Vector2(570, 120), Blue, null, 30);
            var fillRt = UiFactory.Rect("Fill", online.transform, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 12));
            var fill = UiFactory.Image(fillRt, Color.white, null, false);
            online.gameObject.AddComponent<HoldToConfirm>().Init(fill, 3f, () => _net.HostOnline());

            Line("Games on this Wi-Fi:", -375, 32, Color.white);
            _nearbyHint = Line("Looking for games...", -455, 28, new Color(1, 1, 1, 0.6f));
            for (int i = 0; i < _nearbyButtons.Length; i++)
            {
                int index = i;
                var b = Btn("", new Vector2(-400 + i * 400, -455), new Vector2(380, 96), Green, () => JoinNearby(index), 28);
                _nearbyButtons[i] = b;
                _nearbyLabels[i] = b.GetComponentInChildren<Text>();
                b.gameObject.SetActive(false);
            }
            RefreshNearby();

            Line("Or type a code from a friend:", -560, 32, Color.white);
            var input = MakeInput(new Vector2(-170, -650), new Vector2(560, 104));
            Btn("Join", new Vector2(300, -650), new Vector2(300, 104), Green, () => _net.Join(input.text));

            Line(_net.Status, -755, 32, Yellow);
            Btn("Back to playing", new Vector2(0, -850), new Vector2(420, 90), Grey, Close);
        }

        void BuildBusy()
        {
            Line("Play together", -70, 60, Color.white);
            Line(_net.Status, -380, 44, Yellow);
            Btn("Cancel", new Vector2(0, -850), new Vector2(420, 90), Grey, () => _net.Leave());
        }

        void BuildHosting()
        {
            Line("Your game is open!", -70, 60, Color.white);
            if (!string.IsNullOrEmpty(_net.OnlineCode))
            {
                Line("Friend code", -180, 36, Color.white);
                Line(Spaced(_net.OnlineCode), -290, 120, Yellow);
                Line("Friends tap Friends, then type this code.", -400, 30, Color.white);
            }
            else
            {
                Line("Wi-Fi code", -180, 36, Color.white);
                Line(Spaced(_net.WifiCode), -290, 120, Yellow);
                Line("Friends on the same Wi-Fi tap Friends, then your name or this code.", -400, 30, Color.white);
            }
            Line($"Dinos playing: {_net.PlayerCount}", -490, 40, Color.white);

            bool bite = _net.AllowPlayerDamage;
            Btn(bite ? "Friends can bite each other: ON" : "Friends can bite each other: OFF", new Vector2(0, -600),
                new Vector2(760, 96), bite ? Red : Grey, () => _net.SetAllowPlayerDamage(!_net.AllowPlayerDamage), 32);

            Btn("Close the game", new Vector2(-240, -850), new Vector2(420, 90), Red, () => _net.Leave());
            Btn("Back to playing", new Vector2(240, -850), new Vector2(420, 90), Grey, Close);
        }

        void BuildJoined()
        {
            Line("You're playing with friends!", -70, 60, Color.white);
            Line($"Dinos playing: {_net.PlayerCount}", -300, 48, Color.white);
            Line(_net.AllowPlayerDamage ? "In this game friends can bite each other." : "In this game friends can't hurt each other.", -400, 32, Color.white);
            Btn("Leave the game", new Vector2(-240, -850), new Vector2(420, 90), Red, () => _net.Leave());
            Btn("Back to playing", new Vector2(240, -850), new Vector2(420, 90), Grey, Close);
        }

        void RefreshNearby()
        {
            _seenNearby = _net.NearbyVersion;
            if (_nearbyButtons[0] == null) return;
            var games = _net.Beacon.Games;
            for (int i = 0; i < _nearbyButtons.Length; i++)
            {
                bool has = i < games.Count;
                _nearbyButtons[i].gameObject.SetActive(has);
                _nearbyAddress[i] = has ? games[i].Address : null;
                if (has) _nearbyLabels[i].text = $"{games[i].HostName}\n({games[i].Players} playing)";
            }
            if (_nearbyHint != null) _nearbyHint.gameObject.SetActive(games.Count == 0);
        }

        void JoinNearby(int index)
        {
            string address = _nearbyAddress[index];
            if (!string.IsNullOrEmpty(address)) _net.JoinWifi(address);
        }

        // ------------------------------------------------------------------ little builders

        static string Spaced(string code) => string.IsNullOrEmpty(code) ? "" : string.Join(" ", code.ToCharArray());

        Text Line(string text, float y, int size, Color color)
        {
            var rt = UiFactory.Rect("Line", _content, Top, Top, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(1200, size * 1.4f));
            return UiFactory.Label(rt, text ?? "", size, color);
        }

        Button Btn(string text, Vector2 pos, Vector2 size, Color color, Action onClick, int fontSize = 36)
        {
            var b = UiFactory.Button(_content, text, text, Top, pos, size, color, onClick);
            var label = b.GetComponentInChildren<Text>();
            if (label != null) label.fontSize = fontSize;
            return b;
        }

        InputField MakeInput(Vector2 pos, Vector2 size)
        {
            var rt = UiFactory.Rect("CodeInput", _content, Top, Top, new Vector2(0.5f, 0.5f), pos, size);
            var bg = UiFactory.Image(rt, Color.white);
            var input = rt.gameObject.AddComponent<InputField>();
            input.targetGraphic = bg;
            var text = UiFactory.Label(rt, "", 60, Color.black);
            text.supportRichText = false;
            var hint = UiFactory.Label(rt, "CODE", 60, new Color(0, 0, 0, 0.3f));
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = LanCode.Length;
            input.characterValidation = InputField.CharacterValidation.Alphanumeric;
            input.lineType = InputField.LineType.SingleLine;
            input.text = _typedCode;
            input.onValueChanged.AddListener(v =>
            {
                string up = (v ?? "").ToUpperInvariant();
                _typedCode = up;
                if (up != v) input.text = up;
            });
            return input;
        }
    }

    /// <summary>The grown-up gate: the action only happens after holding the button for a few seconds.</summary>
    public class HoldToConfirm : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        Image _fill;
        float _seconds = 3f, _held;
        bool _down, _fired;
        Action _onConfirm;

        public void Init(Image fill, float seconds, Action onConfirm)
        {
            _fill = fill; _seconds = Mathf.Max(0.1f, seconds); _onConfirm = onConfirm;
            SetFill(0f);
        }

        public void OnPointerDown(PointerEventData e) { _down = true; _held = 0f; _fired = false; }
        public void OnPointerUp(PointerEventData e) { _down = false; SetFill(0f); }
        public void OnPointerExit(PointerEventData e) { _down = false; SetFill(0f); }

        void Update()
        {
            if (!_down || _fired) return;
            _held += Time.unscaledDeltaTime;
            SetFill(_held / _seconds);
            if (_held < _seconds) return;
            _fired = true; _down = false;
            SetFill(0f);
            _onConfirm?.Invoke();
        }

        void SetFill(float t)
        {
            if (_fill == null) return;
            var rt = _fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(t), 0f);
        }
    }
}
