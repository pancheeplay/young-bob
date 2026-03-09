mergeInto(LibraryManager.library, {
  DebugRelayWebSocket_Connect: function (urlPtr, gameObjectPtr) {
    var url = UTF8ToString(urlPtr);
    var gameObjectName = UTF8ToString(gameObjectPtr);

    if (Module.debugRelaySocket) {
      try {
        Module.debugRelaySocket.close();
      } catch (error) {
      }
      Module.debugRelaySocket = null;
    }

    try {
      var socket = new WebSocket(url);
      Module.debugRelaySocket = socket;

      socket.onopen = function () {
        SendMessage(gameObjectName, "OnDebugRelayOpen", "");
      };

      socket.onmessage = function (event) {
        var data = typeof event.data === "string" ? event.data : "";
        SendMessage(gameObjectName, "OnDebugRelayMessage", data);
      };

      socket.onerror = function (event) {
        var message = "WebSocket error";
        if (event && typeof event.message === "string" && event.message.length > 0) {
          message = event.message;
        }
        SendMessage(gameObjectName, "OnDebugRelayError", message);
      };

      socket.onclose = function (event) {
        var reason = "";
        if (event) {
          reason = "code=" + event.code + " reason=" + (event.reason || "");
        }
        SendMessage(gameObjectName, "OnDebugRelayClose", reason);
      };
    } catch (error) {
      SendMessage(gameObjectName, "OnDebugRelayError", String(error));
    }
  },

  DebugRelayWebSocket_Send: function (messagePtr) {
    var message = UTF8ToString(messagePtr);
    var socket = Module.debugRelaySocket;
    if (!socket || socket.readyState !== 1) {
      return;
    }

    socket.send(message);
  },

  DebugRelayWebSocket_Close: function () {
    var socket = Module.debugRelaySocket;
    if (!socket) {
      return;
    }

    try {
      socket.close();
    } catch (error) {
    }
    Module.debugRelaySocket = null;
  }
});
