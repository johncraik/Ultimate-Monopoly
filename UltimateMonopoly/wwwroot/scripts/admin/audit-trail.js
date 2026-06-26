// Audit Trail — AJAX search / action-filter / pagination over the _AuditTable partial. Mirrors the other
// admin list scripts. Row expand/collapse is Bootstrap's delegated data-API and per-key/​id copy is copy.js
// (both delegated on document), so they keep working after the table partial is swapped in.
(function () {
    const container = document.getElementById('audit-table');
    const form = document.getElementById('audit-filters');
    if (!container || !form) return;

    const searchInput = form.querySelector('input[name="Search"]');
    let debounce;

    // The system trail has no {userId} route segment — it's carried as ?system=true, so preserve it
    // (the path-only keep below doesn't cover query flags). Constant for the page's lifetime.
    const systemFlag = new URLSearchParams(location.search).get('system');

    function tableUrl(page) {
        const fd = new FormData(form);
        const params = new URLSearchParams();
        params.set('handler', 'Table');
        const s = (fd.get('Search') || '').toString().trim();
        if (s) params.set('Search', s);
        const a = (fd.get('Action') || '').toString();
        if (a) params.set('Action', a);
        if (systemFlag) params.set('system', systemFlag);
        params.set('pageNumber', page || 1);
        // location.pathname keeps the {userId} route segment; only the query state changes.
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

    // Action radios → re-fetch from page 1.
    form.querySelectorAll('input[type="radio"]').forEach(r => r.addEventListener('change', () => refetch(1)));

    // Search — debounced per keystroke; the button is the explicit/no-JS fallback.
    if (searchInput) {
        searchInput.addEventListener('input', () => {
            clearTimeout(debounce);
            debounce = setTimeout(() => refetch(1), 300);
        });
    }
    form.addEventListener('submit', (e) => { e.preventDefault(); refetch(1); });

    // Pagination — upgrade the link's full-page href to a partial fetch.
    container.addEventListener('click', (e) => {
        const link = e.target.closest('.pagination a');
        if (!link) return;
        e.preventDefault();
        const url = new URL(link.href, location.href);
        url.searchParams.set('handler', 'Table');
        // The pagination href is built without the system flag — re-add it (see tableUrl).
        if (systemFlag) url.searchParams.set('system', systemFlag);
        load(url.toString());
    });
})();