// Player profile page interactions — the Roll / End Turn command buttons.
// Mirrors the host play-page.js command binding (the drawer there is host-only).
//
// The buttons live inside the live-swapped body ([data-player]), so clicks are
// delegated. Each invokes the matching GamePlayHub method, which gates
// server-side (host-bypass aware) and enqueues on the game's single-writer
// executor. Disable on click to avoid a double-send before the next StateChanged
// frame re-renders the button into its new state.
//
// The mock/stub buttons ([data-noop]) are intentionally unbound — they do
// nothing until their engine commands exist.
(function () {
    'use strict';

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
})();