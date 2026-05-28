// Dice-roll prompt handler — renders _DiceRollPrompt.cshtml from the engine's
// DiceRollPrompt and submits the chosen dice. Plugs into the game-play hub
// coordinator (../game-play-hub.js), which owns the connection and routes the
// PromptOpened / PromptClosed for this prompt type here. The element ids /
// data-* hooks below mirror _DiceRollPrompt.cshtml.
(function () {
    'use strict';

    if (!window.GamePlayHub || typeof bootstrap === 'undefined') return;

    const modalEl = document.getElementById('diceRollModal');
    if (!modalEl) return;   // partial not on this page — nothing to handle

    const diceModal = new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false });
    const titleEl = document.getElementById('diceRollTitle');
    const bodyEl = document.getElementById('diceRollBody');
    const errEl = document.getElementById('diceRollError');
    const rollBtn = document.getElementById('diceRollSubmit');
    const groups = {
        1: modalEl.querySelector('[data-die="1"]'),
        2: modalEl.querySelector('[data-die="2"]'),
        3: modalEl.querySelector('[data-die="3"]')
    };

    let current = null;   // { promptId, stamp, diceCount }
    let ctx = null;       // hub context, captured when the prompt opens

    function selectedValue(groupEl) {
        const checked = groupEl.querySelector('input.btn-check:checked');
        return checked ? Number(checked.value) : null;
    }

    function resetGroup(groupEl) {
        groupEl.querySelectorAll('input.btn-check').forEach(r => { r.checked = false; });
    }

    function showError(message) {
        errEl.textContent = message;
        errEl.classList.remove('d-none');
    }

    function onOpen(prompt, stamp, hubCtx) {
        // Render only for the player this profile page belongs to. (The host
        // answers on a player's behalf server-side; the page is still that
        // player's profile, so the playerId check holds for both.)
        if (prompt.playerId !== hubCtx.userId) return;

        ctx = hubCtx;
        const diceCount = Number(prompt.diceCount) || 3;
        current = { promptId: prompt.promptId, stamp: stamp, diceCount: diceCount };

        titleEl.textContent = prompt.title || 'Roll the dice';
        bodyEl.textContent = prompt.body || '';
        errEl.classList.add('d-none');

        // Reveal Die 1..diceCount, hide the rest (1 -> Die1, 2 -> Die1+Die2,
        // 3 -> all). DiceRollResponse never wants Die1 + third without Die2.
        for (let i = 1; i <= 3; i++) {
            const wrap = groups[i].closest('[data-die-wrap]');
            resetGroup(groups[i]);
            wrap.classList.toggle('d-none', i > diceCount);
        }

        diceModal.show();
    }

    function onClose(promptId) {
        // Ignore a close for a prompt we're no longer showing.
        if (current && promptId && current.promptId !== promptId) return;
        current = null;
        diceModal.hide();
    }

    rollBtn.addEventListener('click', async () => {
        if (!current || !ctx) return;

        const d1 = selectedValue(groups[1]);
        const d2 = current.diceCount >= 2 ? selectedValue(groups[2]) : null;
        const d3 = current.diceCount === 3 ? selectedValue(groups[3]) : null;

        if (d1 === null
            || (current.diceCount >= 2 && d2 === null)
            || (current.diceCount === 3 && d3 === null)) {
            showError('Select a value for each die.');
            return;
        }

        const response = {
            '$type': 'DiceRoll',
            promptId: current.promptId,
            die1: d1,
            die2: d2,
            thirdDie: d3
        };

        rollBtn.disabled = true;
        try {
            const ok = await ctx.submit(current.stamp, response);
            if (ok) {
                onClose(current.promptId);
            } else {
                showError('That roll could not be accepted — your view may be out of date. Refreshing…');
                await ctx.refresh();
            }
        } catch (e) {
            console.error('SubmitPrompt failed:', e);
            showError('Something went wrong submitting your roll.');
        } finally {
            rollBtn.disabled = false;
        }
    });

    window.GamePlayHub.registerPrompt({
        type: 'DiceRoll',
        onOpen: onOpen,
        onClose: onClose
    });
})();