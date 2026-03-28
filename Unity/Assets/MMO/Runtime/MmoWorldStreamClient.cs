#if UNITY_WEBGL && !UNITY_EDITOR
// ClientWebSocket недоступен в WebGL-сборке; используйте отдельный транспорт или только HTTP.
#else

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Mmo.Cell.V1;
using Mmo.Game.V1;

namespace Mmo.Client.Gateway
{
    /// <summary>Приём бинарных <see cref="WorldChunk"/> с gateway WebSocket (как в Go <c>proto.Marshal(chunk)</c>).</summary>
    public sealed class MmoWorldStreamClient : IDisposable
    {
        readonly ClientWebSocket _ws = new ClientWebSocket();
        CancellationTokenSource _lifetime;

        public bool IsConnected => _ws.State == WebSocketState.Open;

        /// <summary>Событие в потоке фонового read-loop; маршалить на main thread при необходимости.</summary>
        public event Action<WorldChunk> OnWorldChunk;

        public event Action<Exception> OnError;

        /// <summary>
        /// Подключение к <c>/v1/ws?token=</c>. При 409 (handoff) будет исключение; сначала вызовите POST /v1/session с новыми resolve_x/z.
        /// </summary>
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
                    using var ms = new System.IO.MemoryStream();
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
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // ignore
                }
            }
        }

        /// <summary>Бинарный кадр <see cref="ClientInput"/> (совместимо с gateway <c>proto.Unmarshal</c>).</summary>
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
