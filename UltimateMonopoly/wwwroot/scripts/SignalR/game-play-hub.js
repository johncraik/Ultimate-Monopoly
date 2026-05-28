(function () {
    'use strict';

    const root = document.querySelector('[data-player]');
    if (!root || typeof signalR === 'undefined') return;

    const gameId = root.dataset.gameId;
    const userId = root.dataset.userId;          // the player this page is a profile of
    if (!gameId || !userId) return;

    const modalEl = document.getElementById('diceRollModal');
    if (!modalEl || typeof bootstrap === 'undefined') return;

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

    // The prompt currently being answered: { promptId, stamp, diceCount }.
    let current = null;

    const notify = (message, type) =>
        window.showFloatingAlert && window.showFloatingAlert(message, type);

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/game-play?gameId=' + encodeURIComponent(gameId))
        .withAutomaticReconnect()
        .build();

    function selectedValue(groupEl) {
        const checked = groupEl.querySelector('input.btn-check:checked');
        return checked ? Number(checked.value) : null;
    }

    function resetGroup(groupEl) {
        groupEl.querySelectorAll('input.btn-check').forEach(r => { r.checked = false; });
    }

    function isOurDiceRoll(prompt) {
        // Polymorphic discriminator from the engine's [JsonPolymorphic] base.
        return prompt && prompt['$type'] === 'DiceRoll' && prompt.playerId === userId;
    }

    function showDicePrompt(prompt, stamp) {
        const diceCount = Number(prompt.diceCount) || 3;
        current = { promptId: prompt.promptId, stamp: stamp, diceCount: diceCount };

        titleEl.textContent = prompt.title || 'Roll the dice';
        bodyEl.textContent = prompt.body || '';
        errEl.classList.add('d-none');

        for (let i = 1; i <= 3; i++) {
            const wrap = groups[i].closest('[data-die-wrap]');
            resetGroup(groups[i]);
            wrap.classList.toggle('d-none', i > diceCount);
        }

        diceModal.show();
    }

    function hideDicePrompt(promptId) {
        // Ignore a close for a prompt we're no longer showing.
        if (current && promptId && current.promptId !== promptId) return;
        current = null;
        diceModal.hide();
    }

    async function refreshPrompt() {
        try {
            const msg = await connection.invoke('GetCurrentPrompt');
            if (msg && isOurDiceRoll(msg.prompt)) showDicePrompt(msg.prompt, msg.concurrencyStamp);
            else hideDicePrompt(null);
        } catch (e) {
            console.error('GetCurrentPrompt failed:', e);
        }
    }

    connection.on('PromptOpened', (msg) => {
        if (msg && isOurDiceRoll(msg.prompt)) showDicePrompt(msg.prompt, msg.concurrencyStamp);
    });

    connection.on('PromptClosed', (msg) => {
        if (msg) hideDicePrompt(msg.promptId);
    });

    rollBtn.addEventListener('click', async () => {
        if (!current) return;

        const d1 = selectedValue(groups[1]);
        const d2 = current.diceCount >= 2 ? selectedValue(groups[2]) : null;
        const d3 = current.diceCount === 3 ? selectedValue(groups[3]) : null;

        if (d1 === null
            || (current.diceCount >= 2 && d2 === null)
            || (current.diceCount === 3 && d3 === null)) {
            errEl.textContent = 'Select a value for each die.';
            errEl.classList.remove('d-none');
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
            const ok = await connection.invoke('SubmitPrompt', current.stamp, response);
            if (ok) {
                hideDicePrompt(current.promptId);
            } else {
                errEl.textContent = 'That roll could not be accepted — your view may be out of date. Refreshing…';
                errEl.classList.remove('d-none');
                await refreshPrompt();
            }
        } catch (e) {
            console.error('SubmitPrompt failed:', e);
            errEl.textContent = 'Something went wrong submitting your roll.';
            errEl.classList.remove('d-none');
        } finally {
            rollBtn.disabled = false;
        }
    });

    connection.start()
        .then(refreshPrompt)
        .catch(err => console.error('Game play hub failed to connect:', err));
})();