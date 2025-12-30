namespace RuneRelic.Utils
{
    /// <summary>
    /// Game constants matching the server configuration.
    /// </summary>
    public static class Constants
    {
        // Server connection
        public const string DEFAULT_SERVER_URL = "ws://localhost:8080";
        public const string CLIENT_VERSION = "0.1.0";

        // Timing
        public const int TICK_RATE = 60;                    // Server tick rate (Hz)
        public const int STATE_UPDATE_RATE = 20;            // State updates per second
        public const float TICK_DURATION = 1f / TICK_RATE;  // ~16.67ms
        public const int MATCH_DURATION_TICKS = 10800;      // 180 seconds
        public const float MATCH_DURATION_SECONDS = 180f;

        // Arena
        public const float ARENA_WIDTH = 400f;
        public const float ARENA_HEIGHT = 400f;
        public const float ARENA_HALF_WIDTH = 200f;
        public const float ARENA_HALF_HEIGHT = 200f;

        // Interpolation
        public const float INTERPOLATION_DELAY_MS = 100f;   // 100ms buffer
        public const int STATE_BUFFER_SIZE = 3;             // Keep last 3 states

        // Input
        public const sbyte INPUT_NO_VALUE = -128;           // No joystick input
        public const sbyte INPUT_MAX = 127;
        public const sbyte INPUT_MIN = -127;

        // Forms (evolution tiers)
        public static readonly string[] FORM_NAMES = { "Spark", "Glyph", "Ward", "Arcane", "Ancient" };
        public static readonly float[] FORM_RADII = { 0.5f, 0.7f, 1.0f, 1.4f, 2.0f };
        public static readonly float[] FORM_SPEEDS = { 6.0f, 5.5f, 5.0f, 4.5f, 4.0f };
        public static readonly int[] FORM_THRESHOLDS = { 0, 100, 300, 600, 1000 };
        public static readonly int[] EVOLUTION_THRESHOLDS = { 0, 100, 300, 600, 1000 };
        public static readonly string[] ABILITY_NAMES = { "Dash", "Phase", "Shield", "Surge", "Dominate" };
        public static readonly float[] ABILITY_COOLDOWNS = { 3f, 4f, 5f, 6f, 8f };  // Cooldown in seconds per form

        // Rune types
        public static readonly string[] RUNE_NAMES = { "Wisdom", "Power", "Speed", "Shield", "Arcane", "Chaos" };
        public static readonly int[] RUNE_POINTS = { 10, 15, 12, 8, 25, 50 };

        // Shrine types
        public static readonly string[] SHRINE_NAMES = { "Wisdom", "Power", "Speed", "Shield" };
        public const float SHRINE_CHANNEL_TIME = 5f;        // 300 ticks
        public const float SHRINE_COOLDOWN = 60f;           // 3600 ticks
        public const float SHRINE_INTERACTION_RADIUS = 3f;

        // Gameplay
        public const int ELIMINATION_POINTS = 100;
        public const float ZONE_SHRINK_START_TICK = 1800;   // 30 seconds
        public const float RUNE_COLLISION_RADIUS = 0.3f;
    }

    /// <summary>
    /// Match modes matching server MatchMode enum.
    /// </summary>
    public enum MatchMode
    {
        Casual = 0,
        Ranked = 1,
        Private = 2,
        Practice = 3
    }

    /// <summary>
    /// Matchmaking status from server.
    /// </summary>
    public enum MatchmakingStatus
    {
        Searching,
        Found,
        Starting,
        Cancelled,
        Failed
    }

    /// <summary>
    /// Player form/evolution tier.
    /// </summary>
    public enum Form
    {
        Spark = 0,
        Glyph = 1,
        Ward = 2,
        Arcane = 3,
        Ancient = 4
    }

    /// <summary>
    /// Rune types.
    /// </summary>
    public enum RuneType
    {
        Wisdom = 0,
        Power = 1,
        Speed = 2,
        Shield = 3,
        Arcane = 4,
        Chaos = 5
    }

    /// <summary>
    /// Shrine types.
    /// </summary>
    public enum ShrineType
    {
        Wisdom = 0,
        Power = 1,
        Speed = 2,
        Shield = 3
    }

    /// <summary>
    /// Game state machine states.
    /// </summary>
    public enum GameState
    {
        Disconnected,
        Connecting,
        Connected,
        Authenticating,
        Authenticated,
        InQueue,
        MatchFound,
        ReadyCheck,
        Countdown,
        Playing,
        MatchEnded
    }
}
