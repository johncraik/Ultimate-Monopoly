// Game Settings — each [data-setting-toggle] switch enables/disables its dependent retention input(s),
// named by a CSS selector in data-target (comma-separated for more than one). Purely cosmetic/UX: the
// server also nulls a disabled toggle's retention, so a disabled (non-posting) input is harmless.
(function () {
    const toggles = document.querySelectorAll('[data-setting-toggle]');
    if (!toggles.length) return;

    function sync(toggle) {
        const targets = document.querySelectorAll(toggle.dataset.target);
        targets.forEach(t => {
            t.disabled = !toggle.checked;
            t.classList.toggle('opacity-50', !toggle.checked);
        });
    }

    toggles.forEach(toggle => {
        sync(toggle);
        toggle.addEventListener('change', () => sync(toggle));
    });
})();
