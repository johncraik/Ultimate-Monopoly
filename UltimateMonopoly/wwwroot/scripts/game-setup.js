(() => {
    'use strict';

    // ---- Set Dice modal: populate from the clicked card's button ----
    const diceModal = document.getElementById('setDiceModal');
    if (diceModal) {
        diceModal.addEventListener('show.bs.modal', (event) => {
            const btn = event.relatedTarget;
            if (!btn) return;
            diceModal.querySelector('[data-dice-target]').value = btn.dataset.userId || '';
            diceModal.querySelector('[data-dice-player]').textContent =
                `Dice numbers for ${btn.dataset.name || 'this player'}.`;
            diceModal.querySelector('[data-dice1]').value = btn.dataset.dice1 || '';
            diceModal.querySelector('[data-dice2]').value = btn.dataset.dice2 || '';
        });
    }

    // ---- Floating status alert: auto-dismiss after 5s ----
    const statusAlert = document.querySelector('[data-setup-alert] .alert');
    if (statusAlert) {
        setTimeout(() => {
            bootstrap.Alert.getOrCreateInstance(statusAlert).close();
        }, 5000);
    }

    // ---- Drag reorder (host only) ----
    const list = document.querySelector('[data-player-list]');
    const reorderForm = document.querySelector('[data-reorder-form]');
    if (!list || !reorderForm || list.dataset.canReorder !== 'true' || typeof Sortable === 'undefined') return;

    Sortable.create(list, {
        animation: 180,
        easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
        forceFallback: true,
        ghostClass: 'player-card-ghost',
        dragClass: 'player-card-drag',
        onEnd: (evt) => {
            if (evt.oldIndex !== evt.newIndex) submitOrder();
        }
    });

    function submitOrder() {
        reorderForm.querySelectorAll('input[name="orderedUserIds"]').forEach((i) => i.remove());
        list.querySelectorAll('[data-player-card]').forEach((card) => {
            const input = document.createElement('input');
            input.type = 'hidden';
            input.name = 'orderedUserIds';
            input.value = card.dataset.userId;
            reorderForm.appendChild(input);
        });
        reorderForm.submit();
    }
})();