// Host Play page interactions — the Roll / End Turn turn commands.
// (The player-profile drawer is owned by player-drawer.js.)
//
// Everything inside .play-page is re-rendered by play-state.js on each live
// frame, so all clicks here are delegated (the buttons are re-created). Disable
// on click to avoid a double-send before the next frame swaps the button.
(function () {
    'use strict';

    // Turn commands — invoke the matching GamePlayHub method, which gates
    // server-side and enqueues on the game's single-writer executor.
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

    // Property portfolio commands inside the host's player-profile drawer — the
    // button carries data-cmd + data-board-index. Invokes the matching GamePlayHub
    // method (gated server-side, host-bypass), which opens the AcquirePropertyPrompt
    // confirmation. Build/sell stay [data-noop] until their engine commands exist;
    // only the whitelisted commands below are dispatched.
    const PORTFOLIO_COMMANDS = {
        mortgage: 'MortgageProperty',
        unmortgage: 'UnmortgageProperty',
        unreserve: 'UnReserveProperty'
    };

    document.addEventListener('click', function (e) {
        const btn = e.target.closest('[data-cmd]');
        if (!btn || btn.disabled || !window.GamePlayHub) return;

        const method = PORTFOLIO_COMMANDS[btn.dataset.cmd];
        const boardIndex = Number(btn.dataset.boardIndex);
        if (!method || !Number.isInteger(boardIndex)) return;

        btn.disabled = true;
        GamePlayHub.invoke(method, boardIndex).catch(err => {
            console.error(method + ' failed:', err);
            btn.disabled = false;   // re-enable on transient failure
        });
    });
})();