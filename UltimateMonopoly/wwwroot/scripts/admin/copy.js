// Generic click-to-copy: any element with [data-copy] copies that value (or its text) to the clipboard
// on click, flashing the .copied state (see admin.css). CSP-safe (external file, delegated listener).
document.addEventListener('click', async (e) => {
    const el = e.target.closest('[data-copy]');
    if (!el) return;
    const value = el.getAttribute('data-copy') || el.textContent.trim();
    if (!value) return;
    try {
        await navigator.clipboard.writeText(value);
        el.classList.add('copied');
        setTimeout(() => el.classList.remove('copied'), 1200);
    } catch { /* clipboard unavailable — no-op */ }
});
