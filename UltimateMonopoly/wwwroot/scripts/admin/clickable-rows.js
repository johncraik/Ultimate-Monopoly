// Makes any table row carrying data-href navigate to it on click — used by the admin tables.
// Delegated on the document so it works for every admin table (incl. partials re-fetched over AJAX).
// Clicks that land on a genuine interactive element (link, button, the id-copy code, form controls)
// are left alone, so the per-row view buttons, pagination, dropdowns, and click-to-copy still work.
(function () {
    document.addEventListener('click', (e) => {
        if (e.target.closest('a, button, input, label, .user-id-copy')) return;

        const row = e.target.closest('tr[data-href]');
        if (!row) return;

        window.location.href = row.getAttribute('data-href');
    });
})();