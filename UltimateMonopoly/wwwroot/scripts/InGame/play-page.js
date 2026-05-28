// Host Play page interactions — End Turn command + player-profile drawer.
//
// Everything inside .play-page is re-rendered by play-state.js on each live
// frame, so all clicks here are delegated (the buttons are re-created). The
// drawer markup itself is outside .play-page and persists.
(function () {
    'use strict';

    // Turn commands — invoke the matching GamePlayHub method, which gates
    // server-side and enqueues on the game's single-writer executor. Buttons are
    // re-rendered (which one, enabled/disabled) from each live state frame, so
    // these are delegated. Disable on click to avoid a double-send before the
    // next frame swaps the button.
    function bindCommand(selector, method) {
        document.addEventListener('click', function (e) {
            const btn = e.target.closest(selector);
            if (!btn || btn.disabled || !window.GamePlayHub) return;
            btn.disabled = true;
            GamePlayHub.invoke(method).catch(err => {
                console.error(method + ' failed:', err);
                btn.disabled = false;   // re-enable so it isn't stuck on a transient failure
            });
        });
    }

    bindCommand('[data-start-turn]', 'StartTurn');   // Roll Dice
    bindCommand('[data-end-turn]', 'EndTurn');

    // ── Player profile drawer ──
    // "View" opens a left drawer over a full-page backdrop; the header steps
    // through players (prev/next) and closes. Content is a placeholder for now —
    // TODO: load the real /player-profile/{gameId}/{userId} view into
    // [data-drawer-content].
    const drawer = document.querySelector('[data-player-drawer]');
    const backdrop = document.querySelector('[data-drawer-backdrop]');
    if (!drawer || !backdrop) return;

    const titleEl = drawer.querySelector('[data-drawer-title]');
    const contentEl = drawer.querySelector('[data-drawer-content]');

    // Names are resolved server-side (PlayerCacheService) into data-player-name;
    // the client only has the userId otherwise.
    let players = [];
    let index = -1;

    function readPlayers() {
        return Array.from(document.querySelectorAll('[data-player-card]'))
            .map(el => ({ userId: el.dataset.userId, name: el.dataset.playerName || el.dataset.userId }))
            .filter(p => p.userId);
    }

    function render() {
        const player = players[index];
        if (!player) return;
        if (titleEl) titleEl.textContent = player.name;
        // Placeholder until the real profile partial is wired in.
        if (contentEl) contentEl.textContent = 'This is player profile for ' + player.userId;
    }

    function open(userId) {
        players = readPlayers();                 // re-read — the list may have re-rendered
        index = players.findIndex(p => p.userId === userId);
        if (index === -1) return;
        render();
        drawer.classList.add('open');
        backdrop.classList.add('open');
        drawer.setAttribute('aria-hidden', 'false');
    }

    function close() {
        drawer.classList.remove('open');
        backdrop.classList.remove('open');
        drawer.setAttribute('aria-hidden', 'true');
    }

    function step(delta) {
        if (!players.length || index === -1) return;
        index = (index + delta + players.length) % players.length;
        render();
    }

    // Delegated — View buttons are re-created when the play body re-renders.
    document.addEventListener('click', function (e) {
        const btn = e.target.closest('[data-view-player]');
        if (btn) open(btn.dataset.userId);
    });

    drawer.querySelector('[data-drawer-prev]')?.addEventListener('click', () => step(-1));
    drawer.querySelector('[data-drawer-next]')?.addEventListener('click', () => step(1));
    drawer.querySelector('[data-drawer-close]')?.addEventListener('click', close);
    backdrop.addEventListener('click', close);
    document.addEventListener('keydown', e => { if (e.key === 'Escape') close(); });
})();