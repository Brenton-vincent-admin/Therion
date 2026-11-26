using System;
using Dino.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Dino.UI
{
    /// <summary>
    /// Pause screen: keep playing, or pick a new dino (asks first, since the current dino is released).
    /// Built in code. The session decides what the buttons do.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public event Action ResumeClicked;
        public event Action NewDinoConfirmed;

        Canvas _canvas;
        GameObject _main, _confirm;
        Text _title, _info;

        void Awake() => Build();

        public bool IsOpen => _canvas && _canvas.gameObject.activeSelf;

        public void Open(string dinoName, GameMode mode, int level, int xp)
        {
            _canvas.gameObject.SetActive(true);
            _main.SetActive(true);
            _confirm.SetActive(false);
            _title.text = "Paused";
            string modeName = mode == GameMode.Survival ? "Survival" : mode == GameMode.Hardcore ? "Hardcore" : "Explorer";
            _info.text = $"{dinoName}   •   {modeName}   •   Level {level}  ({xp} XP)";
        }

        public void Close() => _canvas.gameObject.SetActive(false);

        void Build()
        {
            _canvas = UiFactory.CreateCanvas("PauseMenu", 30);
            _canvas.transform.SetParent(transform, false);
            var dim = UiFactory.Stretch("Dim", _canvas.transform);
            UiFactory.Image(dim, new Color(0f, 0f, 0f, 0.6f)); // also blocks touches to the game

            var c = new Vector2(0.5f, 0.5f);
            var card = UiFactory.Rect("Card", dim, c, c, c, Vector2.zero, new Vector2(900, 640));
            UiFactory.Panel(card, UiTheme.PanelSolid, 40);

            var top = new Vector2(0.5f, 1);
            var titleRt = UiFactory.Rect("Title", card, top, top, top, new Vector2(0, -36), new Vector2(800, 90));
            _title = UiFactory.Label(titleRt, "Paused", 64, UiTheme.Text);
            var infoRt = UiFactory.Rect("Info", card, top, top, top, new Vector2(0, -130), new Vector2(820, 50));
            _info = UiFactory.Label(infoRt, "", 28, UiTheme.TextDim);

            _main = UiFactory.Stretch("Main", card).gameObject;
            UiFactory.Button(_main.transform, "Resume", "Keep playing", c, new Vector2(0, 10), new Vector2(620, 150), UiTheme.Good, () => ResumeClicked?.Invoke());
            UiFactory.Button(_main.transform, "NewDino", "Pick a new dino", c, new Vector2(0, -170), new Vector2(620, 120), UiTheme.Neutral, () => { _main.SetActive(false); _confirm.SetActive(true); _title.text = "Pick a new dino?"; });

            _confirm = UiFactory.Stretch("Confirm", card).gameObject;
            var msgRt = UiFactory.Rect("Message", _confirm.transform, c, c, c, new Vector2(0, 40), new Vector2(780, 160));
            UiFactory.Label(msgRt, "Your dino goes back to the wild and you start as a new baby.\nYou keep all your XP!", 32, UiTheme.Text);
            UiFactory.Button(_confirm.transform, "Yes", "Yes, new dino", c, new Vector2(-210, -170), new Vector2(380, 130), UiTheme.Warn, () => NewDinoConfirmed?.Invoke());
            UiFactory.Button(_confirm.transform, "No", "No, go back", c, new Vector2(210, -170), new Vector2(380, 130), UiTheme.Good, () => { _confirm.SetActive(false); _main.SetActive(true); _title.text = "Paused"; });

            _canvas.gameObject.SetActive(false);
        }
    }
}
