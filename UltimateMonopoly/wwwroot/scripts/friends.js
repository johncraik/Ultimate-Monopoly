// Friends page — disable Add Friend submit on double-click, persistent view toggle.

(() => {
    // Disable the submit button on first submit so a double-click can't
    // create two friend requests (and two notifications) before the
    // server-side pending-request check sees the first row.
    const addForm = document.getElementById('add-friend-form');
    addForm?.addEventListener('submit', () => {
        addForm.querySelectorAll('button[type="submit"]').forEach(b => b.disabled = true);
    });
})();

(() => {
    // Report modal — copy the friend's id/name from the clicked trigger into
    // the shared form before it shows. Also reset state between opens.
    const reportModal = document.getElementById('reportModal');
    if (reportModal) {
        const userIdInput = reportModal.querySelector('[data-report-user-id]');
        const userNameEl = reportModal.querySelector('[data-report-user-name]');
        const form = reportModal.querySelector('form');

        reportModal.addEventListener('show.bs.modal', (e) => {
            const trigger = e.relatedTarget;
            if (userIdInput) userIdInput.value = trigger?.dataset.friendId ?? '';
            if (userNameEl) userNameEl.textContent = trigger?.dataset.friendName ?? '';
            form?.reset();
        });
    }
})();

(() => {
    const cardsView = document.querySelector('[data-friends-view="cards"]');
    const listView = document.querySelector('[data-friends-view="list"]');
    const cardsRadio = document.getElementById('view-cards');
    const listRadio = document.getElementById('view-list');
    if (!cardsView || !listView || !cardsRadio || !listRadio) return;

    const KEY = 'friends.view';
    const apply = (mode) => {
        const useList = mode === 'list';
        cardsView.hidden = useList;
        listView.hidden = !useList;
        cardsRadio.checked = !useList;
        listRadio.checked = useList;
    };

    apply(localStorage.getItem(KEY) || 'cards');
    cardsRadio.addEventListener('change', () => { if (cardsRadio.checked) { apply('cards'); localStorage.setItem(KEY, 'cards'); } });
    listRadio.addEventListener('change', () => { if (listRadio.checked) { apply('list'); localStorage.setItem(KEY, 'list'); } });
})();