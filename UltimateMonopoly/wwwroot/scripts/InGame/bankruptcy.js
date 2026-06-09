// Declare-bankruptcy confirmation flow. The trigger button lives inside the
// live-swapped player profile and opens the static #bankruptcyConfirmModal via
// Bootstrap's delegated data-bs-toggle, carrying the target player's id in
// data-bankrupt-target. We read that id off the show event's relatedTarget, stash
// it on the confirm button, and invoke GamePlayHub.DeclareBankruptcy on confirm.
//
// Document-delegated / event-driven so it survives the profile re-render (the
// modal itself is stable page DOM and never swapped). The hub gates server-side
// (CanDeclareBankruptcy, host-bypass aware) and enqueues on the single-writer
// executor — the engine returns the player's assets to the bank and concludes the
// game if they were the last solvent player.
(function () {
    'use strict';

    const modalEl = document.getElementById('bankruptcyConfirmModal');
    if (!modalEl) return;

    const confirmBtn = modalEl.querySelector('[data-bankruptcy-confirm]');

    // Capture which player the opening button is for (the host drawer can show any
    // player; the phone only ever shows the viewer's own profile).
    modalEl.addEventListener('show.bs.modal', function (e) {
        const trigger = e.relatedTarget;
        const playerId = trigger ? trigger.getAttribute('data-bankrupt-target') : null;
        if (confirmBtn) {
            confirmBtn.dataset.bankruptPlayer = playerId || '';
            confirmBtn.disabled = false;   // reset from any prior in-flight attempt
        }
    });

    if (!confirmBtn) return;

    confirmBtn.addEventListener('click', function () {
        const playerId = confirmBtn.dataset.bankruptPlayer;
        if (!playerId || !window.GamePlayHub) return;

        confirmBtn.disabled = true;
        GamePlayHub.invoke('DeclareBankruptcy', playerId)
            .then(function () {
                // The engine drives the rest (assets to bank, possible game-over). Close
                // the modal; the next StateChanged frame re-renders the profile.
                const modal = window.bootstrap && bootstrap.Modal.getInstance(modalEl);
                if (modal) modal.hide();
            })
            .catch(function (err) {
                console.error('DeclareBankruptcy failed:', err);
                confirmBtn.disabled = false;   // re-enable on transient failure
            });
    });
})();