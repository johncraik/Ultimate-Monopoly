// Host Play page — the player-profile drawer (left pull-out).
//
// Two ways it opens:
//   • Manual — the host taps a player's "View" button: the drawer slides in over
//     its own backdrop (a browse, with prev/next stepping).
//   • Prompt-driven — a player prompt fires: the drawer auto-opens on *that*
//     player (no own backdrop; the prompt modal supplies it) so the host sees
//     whose prompt it is, and auto-closes when the prompt resolves.
//
// Either way the body is the real /player-profile partial (Index handler=State),
// the same view the player sees on their phone — fetched with the host as viewer,
// so the host-bypass gates let the host act on the player's behalf. The open
// drawer also refreshes on each live StateChanged frame.
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
    let promptDriven = false;       // opened by a prompt — manual close/step suppressed until it resolves
    let inFlight = false;
    let pendingReload = false;

    // Player roster (id + name) read from the live player-list cards, which carry
    // the server-resolved display name. Re-read each time — the list re-renders.
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

    function showDrawer(withBackdrop) {
        drawer.classList.add('open');
        drawer.setAttribute('aria-hidden', 'false');
        backdrop.classList.toggle('open', !!withBackdrop);
        isOpen = true;
    }

    function close() {
        drawer.classList.remove('open');
        backdrop.classList.remove('open');
        drawer.setAttribute('aria-hidden', 'true');
        isOpen = false;
        promptDriven = false;
        currentUserId = null;
    }

    function openManual(userId) {
        players = readPlayers();
        index = players.findIndex(p => p.userId === userId);
        promptDriven = false;
        currentUserId = userId;
        if (titleEl) titleEl.textContent = nameFor(userId);
        loadContent(userId);
        showDrawer(true);
    }

    function openForPrompt(userId) {
        if (!userId) return;
        players = readPlayers();
        index = players.findIndex(p => p.userId === userId);
        promptDriven = true;
        currentUserId = userId;
        if (titleEl) titleEl.textContent = nameFor(userId);
        loadContent(userId);
        showDrawer(false);   // the prompt modal supplies the backdrop
    }

    function step(delta) {
        if (promptDriven) return;   // don't browse away from an active prompt
        if (!players.length || index === -1) return;
        index = (index + delta + players.length) % players.length;
        currentUserId = players[index].userId;
        if (titleEl) titleEl.textContent = nameFor(currentUserId);
        loadContent(currentUserId);
    }

    // "View" buttons live inside the live-swapped .play-page, so delegate.
    document.addEventListener('click', function (e) {
        const btn = e.target.closest('[data-view-player]');
        if (btn) openManual(btn.dataset.userId);
    });

    // Header controls are static (outside .play-page). Manual close is suppressed
    // while a prompt is driving the drawer — it closes when the prompt resolves.
    drawer.querySelector('[data-drawer-prev]')?.addEventListener('click', () => step(-1));
    drawer.querySelector('[data-drawer-next]')?.addEventListener('click', () => step(1));
    drawer.querySelector('[data-drawer-close]')?.addEventListener('click', () => { if (!promptDriven) close(); });
    backdrop.addEventListener('click', () => { if (!promptDriven) close(); });
    document.addEventListener('keydown', e => { if (e.key === 'Escape' && !promptDriven) close(); });

    // Auto-open the drawer on the prompt's player; auto-close when it resolves.
    // (Covers the reconnect/initial resync too — see game-play-hub.js.)
    GamePlayHub.observePrompts({
        onOpen: function (prompt) { if (prompt && prompt.playerId) openForPrompt(prompt.playerId); },
        onClose: function () { close(); }
    });

    // Keep the open drawer's profile current with the live state.
    GamePlayHub.on('StateChanged', function () { if (isOpen && currentUserId) loadContent(currentUserId); });
})();