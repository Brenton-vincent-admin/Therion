using UnityEngine;

namespace Dino.Input
{
    /// <summary>
    /// One frame of "what the dino wants to do". Produced by touch controls, keyboard/gamepad,
    /// wild-dino AI or the network, and consumed by DinoMotor / DinoInteractor / DinoAttack.
    /// </summary>
    public struct DinoInputState
    {
        /// <summary>Move stick, -1..1. For players this is camera-relative; see MoveIsWorldSpace.</summary>
        public Vector2 Move;
        /// <summary>Camera look delta this frame (degrees-ish). Only used by the camera.</summary>
        public Vector2 Look;
        public bool Run;
        public bool Sneak;
        /// <summary>Pressed this frame.</summary>
        public bool Jump;
        /// <summary>Held: eat / drink / rest at nest (context action).</summary>
        public bool Action;
        /// <summary>Pressed this frame.</summary>
        public bool Attack;
        /// <summary>Pressed this frame: friendly call/roar (emote, no chat).</summary>
        public bool Call;
        /// <summary>True when Move is already in world XZ (AI, network), false = relative to camera.</summary>
        public bool MoveIsWorldSpace;

        public static DinoInputState Merge(DinoInputState a, DinoInputState b)
        {
            return new DinoInputState
            {
                Move = a.Move.sqrMagnitude >= b.Move.sqrMagnitude ? a.Move : b.Move,
                Look = a.Look + b.Look,
                Run = a.Run || b.Run,
                Sneak = a.Sneak || b.Sneak,
                Jump = a.Jump || b.Jump,
                Action = a.Action || b.Action,
                Attack = a.Attack || b.Attack,
                Call = a.Call || b.Call,
                MoveIsWorldSpace = a.MoveIsWorldSpace && b.MoveIsWorldSpace,
            };
        }
    }

    /// <summary>A source of player input (touch, keyboard/gamepad).</summary>
    public interface IDinoInputSource
    {
        /// <summary>Reads this frame's input. Called once per frame by PlayerInputRouter.</summary>
        DinoInputState Read();
    }
}
