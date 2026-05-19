// Measures the navbar's actual rendered height and exposes it as a CSS var
// so .sticky-tabs (and any other below-the-bar pinning) can sit flush.
(() => {
    const root = document.documentElement;
    const nav = document.querySelector('.navbar-glass');
    if (!nav) return;

    const update = () => root.style.setProperty('--app-navbar-height', nav.offsetHeight + 'px');
    update();
    window.addEventListener('resize', update);
    document.fonts?.ready?.then(update);
    new ResizeObserver(update).observe(nav);
})();