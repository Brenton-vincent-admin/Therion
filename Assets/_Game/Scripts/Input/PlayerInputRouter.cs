using System.Collections.Generic;
using UnityEngine;

namespace Dino.Input
{
    /// <summary>
    /// Merges all player input sources (touch + keyboard/gamepad) into one DinoInputState per frame.
    /// Other scripts read PlayerInputRouter.Current.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerInputRouter : MonoBehaviour
    {
        public static PlayerInputRouter Instance { get; private set; }
        public DinoInputState Current { get; private set; }
        /// <summary>When false (menus open), Current is empty.</summary>
        public bool InputEnabled = true;

        readonly List<IDinoInputSource> _sources = new List<IDinoInputSource>();

        void Awake()
        {
            Instance = this;
            _sources.Add(new KeyboardGamepadInput());
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void AddSource(IDinoInputSource source) { if (!_sources.Contains(source)) _sources.Add(source); }
        public void RemoveSource(IDinoInputSource source) => _sources.Remove(source);

        void Update()
        {
            var merged = new DinoInputState();
            foreach (var s in _sources) merged = DinoInputState.Merge(merged, s.Read());
            Current = InputEnabled ? merged : default;
        }
    }
}
