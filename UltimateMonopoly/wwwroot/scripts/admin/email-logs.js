// Email logs — AJAX search / pagination over the _EmailLogsTable partial. Mirrors admin-logs.js. Row
// expand/collapse is Bootstrap's delegated data-API and id-copy is copy.js (both delegated on document),
// so they keep working after the table partial is swapped in.
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

    if (searchInput) {
        searchInput.addEventListener('input', () => {
            clearTimeout(debounce);
            debounce = setTimeout(() => refetch(1), 300);
        });
    }
    form.addEventListener('submit', (e) => { e.preventDefault(); refetch(1); });

    container.addEventListener('click', (e) => {
        const link = e.target.closest('.pagination a');
        if (!link) return;
        e.preventDefault();
        const url = new URL(link.href, location.href);
        url.searchParams.set('handler', 'Table');
        load(url.toString());
    });
})();
