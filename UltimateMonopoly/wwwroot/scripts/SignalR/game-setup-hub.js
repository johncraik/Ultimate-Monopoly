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

    const minPlayers = parseInt(list.dataset.minPlayers, 10) || 0;

    function updatePlayersHeader() {
        const cards = list.querySelectorAll('[data-player-card]');
        const count = cards.length;

        const countEl = document.querySelector('[data-player-count]');
        if (countEl) countEl.textContent = count;

        const enoughPlayers = count >= minPlayers;
        const allDiceSet = count > 0 && [...cards].every(c => c.dataset.diceSet === 'true');
        const ready = enoughPlayers && allDiceSet;

        const badge = countEl ? countEl.closest('.badge') : null;
        if (badge) {
            badge.classList.toggle('text-bg-success', enoughPlayers);
            badge.classList.toggle('text-bg-danger', !enoughPlayers);
        }

        const banner = document.querySelector('[data-readiness-banner]');
        if (banner) {
            banner.classList.toggle('alert-success', ready);
            banner.classList.toggle('alert-danger', !ready);
            banner.textContent = !enoughPlayers
                ? 'Not enough players — at least ' + minPlayers + ' are needed to start.'
                : !allDiceSet
                    ? 'All players must set their dice numbers before the game can start.'
                    : 'Ready to Start';
        }
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
            updatePlayersHeader();
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

        card.dataset.diceSet = 'true';
        updatePlayersHeader();
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

    connection.on('GameStarted', () => {
        window.location.href = '/Game/Play/' + encodeURIComponent(gameId);
    });

    connection.start().catch(err => console.error('Game setup hub failed to connect:', err));
})();