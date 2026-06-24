// Rule Management — AJAX search over the grouped _RulesTable partial (no filters, no pagination).
// The search box re-fetches the table; row clicks are handled globally by clickable-rows.js.
(function () {
    const container = document.getElementById('rules-table');
    const form = document.getElementById('rule-filters');
    if (!container || !form) return;

    const searchInput = form.querySelector('input[name="Search"]');
    let debounce;

    function tableUrl() {
        const fd = new FormData(form);
        const params = new URLSearchParams();
        params.set('handler', 'Table');
        const s = (fd.get('Search') || '').toString().trim();
        if (s) params.set('Search', s);
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

    // Search — each keystroke (debounced); the search button is the explicit/no-JS fallback.
    if (searchInput) {
        searchInput.addEventListener('input', () => {
            clearTimeout(debounce);
            debounce = setTimeout(() => load(tableUrl()), 300);
        });
    }
    form.addEventListener('submit', (e) => { e.preventDefault(); load(tableUrl()); });
})();