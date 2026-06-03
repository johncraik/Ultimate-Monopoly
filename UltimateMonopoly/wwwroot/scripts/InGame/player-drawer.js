// Host Play page — the player-profile drawer (left pull-out).
//
// The drawer is a "phone": it loads the real /player-profile partial (Index
// handler=State) for the selected player — the same view that player sees on their
// own phone, fetched with the host as viewer so the host-bypass gates let the host
// act on their behalf. Any pending prompt for that player renders *inside* the
// partial (server-side; see _PlayerProfileView's .pp-prompt overlay).
//
// Opening:
//   • Manual — the host taps a player's "View" button.
//   • Prompt-driven — a player prompt fires → the drawer auto-opens on that player so
//     the host sees it. It does NOT auto-close: the host closes it manually, and can
//     close it / step to another player / use the host page while the prompt sits
//     unanswered (it stays pending server-side).
//
// The open drawer refreshes on each live StateChanged frame.
(function () {
    'use strict';

    const host = document.querySelector('[data-play]');
    const drawer = document.querySelector('[data-player-drawer]');
    const backdrop = document.querySelector('[data-drawer-backdrop]');
    if (!host || !drawer || !backdrop || !window.GamePlayHub) return;

    const gameId = host.dataset.gameId;
    if (!gameId) return;

    const titleEl = drawer.querySelector('[data-drawer-title]');
    const contentEl = drawer.querySelector('[data-drawer-content]');

    let players = [];
    let index = -1;
    let currentUserId = null;
    let isOpen = false;
    let inFlight = false;
    let pendingReload = false;

    // Player roster (id + name) read from the live player-list cards, which carry the
    // server-resolved display name. Re-read each time — the list re-renders.
    function readPlayers() {
        return Array.from(document.querySelectorAll('[data-player-card]'))
            .map(el => ({ userId: el.dataset.userId, name: el.dataset.playerName || el.dataset.userId }))
            .filter(p => p.userId);
    }

    function nameFor(userId) {
        const p = readPlayers().find(x => x.userId === userId);
        return p ? p.name : 'Player';
    }

    // Preserve the active bottom tab across a content reload (mirrors player-state.js).
    function activeTabTarget() {
        const el = contentEl.querySelector('.pp-tab.active');
        return el ? el.getAttribute('data-bs-target') : null;
    }
    function restoreTab(target) {
        if (!target || typeof bootstrap === 'undefined') return;
        const btn = contentEl.querySelector('.pp-tab[data-bs-target="' + target + '"]');
        if (btn && !btn.classList.contains('active')) bootstrap.Tab.getOrCreateInstance(btn).show();
    }

    async function loadContent(userId) {
        if (!userId) return;
        if (inFlight) { pendingReload = true; return; }
        inFlight = true;
        const keepTab = activeTabTarget();
        try {
            const url = '/player-profile/' + encodeURIComponent(gameId) +
                '/' + encodeURIComponent(userId) + '?handler=State';
            const resp = await fetch(url);
            if (resp.ok) {
                contentEl.innerHTML = await resp.text();
                restoreTab(keepTab);
            } else {
                contentEl.textContent = 'Could not load this player.';
                console.error('Drawer profile fetch returned', resp.status);
            }
        } catch (e) {
            console.error('Drawer profile load failed:', e);
        } finally {
            inFlight = false;
            if (pendingReload) { pendingReload = false; loadContent(currentUserId); }
        }
    }

    // Open (or switch) the drawer on a player, with its own backdrop. Used for both a
    // manual "View" and a prompt auto-open — the drawer behaves identically either way.
    function open(userId) {
        if (!userId) return;
        players = readPlayers();
        index = players.findIndex(p => p.userId === userId);
        currentUserId = userId;
        if (titleEl) titleEl.textContent = nameFor(userId);
        loadContent(userId);
        drawer.classList.add('open');
        drawer.setAttribute('aria-hidden', 'false');
        backdrop.classList.add('open');
        isOpen = true;
    }

    function close() {
        drawer.classList.remove('open');
        backdrop.classList.remove('open');
        drawer.setAttribute('aria-hidden', 'true');
        isOpen = false;
        currentUserId = null;
    }

    function step(delta) {
        if (!players.length || index === -1) return;
        index = (index + delta + players.length) % players.length;
        currentUserId = players[index].userId;
        if (titleEl) titleEl.textContent = nameFor(currentUserId);
        loadContent(currentUserId);
    }

    // "View" buttons live inside the live-swapped .play-page, so delegate.
    document.addEventListener('click', function (e) {
        const btn = e.target.closest('[data-view-player]');
        if (btn) open(btn.dataset.userId);
    });

    // Header controls are static (outside .play-page). Close / step are always
    // allowed — a pending prompt no longer locks the drawer (it stays pending
    // server-side, so closing or switching away is fine).
    drawer.querySelector('[data-drawer-prev]')?.addEventListener('click', () => step(-1));
    drawer.querySelector('[data-drawer-next]')?.addEventListener('click', () => step(1));
    drawer.querySelector('[data-drawer-close]')?.addEventListener('click', close);
    backdrop.addEventListener('click', close);
    document.addEventListener('keydown', e => { if (e.key === 'Escape') close(); });

    // Auto-open the drawer on the prompted player so the host sees the prompt (it
    // renders inside the loaded profile). No auto-close — manual only. Covers the
    // reconnect/initial resync too (see game-play-hub.js).
    GamePlayHub.observePrompts({
        onOpen: function (prompt) { if (prompt && prompt.playerId) open(prompt.playerId); }
    });

    // Keep the open drawer's profile current with the live state.
    GamePlayHub.on('StateChanged', function () { if (isOpen && currentUserId) loadContent(currentUserId); });
})();
