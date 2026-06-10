// Host control panel (top sheet) — open/close + the confirm-then-invoke wiring for the
// host-only game controls. Opened by the thin trigger bar below the navbar, a swipe down
// from the top edge, and closed by the close button, the backdrop, Escape, or a swipe up.
//
// Each action confirms via its own Bootstrap modal; the modal's confirm button invokes the
// matching GamePlayHub method (host-gated server-side). Draw Game enqueues the draw — the
// engine concludes the game and the GameCompleted broadcast (game-play-hub.js) redirects
// every client to the results page. Force Refresh / Cancel Game are scaffolded; their hub
// methods are still TODO.
(function () {
    'use strict';

    const panel = document.querySelector('[data-host-panel]');
    if (!panel) return;

    const backdrop = document.querySelector('[data-host-panel-backdrop]');
    const trigger = document.querySelector('[data-host-panel-open]');

    function open() {
        panel.classList.add('open');
        panel.setAttribute('aria-hidden', 'false');
        if (backdrop) backdrop.classList.add('open');
    }

    function close() {
        panel.classList.remove('open');
        panel.setAttribute('aria-hidden', 'true');
        if (backdrop) backdrop.classList.remove('open');
    }

    if (trigger) trigger.addEventListener('click', open);
    if (backdrop) backdrop.addEventListener('click', close);
    panel.querySelectorAll('[data-host-panel-close]').forEach(b => b.addEventListener('click', close));
    document.addEventListener('keydown', e => { if (e.key === 'Escape') close(); });

    // ── Swipe gestures: down from the very top edge opens; up on the panel closes. ──
    const TOP_ZONE = 40;      // px from the top edge that starts an opening swipe
    const THRESHOLD = 60;     // px of travel to trigger

    let openStartY = null;
    document.addEventListener('touchstart', e => {
        const t = e.touches[0];
        openStartY = (t && t.clientY <= TOP_ZONE && !panel.classList.contains('open')) ? t.clientY : null;
    }, { passive: true });
    document.addEventListener('touchmove', e => {
        if (openStartY === null) return;
        const t = e.touches[0];
        if (t && t.clientY - openStartY > THRESHOLD) { open(); openStartY = null; }
    }, { passive: true });

    let closeStartY = null;
    panel.addEventListener('touchstart', e => { closeStartY = e.touches[0] ? e.touches[0].clientY : null; }, { passive: true });
    panel.addEventListener('touchmove', e => {
        if (closeStartY === null) return;
        const y = e.touches[0] ? e.touches[0].clientY : closeStartY;
        if (closeStartY - y > THRESHOLD) { close(); closeStartY = null; }
    }, { passive: true });

    // ── Confirm → hub invoke ──
    function wire(modalId, action, hubMethod) {
        const modalEl = document.getElementById(modalId);
        if (!modalEl) return;
        const confirmBtn = modalEl.querySelector('[data-host-action="' + action + '"]');
        if (!confirmBtn) return;

        // Reset the button each time the modal opens (clear a prior in-flight disable).
        modalEl.addEventListener('show.bs.modal', () => { confirmBtn.disabled = false; });

        confirmBtn.addEventListener('click', function () {
            if (!window.GamePlayHub) return;
            confirmBtn.disabled = true;
            GamePlayHub.invoke(hubMethod)
                .then(function () {
                    // Success drives client-side via broadcasts (Draw → GameCompleted redirect).
                    const modal = window.bootstrap && bootstrap.Modal.getInstance(modalEl);
                    if (modal) modal.hide();
                    close();
                })
                .catch(function (err) {
                    console.error(hubMethod + ' failed:', err);
                    confirmBtn.disabled = false;   // re-enable on transient failure
                });
        });
    }

    wire('drawGameConfirmModal', 'draw', 'DrawGame');
    wire('forceRefreshConfirmModal', 'force-refresh', 'ForceRefresh');
    // Cancel Game isn't wired here — its confirm button is a plain POST form to the Play page
    // (Cancel handler), which cancels server-side and broadcasts the redirect home.
})();