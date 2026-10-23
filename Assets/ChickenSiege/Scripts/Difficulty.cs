using Fusion;
using UnityEngine;

namespace Starter
{
    /// <summary>
    /// CLASE 2 · Propiedad de sala "Dificultad".
    /// Se guarda como int en SessionProperties al crear la sala y la leen
    /// la lista de salas (para mostrarla) y el ChickenSpawner (para el juego).
    /// </summary>
    public static class Difficulty
    {
        public const string Key = "Dificultad";

        public static readonly string[] Names = { "Fácil", "Normal", "Difícil" };

        public const int Default = 1; // Normal

        /// <summary>Lee la dificultad de una sala. Si no tiene, devuelve Normal.</summary>
        public static int FromSession(SessionInfo session)
        {
            if (session != null && session.IsValid && session.Properties != null &&
                session.Properties.TryGetValue(Key, out var property) && property.IsInt)
            {
                return Mathf.Clamp((int)property, 0, Names.Length - 1);
            }

            return Default;
        }

        public static string Name(int difficulty)
        {
            return Names[Mathf.Clamp(difficulty, 0, Names.Length - 1)];
        }

        /// <summary>Velocidad de los pollos: Fácil más lentos, Difícil más rápidos.</summary>
        public static float SpeedMultiplier(int difficulty)
        {
            switch (difficulty)
            {
                case 0: return 0.7f;
                case 2: return 1.4f;
                default: return 1f;
            }
        }

        /// <summary>Vida de los pollos: en Difícil aguantan el doble.</summary>
        public static float HealthMultiplier(int difficulty)
        {
            switch (difficulty)
            {
                case 2: return 2f;
                default: return 1f;
            }
        }
    }
}
