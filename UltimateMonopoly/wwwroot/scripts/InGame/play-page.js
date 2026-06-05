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
    bindCommand('[data-leave-jail-pay]', 'LeaveJailPay');   // pay the fee to leave jail

    // Property portfolio commands inside the host's player-profile drawer — the
    // button carries data-cmd + data-board-index. Invokes the matching GamePlayHub
    // method (gated server-side, host-bypass), which opens the AcquirePropertyPrompt
    // confirmation. Only the whitelisted commands below are dispatched.
    const PORTFOLIO_COMMANDS = {           // carry data-board-index
        mortgage: 'MortgageProperty',
        unmortgage: 'UnmortgageProperty',
        unreserve: 'UnReserveProperty',
        build: 'BuildProperty',
        buildset: 'BuildSet',              // any index in the set; resolved server-side
        sell: 'SellProperty',
        sellset: 'SellSet'                 // any index in the set; resolved server-side
    };
    const PORTFOLIO_COMMANDS_NO_INDEX = {  // no board index
        buildall: 'BuildAll',
        sellall: 'SellAll'
    };

    document.addEventListener('click', function (e) {
        const btn = e.target.closest('[data-cmd]');
        if (!btn || btn.disabled || !window.GamePlayHub) return;

        const cmd = btn.dataset.cmd;
        const noIndexMethod = PORTFOLIO_COMMANDS_NO_INDEX[cmd];
        if (noIndexMethod) {
            btn.disabled = true;
            GamePlayHub.invoke(noIndexMethod).catch(err => {
                console.error(noIndexMethod + ' failed:', err);
                btn.disabled = false;   // re-enable on transient failure
            });
            return;
        }

        const method = PORTFOLIO_COMMANDS[cmd];
        const boardIndex = Number(btn.dataset.boardIndex);
        if (!method || !Number.isInteger(boardIndex)) return;

        btn.disabled = true;
        GamePlayHub.invoke(method, boardIndex).catch(err => {
            console.error(method + ' failed:', err);
            btn.disabled = false;   // re-enable on transient failure
        });
    });

    // Custom loan repayment — reads the amount from the sibling input and invokes
    // RepayLoanCustom (pays the oldest loan first). Whole pounds, > 0; the engine
    // re-gates, normalises, and caps to the player's cash.
    document.addEventListener('click', function (e) {
        const btn = e.target.closest('[data-loan-repay]');
        if (!btn || btn.disabled || !window.GamePlayHub) return;

        const input = btn.closest('.input-group')?.querySelector('[data-loan-amount]');
        const amount = input ? Math.floor(Number(input.value)) : 0;
        if (!Number.isInteger(amount) || amount <= 0) return;

        btn.disabled = true;
        GamePlayHub.invoke('RepayLoanCustom', amount).catch(err => {
            console.error('RepayLoanCustom failed:', err);
            btn.disabled = false;   // re-enable on transient failure
        });
    });
})();