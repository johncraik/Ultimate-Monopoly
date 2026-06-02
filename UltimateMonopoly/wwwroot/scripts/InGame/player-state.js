// Player profile page — live state. Mirrors play-state.js: listens on the
// game-play hub coordinator for the engine's whole-cache StateChanged broadcast
// (web-orchestration.md §6) and re-renders the profile body by re-fetching the
// server-rendered _PlayerProfileView partial (Index handler=State) and swapping
// it into [data-player]. Rendering stays on the server (same Razor + cache as
// first load) — the client holds no game state.
//
// The one extra concern over the host page: the body carries the bottom tab bar,
// so the active tab is preserved across a swap (otherwise every frame would snap
// the user back to Profile).
(function () {
    'use strict';

    const root = document.querySelector('[data-player]');
    if (!root || !window.GamePlayHub) return;

    const gameId = root.dataset.gameId;
    const userId = root.dataset.userId;
    if (!gameId || !userId) return;

    let inFlight = false;
    let pending = false;

    function activeTabTarget() {
        const el = root.querySelector('.pp-tab.active');
        return el ? el.getAttribute('data-bs-target') : null;
    }

    function restoreTab(target) {
        if (!target || typeof bootstrap === 'undefined') return;
        const btn = root.querySelector('.pp-tab[data-bs-target="' + target + '"]');
        // Default markup activates Profile; only re-show if the user was elsewhere.
        if (btn && !btn.classList.contains('active')) {
            bootstrap.Tab.getOrCreateInstance(btn).show();
        }
    }

    async function refresh() {
        // Coalesce bursts: if a frame arrives mid-fetch, do exactly one more pass.
        if (inFlight) { pending = true; return; }
        inFlight = true;
        const keepTab = activeTabTarget();
        try {
            const url = '/player-profile/' + encodeURIComponent(gameId) +
                '/' + encodeURIComponent(userId) + '?handler=State';
            const resp = await fetch(url);
            if (resp.ok) {
                root.innerHTML = await resp.text();
                restoreTab(keepTab);
            } else {
                console.error('Player state fetch returned', resp.status);
            }
        } catch (e) {
            console.error('Player state refresh failed:', e);
        } finally {
            inFlight = false;
            if (pending) { pending = false; refresh(); }
        }
    }

    GamePlayHub.on('StateChanged', refresh);
})();