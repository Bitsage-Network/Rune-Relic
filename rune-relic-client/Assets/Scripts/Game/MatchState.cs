using System.Collections.Generic;
using UnityEngine;
using RuneRelic.Network.Messages;
using RuneRelic.Utils;

namespace RuneRelic.Game
{
    /// <summary>
    /// Client-side match state with interpolation support.
    /// Stores recent server states for smooth rendering.
    /// </summary>
    public class MatchState
    {
        // Match info
        public byte[] MatchId { get; private set; }
        public ulong RngSeed { get; private set; }
        public List<byte[]> PlayerIds { get; private set; }

        // Timing
        public uint LastServerTick { get; set; }
        public uint TimeRemaining { get; private set; }
        public float MatchStartTime { get; private set; }

        // State buffer for interpolation
        private readonly Queue<GameStateUpdate> _stateBuffer = new Queue<GameStateUpdate>();
        private GameStateUpdate _previousState;
        private GameStateUpdate _currentState;
        private float _lastUpdateTime;

        // Player states (latest)
        public Dictionary<string, PlayerState> Players { get; } = new Dictionary<string, PlayerState>();

        public MatchState(MatchStartInfo startInfo)
        {
            MatchId = startInfo.match_id;
            RngSeed = startInfo.rng_seed;
            MatchStartTime = Time.time;

            PlayerIds = new List<byte[]>();
            foreach (var player in startInfo.players)
            {
                PlayerIds.Add(player.player_id);

                string id = BytesToHex(player.player_id);
                Players[id] = new PlayerState
                {
                    PlayerId = player.player_id,
                    Position = FixedPoint.ToVector3(player.position),
                    TargetPosition = FixedPoint.ToVector3(player.position),
                    Form = Form.Spark,
                    Score = 0,
                    Alive = true,
                    SpawnZoneId = -1,
                    SpawnZoneActive = false,
                    ColorIndex = player.color_index
                };
            }
        }

        /// <summary>
        /// Apply a state update from the server.
        /// </summary>
        public void ApplyUpdate(GameStateUpdate update)
        {
            // Shift buffer
            _previousState = _currentState;
            _currentState = update;
            _lastUpdateTime = Time.time;

            // Keep buffer limited
            _stateBuffer.Enqueue(update);
            while (_stateBuffer.Count > Constants.STATE_BUFFER_SIZE)
            {
                _stateBuffer.Dequeue();
            }

            LastServerTick = update.tick;
            TimeRemaining = update.time_remaining;

            // Update player states
            foreach (var playerUpdate in update.players)
            {
                string id = BytesToHex(playerUpdate.player_id);

                if (!Players.TryGetValue(id, out var state))
                {
                    state = new PlayerState { PlayerId = playerUpdate.player_id };
                    Players[id] = state;
                }

                // Store previous position for interpolation
                state.PreviousPosition = state.TargetPosition;
                state.TargetPosition = FixedPoint.ToVector3(playerUpdate.position);
                state.Velocity = FixedPoint.VelocityToVector3(playerUpdate.velocity);
                state.Form = (Form)playerUpdate.form;
                state.Score = playerUpdate.score;
                state.Alive = playerUpdate.alive;
                state.SpawnZoneId = playerUpdate.spawn_zone_id;
                state.SpawnZoneActive = playerUpdate.spawn_zone_active;
                state.Radius = FixedPoint.ToFloat(playerUpdate.radius);
                state.AbilityCooldown = FixedPoint.ToFloat(playerUpdate.ability_cooldown);

                if (playerUpdate.buffs != null)
                {
                    state.SpeedBuffTicks = playerUpdate.buffs.speed;
                    state.ShieldBuffTicks = playerUpdate.buffs.shield;
                    state.InvulnerableTicks = playerUpdate.buffs.invulnerable;
                }
            }
        }

        /// <summary>
        /// Get interpolation factor (0-1) between previous and current state.
        /// </summary>
        public float GetInterpolationT()
        {
            if (_previousState == null || _currentState == null)
                return 1f;

            float timeSinceUpdate = Time.time - _lastUpdateTime;
            float updateInterval = 1f / Constants.STATE_UPDATE_RATE; // 50ms

            return Mathf.Clamp01(timeSinceUpdate / updateInterval);
        }

        /// <summary>
        /// Get interpolated position for a player.
        /// </summary>
        public Vector3 GetInterpolatedPosition(string playerId)
        {
            if (!Players.TryGetValue(playerId, out var state))
                return Vector3.zero;

            float t = GetInterpolationT();
            return Vector3.Lerp(state.PreviousPosition, state.TargetPosition, t);
        }

        /// <summary>
        /// Get time remaining in seconds.
        /// </summary>
        public float GetTimeRemainingSeconds()
        {
            return TimeRemaining / (float)Constants.TICK_RATE;
        }

        private static string BytesToHex(byte[] bytes)
        {
            if (bytes == null) return "";
            return System.BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }

    /// <summary>
    /// Client-side player state.
    /// </summary>
    public class PlayerState
    {
        public byte[] PlayerId;
        public int ColorIndex;

        // Position interpolation
        public Vector3 Position;
        public Vector3 PreviousPosition;
        public Vector3 TargetPosition;
        public Vector3 Velocity;

        // Game state
        public Form Form;
        public uint Score;
        public bool Alive;
        public float Radius;
        public float AbilityCooldown;
        public int SpawnZoneId;
        public bool SpawnZoneActive;

        // Buffs (ticks remaining)
        public uint SpeedBuffTicks;
        public uint ShieldBuffTicks;
        public uint InvulnerableTicks;

        public bool HasSpeedBuff => SpeedBuffTicks > 0;
        public bool HasShieldBuff => ShieldBuffTicks > 0;
        public bool IsInvulnerable => InvulnerableTicks > 0;
    }
}
