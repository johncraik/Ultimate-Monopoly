// Profile page — tab sync, avatar-card highlight, Syncfusion picker change.

(() => {
    // Keep the hidden Tab input in sync with the currently shown tab,
    // and show/hide the Update button based on the tab's data-hide-update flag.
    const tabHidden = document.querySelector('input[name="Tab"]');
    const updateBtn = document.getElementById('updateProfileBtn');
    document.querySelectorAll('#profileTabs [data-tab-value]').forEach(btn => {
        btn.addEventListener('shown.bs.tab', () => {
            if (tabHidden) tabHidden.value = btn.dataset.tabValue;
            if (updateBtn) updateBtn.classList.toggle('d-none', btn.dataset.hideUpdate === 'true');
        });
    });

    // Selected-card highlight follows the radio state
    const cards = document.querySelectorAll('.avatar-card');
    document.querySelectorAll('input[name="Input.AvatarImageName"]').forEach(input => {
        input.addEventListener('change', () => {
            cards.forEach(c => c.classList.remove('border-primary', 'border-2'));
            if (input.checked) {
                const label = document.querySelector(`label[for="${input.id}"]`);
                label?.classList.add('border-primary', 'border-2');
            }
        });
    });
})();

// Syncfusion ColorPicker change handler — syncs to the hidden field that
// is bound to Input.AvatarColour, and updates all live previews.
// Must be a top-level function so Syncfusion can resolve `change="onColourChange"`.
function onColourChange(args) {
    const colour = args.currentValue?.hex || args.value;
    const hidden = document.getElementById('AvatarColourHidden');
    if (hidden) hidden.value = colour;
    document.querySelectorAll('[data-avatar-preview]').forEach(el => {
        el.style.backgroundColor = colour;
    });
}