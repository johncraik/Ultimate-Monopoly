// Turn Tax — per-bracket "Clear" buttons zero that bracket's threshold + rate inputs (client-side only;
// the change isn't persisted until Save). Each button lives in a row marked [data-bracket].
(function () {
    document.querySelectorAll('[data-clear-bracket]').forEach(btn => {
        btn.addEventListener('click', () => {
            const row = btn.closest('[data-bracket]');
            if (!row) return;
            row.querySelectorAll('input[type="number"]').forEach(input => { input.value = 0; });
        });
    });
})();