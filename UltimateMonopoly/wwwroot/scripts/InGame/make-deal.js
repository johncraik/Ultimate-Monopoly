// Deal builder — live "what you give / what you receive" summary for _MakeDealPartial.
// The property squares act as checkboxes and the two money inputs feed the summary
// boxes. Document-delegated so it survives the live profile re-render (innerHTML swap),
// same pattern as ingame-prompts.js.
//
// This module ONLY maintains the summary. Submitting the built deal — reading the
// checked squares + money inputs into a DealContents — is wired where the Propose/Cancel
// buttons live (the BuildDealPrompt response, or the turn-boundary command), not here.
(function () {
    'use strict';

    // Rebuild one side's summary box from its grid selection + money input.
    function refresh(root, side) {
        const cur = root.dataset.dealCurrency || '';
        const summary = root.querySelector('[data-deal-summary="' + side + '"]');
        if (!summary) return;

        // Money badge — shown only when a positive amount is entered.
        const moneyInput = root.querySelector('[data-deal-money="' + side + '"]');
        const amount = moneyInput ? Math.max(0, Math.floor(Number(moneyInput.value) || 0)) : 0;
        const moneyBadge = summary.querySelector('[data-deal-summary-money]');
        if (moneyBadge) {
            moneyBadge.textContent = amount > 0 ? cur + amount : '';
            moneyBadge.classList.toggle('d-none', amount <= 0);
        }

        // Property badges — one per checked square, coloured by its set.
        const props = summary.querySelector('[data-deal-summary-props]');
        if (props) {
            props.replaceChildren();
            const grid = root.querySelector('[data-deal-grid="' + side + '"]');
            const checked = grid ? grid.querySelectorAll('input.btn-check[data-deal-index]:checked') : [];
            checked.forEach(function (input) {
                const badge = document.createElement('span');
                badge.className = 'badge deal-badge text-bg-prop-' + (input.dataset.dealSlug || '');
                badge.textContent = input.dataset.dealName || ('#' + input.dataset.dealIndex);
                props.appendChild(badge);
            });
        }

        // Empty hint — only when nothing at all is selected on this side.
        const empty = summary.querySelector('[data-deal-summary-empty]');
        if (empty) {
            const propCount = props ? props.childElementCount : 0;
            empty.classList.toggle('d-none', amount > 0 || propCount > 0);
        }
    }

    // A property square was toggled → refresh its side.
    document.addEventListener('change', function (e) {
        const input = e.target.closest('input.btn-check[data-deal-index]');
        if (!input) return;
        const root = input.closest('[data-deal]');
        const grid = input.closest('[data-deal-grid]');
        if (root && grid) refresh(root, grid.dataset.dealGrid);
    });

    // A money input changed → clamp to the side's cash and refresh.
    document.addEventListener('input', function (e) {
        const money = e.target.closest('[data-deal-money]');
        if (!money) return;
        const root = money.closest('[data-deal]');
        if (!root) return;
        const max = Number(money.max);
        if (!Number.isNaN(max) && Number(money.value) > max) money.value = max;
        refresh(root, money.dataset.dealMoney);
    });
})();