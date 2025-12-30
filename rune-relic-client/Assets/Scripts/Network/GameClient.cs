using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using RuneRelic.Network.Messages;
using RuneRelic.Utils;

namespace RuneRelic.Network
{
    /// <summary>
    /// WebSocket client for connecting to the Rune Relic game server.
    /// Handles connection lifecycle, message sending/receiving, and reconnection.
    /// </summary>
    public class GameClient : MonoBehaviour
    {
        public static GameClient Instance { get; private set; }

        [Header("Connection Settings")]
        [SerializeField] private string serverUrl = Constants.DEFAULT_SERVER_URL;
        [SerializeField] private float reconnectDelay = 3f;
        [SerializeField] private int maxReconnectAttempts = 5;

        // Connection state
        private WebSocketClient _websocket;
        private bool _isConnecting;
        private int _reconnectAttempts;

        // Message queues (thread-safe)
        private readonly Queue<string> _incomingMessages = new Queue<string>();
        private readonly object _messageLock = new object();

        // Events
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;
        public event Action<AuthResult> OnAuthResult;
        public event Action<MatchmakingResponse> OnMatchmakingUpdate;
        public event Action<MatchFoundInfo> OnMatchFound;
        public event Action<MatchStartInfo> OnMatchStart;
        public event Action<GameStateUpdate> OnStateUpdate;
        public event Action<MatchEvent> OnMatchEvent;
        public event Action<MatchEndInfo> OnMatchEnd;
        public event Action<InputAck> OnInputAck;
        public event Action<Pong> OnPong;
        public event Action<ServerError> OnServerError;

        // State
        public bool IsConnected => _websocket?.IsConnected ?? false;
        public GameState CurrentState { get; private set; } = GameState.Disconnected;
        public int Ping { get; private set; } = 0;
        public byte[] LocalPlayerId { get; private set; }
        private ulong _lastPingTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            // Dispatch queued WebSocket messages on main thread
            _websocket?.DispatchMessages();

            // Process queued incoming messages
            ProcessIncomingMessages();
        }

        private async void OnApplicationQuit()
        {
            await Disconnect();
        }

        private void OnDestroy()
        {
            _websocket?.Dispose();
        }

        // =====================================================================
        // Connection Management
        // =====================================================================

        /// <summary>
        /// Connect to the game server.
        /// </summary>
        public async Task Connect(string url = null)
        {
            if (_isConnecting || IsConnected)
            {
                Debug.LogWarning("[GameClient] Already connected or connecting");
                return;
            }

            _isConnecting = true;
            CurrentState = GameState.Connecting;

            string connectUrl = url ?? serverUrl;
            Debug.Log($"[GameClient] Connecting to {connectUrl}...");

            try
            {
                _websocket?.Dispose();
                _websocket = new WebSocketClient(connectUrl);

                _websocket.OnOpen += HandleOpen;
                _websocket.OnClose += HandleClose;
                _websocket.OnError += HandleError;
                _websocket.OnMessage += HandleMessage;

                await _websocket.Connect();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameClient] Connection failed: {e.Message}");
                _isConnecting = false;
                CurrentState = GameState.Disconnected;
                OnError?.Invoke(e.Message);
            }
        }

        /// <summary>
        /// Disconnect from the server.
        /// </summary>
        public async Task Disconnect()
        {
            if (_websocket == null) return;

            _reconnectAttempts = maxReconnectAttempts; // Prevent auto-reconnect
            await _websocket.Close();
        }

        private void HandleOpen()
        {
            Debug.Log("[GameClient] Connected to server");
            _isConnecting = false;
            _reconnectAttempts = 0;
            CurrentState = GameState.Connected;
            OnConnected?.Invoke();
        }

        private void HandleClose()
        {
            Debug.Log("[GameClient] Disconnected from server");
            _isConnecting = false;
            CurrentState = GameState.Disconnected;
            OnDisconnected?.Invoke();

            // Auto-reconnect if not intentional disconnect
            if (_reconnectAttempts < maxReconnectAttempts)
            {
                _reconnectAttempts++;
                Debug.Log($"[GameClient] Reconnecting in {reconnectDelay}s (attempt {_reconnectAttempts}/{maxReconnectAttempts})");
                Invoke(nameof(TryReconnect), reconnectDelay);
            }
        }

        private void HandleError(string error)
        {
            Debug.LogError($"[GameClient] WebSocket error: {error}");
            OnError?.Invoke(error);
        }

        private void TryReconnect()
        {
            _ = Connect();
        }

        // =====================================================================
        // Message Handling
        // =====================================================================

        private void HandleMessage(string json)
        {
            lock (_messageLock)
            {
                _incomingMessages.Enqueue(json);
            }
        }

        private void ProcessIncomingMessages()
        {
            lock (_messageLock)
            {
                while (_incomingMessages.Count > 0)
                {
                    string json = _incomingMessages.Dequeue();
                    ParseAndDispatchMessage(json);
                }
            }
        }

        private void ParseAndDispatchMessage(string json)
        {
            try
            {
                // Parse base message to get type
                var baseMsg = JsonUtility.FromJson<ServerMessage>(json);

                switch (baseMsg.type)
                {
                    case "auth_result":
                        var authResult = JsonUtility.FromJson<AuthResult>(json);
                        HandleAuthResult(authResult);
                        break;

                    case "matchmaking":
                        var mmResponse = JsonUtility.FromJson<MatchmakingResponse>(json);
                        OnMatchmakingUpdate?.Invoke(mmResponse);
                        break;

                    case "match_found":
                        var matchFound = JsonUtility.FromJson<MatchFoundInfo>(json);
                        CurrentState = GameState.MatchFound;
                        OnMatchFound?.Invoke(matchFound);
                        break;

                    case "match_start":
                        var matchStart = JsonUtility.FromJson<MatchStartInfo>(json);
                        CurrentState = GameState.Countdown;
                        OnMatchStart?.Invoke(matchStart);
                        break;

                    case "state":
                        var stateUpdate = JsonUtility.FromJson<GameStateUpdate>(json);
                        OnStateUpdate?.Invoke(stateUpdate);
                        break;

                    case "event":
                        var matchEvent = JsonUtility.FromJson<MatchEvent>(json);
                        HandleMatchEvent(matchEvent);
                        break;

                    case "match_end":
                        var matchEnd = JsonUtility.FromJson<MatchEndInfo>(json);
                        CurrentState = GameState.MatchEnded;
                        OnMatchEnd?.Invoke(matchEnd);
                        break;

                    case "input_ack":
                        var inputAck = JsonUtility.FromJson<InputAck>(json);
                        OnInputAck?.Invoke(inputAck);
                        break;

                    case "pong":
                        var pong = JsonUtility.FromJson<Pong>(json);
                        // Calculate ping from round-trip time
                        ulong now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        Ping = (int)(now - _lastPingTime);
                        OnPong?.Invoke(pong);
                        break;

                    case "error":
                        var error = JsonUtility.FromJson<ServerError>(json);
                        OnServerError?.Invoke(error);
                        break;

                    case "shutdown":
                        var shutdown = JsonUtility.FromJson<Shutdown>(json);
                        Debug.LogWarning($"[GameClient] Server shutdown: {shutdown.reason}");
                        break;

                    default:
                        Debug.LogWarning($"[GameClient] Unknown message type: {baseMsg.type}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameClient] Failed to parse message: {e.Message}\nJSON: {json}");
            }
        }

        private void HandleAuthResult(AuthResult result)
        {
            if (result.success)
            {
                CurrentState = GameState.Authenticated;
                Debug.Log($"[GameClient] Authenticated. Session: {result.session_id}, Server: {result.server_version}");
            }
            else
            {
                Debug.LogError($"[GameClient] Auth failed: {result.error}");
            }
            OnAuthResult?.Invoke(result);
        }

        private void HandleMatchEvent(MatchEvent evt)
        {
            string eventType = ResolveEventType(evt);
            if (!string.IsNullOrEmpty(eventType))
            {
                evt.type = eventType;
            }

            // Handle countdown -> playing transition
            if (eventType == "match_started")
            {
                CurrentState = GameState.Playing;
            }

            OnMatchEvent?.Invoke(evt);
        }

        private static string ResolveEventType(MatchEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.@event))
            {
                return evt.@event;
            }

            if (!string.IsNullOrEmpty(evt.event_type))
            {
                return evt.event_type;
            }

            if (!string.IsNullOrEmpty(evt.type) && evt.type != "event")
            {
                return evt.type;
            }

            return evt.type;
        }

        // =====================================================================
        // Message Sending
        // =====================================================================

        /// <summary>
        /// Send a message to the server.
        /// </summary>
        public async Task Send(ClientMessage message)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[GameClient] Cannot send - not connected");
                return;
            }

            try
            {
                string json = JsonUtility.ToJson(message);
                await _websocket.Send(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameClient] Send failed: {e.Message}");
            }
        }

        /// <summary>
        /// Send authentication request.
        /// </summary>
        public async Task Authenticate(byte[] playerId, string token = "")
        {
            LocalPlayerId = playerId;
            CurrentState = GameState.Authenticating;
            await Send(new AuthRequest(playerId, token));
        }

        /// <summary>
        /// Request matchmaking.
        /// </summary>
        public async Task RequestMatchmaking(MatchMode mode = MatchMode.Casual)
        {
            CurrentState = GameState.InQueue;
            await Send(new MatchmakingRequest(mode));
        }

        /// <summary>
        /// Cancel matchmaking.
        /// </summary>
        public async Task CancelMatchmaking()
        {
            CurrentState = GameState.Authenticated;
            await Send(new CancelMatchmaking());
        }

        /// <summary>
        /// Signal ready for match.
        /// </summary>
        public async Task SendReady()
        {
            CurrentState = GameState.ReadyCheck;
            await Send(new Ready());
        }

        /// <summary>
        /// Send game input.
        /// </summary>
        public async Task SendInput(uint tick, float horizontal, float vertical, bool ability = false)
        {
            var input = GameInput.FromAxes(tick, horizontal, vertical, ability);
            await Send(input);
        }

        /// <summary>
        /// Send raw game input.
        /// </summary>
        public async Task SendInput(GameInput input)
        {
            await Send(input);
        }

        /// <summary>
        /// Send ping for latency measurement.
        /// </summary>
        public async Task SendPing()
        {
            _lastPingTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await Send(new Messages.Ping());
        }

        /// <summary>
        /// Leave current match.
        /// </summary>
        public async Task LeaveMatch()
        {
            await Send(new Leave());
            CurrentState = GameState.Authenticated;
        }
    }
}
