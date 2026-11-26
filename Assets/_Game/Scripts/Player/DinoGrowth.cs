using Dino.Core;
using UnityEngine;

namespace Dino.Player
{
    /// <summary>
    /// Grows the dino through its stages while it is well fed (food and water above a quarter).
    /// Progress is play time in the current stage; saved and restored by the session.
    /// </summary>
    [RequireComponent(typeof(DinoActor))]
    public class DinoGrowth : MonoBehaviour
    {
        [Tooltip("Food and water must be above this fraction to grow.")]
        public float wellFedThreshold = 0.25f;
        [Tooltip("Debug/testing speed-up.")]
        public float speedMultiplier = 1f;

        /// <summary>Seconds of growth in the current stage.</summary>
        public float SecondsInStage { get; set; }
        public bool IsGrowing { get; private set; }

        DinoActor _actor;
        void Awake() => _actor = GetComponent<DinoActor>();

        public float StageDurationSeconds => _actor.Stage.minutesToGrow * 60f;
        /// <summary>0..1 progress toward the next stage (1 when adult).</summary>
        public float Progress01 => _actor.IsAdult || StageDurationSeconds <= 0f ? 1f : Mathf.Clamp01(SecondsInStage / StageDurationSeconds);

        void Update()
        {
            if (_actor.Species == null || _actor.IsAdult || _actor.Stats.IsFainted) { IsGrowing = false; return; }
            var s = _actor.Stats;
            IsGrowing = s.Food.Normalized > wellFedThreshold && s.Water.Normalized > wellFedThreshold;
            if (!IsGrowing) return;

            SecondsInStage += Time.deltaTime * speedMultiplier;
            if (StageDurationSeconds > 0f && SecondsInStage >= StageDurationSeconds) GrowUp();
        }

        public void GrowUp()
        {
            if (_actor.IsAdult) return;
            SecondsInStage = 0f;
            _actor.SetStage(_actor.StageIndex + 1);
            GameEvents.RaiseGrewUp(gameObject, _actor.StageIndex);
        }
    }
}
