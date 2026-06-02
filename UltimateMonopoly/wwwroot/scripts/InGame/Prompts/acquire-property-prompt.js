// Acquire-property prompt handler — renders _AcquirePropertyPrompt.cshtml from the
// engine's AcquirePropertyPrompt and submits the take-it / decline choice. Covers
// BUY (decline → auction) and RESERVE (decline → no-op) via the prompt's IsReserve
// flag, and colours the card by the property's set (the index→slug map emitted by
// the partial). Plugs into the game-play hub coordinator (../game-play-hub.js),
// which owns the connection and routes PromptOpened / PromptClosed here. The element
// ids / colour-map id below mirror _AcquirePropertyPrompt.cshtml.
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

        titleEl.textContent = prompt.title || (prompt.isReserve ? 'Reserve property' : 'Buy property');
        bodyEl.textContent = prompt.body || '';
        errEl.classList.add('d-none');

        // Wording differs by mode: buy → Buy / Auction; reserve → Reserve / Ignore.
        acceptBtn.textContent = prompt.isReserve ? 'Reserve' : 'Buy';
        declineBtn.textContent = prompt.isReserve ? 'Ignore' : 'Auction';

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
