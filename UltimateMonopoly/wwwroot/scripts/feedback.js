// Production "Give Feedback" modal — posts { type, description, metadata } to /BugReport (the same endpoint
// and JSON shape the dev <bug-reporter> widget uses). Antiforgery via the app's csrf meta tags.
(function () {
    const body = document.querySelector('[data-feedback]');
    if (!body) return;

    const modalEl = document.getElementById('feedbackModal');
    const typeSel = document.getElementById('feedback-type');
    const desc = document.getElementById('feedback-desc');
    const alertEl = body.querySelector('[data-feedback-alert]');
    const submitBtn = document.querySelector('[data-feedback-submit]');

    const headerName = document.querySelector('meta[name="csrf-header"]')?.content;
    const token = document.querySelector('meta[name="csrf-token"]')?.content;

    function showAlert(msg, kind) {
        alertEl.className = 'alert alert-' + kind + ' py-2 small';
        alertEl.textContent = msg;
    }

    submitBtn.addEventListener('click', async () => {
        const text = (desc.value || '').trim();
        if (!text) { showAlert('Please enter a description.', 'warning'); return; }

        submitBtn.disabled = true;
        showAlert('Submitting…', 'secondary');

        const headers = { 'Content-Type': 'application/json' };
        if (headerName && token) headers[headerName] = token;

        try {
            const res = await fetch(body.getAttribute('data-endpoint'), {
                method: 'POST',
                headers,
                body: JSON.stringify({
                    type: typeSel.value,
                    description: text,
                    metadata: body.getAttribute('data-metadata')
                })
            });
            if (!res.ok) throw new Error('failed');

            showAlert('Thank you for your feedback! An administrator will be in touch.', 'success');
            desc.value = '';
            setTimeout(() => {
                if (window.bootstrap && modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).hide();
                if (alertEl) alertEl.className = 'd-none';
                submitBtn.disabled = false;
            }, 2500);
        } catch {
            showAlert('Something went wrong. Please try again.', 'danger');
            submitBtn.disabled = false;
        }
    });

    // Reset transient state each time the modal opens.
    modalEl?.addEventListener('show.bs.modal', () => {
        if (alertEl) alertEl.className = 'd-none';
        submitBtn.disabled = false;
    });
})();
