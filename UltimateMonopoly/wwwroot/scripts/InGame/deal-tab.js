// Deal tab — toggles between Block 1 (pick a player) and Block 2 (the deal builder
// for the chosen player) inside the player profile's Deal tab, and submits the built
// deal. One builder is pre-rendered per candidate (hidden); clicking a candidate
// reveals theirs and hides the list, Cancel reverses it, and Offer Deal proposes it as
// a turn-boundary command. Document-delegated so it survives the live profile re-render
// (innerHTML swap), same pattern as ingame-prompts.js / make-deal.js.
(function () {
    'use strict';

    function esc(value) {
        return (window.CSS && CSS.escape) ? CSS.escape(value) : value;
    }

    // Read the embedded _MakeDealPartial builder into a DealContents-shaped object.
    function readContents(root) {
        const moneyOf = side => {
            const el = root.querySelector('[data-deal-money="' + side + '"]');
            return el ? Math.max(0, Math.floor(Number(el.value) || 0)) : 0;
        };
        const propsOf = side =>
            Array.from(root.querySelectorAll('[data-deal-grid="' + side + '"] input.btn-check[data-deal-index]:checked'))
                .map(i => Number(i.dataset.dealIndex));
        return {
            moneyFromProposer: moneyOf('proposer'),
            moneyFromCounterParty: moneyOf('counterparty'),
            propertiesFromProposer: propsOf('proposer'),
            propertiesFromCounterParty: propsOf('counterparty')
        };
    }

    function showList(tab, block) {
        if (block) block.classList.add('d-none');
        const list = tab ? tab.querySelector('[data-deal-list]') : null;
        if (list) list.classList.remove('d-none');
    }

    document.addEventListener('click', function (e) {
        // Pick a player → hide the list, reveal that player's builder.
        const target = e.target.closest('[data-deal-target]');
        if (target && !target.disabled) {
            const tab = target.closest('#pp-deal');
            if (!tab) return;
            const block = tab.querySelector('[data-deal-block="' + esc(target.dataset.dealTarget) + '"]');
            if (!block) return;

            const list = tab.querySelector('[data-deal-list]');
            if (list) list.classList.add('d-none');
            tab.querySelectorAll('[data-deal-block]').forEach(b => b.classList.add('d-none'));
            block.classList.remove('d-none');
            return;
        }

        // Offer Deal → propose the built deal as a turn-boundary command, then return to the list.
        const offer = e.target.closest('[data-deal-offer]');
        if (offer && !offer.disabled && window.GamePlayHub) {
            const tab = offer.closest('#pp-deal');
            const block = offer.closest('[data-deal-block]');
            const root = block ? block.querySelector('[data-deal]') : null;
            if (!root) return;

            offer.disabled = true;
            GamePlayHub.invoke('ProposeDeal', root.dataset.proposerId, root.dataset.counterpartyId, readContents(root))
                .then(function () {
                    // The engine now drives the accept/decline; return the tab to the list.
                    showList(tab, block);
                    offer.disabled = false;
                })
                .catch(function () { offer.disabled = false; });
            return;
        }

        // Cancel → hide the open builder, show the list again.
        const cancel = e.target.closest('[data-deal-cancel]');
        if (cancel) {
            showList(cancel.closest('#pp-deal'), cancel.closest('[data-deal-block]'));
        }
    });
})();