// leaderboard-refresh.js — listens for ReceiveLeaderboardUpdate from
// the SignalR notification hub and triggers a page reload. Debounced
// to max once per 60 seconds. Skips reload if the tab is not visible
// (Page Visibility API). See docs/plans/v2-roadmap.md Sprint 8 S8.4.

(function () {
    "use strict";

    // Only run on the leaderboard page.
    if (window.location.pathname !== "/stats") return;

    var lastReload = 0;
    var DEBOUNCE_MS = 60000;

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/notifications")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveLeaderboardUpdate", function () {
        var now = Date.now();
        if (now - lastReload < DEBOUNCE_MS) return;
        if (document.visibilityState !== "visible") return;

        lastReload = now;
        window.location.reload();
    });

    connection.start().catch(function (err) {
        console.debug("SignalR leaderboard refresh: " + err);
    });
})();
