// Acquire-property prompt handler — renders _AcquirePropertyPrompt.cshtml from the
// engine's AcquirePropertyPrompt and submits the binary confirm / decline choice.
// It's a generic confirmation for any property action: the prompt's Type
// (AcquirePropertyType) drives the title + button wording (Buy → Buy/Auction,
// Reserve → Reserve/Ignore, every other action → <action>/Cancel). Colours the card
// by the property's set (the index→slug map emitted by the partial). Plugs into the
// game-play hub coordinator (../game-play-hub.js), which owns the connection and
// routes PromptOpened / PromptClosed here. The element ids / colour-map id below
// mirror _AcquirePropertyPrompt.cshtml.
(function () {
    'use strict';

    if (!window.GamePlayHub || typeof bootstrap === 'undefined') return;

    const modalEl = document.getElementById('acquirePropertyModal');
    if (!modalEl) return;   // partial not on this page — nothing to handle

    const modal = new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false });
    const contentEl = document.getElementById('acquirePropertyContent');
    const headerEl = document.getElementById('acquirePropertyHeader');
    const titleEl = document.getElementById('acquirePropertyTitle');
    const bodyEl = document.getElementById('acquirePropertyBody');
    const errEl = document.getElementById('acquirePropertyError');
    const acceptBtn = document.getElementById('acquirePropertyAccept');
    const declineBtn = document.getElementById('acquirePropertyDecline');

    // Board index → set slug (e.g. "dark-blue", "station"), emitted server-side by
    // the partial via PropertySetHelper. Drives the card colour per prompt.
    let colours = {};
    try {
        const el = document.getElementById('acquirePropertyColours');
        if (el) colours = JSON.parse(el.textContent || '{}');
    } catch (e) {
        console.error('Failed to parse acquire-property colours:', e);
    }

    // AcquirePropertyType → wording. Keyed by the enum's numeric value (SignalR's
    // default JSON protocol serialises enums by value, not name). Order mirrors
    // MP.GameEngine ... PromptTypes.AcquirePropertyType — keep in sync.
    // Only Buy/Reserve have a meaningful "decline" side-effect (auction / ignore);
    // every other action is a confirm whose decline is a plain cancel.
    const ACTIONS = {
        0:  { title: 'Buy property',          accept: 'Buy',          decline: 'Auction' },
        1:  { title: 'Reserve property',      accept: 'Reserve',      decline: 'Ignore' },
        2:  { title: 'Un-reserve property',   accept: 'Un-reserve',   decline: 'Cancel' },
        3:  { title: 'Mortgage property',     accept: 'Mortgage',     decline: 'Cancel' },
        4:  { title: 'Un-mortgage property',  accept: 'Un-mortgage',  decline: 'Cancel' },
        5:  { title: 'Build',                 accept: 'Build',        decline: 'Cancel' },
        6:  { title: 'Build set',             accept: 'Build set',    decline: 'Cancel' },
        7:  { title: 'Build all',             accept: 'Build all',    decline: 'Cancel' },
        8:  { title: 'Sell',                  accept: 'Sell',         decline: 'Cancel' },
        9:  { title: 'Sell set',              accept: 'Sell set',     decline: 'Cancel' },
        10: { title: 'Sell all',              accept: 'Sell all',     decline: 'Cancel' }
    };

    let current = null;   // { promptId, stamp }
    let ctx = null;       // hub context, captured when the prompt opens

    function showError(message) {
        errEl.textContent = message;
        errEl.classList.remove('d-none');
    }

    // Reset to the base classes (drops any prop-colour applied by a prior prompt),
    // then colour by the property's set — falling back to primary if the index has
    // no mapped set (shouldn't happen: every acquirable space has one).
    function applyColour(boardIndex) {
        headerEl.className = 'modal-header border-0';
        contentEl.className = 'modal-content border border-2';

        const slug = colours[String(boardIndex)];
        if (slug) {
            headerEl.classList.add('text-bg-prop-' + slug);
            contentEl.classList.add('border-prop-' + slug);
        } else {
            headerEl.classList.add('text-bg-primary');
            contentEl.classList.add('border-primary');
        }
    }

    function onOpen(prompt, stamp, hubCtx) {
        // Phones (carry a userId) render only their own player's prompt; the host
        // tablet (no userId — the controller) renders every prompt and answers on a
        // player's behalf server-side via host-bypass.
        if (hubCtx.userId && prompt.playerId !== hubCtx.userId) return;

        ctx = hubCtx;
        current = { promptId: prompt.promptId, stamp: stamp };

        const action = ACTIONS[Number(prompt.type)] || ACTIONS[0];
        titleEl.textContent = prompt.title || action.title;
        bodyEl.textContent = prompt.body || '';
        errEl.classList.add('d-none');

        // Wording per action type (server sets the matching Type).
        acceptBtn.textContent = action.accept;
        declineBtn.textContent = action.decline;

        applyColour(prompt.boardIndex);
        modal.show();
    }

    function onClose(promptId) {
        // Ignore a close for a prompt we're no longer showing.
        if (current && promptId && current.promptId !== promptId) return;
        current = null;
        modal.hide();
    }

    async function submit(accept) {
        if (!current || !ctx) return;

        const response = {
            '$type': 'AcquireProperty',
            promptId: current.promptId,
            accept: accept
        };

        acceptBtn.disabled = true;
        declineBtn.disabled = true;
        try {
            const ok = await ctx.submit(current.stamp, response);
            if (ok) {
                onClose(current.promptId);
            } else {
                showError('That could not be accepted — your view may be out of date. Refreshing…');
                await ctx.refresh();
            }
        } catch (e) {
            console.error('SubmitPrompt failed:', e);
            showError('Something went wrong.');
        } finally {
            acceptBtn.disabled = false;
            declineBtn.disabled = false;
        }
    }

    acceptBtn.addEventListener('click', () => submit(true));
    declineBtn.addEventListener('click', () => submit(false));

    window.GamePlayHub.registerPrompt({
        type: 'AcquireProperty',
        onOpen: onOpen,
        onClose: onClose
    });
})();
