// Navbar: expose the bar height as a CSS var, and drive the "mega" nav dropdowns.

(() => {
    const root = document.documentElement;
    const nav = document.querySelector('.navbar-glass');
    if (!nav) return;

    const mainNav = nav.querySelector('.navbar-collapse');

    // --- Bar height -> --app-navbar-height (used by .sticky-tabs). Skip while the
    //     burger menu is expanded so the value stays pinned to the BAR. ---
    const update = () => {
        if (mainNav && mainNav.classList.contains('show')) return;
        root.style.setProperty('--app-navbar-height', nav.offsetHeight + 'px');
    };
    update();
    window.addEventListener('resize', update);
    document.fonts?.ready?.then(update);
    new ResizeObserver(update).observe(nav);
    mainNav?.addEventListener('transitionend', update);

    // --- Mega dropdowns: tap/click toggle everywhere; on hover-capable devices,
    //     open on pointer-enter and close shortly after it leaves both the button
    //     and the panel (the delay bridges the gap between them). ✕, outside-click
    //     and Escape close. One open at a time. ---
    const megas = [...nav.querySelectorAll('[data-mega]')];
    if (!megas.length) return;

    const canHover = window.matchMedia('(hover: hover)').matches;

    const close = (m) => {
        m.classList.remove('open');
        m.querySelector('[data-mega-toggle]')?.setAttribute('aria-expanded', 'false');
    };
    const closeAll = (except) => megas.forEach(m => { if (m !== except) close(m); });

    megas.forEach(m => {
        const toggle = m.querySelector('[data-mega-toggle]');
        const card = m.querySelector('.nav-mega-card');
        let timer;

        const open = () => {
            clearTimeout(timer);
            closeAll(m);
            m.classList.add('open');
            toggle?.setAttribute('aria-expanded', 'true');
        };
        const later = () => { clearTimeout(timer); timer = setTimeout(() => close(m), 160); };
        const keep = () => clearTimeout(timer);

        toggle?.addEventListener('click', (e) => {
            e.preventDefault();
            m.classList.contains('open') ? close(m) : open();
        });
        m.querySelector('[data-mega-close]')?.addEventListener('click', () => close(m));

        if (canHover) {
            toggle?.addEventListener('mouseenter', open);
            toggle?.addEventListener('mouseleave', later);
            card?.addEventListener('mouseenter', keep);
            card?.addEventListener('mouseleave', later);
        }
    });

    document.addEventListener('click', (e) => {
        if (!e.target.closest('[data-mega]')) closeAll(null);
    });
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') closeAll(null);
    });
})();
