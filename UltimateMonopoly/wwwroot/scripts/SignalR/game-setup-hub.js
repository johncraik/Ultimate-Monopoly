(function () {
    'use strict';

    const list = document.querySelector('[data-player-list]');
    if (!list || typeof signalR === 'undefined') return;

    const gameId = list.dataset.gameId;
    if (!gameId) return;

    const escapeAttr = (v) =>
        window.CSS && CSS.escape ? CSS.escape(v) : String(v).replace(/["\\]/g, '\\$&');

    const cardFor = (userId) =>
        list.querySelector('[data-player-card][data-user-id="' + escapeAttr(userId) + '"]');

    function refreshCount() {
        const countEl = document.querySelector('[data-player-count]');
        if (countEl) countEl.textContent = list.querySelectorAll('[data-player-card]').length;
    }

    const notify = (message, type) =>
        window.showFloatingAlert && window.showFloatingAlert(message, type);

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/game-setup?gameId=' + encodeURIComponent(gameId))
        .withAutomaticReconnect()
        .build();

    connection.on('PlayerJoined', async (userId) => {
        if (cardFor(userId)) return;
        try {
            const resp = await fetch('/Game/Setup/' + encodeURIComponent(gameId) +
                '?handler=PlayerCard&userId=' + encodeURIComponent(userId));
            if (!resp.ok) return;
            list.insertAdjacentHTML('beforeend', await resp.text());
            refreshCount();
            notify('A player joined the game.', 'success');
        } catch (err) {
            console.error('Failed to load joined player card:', err);
        }
    });

    connection.on('PlayerDiceSet', (userId, dice1, dice2) => {
        const card = cardFor(userId);
        if (!card) return;

        const display = card.querySelector('[data-dice-display]');
        if (display) {
            display.innerHTML = '<span class="badge text-bg-secondary">' + dice1 + ' | ' + dice2 + '</span>';
        }

        const btn = card.querySelector('[data-set-dice]');
        if (btn) {
            btn.dataset.dice1 = dice1;
            btn.dataset.dice2 = dice2;
        }

        notify('Dice numbers updated.', 'success');
    });

    connection.on('PlayerLeft', (userId) => {
        const card = cardFor(userId);
        if (!card) return;
        card.remove();
        refreshCount();
        notify('A player left the game.', 'success');
    });

    connection.on('SeatOrderChanged', (orderedUserIds) => {
        orderedUserIds.forEach((userId) => {
            const card = cardFor(userId);
            if (card) list.appendChild(card);
        });
        notify('Seat order updated.', 'success');
    });

    connection.start().catch(err => console.error('Game setup hub failed to connect:', err));
})();