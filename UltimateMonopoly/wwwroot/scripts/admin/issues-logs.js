// Reported issues — AJAX search / type / status filter / pagination over the _ReportedIssuesTable partial.
// Mirrors admin-logs.js. Row expand/collapse and id-copy are delegated, so they keep working after the
// partial is swapped in.
(function () {
    const container = document.getElementById('logs-table');
    const form = document.getElementById('log-filters');
    if (!container || !form) return;

    const searchInput = form.querySelector('input[name="Search"]');
    let debounce;

    function tableUrl(page) {
        const fd = new FormData(form);
        const params = new URLSearchParams();
        params.set('handler', 'Table');
        const s = (fd.get('Search') || '').toString().trim();
        if (s) params.set('Search', s);
        const type = (fd.get('Type') || '').toString();
        if (type) params.set('Type', type);
        const closed = (fd.get('Closed') || '').toString();
        if (closed) params.set('Closed', closed);
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

    form.querySelectorAll('select').forEach(sel => sel.addEventListener('change', () => refetch(1)));

    if (searchInput) {
        searchInput.addEventListener('input', () => {
            clearTimeout(debounce);
            debounce = setTimeout(() => refetch(1), 300);
        });
    }
    form.addEventListener('submit', (e) => { e.preventDefault(); refetch(1); });

    // Bootstrap's collapse data-API fires in the CAPTURE phase on `document`, so a bubble-phase
    // stopPropagation can't beat it. Intercept on `window` (capture) — the very first listener to run for any
    // click — so a click on an in-row link (the GitHub "Synced" badge) opens the link WITHOUT toggling the
    // row's accordion. A normal row click (not on a link) falls through and toggles as usual.
    window.addEventListener('click', (e) => {
        if (e.target.closest('.audit-row a')) e.stopPropagation();
    }, true);

    container.addEventListener('click', (e) => {
        const pageLink = e.target.closest('.pagination a');
        if (!pageLink) return;
        e.preventDefault();
        const url = new URL(pageLink.href, location.href);
        url.searchParams.set('handler', 'Table');
        load(url.toString());
    });
})();
