// A user's notifications — AJAX search / type / read-state / status filter / pagination over the
// _NotificationsTable partial. The notification rows expand (Bootstrap delegated) to their read/unread log
// table; copy.js handles id-copy — both survive the partial swap. location.pathname keeps the {userId}
// route segment.
(function () {
    const container = document.getElementById('notifications-table');
    const form = document.getElementById('notif-filters');
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
        const read = (fd.get('Read') || '').toString();
        if (read) params.set('Read', read);
        const status = (fd.get('Status') || '').toString();
        if (status) params.set('Status', status);
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

    container.addEventListener('click', (e) => {
        const link = e.target.closest('.pagination a');
        if (!link) return;
        e.preventDefault();
        const url = new URL(link.href, location.href);
        url.searchParams.set('handler', 'Table');
        load(url.toString());
    });
})();
