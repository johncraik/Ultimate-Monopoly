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
    bindCommand('[data-leave-jail-pay]', 'LeaveJailPay');   // pay the fee to leave jail

    // Property portfolio commands — the button carries data-cmd + data-board-index.
    // Invokes the matching GamePlayHub method (gated server-side), which opens the
    // AcquirePropertyPrompt confirmation over this same connection. Only the
    // whitelisted commands below are dispatched.
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