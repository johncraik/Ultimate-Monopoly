(function () {
    'use strict';

    const lobby = document.querySelector('[data-lobby]');
    if (!lobby || typeof signalR === 'undefined') return;

    const gameId = lobby.dataset.gameId;
    const userId = lobby.dataset.userId;
    if (!gameId || !userId) return;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/game-setup?gameId=' + encodeURIComponent(gameId))
        .withAutomaticReconnect()
        .build();

    function selectDie(name, value) {
        lobby.querySelectorAll('input[name="' + name + '"]').forEach((radio) => {
            radio.checked = Number(radio.value) === Number(value);
        });
    }

    const notify = (message, type) =>
        window.showFloatingAlert && window.showFloatingAlert(message, type);

    connection.on('PlayerDiceSet', (changedUserId, dice1, dice2) => {
        if (changedUserId !== userId) return;

        const display = lobby.querySelector('[data-dice-display]');
        if (display) {
            display.innerHTML = '<span class="badge text-bg-secondary">' + dice1 + ' | ' + dice2 + '</span>';
        }

        selectDie('dice1', dice1);
        selectDie('dice2', dice2);

        notify('Your dice numbers were set.', 'success');
    });

    connection.on('Kicked', () => {
        notify('You have been removed from the game.', 'warning');
        setTimeout(() => { window.location.href = '/'; }, 3000);
    });

    connection.start().catch(err => console.error('Game lobby hub failed to connect:', err));
})();