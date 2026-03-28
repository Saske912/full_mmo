using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mmo.Cell.V1;
using Mmo.Client.Gateway;
using Mmo.Game.V1;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Mmo.Client.Unity
{
    /// <summary>Первый вертикальный срез: сессия → resolve-preview/handoff → WS → WorldChunk → ввод.</summary>
    public sealed class MmoGameBootstrap : MonoBehaviour
    {
        [Header("Сеть (можно переопределить из UI)")]
        [SerializeField] string _defaultBaseUrl = "https://mmo.pass-k8s.ru";
        [SerializeField] string _defaultPlayerId = "unity-play-1";
        [SerializeField] bool _useWorldCoordsFromFields = true;
        [SerializeField] double _defaultResolveX;
        [SerializeField] double _defaultResolveZ;

        [Header("Ввод")]
        [SerializeField] float _inputSendHz = 20f;
        [SerializeField] float _entityCubeScale = 0.5f;
        [Tooltip("Сглаживание позиций кубов к авторитетным координатам соты (выше — меньше отставание).")]
        [SerializeField] float _positionSmoothing = 14f;
        [Tooltip("0 = выкл. Линейное предсказание позиции по последней дельте авторитета (доля смешивания к extrapolated).")]
        [Range(0f, 1f)]
        [SerializeField] float _positionExtrapolation = 0f;

        MmoGatewayClient _gateway;
        MmoWorldStreamClient _world;
        CancellationTokenSource _cts;
        readonly Queue<WorldChunk> _pendingChunks = new Queue<WorldChunk>();
        readonly Dictionary<ulong, GameObject> _entityObjects = new Dictionary<ulong, GameObject>();
        readonly MmoClientWorldState _worldState = new MmoClientWorldState();
        Transform _entityRoot;
        uint _inputSeq;

        InputField _uiBaseUrl;
        InputField _uiPlayerId;
        InputField _uiRx;
        InputField _uiRz;
        Button _uiConnect;
        Text _uiStatus;
        Text _uiMeta;
        Button _uiQuestProg;
        InputField _uiQuestId;
        Button _uiItemRemove;
        InputField _uiItemId;
        RectTransform _inventoryRowsRoot;

        string _jwt;
        volatile bool _connectBusy;

        void Awake()
        {
            EnsureEventSystem();
            _entityRoot = new GameObject("MmoEntities").transform;
            _entityRoot.SetParent(transform, false);
            BuildUi();
        }

        static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                return;
            }
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            if (_world != null)
            {
                _world.OnWorldChunk -= WsOnChunk;
                _world.Dispose();
                _world = null;
            }
            _gateway?.Dispose();
            _gateway = null;
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("MmoPlayCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            AddLeftHudBackdrop(canvasGo.transform);

            float y = -36f;
            const float row = 44f;
            const float labelW = 200f;
            const float fieldW = 520f;
            const int labelFs = 22;
            const int fieldFs = 22;
            const int btnFs = 22;

            _uiBaseUrl = AddLabeledField(canvasGo.transform, "Адрес gateway", labelW, fieldW, y, labelFs, fieldFs, _defaultBaseUrl);
            y -= row;
            _uiPlayerId = AddLabeledField(canvasGo.transform, "Игрок (player_id)", labelW, fieldW, y, labelFs, fieldFs, _defaultPlayerId);
            y -= row;
            _uiRx = AddLabeledField(canvasGo.transform, "resolve_x (мир)", labelW, fieldW, y, labelFs, fieldFs, "0");
            y -= row;
            _uiRz = AddLabeledField(canvasGo.transform, "resolve_z (мир)", labelW, fieldW, y, labelFs, fieldFs, "0");
            y -= row * 1.15f;

            _uiConnect = AddButton(canvasGo.transform, "Подключиться к миру", 320f, 44f, y, btnFs, OnConnectClicked);
            y -= row * 1.55f;

            _uiStatus = AddText(canvasGo.transform, "Статус: не подключено", y, 32, new Color(1f, 1f, 0.92f), true);
            y -= row * 1.45f;
            _uiMeta = AddTextMultiline(canvasGo.transform, "", y, 26, new Color(0.78f, 0.92f, 1f), 380f);
            y -= 392f;
            _inventoryRowsRoot = AddInventoryRowsHost(canvasGo.transform, y);
            y -= 220f;

            _uiQuestId = AddLabeledField(canvasGo.transform, "Квест (id для отладки)", labelW, fieldW, y, labelFs, fieldFs, "tutorial_intro");
            y -= row;
            _uiQuestProg = AddButton(canvasGo.transform, "Отправить +1 к прогрессу квеста", 420f, 44f, y, btnFs, OnQuestProgressClicked);
            y -= row * 1.2f;

            _uiItemId = AddLabeledField(canvasGo.transform, "Предмет (удалить)", labelW, fieldW, y, labelFs, fieldFs, "tutorial_shard");
            y -= row;
            _uiItemRemove = AddButton(canvasGo.transform, "Удалить 1 шт. предмета", 320f, 44f, y, btnFs, OnItemRemoveClicked);
        }

        static void AddLeftHudBackdrop(Transform canvasTransform)
        {
            var go = new GameObject("HudBackdrop");
            go.transform.SetParent(canvasTransform, false);
            go.transform.SetAsFirstSibling();
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(820f, 0f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.04f, 0.06f, 0.1f, 0.88f);
            img.raycastTarget = false;
        }

        static Font UiFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static void AddTextOutline(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.78f);
            o.effectDistance = new Vector2(2f, -2f);
        }

        static InputField AddLabeledField(Transform parent, string label, float lw, float fw, float y, int labelSize, int fieldSize, string initial)
        {
            var lg = new GameObject("lbl_" + label);
            lg.transform.SetParent(parent, false);
            var rt = lg.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(28f, y);
            rt.sizeDelta = new Vector2(lw, Mathf.Max(28f, labelSize + 8f));
            var txt = lg.AddComponent<Text>();
            txt.font = UiFont();
            txt.text = label;
            txt.fontSize = labelSize;
            txt.color = new Color(0.95f, 0.96f, 1f);
            txt.alignment = TextAnchor.UpperLeft;
            AddTextOutline(txt);

            var inf = CreateInput(parent, new Vector2(28f + lw, y), new Vector2(fw, 40f), fieldSize, initial);
            return inf;
        }

        static InputField CreateInput(Transform parent, Vector2 pos, Vector2 size, int fontSize, string initial)
        {
            var go = new GameObject("InputField");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.1f, 0.14f, 0.96f);
            var input = go.AddComponent<InputField>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10f, 6f);
            trt.offsetMax = new Vector2(-10f, -6f);
            var t = textGo.AddComponent<Text>();
            t.font = UiFont();
            t.fontSize = fontSize;
            t.color = new Color(1f, 1f, 1f);
            t.lineSpacing = 1f;
            input.textComponent = t;
            input.text = initial ?? "";
            return input;
        }

        static Button AddButton(Transform parent, string caption, float w, float h, float y, int fontSize, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(28f, y);
            rt.sizeDelta = new Vector2(w, h);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.52f, 0.85f, 0.98f);
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.highlightedColor = new Color(0.35f, 0.62f, 0.98f);
            cb.pressedColor = new Color(0.15f, 0.4f, 0.7f);
            b.colors = cb;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 4f);
            trt.offsetMax = new Vector2(-8f, -4f);
            var t = textGo.AddComponent<Text>();
            t.font = UiFont();
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = fontSize;
            t.color = Color.white;
            t.text = caption;
            AddTextOutline(t);
            b.onClick.AddListener(onClick);
            return b;
        }

        static Text AddText(Transform parent, string initial, float y, int size, Color c, bool outline)
        {
            var go = new GameObject("txt");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(28f, y);
            rt.sizeDelta = new Vector2(760f, 120f);
            var t = go.AddComponent<Text>();
            t.font = UiFont();
            t.text = initial;
            t.fontSize = size;
            t.color = c;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            if (outline)
            {
                AddTextOutline(t);
            }
            return t;
        }

        static Text AddTextMultiline(Transform parent, string initial, float y, int size, Color c, float height)
        {
            var go = new GameObject("txt_meta");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(28f, y);
            rt.sizeDelta = new Vector2(760f, height);
            var t = go.AddComponent<Text>();
            t.font = UiFont();
            t.text = initial;
            t.fontSize = size;
            t.color = c;
            t.alignment = TextAnchor.UpperLeft;
            t.lineSpacing = 1.12f;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            AddTextOutline(t);
            return t;
        }

        void Update()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _world?.Poll();
#endif
            lock (_pendingChunks)
            {
                while (_pendingChunks.Count > 0)
                {
                    ApplyChunk(_pendingChunks.Dequeue());
                }
            }

            if (_world != null && _world.IsConnected)
            {
                SendInputIfDue();
            }
        }

        void LateUpdate()
        {
            float k = Mathf.Max(0f, _positionSmoothing);
            float t = k <= 0f ? 1f : 1f - Mathf.Exp(-k * Time.deltaTime);
            foreach (var kv in _entityObjects)
            {
                var tr = kv.Value != null ? kv.Value.transform : null;
                if (tr == null)
                {
                    continue;
                }
                if (!_worldState.TryGetDisplayTarget(kv.Key, _positionExtrapolation, out var target))
                {
                    continue;
                }
                tr.position = Vector3.Lerp(tr.position, target, t);
            }
        }

        void ApplyChunk(WorldChunk chunk)
        {
            if (chunk.KindCase == WorldChunk.KindOneofCase.Snapshot)
            {
                ClearEntities();
                foreach (var e in chunk.Snapshot.Entities)
                {
                    UpsertEntity(e);
                }
            }
            else if (chunk.KindCase == WorldChunk.KindOneofCase.Delta)
            {
                foreach (var e in chunk.Delta.Changed)
                {
                    UpsertEntity(e);
                }
            }
        }

        void ClearEntities()
        {
            foreach (var kv in _entityObjects)
            {
                if (kv.Value != null)
                {
                    Destroy(kv.Value);
                }
            }
            _entityObjects.Clear();
            _worldState.Clear();
        }

        void UpsertEntity(EntityState e)
        {
            if (e == null)
            {
                return;
            }
            _worldState.MergeEntity(e);
            var id = e.EntityId;
            if (!_entityObjects.TryGetValue(id, out var go) || go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "ent_" + id;
                go.transform.SetParent(_entityRoot, false);
                go.transform.localScale = Vector3.one * _entityCubeScale;
                _entityObjects[id] = go;
                var ap = e.Position;
                go.transform.position = new Vector3(ap.X, ap.Y, ap.Z);
            }
        }

        float _lastInputSent;

        /// <summary>WASD: биты как на соте (1 вперёд +Z, 2 назад, 4 влево, 8 вправо). Используем новый Input System — иначе при только Package в проекте <see cref="Input.GetKey"/> молчит.</summary>
        static uint ReadWasdMask()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                uint m = 0;
                if (kb.wKey.isPressed)
                {
                    m |= 1;
                }
                if (kb.sKey.isPressed)
                {
                    m |= 2;
                }
                if (kb.aKey.isPressed)
                {
                    m |= 4;
                }
                if (kb.dKey.isPressed)
                {
                    m |= 8;
                }
                return m;
            }
            uint mask = 0;
            if (Input.GetKey(KeyCode.W))
            {
                mask |= 1;
            }
            if (Input.GetKey(KeyCode.S))
            {
                mask |= 2;
            }
            if (Input.GetKey(KeyCode.A))
            {
                mask |= 4;
            }
            if (Input.GetKey(KeyCode.D))
            {
                mask |= 8;
            }
            return mask;
        }

        void SendInputIfDue()
        {
            var hz = Mathf.Max(1f, _inputSendHz);
            if (Time.unscaledTime - _lastInputSent < 1f / hz)
            {
                return;
            }
            _lastInputSent = Time.unscaledTime;

            uint mask = ReadWasdMask();

            float yaw = 0f;
            if (mask != 0)
            {
                var cam = Camera.main;
                yaw = cam != null ? cam.transform.eulerAngles.y : transform.eulerAngles.y;
            }

            _inputSeq++;
            var input = new ClientInput
            {
                Seq = _inputSeq,
                InputMask = mask,
                AimYawDeg = yaw,
            };
            _ = _world.SendInputAsync(input, CancellationToken.None);
        }

        async void OnConnectClicked()
        {
            if (_connectBusy)
            {
                return;
            }
            _connectBusy = true;
            _uiConnect.interactable = false;
            SetStatus("Подключение…");
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                if (_world != null)
                {
                    _world.OnWorldChunk -= WsOnChunk;
                    _world.Dispose();
                }
                _world = null;
                _gateway?.Dispose();
                _gateway = new MmoGatewayClient(_uiBaseUrl.text.Trim());
                _world = new MmoWorldStreamClient();
                _world.OnWorldChunk += WsOnChunk;
                _world.OnError += ex => Debug.LogWarning("[MmoWS] " + ex.Message);

                double? wx = null;
                double? wz = null;
                if (_useWorldCoordsFromFields)
                {
                    wx = ParseDouble(_uiRx.text, _defaultResolveX);
                    wz = ParseDouble(_uiRz.text, _defaultResolveZ);
                }

                SessionResponse session = null;

                async Task<SessionResponse> SessionOnceAsync(double? rx, double? rz)
                {
                    var req = new SessionRequest
                    {
                        PlayerId = _uiPlayerId.text.Trim(),
                        DisplayName = "Unity",
                    };
                    if (rx.HasValue && rz.HasValue)
                    {
                        req.ResolveX = rx;
                        req.ResolveZ = rz;
                    }
                    return await _gateway.PostSessionAsync(req, ct).ConfigureAwait(true);
                }

                session = await SessionOnceAsync(wx, wz).ConfigureAwait(true);
                _jwt = session.Token;
                RefreshMeta(session);

                await ApplyHandoffIfNeededAsync(wx, wz, ct).ConfigureAwait(true);

                var wsUri = MmoGatewayClient.BuildWebSocketUri(_uiBaseUrl.text.Trim(), _jwt);
                try
                {
                    await _world.ConnectAsync(wsUri, ct).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Mmo] WS connect failed, handoff retry: " + ex.Message);
                    if (_useWorldCoordsFromFields)
                    {
                        wx = ParseDouble(_uiRx.text, _defaultResolveX);
                        wz = ParseDouble(_uiRz.text, _defaultResolveZ);
                    }
                    await ApplyHandoffIfNeededAsync(wx, wz, ct).ConfigureAwait(true);
                    wsUri = MmoGatewayClient.BuildWebSocketUri(_uiBaseUrl.text.Trim(), _jwt);
                    await _world.ConnectAsync(wsUri, ct).ConfigureAwait(true);
                }

                SetStatus("WS: открыт; WASD — движение.");
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка: " + ex.Message);
                Debug.LogException(ex);
            }
            finally
            {
                _connectBusy = false;
                _uiConnect.interactable = true;
            }
        }

        async Task ApplyHandoffIfNeededAsync(double? rx, double? rz, CancellationToken ct)
        {
            ResolvePreviewResponse preview;
            try
            {
                preview = await _gateway
                    .GetResolvePreviewAsync(_jwt, rx, rz, ct)
                    .ConfigureAwait(true);
            }
            catch
            {
                return;
            }

            if (!preview.CellIdMismatch || !preview.LastCell.Found)
            {
                return;
            }

            var lx = preview.LastCell.ResolveX;
            var lz = preview.LastCell.ResolveZ;
            if (!lx.HasValue || !lz.HasValue)
            {
                return;
            }

            SetStatus("Handoff: несовпадение соты — новая сессия с last_cell координатами.");
            var req = new SessionRequest
            {
                PlayerId = _uiPlayerId.text.Trim(),
                DisplayName = "Unity",
                ResolveX = lx,
                ResolveZ = lz,
            };
            var session = await _gateway.PostSessionAsync(req, ct).ConfigureAwait(true);
            _jwt = session.Token;
            _uiRx.text = lx.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _uiRz.text = lz.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            RefreshMeta(session);
        }

        void WsOnChunk(WorldChunk chunk)
        {
            lock (_pendingChunks)
            {
                _pendingChunks.Enqueue(chunk);
            }
        }

        void RefreshMeta(SessionResponse s)
        {
            var gold = s.Wallet != null ? s.Wallet.Gold : 0L;
            var items = s.Items ?? new List<ItemStackDto>();
            var itemLines = "";
            foreach (var it in items)
            {
                var label = string.IsNullOrEmpty(it.DisplayName) ? it.ItemId : it.DisplayName;
                var idPart = string.IsNullOrEmpty(it.ItemId) ? "?" : it.ItemId;
                itemLines += $"• {idPart} × {it.Quantity} ({label})\n";
            }
            if (itemLines.Length == 0)
            {
                itemLines = "• предметов в ответе нет\n";
            }
            var qs = s.Quests ?? new List<QuestApiRow>();
            var questLines = "";
            foreach (var q in qs)
            {
                var pre = string.IsNullOrEmpty(q.PrerequisiteQuestId) ? "нет" : q.PrerequisiteQuestId;
                questLines += $"• {q.QuestId} — {q.Progress} / {q.TargetProgress} (нужен квест: {pre})\n";
            }
            if (questLines.Length == 0)
            {
                questLines = "• квестов в ответе нет\n";
            }
            _uiMeta.text = $"Золото: {gold}\n\nИнвентарь (сессия):\n{itemLines}Нажмите строку ниже для выбора id.\nКвесты:\n{questLines}";
            RebuildInventoryRows(items);
        }

        static RectTransform AddInventoryRowsHost(Transform parent, float y)
        {
            var go = new GameObject("InventoryRows");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(28f, y);
            rt.sizeDelta = new Vector2(720f, 200f);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperLeft;
            v.spacing = 4f;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return rt;
        }

        void RebuildInventoryRows(List<ItemStackDto> items)
        {
            if (_inventoryRowsRoot == null)
            {
                return;
            }
            for (var i = _inventoryRowsRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_inventoryRowsRoot.GetChild(i).gameObject);
            }
            if (items == null || items.Count == 0)
            {
                return;
            }
            foreach (var it in items)
            {
                var id = string.IsNullOrEmpty(it.ItemId) ? "" : it.ItemId;
                var dn = string.IsNullOrEmpty(it.DisplayName) ? id : it.DisplayName;
                var caption = $"{id} × {it.Quantity} — {dn}";
                AddInventoryRowButton(_inventoryRowsRoot, caption, id);
            }
        }

        void AddInventoryRowButton(Transform parent, string caption, string itemId)
        {
            var go = new GameObject("InvRow");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 36f;
            le.preferredHeight = 36f;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.18f, 0.28f, 0.92f);
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(0.2f, 0.28f, 0.42f);
            colors.pressedColor = new Color(0.1f, 0.14f, 0.22f);
            b.colors = colors;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10f, 4f);
            trt.offsetMax = new Vector2(-10f, -4f);
            var tx = textGo.AddComponent<Text>();
            tx.font = UiFont();
            tx.fontSize = 20;
            tx.color = new Color(0.92f, 0.95f, 1f);
            tx.alignment = TextAnchor.MiddleLeft;
            tx.text = caption;
            AddTextOutline(tx);

            var pick = itemId;
            b.onClick.AddListener(() =>
            {
                _uiItemId.text = pick;
                SetStatus("Предмет для remove: " + pick);
            });
        }

        void SetStatus(string msg)
        {
            _uiStatus.text = "Статус: " + msg;
        }

        static double ParseDouble(string s, double fallback)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return fallback;
            }
            return double.TryParse(
                s.Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var v)
                ? v
                : fallback;
        }

        async void OnQuestProgressClicked()
        {
            if (string.IsNullOrEmpty(_jwt))
            {
                SetStatus("Сначала подключитесь.");
                return;
            }
            try
            {
                var qid = string.IsNullOrWhiteSpace(_uiQuestId.text) ? "tutorial" : _uiQuestId.text.Trim();
                var res = await _gateway.PostQuestProgressAsync(
                    _jwt,
                    new QuestProgressRequest { QuestId = qid, Progress = 1 },
                    CancellationToken.None).ConfigureAwait(true);
                SetStatus($"quest-progress: ok={res.Ok} progress={res.Progress}/{res.TargetProgress}");
            }
            catch (Exception ex)
            {
                SetStatus("quest-progress: " + ex.Message);
            }
        }

        async void OnItemRemoveClicked()
        {
            if (string.IsNullOrEmpty(_jwt))
            {
                SetStatus("Сначала подключитесь.");
                return;
            }
            try
            {
                var item = string.IsNullOrWhiteSpace(_uiItemId.text) ? "tutorial_shard" : _uiItemId.text.Trim();
                await _gateway.PostItemsRemoveAsync(
                    _jwt,
                    new ItemsRemoveRequest { ItemId = item, Quantity = 1 },
                    CancellationToken.None).ConfigureAwait(true);
                SetStatus("items/remove: ok");
            }
            catch (Exception ex)
            {
                SetStatus("items/remove: " + ex.Message);
            }
        }
    }
}
