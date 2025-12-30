using System;
using RuneRelic.Utils;

namespace RuneRelic.Network.Messages
{
    /// <summary>
    /// Base class for client-to-server messages.
    /// Serialized as tagged JSON with "type" field.
    /// </summary>
    [Serializable]
    public abstract class ClientMessage
    {
        public string type;
    }

    // =========================================================================
    // Authentication
    // =========================================================================

    [Serializable]
    public class AuthRequest : ClientMessage
    {
        public string player_id;  // Hex string for JSON compatibility
        public string token;
        public string client_version;

        public AuthRequest(byte[] playerId, string token = "")
        {
            this.type = "auth";
            this.player_id = BitConverter.ToString(playerId).Replace("-", "").ToLower();
            this.token = token;
            this.client_version = Constants.CLIENT_VERSION;
        }
    }

    // =========================================================================
    // Matchmaking
    // =========================================================================

    [Serializable]
    public class MatchmakingRequest : ClientMessage
    {
        public string mode;

        public MatchmakingRequest(MatchMode matchMode)
        {
            this.type = "matchmaking";
            this.mode = matchMode.ToString().ToLower();
        }
    }

    [Serializable]
    public class CancelMatchmaking : ClientMessage
    {
        public CancelMatchmaking()
        {
            this.type = "cancel_matchmaking";
        }
    }

    // =========================================================================
    // Match Control
    // =========================================================================

    [Serializable]
    public class Ready : ClientMessage
    {
        public Ready()
        {
            this.type = "ready";
        }
    }

    [Serializable]
    public class Leave : ClientMessage
    {
        public Leave()
        {
            this.type = "leave";
        }
    }

    [Serializable]
    public class SyncRequest : ClientMessage
    {
        public SyncRequest()
        {
            this.type = "sync_request";
        }
    }

    // =========================================================================
    // Game Input
    // =========================================================================

    [Serializable]
    public class GameInput : ClientMessage
    {
        public uint tick;
        public int move_x;
        public int move_y;
        public int flags;
        public long timestamp;

        public GameInput(uint tick, sbyte moveX, sbyte moveY, bool ability = false)
        {
            this.type = "input";
            this.tick = tick;
            this.move_x = moveX;
            this.move_y = moveY;
            this.flags = ability ? 1 : 0;
            this.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Create input from Unity input axes (-1 to 1) converted to server format (-127 to 127).
        /// </summary>
        public static GameInput FromAxes(uint tick, float horizontal, float vertical, bool ability = false)
        {
            sbyte moveX = (sbyte)(horizontal * Constants.INPUT_MAX);
            sbyte moveY = (sbyte)(vertical * Constants.INPUT_MAX);

            // Clamp to valid range
            moveX = (sbyte)Math.Clamp(moveX, Constants.INPUT_MIN, Constants.INPUT_MAX);
            moveY = (sbyte)Math.Clamp(moveY, Constants.INPUT_MIN, Constants.INPUT_MAX);

            // If no input, use special "no input" value
            if (Math.Abs(horizontal) < 0.01f && Math.Abs(vertical) < 0.01f)
            {
                moveX = Constants.INPUT_NO_VALUE;
                moveY = Constants.INPUT_NO_VALUE;
            }

            return new GameInput(tick, moveX, moveY, ability);
        }
    }

    // =========================================================================
    // Latency
    // =========================================================================

    [Serializable]
    public class Ping : ClientMessage
    {
        public long timestamp;

        public Ping()
        {
            this.type = "ping";
            this.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
