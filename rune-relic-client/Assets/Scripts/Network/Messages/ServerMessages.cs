using System;
using System.Collections.Generic;
using RuneRelic.Utils;

namespace RuneRelic.Network.Messages
{
    /// <summary>
    /// Base class for all server messages. Uses tagged JSON format.
    /// </summary>
    [Serializable]
    public class ServerMessage
    {
        public string type;
    }

    // =========================================================================
    // Authentication
    // =========================================================================

    [Serializable]
    public class AuthResult
    {
        public bool success;
        public string session_id;
        public string error;
        public string server_version;
    }

    // =========================================================================
    // Matchmaking
    // =========================================================================

    [Serializable]
    public class MatchmakingResponse
    {
        public string status;  // "searching", "found", "starting", "cancelled", "failed"
        public int? estimated_wait;
        public int players_found;
        public int players_needed;
    }

    [Serializable]
    public class MatchFoundInfo
    {
        public byte[] match_id;
        public List<byte[]> player_ids;
        public string mode;
        public int ready_timeout;
    }

    [Serializable]
    public class MatchStartInfo
    {
        public byte[] match_id;
        public ulong rng_seed;
        public int start_tick;
        public List<InitialPlayerInfo> players;
        public byte[] config_hash;
        public byte[] block_hash;
    }

    [Serializable]
    public class InitialPlayerInfo
    {
        public byte[] player_id;
        public int[] position;  // Fixed-point [x, y]
        public int color_index;
    }

    // =========================================================================
    // Game State Updates
    // =========================================================================

    [Serializable]
    public class GameStateUpdate
    {
        public uint tick;
        public uint time_remaining;
        public List<PlayerStateUpdate> players;
        public List<RuneUpdate> runes;
        public List<ShrineUpdate> shrines;
        public byte[] state_hash;
    }

    [Serializable]
    public class PlayerStateUpdate
    {
        public byte[] player_id;
        public int[] position;   // Fixed-point [x, y]
        public int[] velocity;   // Fixed-point [x, y]
        public int form;
        public uint score;
        public bool alive;
        public int spawn_zone_id;
        public bool spawn_zone_active;
        public int radius;       // Fixed-point
        public int ability_cooldown;
        public PlayerBuffs buffs;
    }

    [Serializable]
    public class PlayerBuffs
    {
        public uint speed;
        public uint shield;
        public uint invulnerable;
        public List<int> shrine_buffs;
    }

    [Serializable]
    public class RuneUpdate
    {
        public uint id;
        public int rune_type;
        public int[] position;  // Fixed-point [x, y]
        public bool collected;
    }

    [Serializable]
    public class ShrineUpdate
    {
        public uint id;
        public int shrine_type;
        public int[] position;  // Fixed-point [x, y]
        public bool active;
        public byte[] controller;  // Nullable player_id
        public byte[] controller_id;  // Alias for controller
        public float channel_progress;  // 0-1 progress of channeling
    }

    // =========================================================================
    // Game Events
    // =========================================================================

    [Serializable]
    public class MatchEvent
    {
        public string type;  // Event type discriminator
        public string @event;  // Server event tag
        public string event_type;  // Alternate event tag

        // RuneCollected
        public uint tick;
        public byte[] player_id;
        public uint rune_id;
        public int rune_type;
        public uint points;
        public int[] position;

        // PlayerEvolved
        public int old_form;
        public int new_form;

        // PlayerEliminated
        public byte[] victim_id;
        public byte[] killer_id;
        public int victim_form;

        // AbilityUsed
        public int ability_type;

        // ShrineCaptured / ShrinePowerActivated
        public uint shrine_id;
        public int shrine_type;

        // Countdown
        public uint seconds;
    }

    // =========================================================================
    // Match End
    // =========================================================================

    [Serializable]
    public class MatchEndInfo
    {
        public byte[] match_id;
        public uint end_tick;
        public byte[] winner_id;  // Nullable
        public List<PlayerPlacement> placements;
        public byte[] final_state_hash;
        public byte[] transcript;  // Nullable, for ranked
    }

    [Serializable]
    public class PlayerPlacement
    {
        public byte[] player_id;
        public int place;
        public uint score;
        public uint final_score;  // Alias for score
        public int final_form;
        public uint eliminations;
        public uint runes_collected;
        public uint survival_ticks;
        public uint damage_dealt;
    }

    // =========================================================================
    // Input Acknowledgment
    // =========================================================================

    [Serializable]
    public class InputAck
    {
        public uint tick;
        public uint server_tick;
    }

    // =========================================================================
    // Latency / Ping
    // =========================================================================

    [Serializable]
    public class Pong
    {
        public ulong timestamp;
        public ulong server_time;
    }

    // =========================================================================
    // Errors
    // =========================================================================

    [Serializable]
    public class ServerError
    {
        public string code;
        public string message;
    }

    [Serializable]
    public class Shutdown
    {
        public string reason;
    }
}
