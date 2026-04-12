// notification-badge.js — SignalR client for real-time badge updates.
// Connects to /hubs/notifications and listens for ReceiveUnreadCount.
// Updates the DOM element with [data-notification-badge] attribute.
// Graceful degradation: if the hub is unreachable, the server-rendered
// count remains visible. See docs/plans/v2-roadmap.md Sprint 8 S8.2.

(function () {
    "use strict";

    var badge = document.querySelector("[data-notification-badge]");
    if (!badge) return; // No badge on page (unauthenticated).

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/notifications")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveUnreadCount", function (count) {
        if (badge) {
            badge.textContent = count;
            badge.style.display = count > 0 ? "" : "none";
            // Announce to screen readers via the aria-live region.
            badge.setAttribute("aria-label", count + " unread notification(s)");
        }
    });

    connection.start().catch(function (err) {
        console.debug("SignalR notification hub: " + err);
    });
})();
