// Game Management — AJAX filter / search / pagination over the _GamesTable partial.
// Two filter radios (state, outcome) and two search boxes (normal + exact host id) re-fetch the table;
// pagination links are upgraded from a full-page reload (the no-JS fallback) to a partial fetch.
(function () {
    const container = document.getElementById('games-table');
    const form = document.getElementById('game-filters');
    if (!container || !form) return;

    let debounce;

    function tableUrl(page) {
        const fd = new FormData(form);
        const params = new URLSearchParams();
        params.set('handler', 'Table');

        const state = (fd.get('State') || '').toString();
        if (state) params.set('State', state);
        const outcome = (fd.get('Outcome') || '').toString();
        if (outcome) params.set('Outcome', outcome);

        const s = (fd.get('Search') || '').toString().trim();
        if (s) params.set('Search', s);
        const host = (fd.get('HostId') || '').toString().trim();
        if (host) params.set('HostId', host);

        params.set('pageNumber', page || 1);
        return location.pathname + '?' + params.toString();
    }

    async function load(url) {
        container.classList.add('admin-table-loading');
        try {
            const res = await fetch(url, { headers: { 'X-Requested-With': 'fetch' } });
            if (res.ok) container.innerHTML = await res.text();
        } finally {
            container.classList.remove('admin-table-loading');
        }
    }

    const refetch = (page) => load(tableUrl(page));

    // Filter radios → re-fetch from page 1.
    form.querySelectorAll('input[type="radio"]').forEach(r =>
        r.addEventListener('change', () => refetch(1)));

    // Both search boxes — each keystroke (debounced); the search button is the explicit/no-JS fallback.
    form.querySelectorAll('input[type="search"]').forEach(input => {
        input.addEventListener('input', () => {
            clearTimeout(debounce);
            debounce = setTimeout(() => refetch(1), 300);
        });
    });
    form.addEventListener('submit', (e) => { e.preventDefault(); refetch(1); });

    // Pagination — upgrade the link's full-page href to a partial fetch.
    container.addEventListener('click', (e) => {
        const link = e.target.closest('.pagination a');
        if (!link) return;
        e.preventDefault();
        const url = new URL(link.href, location.href);
        url.searchParams.set('handler', 'Table');
        load(url.toString());
    });

    // Click-to-copy the (truncated) game / host id; flashes "Copied!".
    container.addEventListener('click', async (e) => {
        const el = e.target.closest('.user-id-copy');
        if (!el) return;
        const id = el.getAttribute('data-copy-id');
        if (!id) return;
        try {
            await navigator.clipboard.writeText(id);
            el.classList.add('copied');
            setTimeout(() => el.classList.remove('copied'), 1200);
        } catch { /* clipboard unavailable — no-op */ }
    });
})();