using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Mmo.Cell.V1;
using Mmo.Game.V1;

#if UNITY_WEBGL && !UNITY_EDITOR

namespace Mmo.Client.Gateway
{
    /// <summary>WebGL: бинарный WebSocket через jslib (браузерный API). Read-loop — <see cref="Poll"/> с главного потока.</summary>
    public sealed class MmoWorldStreamClient : IDisposable
    {
        int _id = -1;
        CancellationTokenSource _lifetime;

        public bool IsConnected => _id >= 0 && MmoWs_GetReadyState(_id) == 1;

        public event Action<WorldChunk> OnWorldChunk;
        public event Action<Exception> OnError;

        public async Task ConnectAsync(Uri wsUri, CancellationToken cancellationToken = default)
        {
            CloseSocket();
            DisposeLifetime();
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var id = MmoWs_Create(wsUri.AbsoluteUri);
            if (id < 0)
            {
                throw new InvalidOperationException("MmoWs_Create failed");
            }
            _id = id;
            try
            {
                while (MmoWs_GetReadyState(_id) == 0)
                {
                    _lifetime.Token.ThrowIfCancellationRequested();
                    await Task.Delay(32, _lifetime.Token).ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException)
            {
                Dispose();
                throw;
            }
            if (MmoWs_GetReadyState(_id) != 1)
            {
                Dispose();
                throw new InvalidOperationException("WebSocket did not open");
            }
        }

        /// <summary>Вызывать из <c>Update</c> главного потока для приёма кадров.</summary>
        public void Poll()
        {
            if (_id < 0 || MmoWs_GetReadyState(_id) != 1)
            {
                return;
            }
            const int max = 1024 * 64;
            var buf = Marshal.AllocHGlobal(max);
            var wptr = Marshal.AllocHGlobal(4);
            try
            {
                for (var i = 0; i < 32; i++)
                {
                    var r = MmoWs_DequeueRecv(_id, buf, max, wptr);
                    var written = Marshal.ReadInt32(wptr);
                    if (r == 0 || written <= 0)
                    {
                        break;
                    }
                    if (r < 0)
                    {
                        OnError?.Invoke(new InvalidOperationException("MmoWs recv buffer too small"));
                        break;
                    }
                    var data = new byte[written];
                    Marshal.Copy(buf, data, 0, written);
                    try
                    {
                        var chunk = WorldChunk.Parser.ParseFrom(data);
                        OnWorldChunk?.Invoke(chunk);
                    }
                    catch (InvalidProtocolBufferException ex)
                    {
                        OnError?.Invoke(ex);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(wptr);
                Marshal.FreeHGlobal(buf);
            }
        }

        public Task SendInputAsync(ClientInput input, CancellationToken ct = default)
        {
            if (_id < 0 || MmoWs_GetReadyState(_id) != 1)
            {
                return Task.CompletedTask;
            }
            var bytes = input.ToByteArray();
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                if (MmoWs_Send(_id, handle.AddrOfPinnedObject(), bytes.Length) == 0)
                {
                    return Task.CompletedTask;
                }
            }
            finally
            {
                handle.Free();
            }
            return Task.CompletedTask;
        }

        public Task CloseAsync()
        {
            CloseSocket();
            DisposeLifetime();
            return Task.CompletedTask;
        }

        void CloseSocket()
        {
            if (_id < 0)
            {
                return;
            }
            MmoWs_Close(_id);
            MmoWs_Destroy(_id);
            _id = -1;
        }

        void DisposeLifetime()
        {
            if (_lifetime == null)
            {
                return;
            }
            try
            {
                _lifetime.Cancel();
                _lifetime.Dispose();
            }
            catch
            {
                // ignore
            }
            _lifetime = null;
        }

        public void Dispose()
        {
            CloseSocket();
            DisposeLifetime();
        }

        [DllImport("__Internal", EntryPoint = "MmoWs_Create")]
        static extern int MmoWs_Create(string url);

        [DllImport("__Internal", EntryPoint = "MmoWs_GetReadyState")]
        static extern int MmoWs_GetReadyState(int id);

        [DllImport("__Internal", EntryPoint = "MmoWs_Send")]
        static extern int MmoWs_Send(int id, IntPtr ptr, int length);

        [DllImport("__Internal", EntryPoint = "MmoWs_Close")]
        static extern void MmoWs_Close(int id);

        [DllImport("__Internal", EntryPoint = "MmoWs_DequeueRecv")]
        static extern int MmoWs_DequeueRecv(int id, IntPtr outPtr, int outBufLen, IntPtr writtenLenPtr);

        [DllImport("__Internal", EntryPoint = "MmoWs_Destroy")]
        static extern void MmoWs_Destroy(int id);
    }
}

#else

using System.Net.WebSockets;

namespace Mmo.Client.Gateway
{
    /// <summary>Приём бинарных <see cref="WorldChunk"/> с gateway WebSocket (как в Go <c>proto.Marshal</c>).</summary>
    public sealed class MmoWorldStreamClient : IDisposable
    {
        readonly ClientWebSocket _ws = new ClientWebSocket();
        CancellationTokenSource _lifetime;

        public bool IsConnected => _ws.State == WebSocketState.Open;

        public event Action<WorldChunk> OnWorldChunk;
        public event Action<Exception> OnError;

        public async Task ConnectAsync(Uri wsUri, CancellationToken cancellationToken = default)
        {
            DisposeLifetime();
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                await _ws.ConnectAsync(wsUri, _lifetime.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                throw;
            }
            _ = RunReceiveLoopAsync(_lifetime.Token);
        }

        async Task RunReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new ArraySegment<byte>(new byte[1024 * 64]);
            try
            {
                while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }
                        if (result.Count > 0)
                        {
                            ms.Write(buffer.Array, buffer.Offset, result.Count);
                        }
                    } while (!result.EndOfMessage);

                    var data = ms.ToArray();
                    if (data.Length == 0)
                    {
                        continue;
                    }
                    try
                    {
                        var chunk = WorldChunk.Parser.ParseFrom(data);
                        OnWorldChunk?.Invoke(chunk);
                    }
                    catch (InvalidProtocolBufferException ex)
                    {
                        OnError?.Invoke(ex);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // нормальная остановка
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        void DisposeLifetime()
        {
            if (_lifetime == null)
            {
                return;
            }
            try
            {
                _lifetime.Cancel();
                _lifetime.Dispose();
            }
            catch
            {
                // ignore
            }
            _lifetime = null;
        }

        public async Task CloseAsync()
        {
            DisposeLifetime();
            if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // ignore
                }
            }
        }

        public Task SendInputAsync(ClientInput input, CancellationToken ct = default)
        {
            if (_ws.State != WebSocketState.Open)
            {
                return Task.CompletedTask;
            }
            var bytes = input.ToByteArray();
            return _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, ct);
        }

        public void Dispose()
        {
            DisposeLifetime();
            _ws.Dispose();
        }
    }
}

#endif
