(function () {
    const HEARTBEAT_MS = 30000;
    let pingHandle = null;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/presence")
        .withAutomaticReconnect()
        .build();

    function ping() {
        if (connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke("Ping").catch(() => { /* swallowed — reconnect handles it */ });
        }
    }

    function startHeartbeat() {
        stopHeartbeat();
        pingHandle = setInterval(ping, HEARTBEAT_MS);
    }

    function stopHeartbeat() {
        if (pingHandle !== null) {
            clearInterval(pingHandle);
            pingHandle = null;
        }
    }

    connection.onreconnected(startHeartbeat);
    connection.onclose(stopHeartbeat);

    connection.start()
        .then(startHeartbeat)
        .catch(err => console.error("Presence hub failed to connect:", err));

    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "hidden") {
            stopHeartbeat();
        } else {
            ping();
            startHeartbeat();
        }
    });
})();