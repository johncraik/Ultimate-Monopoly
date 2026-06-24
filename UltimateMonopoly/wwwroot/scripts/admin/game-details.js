// Game Details — read-only state drawer. Loads (and reloads on turn/player change) the reused player
// profile partial into #game-state-body. No game-play hub is registered, so the rendered view is inert;
// the only client behaviour here is selecting a turn/player and the in-tab deal toggle (for inspection).
(function () {
    const body = document.getElementById('game-state-body');
    if (!body) return;

    const gameId = body.getAttribute('data-game-id');
    const turnSel = document.getElementById('state-turn-select');
    const playerSel = document.getElementById('state-player-select');

    const spinner = '<div class="text-center text-body-secondary py-5"><span class="spinner-border spinner-border-sm me-2"></span>Loading…</div>';
    const errorHtml = '<div class="text-center text-danger py-5">Couldn\'t load this turn.</div>';

    async function load() {
        const params = new URLSearchParams();
        params.set('handler', 'TurnState');
        params.set('gameId', gameId);
        if (turnSel) params.set('turnNumber', turnSel.value);
        if (playerSel && playerSel.value) params.set('playerId', playerSel.value);

        body.innerHTML = spinner;
        try {
            const res = await fetch(location.pathname + '?' + params.toString(), { headers: { 'X-Requested-With': 'fetch' } });
            body.innerHTML = res.ok ? await res.text() : errorHtml;
        } catch {
            body.innerHTML = errorHtml;
        }
    }

    if (turnSel) turnSel.addEventListener('change', load);
    if (playerSel) playerSel.addEventListener('change', load);

    // Deal tab toggle — replicate the live deal-tab.js view switch (no hub): clicking a candidate reveals
    // their builder block; Cancel returns to the list. Delegated, since the body is swapped on each load.
    body.addEventListener('click', (e) => {
        const target = e.target.closest('[data-deal-target]');
        if (target) {
            const id = target.getAttribute('data-deal-target');
            body.querySelector('[data-deal-list]')?.classList.add('d-none');
            body.querySelector(`[data-deal-block="${CSS.escape(id)}"]`)?.classList.remove('d-none');
            return;
        }
        if (e.target.closest('[data-deal-cancel]')) {
            body.querySelectorAll('[data-deal-block]').forEach(b => b.classList.add('d-none'));
            body.querySelector('[data-deal-list]')?.classList.remove('d-none');
        }
    });

    load();
})();