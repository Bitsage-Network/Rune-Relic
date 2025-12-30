using UnityEngine;

namespace RuneRelic.Utils
{
    /// <summary>
    /// Utility for converting Q16.16 fixed-point coordinates from server to Unity floats.
    /// Server uses 32-bit signed integers with 16 fractional bits.
    /// </summary>
    public static class FixedPoint
    {
        /// <summary>
        /// Fixed-point scale factor (2^16 = 65536).
        /// </summary>
        public const float SCALE = 65536f;

        /// <summary>
        /// One in fixed-point representation.
        /// </summary>
        public const int FIXED_ONE = 65536;

        /// <summary>
        /// Convert fixed-point value to float.
        /// </summary>
        public static float ToFloat(int fixedValue)
        {
            return fixedValue / SCALE;
        }

        /// <summary>
        /// Convert float to fixed-point value.
        /// </summary>
        public static int ToFixed(float value)
        {
            return (int)(value * SCALE);
        }

        /// <summary>
        /// Convert fixed-point [x, y] position to Unity Vector3 (Y=0 for ground plane).
        /// Server uses X/Y for horizontal plane, Unity uses X/Z.
        /// </summary>
        public static Vector3 ToVector3(int[] position)
        {
            if (position == null || position.Length < 2)
                return Vector3.zero;

            return new Vector3(
                ToFloat(position[0]),  // Server X -> Unity X
                0f,                     // Ground plane
                ToFloat(position[1])   // Server Y -> Unity Z
            );
        }

        /// <summary>
        /// Convert fixed-point [x, y] velocity to Unity Vector3.
        /// </summary>
        public static Vector3 VelocityToVector3(int[] velocity)
        {
            if (velocity == null || velocity.Length < 2)
                return Vector3.zero;

            return new Vector3(
                ToFloat(velocity[0]),
                0f,
                ToFloat(velocity[1])
            );
        }

        /// <summary>
        /// Convert Unity Vector3 position to fixed-point [x, y].
        /// </summary>
        public static int[] FromVector3(Vector3 position)
        {
            return new int[]
            {
                ToFixed(position.x),
                ToFixed(position.z)  // Unity Z -> Server Y
            };
        }
    }
}
