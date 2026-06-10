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

    function lockDownPage() {
        document.querySelectorAll('button, input[type="submit"]').forEach(el => el.disabled = true);
        document.addEventListener('submit', e => e.preventDefault(), true);
        connection.stop().catch(() => { /* already tearing down */ });
    }

    function showBlockingAlert(message) {
        const overlay = document.createElement('div');
        overlay.className = 'position-fixed top-0 start-0 w-100 h-100 d-flex align-items-center justify-content-center';
        overlay.style.zIndex = '2000';
        overlay.style.background = 'rgba(0, 0, 0, 0.6)';

        const box = document.createElement('div');
        box.className = 'alert alert-warning shadow-lg text-center fs-5 m-4 px-4 py-4';
        box.style.maxWidth = '420px';
        box.setAttribute('role', 'alert');
        box.textContent = message;

        overlay.appendChild(box);
        document.body.appendChild(overlay);
    }

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
        lockDownPage();
        showBlockingAlert('You have been removed from the game.');
        setTimeout(() => { window.location.href = '/'; }, 3000);
    });

    connection.on('GameStarted', () => {
        window.location.href = '/player-profile/'
            + encodeURIComponent(gameId) + '/' + encodeURIComponent(userId);
    });

    // Host cancelled the game while we waited in the lobby — lock the page and head home.
    connection.on('GameCancelled', () => {
        lockDownPage();
        showBlockingAlert('The game was cancelled by the host.');
        setTimeout(() => { window.location.href = '/Index'; }, 3000);
    });

    connection.start().catch(err => console.error('Game lobby hub failed to connect:', err));
})();