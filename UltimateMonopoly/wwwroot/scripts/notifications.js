// Notification dropdown — AJAX dismiss (X button), keepalive mark-as-read (link click).
(() => {
    const root = document.getElementById('notification-dropdown');
    if (!root) return;

    const headerName = document.querySelector('meta[name="csrf-header"]')?.content;
    const token = document.querySelector('meta[name="csrf-token"]')?.content;
    const badge = root.querySelector('[data-notification-badge]');
    const countEl = root.querySelector('[data-notification-count]');
    const listEl = root.querySelector('[data-notification-list]');

    function buildHeaders() {
        const headers = { 'X-Requested-With': 'XMLHttpRequest' };
        if (headerName && token) headers[headerName] = token;
        return headers;
    }

    function updateCount() {
        const items = listEl.querySelectorAll('[data-notification-id]');
        const n = items.length;
        if (badge) {
            badge.textContent = n > 99 ? '99+' : String(n);
            badge.classList.toggle('d-none', n === 0);
        }
        if (countEl) {
            countEl.textContent = String(n);
            countEl.classList.toggle('d-none', n === 0);
        }
        if (n === 0 && !listEl.querySelector('[data-notification-empty]')) {
            listEl.innerHTML =
                '<div class="px-3 py-4 text-center text-body-secondary" data-notification-empty>' +
                '<i class="bi bi-bell-slash fs-2 d-block mb-2"></i>You\'re all caught up!</div>';
        }
    }

    root.addEventListener('click', async (e) => {
        // Explicit dismiss (X) — soft-deletes via TryDismissAsync.
        const btn = e.target.closest('[data-notification-dismiss]');
        if (btn) {
            e.preventDefault();
            e.stopPropagation();

            const id = btn.dataset.notificationDismiss;
            btn.disabled = true;
            try {
                const res = await fetch(`/api/notifications/${encodeURIComponent(id)}/dismiss`, {
                    method: 'POST',
                    headers: buildHeaders()
                });
                if (res.ok) {
                    btn.closest('[data-notification-id]')?.remove();
                    updateCount();
                } else {
                    btn.disabled = false;
                }
            } catch {
                btn.disabled = false;
            }
            return;
        }

        // Link click inside a notification — mark as read via TryMarkAsReadAsync.
        // Fire as keepalive so the request survives navigation. Don't preventDefault.
        const link = e.target.closest('[data-notification-id] a[href]');
        if (link) {
            const id = link.closest('[data-notification-id]')?.dataset.notificationId;
            if (id) {
                try {
                    fetch(`/api/notifications/${encodeURIComponent(id)}/read`, {
                        method: 'POST',
                        headers: buildHeaders(),
                        keepalive: true
                    }).catch(() => { });
                } catch { /* swallow — navigation continues */ }
            }
        }
    });
})();