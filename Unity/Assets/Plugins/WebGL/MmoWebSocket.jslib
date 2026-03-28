var MmoWebSocketLibrary = {
  $mmoWsImpl: {
    nextId: 1,
    sockets: {},
    queues: {},
  },

  MmoWs_Create__deps: ['$mmoWsImpl'],
  MmoWs_Create: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var id = mmoWsImpl.nextId++;
    try {
      var ws = new WebSocket(url);
      ws.binaryType = 'arraybuffer';
      mmoWsImpl.sockets[id] = ws;
      mmoWsImpl.queues[id] = [];
      ws._mmoOpened = false;
      ws._mmoClosed = false;
      ws.onopen = function () {
        ws._mmoOpened = true;
      };
      ws.onclose = function () {
        ws._mmoClosed = true;
      };
      ws.onerror = function () {
        ws._mmoClosed = true;
      };
      ws.onmessage = function (e) {
        var u8 = new Uint8Array(e.data);
        var copy = new Uint8Array(u8.length);
        copy.set(u8);
        mmoWsImpl.queues[id].push(copy);
      };
      return id;
    } catch (e) {
      return -1;
    }
  },

  MmoWs_GetReadyState__deps: ['$mmoWsImpl'],
  MmoWs_GetReadyState: function (id) {
    var ws = mmoWsImpl.sockets[id];
    if (!ws) return 3;
    if (ws._mmoClosed) return 3;
    if (ws._mmoOpened && ws.readyState === 1) return 1;
    return 0;
  },

  MmoWs_Send__deps: ['$mmoWsImpl'],
  MmoWs_Send: function (id, ptr, length) {
    var ws = mmoWsImpl.sockets[id];
    if (!ws || ws.readyState !== 1) return 0;
    ws.send(HEAPU8.subarray(ptr, ptr + length));
    return 1;
  },

  MmoWs_Close__deps: ['$mmoWsImpl'],
  MmoWs_Close: function (id) {
    var ws = mmoWsImpl.sockets[id];
    if (ws && (ws.readyState === 0 || ws.readyState === 1)) {
      try {
        ws.close();
      } catch (e) {}
    }
  },

  MmoWs_DequeueRecv__deps: ['$mmoWsImpl'],
  MmoWs_DequeueRecv: function (id, outPtr, outBufLen, writtenLenPtr) {
    var q = mmoWsImpl.queues[id];
    if (!q || q.length === 0) {
      HEAP32[writtenLenPtr >> 2] = 0;
      return 0;
    }
    var chunk = q.shift();
    var len = chunk.length;
    if (len > outBufLen) {
      q.unshift(chunk);
      HEAP32[writtenLenPtr >> 2] = 0;
      return -1;
    }
    HEAPU8.set(chunk, outPtr);
    HEAP32[writtenLenPtr >> 2] = len;
    return 1;
  },

  MmoWs_Destroy__deps: ['$mmoWsImpl'],
  MmoWs_Destroy: function (id) {
    delete mmoWsImpl.sockets[id];
    delete mmoWsImpl.queues[id];
  },
};

mergeInto(LibraryManager.library, MmoWebSocketLibrary);
