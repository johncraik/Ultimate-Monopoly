// Acknowledge prompt handler — renders _AcknowledgePrompt.cshtml from the
// engine's AcknowledgePrompt (title + body) and submits an empty
// AcknowledgeResponse on "Okay". Plugs into the game-play hub coordinator
// (../game-play-hub.js), which owns the connection and routes the
// PromptOpened / PromptClosed for this prompt type here. The element ids below
// mirror _AcknowledgePrompt.cshtml.
(function () {
    'use strict';

    if (!window.GamePlayHub || typeof bootstrap === 'undefined') return;

    const modalEl = document.getElementById('acknowledgeModal');
    if (!modalEl) return;   // partial not on this page — nothing to handle

    const modal = new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false });
    const titleEl = document.getElementById('acknowledgeTitle');
    const bodyEl = document.getElementById('acknowledgeBody');
    const errEl = document.getElementById('acknowledgeError');
    const okBtn = document.getElementById('acknowledgeSubmit');

    let current = null;   // { promptId, stamp }
    let ctx = null;       // hub context, captured when the prompt opens

    function showError(message) {
        errEl.textContent = message;
        errEl.classList.remove('d-none');
    }

    function onOpen(prompt, stamp, hubCtx) {
        // Render only for the player this profile page belongs to (the host
        // answers on a player's behalf server-side; the page is still that
        // player's profile, so the playerId check holds for both).
        if (prompt.playerId !== hubCtx.userId) return;

        ctx = hubCtx;
        current = { promptId: prompt.promptId, stamp: stamp };

        titleEl.textContent = prompt.title || 'Notice';
        bodyEl.textContent = prompt.body || '';
        errEl.classList.add('d-none');

        modal.show();
    }

    function onClose(promptId) {
        // Ignore a close for a prompt we're no longer showing.
        if (current && promptId && current.promptId !== promptId) return;
        current = null;
        modal.hide();
    }

    okBtn.addEventListener('click', async () => {
        if (!current || !ctx) return;

        // AcknowledgeResponse carries nothing but the prompt id — the pause is the value.
        const response = {
            '$type': 'Acknowledge',
            promptId: current.promptId
        };

        okBtn.disabled = true;
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
            okBtn.disabled = false;
        }
    });

    window.GamePlayHub.registerPrompt({
        type: 'Acknowledge',
        onOpen: onOpen,
        onClose: onClose
    });
})();