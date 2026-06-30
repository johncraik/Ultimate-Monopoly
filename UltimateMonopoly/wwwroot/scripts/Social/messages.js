// E1 — Friend messaging. Two-pane page: AJAX-loads a conversation into the right panel, posts sends to the
// page handler, and receives live messages via the MessagingHub (SignalR is loaded globally in _Layout).
(function () {
    const panel = document.getElementById("msgPanel");
    const list = document.getElementById("msgList");
    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenEl ? tokenEl.value : "";

    let activeThreadId = headerThreadId();

    function headerThreadId() {
        const h = document.getElementById("msgHeader");
        return h ? h.getAttribute("data-thread-id") : null;
    }

    function scrollBody() {
        const body = document.getElementById("msgBody");
        if (body) body.scrollTop = body.scrollHeight;
    }

    function fmtTime(iso) {
        return new Date(iso).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
    }

    function appendBubble(text, fromMe, iso) {
        const body = document.getElementById("msgBody");
        if (!body) return;
        const placeholder = body.querySelector("p");
        if (placeholder) placeholder.remove();
        // a new message makes any prior "Read @time" receipt stale
        const receipt = body.querySelector(".msg-read-receipt");
        if (receipt) receipt.remove();

        const row = document.createElement("div");
        row.className = "d-flex " + (fromMe ? "justify-content-end" : "justify-content-start");

        const bubble = document.createElement("div");
        bubble.className = "msg-bubble " + (fromMe ? "text-bg-primary" : "bg-body-tertiary border");

        const txt = document.createElement("div");
        txt.textContent = text;

        const time = document.createElement("div");
        time.className = "small text-end mt-1 " + (fromMe ? "text-white-50" : "text-body-secondary");
        time.textContent = fmtTime(iso);

        bubble.appendChild(txt);
        bubble.appendChild(time);
        row.appendChild(bubble);
        body.appendChild(row);
        scrollBody();
    }

    function listItem(threadId) {
        return list ? list.querySelector('.msg-list-item[data-thread-id="' + (window.CSS && CSS.escape ? CSS.escape(threadId) : threadId) + '"]') : null;
    }

    function bumpItem(threadId, preview, unread) {
        const item = listItem(threadId);
        if (!item) return;
        if (typeof preview === "string") {
            const prev = item.querySelector(".msg-preview");
            if (prev) prev.textContent = preview;
        }
        const dot = item.querySelector(".msg-unread-dot");
        if (dot) dot.classList.toggle("d-none", !unread);
        if (item.parentElement && item.parentElement.firstChild !== item)
            item.parentElement.insertBefore(item, item.parentElement.firstChild);
    }

    function setActive(threadId) {
        if (!list) return;
        list.querySelectorAll(".msg-list-item").forEach(el =>
            el.classList.toggle("active", el.getAttribute("data-thread-id") === threadId));
    }

    async function loadThread(threadId) {
        const res = await fetch("?handler=Thread&threadId=" + encodeURIComponent(threadId), {
            headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (!res.ok) return;
        panel.innerHTML = await res.text();
        activeThreadId = threadId;
        setActive(threadId);
        bumpItem(threadId, undefined, false); // opening clears the unread dot
        bindForm();
        scrollBody();
    }

    function bindForm() {
        const form = document.getElementById("msgForm");
        if (!form) return;
        form.addEventListener("submit", async (e) => {
            e.preventDefault();
            const input = document.getElementById("msgInput");
            const text = input.value.trim();
            if (!text) return;

            const threadId = form.getAttribute("data-thread-id");
            const res = await fetch("?handler=Send", {
                method: "POST",
                headers: { "RequestVerificationToken": token, "Content-Type": "application/x-www-form-urlencoded" },
                body: new URLSearchParams({ threadId: threadId, message: text }).toString()
            });
            if (!res.ok) {
                const err = await res.text();
                alert(err || "Could not send message.");
                return;
            }
            const data = await res.json();
            input.value = "";
            appendBubble(data.text, true, data.sentAtUtc);
            bumpItem(threadId, data.text, false);
            input.focus();
        });
    }

    if (list) {
        list.addEventListener("click", (e) => {
            const item = e.target.closest(".msg-list-item");
            if (item) loadThread(item.getAttribute("data-thread-id"));
        });
    }

    // Live delivery — push-only hub.
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/messaging")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveMessage", (m) => {
        if (m.threadId === activeThreadId) {
            appendBubble(m.message, false, m.sentAtUtc);
            bumpItem(m.threadId, m.message, false);
        } else {
            bumpItem(m.threadId, m.message, true);
        }
    });

    connection.start().catch(err => console.error("Messaging hub failed to connect:", err));

    // First paint may already have a conversation open (arrived via ?friendId).
    bindForm();
    if (activeThreadId) setActive(activeThreadId);
    scrollBody();
})();
