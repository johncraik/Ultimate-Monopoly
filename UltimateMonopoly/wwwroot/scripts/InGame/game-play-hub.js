// Game-play hub coordinator.
//
// Owns the single SignalR connection to the game-play hub for an in-game page
// and fans engine prompts out to registered per-prompt handlers (one module per
// prompt type, under /scripts/InGame/Prompts/). One connection per page —
// handlers never open their own.
//
// A prompt module registers itself:
//
//   GamePlayHub.registerPrompt({
//       type: 'DiceRoll',                  // engine [JsonPolymorphic] $type it renders
//       onOpen(prompt, stamp, ctx) { },    // a prompt of this type opened
//       onClose(promptId) { },             // the open prompt closed (promptId may be
//                                          //   null on a resync that found none)
//   });
//
// ctx (passed to onOpen) exposes:
//   ctx.gameId, ctx.userId                 // page context — the profiled player
//   ctx.submit(stamp, response) -> Promise<bool>   // SubmitPrompt; false = stale/invalid
//   ctx.refresh() -> Promise               // re-pull the current prompt and re-dispatch
//
// Non-prompt consumers (e.g. a future StateChanged board projection) attach raw
// hub handlers with GamePlayHub.on(eventName, callback) — queued until the
// connection is built, then forwarded.
//
// Load order: this script must come before any /Prompts/* handler so the global
// exists when they register. Registration is synchronous at parse time; the
// connection is started once on DOMContentLoaded, by which point every handler
// loaded after this script has registered.
(function () {
    'use strict';

    const handlers = new Map();   // $type -> handler
    const observers = [];         // type-agnostic { onOpen, onClose } prompt observers
    const queued = [];            // { event, callback } raw subscriptions queued pre-connect
    let connection = null;
    let ctx = null;
    let started = false;

    function registerPrompt(handler) {
        if (handler && handler.type) handlers.set(handler.type, handler);
    }

    // Type-agnostic prompt observer — notified for *every* prompt regardless of
    // type, through the same dispatch path as the typed handlers (so it also
    // fires on the reconnect/initial resync, not just live PromptOpened events).
    // Used by the host drawer to auto-open on the prompt's player.
    function observePrompts(observer) {
        if (observer) observers.push(observer);
    }

    function on(event, callback) {
        if (connection) connection.on(event, callback);
        else queued.push({ event, callback });
    }

    // Invoke a hub method (e.g. a command like EndTurn). Rejects if called before
    // the connection is up.
    function invoke(method) {
        if (!connection) return Promise.reject(new Error('game-play hub not connected'));
        return connection.invoke.apply(connection, arguments);
    }

    function dispatchOpen(msg) {
        if (!msg || !msg.prompt) return;
        const handler = handlers.get(msg.prompt['$type']);
        if (handler) handler.onOpen(msg.prompt, msg.concurrencyStamp, ctx);
        observers.forEach(o => { if (o.onOpen) o.onOpen(msg.prompt, msg.concurrencyStamp, ctx); });
    }

    // Only one prompt is ever open at a time, and PromptClosed carries no type,
    // so tell every handler — each ignores a close for a prompt it isn't showing.
    function dispatchClose(promptId) {
        handlers.forEach(h => { if (h.onClose) h.onClose(promptId); });
        observers.forEach(o => { if (o.onClose) o.onClose(promptId); });
    }

    async function refresh() {
        try {
            const msg = await connection.invoke('GetCurrentPrompt');
            if (msg && msg.prompt) dispatchOpen(msg);
            else dispatchClose(null);
        } catch (e) {
            console.error('GetCurrentPrompt failed:', e);
        }
    }

    // Fatal game error (GameFaulted): the server abandoned the game's pump, so
    // this session is over. Show a centred, static-backdrop alert and force-quit
    // to the home screen. Terminal — there's no dismissing it back into the game.
    function showFatalError(message) {
        if (document.getElementById('gameFaultModal')) return;   // already shown

        const text = message || 'An unexpected error occurred and the game cannot continue.';
        const modalEl = document.createElement('div');
        modalEl.className = 'modal fade';
        modalEl.id = 'gameFaultModal';
        modalEl.tabIndex = -1;
        modalEl.setAttribute('data-bs-backdrop', 'static');
        modalEl.setAttribute('data-bs-keyboard', 'false');
        modalEl.innerHTML =
            '<div class="modal-dialog modal-dialog-centered">' +
              '<div class="modal-content border border-danger border-2">' +
                '<div class="modal-header text-bg-danger border-0">' +
                  '<h5 class="modal-title"><i class="bi bi-exclamation-triangle-fill me-1"></i> Game error</h5>' +
                '</div>' +
                '<div class="modal-body"><p class="mb-0"></p></div>' +
                '<div class="modal-footer">' +
                  '<button type="button" class="btn btn-danger w-100" data-fault-leave>Return to home</button>' +
                '</div>' +
              '</div>' +
            '</div>';
        modalEl.querySelector('.modal-body p').textContent = text;
        document.body.appendChild(modalEl);

        const leave = () => { window.location.href = '/Index'; };
        modalEl.querySelector('[data-fault-leave]').addEventListener('click', leave);

        if (typeof bootstrap !== 'undefined') {
            new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false }).show();
        } else {
            leave();   // no Bootstrap to render the modal — just bail home
        }
    }

    function start() {
        if (started) return;
        // Player profile ([data-player], carries userId) or host play page
        // ([data-play], no userId — prompt handlers there self-filter / aren't
        // registered). Only gameId is required to connect.
        const root = document.querySelector('[data-player], [data-play]');
        if (!root || typeof signalR === 'undefined') return;

        const gameId = root.dataset.gameId;
        if (!gameId) return;
        const userId = root.dataset.userId || null;

        started = true;
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/game-play?gameId=' + encodeURIComponent(gameId))
            .withAutomaticReconnect()
            .build();

        ctx = {
            gameId: gameId,
            userId: userId,
            submit: (stamp, response) => connection.invoke('SubmitPrompt', stamp, response),
            refresh: refresh
        };

        connection.on('PromptOpened', dispatchOpen);
        connection.on('PromptClosed', (msg) => { if (msg) dispatchClose(msg.promptId); });
        connection.on('GameFaulted', (msg) => showFatalError(msg && msg.message));
        // Game over: the server finished the game and persisted the result. Every client
        // (host tablet + phones) moves to the finished-game results page.
        connection.on('GameCompleted', (msg) => {
            const id = (msg && msg.gameId) || gameId;
            window.location.href = '/Games/Finished/' + encodeURIComponent(id);
        });
        queued.forEach(h => connection.on(h.event, h.callback));

        // Re-sync the open prompt after a dropped connection is restored.
        connection.onreconnected(refresh);

        connection.start()
            .then(refresh)
            .catch(err => console.error('Game play hub failed to connect:', err));
    }

    window.GamePlayHub = { registerPrompt: registerPrompt, observePrompts: observePrompts, on: on, invoke: invoke };

    // Defer start until handler modules loaded after this script have registered.
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else setTimeout(start, 0);
})();