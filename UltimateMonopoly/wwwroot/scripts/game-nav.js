(() => {
    'use strict';

    // ---- Floating status alerts: auto-dismiss after 5s ----
    function autoDismiss(alertEl) {
        setTimeout(() => bootstrap.Alert.getOrCreateInstance(alertEl).close(), 5000);
    }

    const serverAlert = document.querySelector('[data-floating-alert] .alert');
    if (serverAlert) autoDismiss(serverAlert);

    const alertClasses = {
        success: 'alert-success',
        danger: 'alert-danger',
        warning: 'alert-warning',
        info: 'alert-info'
    };

    // Exposed so live (SignalR) updates can raise the same toast as a POST.
    window.showFloatingAlert = (message, type = 'info') => {
        let host = document.querySelector('[data-floating-alert]');
        if (!host) {
            host = document.createElement('div');
            host.className = 'position-fixed top-0 end-0 p-3';
            host.style.zIndex = '1090';
            host.dataset.floatingAlert = '';
            document.body.appendChild(host);
        }

        const alertEl = document.createElement('div');
        alertEl.className = `alert ${alertClasses[type] || alertClasses.info} alert-dismissible fade show`;
        alertEl.setAttribute('role', 'alert');
        alertEl.textContent = message;

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'btn-close';
        close.dataset.bsDismiss = 'alert';
        close.setAttribute('aria-label', 'Close');
        alertEl.appendChild(close);

        host.appendChild(alertEl);
        autoDismiss(alertEl);
    };

    const nav = document.querySelector('[data-game-nav]');
    const modalEl = document.getElementById('navConfirmModal');
    if (!nav || !modalEl) return;

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    let pendingNav = null;

    // Any nav-bar link or the Go Back button is gated by a leave-confirmation.
    nav.querySelectorAll('a[href], [data-nav-back]').forEach((el) => {
        el.addEventListener('click', (e) => {
            e.preventDefault();
            pendingNav = el.hasAttribute('data-nav-back')
                ? () => history.back()
                : () => { window.location.href = el.getAttribute('href'); };
            modal.show();
        });
    });

    modalEl.querySelector('[data-nav-confirm-go]').addEventListener('click', () => {
        modal.hide();
        if (pendingNav) pendingNav();
    });
})();