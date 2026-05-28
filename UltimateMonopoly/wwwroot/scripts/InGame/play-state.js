// Host Play page — live state. Listens on the game-play hub coordinator for the
// engine's whole-cache StateChanged broadcast (web-orchestration.md §6) and
// re-renders the play body by re-fetching the server-rendered _PlayView partial
// (PlayModel handler=State) and swapping it into .play-page. Rendering stays on
// the server (same Razor + cache as first load) — the client holds no game state
// and duplicates no rule/gate logic.
(function () {
    'use strict';

    const root = document.querySelector('[data-play]');
    if (!root || !window.GamePlayHub) return;

    const gameId = root.dataset.gameId;
    if (!gameId) return;

    let inFlight = false;
    let pending = false;

    async function refresh() {
        // Coalesce bursts: if a frame arrives mid-fetch, do exactly one more pass.
        if (inFlight) { pending = true; return; }
        inFlight = true;
        try {
            const resp = await fetch('/Game/Play/' + encodeURIComponent(gameId) + '?handler=State');
            if (resp.ok) {
                root.innerHTML = await resp.text();
                // The board's inline init script doesn't run on an innerHTML swap;
                // re-attach popovers from the persisted global.
                if (typeof window.initBoardPopovers === 'function') window.initBoardPopovers();
            } else {
                console.error('Play state fetch returned', resp.status);
            }
        } catch (e) {
            console.error('Play state refresh failed:', e);
        } finally {
            inFlight = false;
            if (pending) { pending = false; refresh(); }
        }
    }

    GamePlayHub.on('StateChanged', refresh);
})();