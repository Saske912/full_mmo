using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Mmo.Client.Gateway
{
    /// <summary>HTTP-клиент к MMO gateway: сессия, квесты, инвентарь, resolve-preview.</summary>
    public sealed class MmoGatewayClient : IDisposable
    {
        readonly HttpClient _http;
        readonly string _baseUrl;

        public MmoGatewayClient(string baseUrl, TimeSpan? timeout = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _http = new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(30) };
        }

        public void Dispose() => _http.Dispose();

        static string AuthHeader(string token) =>
            string.IsNullOrEmpty(token) ? null : "Bearer " + token;

        static async Task<T> ReadJson<T>(HttpResponseMessage res, CancellationToken ct)
        {
            var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                throw new MmoGatewayException((int)res.StatusCode, body);
            }
            return JsonConvert.DeserializeObject<T>(body);
        }

        public async Task<SessionResponse> PostSessionAsync(SessionRequest req, CancellationToken ct = default)
        {
            var json = JsonConvert.SerializeObject(req);
            using var message = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/v1/session")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            using var res = await _http.SendAsync(message, ct).ConfigureAwait(false);
            return await ReadJson<SessionResponse>(res, ct).ConfigureAwait(false);
        }

        public async Task<ResolvePreviewResponse> GetResolvePreviewAsync(
            string token,
            double? resolveX,
            double? resolveZ,
            CancellationToken ct = default)
        {
            var q = new List<string>();
            if (resolveX.HasValue != resolveZ.HasValue)
            {
                throw new ArgumentException("Передайте оба resolve_x и resolve_z или ни одного.");
            }
            if (resolveX.HasValue)
            {
                q.Add("resolve_x=" + Uri.EscapeDataString(resolveX.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                q.Add("resolve_z=" + Uri.EscapeDataString(resolveZ.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            var url = _baseUrl + "/v1/me/resolve-preview";
            if (q.Count > 0)
            {
                url += "?" + string.Join("&", q);
            }
            using var message = new HttpRequestMessage(HttpMethod.Get, url);
            message.Headers.TryAddWithoutValidation("Authorization", AuthHeader(token));
            using var res = await _http.SendAsync(message, ct).ConfigureAwait(false);
            return await ReadJson<ResolvePreviewResponse>(res, ct).ConfigureAwait(false);
        }

        public async Task<OkResponse> PostItemsRemoveAsync(string token, ItemsRemoveRequest req, CancellationToken ct = default)
        {
            var json = JsonConvert.SerializeObject(req);
            using var message = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/v1/me/items/remove")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            message.Headers.TryAddWithoutValidation("Authorization", AuthHeader(token));
            using var res = await _http.SendAsync(message, ct).ConfigureAwait(false);
            return await ReadJson<OkResponse>(res, ct).ConfigureAwait(false);
        }

        public async Task<OkResponse> PostItemsTransferAsync(string token, ItemsTransferRequest req, CancellationToken ct = default)
        {
            var json = JsonConvert.SerializeObject(req);
            using var message = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/v1/me/items/transfer")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            message.Headers.TryAddWithoutValidation("Authorization", AuthHeader(token));
            using var res = await _http.SendAsync(message, ct).ConfigureAwait(false);
            return await ReadJson<OkResponse>(res, ct).ConfigureAwait(false);
        }

        public async Task<QuestProgressResponse> PostQuestProgressAsync(string token, QuestProgressRequest req, CancellationToken ct = default)
        {
            var json = JsonConvert.SerializeObject(req);
            using var message = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/v1/me/quest-progress")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            message.Headers.TryAddWithoutValidation("Authorization", AuthHeader(token));
            using var res = await _http.SendAsync(message, ct).ConfigureAwait(false);
            return await ReadJson<QuestProgressResponse>(res, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Собирает ws(s) URL для /v1/ws. При handoff-случае сервер может вернуть 409 до upgrade — см. <see cref="MmoCellHandoffHeaders"/>.
        /// </summary>
        public static Uri BuildWebSocketUri(string httpBaseUrl, string token)
        {
            var b = httpBaseUrl.TrimEnd('/');
            if (b.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                b = "wss://" + b.Substring("https://".Length);
            }
            else if (b.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                b = "ws://" + b.Substring("http://".Length);
            }
            return new Uri(b + "/v1/ws?token=" + Uri.EscapeDataString(token));
        }
    }

    public sealed class MmoGatewayException : Exception
    {
        public int StatusCode { get; }

        public MmoGatewayException(int statusCode, string body) : base("HTTP " + statusCode + ": " + (body ?? ""))
        {
            StatusCode = statusCode;
        }

        public bool IsCellHandoffConflict => StatusCode == (int)HttpStatusCode.Conflict;
    }

    /// <summary>Заголовки ответа при расхождении last_cell и resolve до upgrade WebSocket (и при 409).</summary>
    public static class MmoCellHandoffHeaders
    {
        public const string LastCellId = "X-MMO-Last-Cell-Id";
        public const string ResolvedCellId = "X-MMO-Resolved-Cell-Id";
    }
}
