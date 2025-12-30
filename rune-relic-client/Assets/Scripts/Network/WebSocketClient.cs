using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RuneRelic.Network
{
    /// <summary>
    /// Cross-platform WebSocket client wrapper.
    /// Uses Unity's native WebSocket on WebGL, NativeWebSocket elsewhere.
    /// </summary>
    public class WebSocketClient : IDisposable
    {
        private readonly string _url;
        private IWebSocketWrapper _socket;
        private bool _isConnecting;
        private bool _disposed;

        // Events
        public event Action OnOpen;
        public event Action<string> OnMessage;
        public event Action<byte[]> OnBinaryMessage;
        public event Action<string> OnError;
        public event Action OnClose;

        public bool IsConnected => _socket?.IsConnected ?? false;

        public WebSocketClient(string url)
        {
            _url = url;
        }

        public async Task Connect()
        {
            if (_isConnecting || IsConnected)
                return;

            _isConnecting = true;

            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                _socket = new WebGLWebSocket(_url);
#else
                _socket = new StandaloneWebSocket(_url);
#endif

                _socket.OnOpen += () => OnOpen?.Invoke();
                _socket.OnMessage += (msg) => OnMessage?.Invoke(msg);
                _socket.OnBinaryMessage += (data) => OnBinaryMessage?.Invoke(data);
                _socket.OnError += (err) => OnError?.Invoke(err);
                _socket.OnClose += () => OnClose?.Invoke();

                await _socket.Connect();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebSocket] Connection failed: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
            finally
            {
                _isConnecting = false;
            }
        }

        public async Task Send(string message)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[WebSocket] Cannot send - not connected");
                return;
            }

            await _socket.Send(message);
        }

        public async Task SendBinary(byte[] data)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[WebSocket] Cannot send - not connected");
                return;
            }

            await _socket.SendBinary(data);
        }

        public void DispatchMessages()
        {
            _socket?.DispatchMessages();
        }

        public async Task Close()
        {
            if (_socket != null)
            {
                await _socket.Close();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _socket?.Dispose();
        }
    }

    /// <summary>
    /// WebSocket wrapper interface.
    /// </summary>
    public interface IWebSocketWrapper : IDisposable
    {
        bool IsConnected { get; }
        event Action OnOpen;
        event Action<string> OnMessage;
        event Action<byte[]> OnBinaryMessage;
        event Action<string> OnError;
        event Action OnClose;

        Task Connect();
        Task Send(string message);
        Task SendBinary(byte[] data);
        Task Close();
        void DispatchMessages();
    }

    /// <summary>
    /// Standalone WebSocket using System.Net.WebSockets.
    /// </summary>
    public class StandaloneWebSocket : IWebSocketWrapper
    {
        private readonly string _url;
        private System.Net.WebSockets.ClientWebSocket _ws;
        private readonly Queue<string> _messageQueue = new Queue<string>();
        private readonly Queue<byte[]> _binaryQueue = new Queue<byte[]>();
        private bool _isReceiving;
        private bool _disposed;

        public bool IsConnected => _ws?.State == System.Net.WebSockets.WebSocketState.Open;

        public event Action OnOpen;
        public event Action<string> OnMessage;
        public event Action<byte[]> OnBinaryMessage;
        public event Action<string> OnError;
        public event Action OnClose;

        public StandaloneWebSocket(string url)
        {
            _url = url;
        }

        public async Task Connect()
        {
            _ws = new System.Net.WebSockets.ClientWebSocket();

            try
            {
                await _ws.ConnectAsync(new Uri(_url), System.Threading.CancellationToken.None);
                OnOpen?.Invoke();
                StartReceiving();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
                throw;
            }
        }

        private async void StartReceiving()
        {
            if (_isReceiving) return;
            _isReceiving = true;

            var buffer = new byte[8192];

            try
            {
                while (_ws.State == System.Net.WebSockets.WebSocketState.Open && !_disposed)
                {
                    var result = await _ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        System.Threading.CancellationToken.None
                    );

                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                    {
                        lock (_messageQueue)
                        {
                            // Signal close
                        }
                        break;
                    }

                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        lock (_messageQueue)
                        {
                            _messageQueue.Enqueue(message);
                        }
                    }
                    else if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Binary)
                    {
                        byte[] data = new byte[result.Count];
                        Array.Copy(buffer, data, result.Count);
                        lock (_binaryQueue)
                        {
                            _binaryQueue.Enqueue(data);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_disposed)
                {
                    Debug.LogError($"[WebSocket] Receive error: {ex.Message}");
                    OnError?.Invoke(ex.Message);
                }
            }
            finally
            {
                _isReceiving = false;
                if (!_disposed)
                {
                    OnClose?.Invoke();
                }
            }
        }

        public async Task Send(string message)
        {
            if (_ws.State != System.Net.WebSockets.WebSocketState.Open)
                return;

            var bytes = Encoding.UTF8.GetBytes(message);
            await _ws.SendAsync(
                new ArraySegment<byte>(bytes),
                System.Net.WebSockets.WebSocketMessageType.Text,
                true,
                System.Threading.CancellationToken.None
            );
        }

        public async Task SendBinary(byte[] data)
        {
            if (_ws.State != System.Net.WebSockets.WebSocketState.Open)
                return;

            await _ws.SendAsync(
                new ArraySegment<byte>(data),
                System.Net.WebSockets.WebSocketMessageType.Binary,
                true,
                System.Threading.CancellationToken.None
            );
        }

        public void DispatchMessages()
        {
            // Dispatch text messages
            while (true)
            {
                string message = null;
                lock (_messageQueue)
                {
                    if (_messageQueue.Count > 0)
                        message = _messageQueue.Dequeue();
                }

                if (message == null) break;
                OnMessage?.Invoke(message);
            }

            // Dispatch binary messages
            while (true)
            {
                byte[] data = null;
                lock (_binaryQueue)
                {
                    if (_binaryQueue.Count > 0)
                        data = _binaryQueue.Dequeue();
                }

                if (data == null) break;
                OnBinaryMessage?.Invoke(data);
            }
        }

        public async Task Close()
        {
            if (_ws?.State == System.Net.WebSockets.WebSocketState.Open)
            {
                await _ws.CloseAsync(
                    System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                    "Client closing",
                    System.Threading.CancellationToken.None
                );
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _ws?.Dispose();
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// WebGL WebSocket using browser's native WebSocket via JS interop.
    /// </summary>
    public class WebGLWebSocket : IWebSocketWrapper
    {
        private readonly string _url;
        private int _instanceId = -1;
        private bool _isConnected;
        private readonly Queue<string> _messageQueue = new Queue<string>();
        private readonly Queue<byte[]> _binaryQueue = new Queue<byte[]>();

        public bool IsConnected => _isConnected;

        public event Action OnOpen;
        public event Action<string> OnMessage;
        public event Action<byte[]> OnBinaryMessage;
        public event Action<string> OnError;
        public event Action OnClose;

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int WebSocket_Create(string url);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void WebSocket_Send(int id, string message);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void WebSocket_Close(int id);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int WebSocket_GetState(int id);

        public WebGLWebSocket(string url)
        {
            _url = url;
        }

        public Task Connect()
        {
            _instanceId = WebSocket_Create(_url);
            // WebGL connects asynchronously, we'll get callback
            return Task.CompletedTask;
        }

        public Task Send(string message)
        {
            if (_instanceId >= 0 && _isConnected)
            {
                WebSocket_Send(_instanceId, message);
            }
            return Task.CompletedTask;
        }

        public Task SendBinary(byte[] data)
        {
            // WebGL binary not implemented in this simple version
            return Task.CompletedTask;
        }

        public void DispatchMessages()
        {
            // Messages dispatched via JS callbacks
        }

        public Task Close()
        {
            if (_instanceId >= 0)
            {
                WebSocket_Close(_instanceId);
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Close();
        }

        // Called from JS
        public void HandleOpen()
        {
            _isConnected = true;
            OnOpen?.Invoke();
        }

        public void HandleMessage(string message)
        {
            OnMessage?.Invoke(message);
        }

        public void HandleError(string error)
        {
            OnError?.Invoke(error);
        }

        public void HandleClose()
        {
            _isConnected = false;
            OnClose?.Invoke();
        }
    }
#endif
}
