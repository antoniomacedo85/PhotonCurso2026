using UnityEngine;

namespace Starter
{
    /// <summary>
    /// Centralized collection of animator parameter IDs used throughout the game.
    /// These IDs are pre-computed using StringToHash for efficient runtime performance.
    /// </summary>
    public static class AnimatorId
    {
        public static readonly int Speed        = Animator.StringToHash("Speed");
        public static readonly int SpeedX       = Animator.StringToHash("SpeedX");
        public static readonly int SpeedZ       = Animator.StringToHash("SpeedZ");
        public static readonly int MotionSpeed  = Animator.StringToHash("MotionSpeed");
        public static readonly int Grounded     = Animator.StringToHash("Grounded");
        public static readonly int Jump         = Animator.StringToHash("Jump");
        public static readonly int FreeFall     = Animator.StringToHash("FreeFall");
        public static readonly int Pitch        = Animator.StringToHash("Pitch");
        public static readonly int Shoot        = Animator.StringToHash("Shoot");
    }
}