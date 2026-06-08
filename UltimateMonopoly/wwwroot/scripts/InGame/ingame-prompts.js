// In-game prompt submit — for the server-rendered prompts (the .pp-prompt overlay
// inside _PlayerProfileView). The overlay is part of the live-swapped profile body,
// so clicks are delegated off [data-prompt-submit] / [data-prompt-decline]. The
// handler reads the prompt type + concurrency stamp + controls straight from the
// rendered markup and calls GamePlayHub.SubmitPrompt.
//
// There is no client open/close: the prompt is shown iff the server renders it into
// the partial, and it "closes" simply by not being in the next StateChanged frame —
// which is why the rapid-succession race is gone. On a successful submit we leave the
// buttons disabled (the imminent swap replaces them); on a false/stale result or a
// transient error we re-enable what we disabled and surface a message.
(function () {
    'use strict';

    function selectedDie(promptEl, n) {
        const grp = promptEl.querySelector('[data-die="' + n + '"]');
        if (!grp) return null;
        const checked = grp.querySelector('input.btn-check:checked');
        return checked ? Number(checked.value) : null;
    }

    // Build the PromptResponse for the prompt's type. Returns { response } to submit,
    // { error } to block with a message, or null to ignore.
    function buildResponse(promptEl, btn) {
        const promptId = promptEl.dataset.promptId;
        const decline = btn.hasAttribute('data-prompt-decline');

        switch (promptEl.dataset.promptType) {
            case 'Acknowledge':
                return { response: { '$type': 'Acknowledge', promptId: promptId } };

            case 'DiceRoll': {
                // Every rendered die group ([data-die]) must have a selection.
                const groups = promptEl.querySelectorAll('[data-die]');
                for (const g of groups) {
                    if (!g.querySelector('input.btn-check:checked')) return { error: 'Select a value for each die.' };
                }
                return { response: { '$type': 'DiceRoll', promptId: promptId,
                    die1: selectedDie(promptEl, 1), die2: selectedDie(promptEl, 2), thirdDie: selectedDie(promptEl, 3) } };
            }

            case 'AcquireProperty':
                // [data-prompt-submit] = accept; [data-prompt-decline] = decline.
                return { response: { '$type': 'AcquireProperty', promptId: promptId, accept: !decline } };

            case 'Deal':
                // Counter party's accept/decline ([data-prompt-submit] = accept,
                // [data-prompt-decline] = decline). Contents are server-authored on the
                // prompt, so the response is just the bool.
                return { response: { '$type': 'Deal', promptId: promptId, accept: !decline } };

            case 'BuildDeal': {
                // The shortfall debtor builds a deal in the embedded _MakeDealPartial.
                // Cancel ([data-prompt-decline]) backs out; Offer Deal reads the builder
                // controls into a DealContents.
                if (decline)
                    return { response: { '$type': 'BuildDeal', promptId: promptId, cancelled: true,
                        contents: { moneyFromProposer: 0, moneyFromCounterParty: 0, propertiesFromProposer: [], propertiesFromCounterParty: [] } } };

                const root = promptEl.querySelector('[data-deal]');
                if (!root) return { error: 'Deal builder not found.' };
                const moneyOf = side => {
                    const el = root.querySelector('[data-deal-money="' + side + '"]');
                    return el ? Math.max(0, Math.floor(Number(el.value) || 0)) : 0;
                };
                const propsOf = side =>
                    Array.from(root.querySelectorAll('[data-deal-grid="' + side + '"] input.btn-check[data-deal-index]:checked'))
                        .map(i => Number(i.dataset.dealIndex));
                return { response: { '$type': 'BuildDeal', promptId: promptId, cancelled: false, contents: {
                    moneyFromProposer: moneyOf('proposer'),
                    moneyFromCounterParty: moneyOf('counterparty'),
                    propertiesFromProposer: propsOf('proposer'),
                    propertiesFromCounterParty: propsOf('counterparty')
                } } };
            }

            case 'AuctionBid':
                // AuctionBidAction is numeric over the wire (Bid = 0, Pass = 1).
                return decline
                    ? { response: { '$type': 'AuctionBid', promptId: promptId, action: 1, bidAmount: null } }
                    : { response: { '$type': 'AuctionBid', promptId: promptId, action: 0, bidAmount: Number(btn.dataset.bidAmount) } };

            case 'Shortfall':
                // ShortfallAction is numeric over the wire (TakeLoan 0, Mortgage 1, SellHouses 2,
                // ProposeDeal 3, DeclareBankruptcy 4) — each button carries its own value.
                return { response: { '$type': 'Shortfall', promptId: promptId, action: Number(btn.dataset.shortfallAction) } };

            case 'LeaveJail':
                // LeaveJailAction is numeric over the wire (PayFee 0, PlayCard 1) — each button carries its own value.
                return { response: { '$type': 'LeaveJail', promptId: promptId, action: Number(btn.dataset.leavejailAction) } };

            case 'TargetProperty': {
                // Collect the checked toggles; must be exactly Count (carried on [data-target-count]).
                const selected = Array.from(promptEl.querySelectorAll('input.btn-check[data-target-index]:checked'))
                    .map(i => Number(i.dataset.targetIndex));
                const countEl = promptEl.querySelector('[data-target-count]');
                const count = countEl ? Number(countEl.dataset.targetCount) : selected.length;
                if (selected.length !== count)
                    return { error: count === 1 ? 'Select one property.' : ('Select exactly ' + count + ' properties.') };
                return { response: { '$type': 'TargetProperty', promptId: promptId, selectedBoardIndexes: selected } };
            }

            default:
                return null;
        }
    }

    function showError(promptEl, message) {
        const err = promptEl.querySelector('[data-prompt-error]');
        if (err) { err.textContent = message; err.classList.remove('d-none'); }
        else console.warn('prompt:', message);
    }

    document.addEventListener('click', function (e) {
        const btn = e.target.closest('[data-prompt-submit], [data-prompt-decline]');
        if (!btn || btn.disabled || !window.GamePlayHub) return;

        const promptEl = btn.closest('.pp-prompt');
        if (!promptEl) return;

        const built = buildResponse(promptEl, btn);
        if (!built) return;
        if (built.error) { showError(promptEl, built.error); return; }

        // Disable only the buttons we actually disable, so server-disabled (e.g.
        // unaffordable auction raise) buttons aren't wrongly re-enabled later.
        const disabled = Array.from(promptEl.querySelectorAll('button')).filter(b => !b.disabled);
        disabled.forEach(b => { b.disabled = true; });

        GamePlayHub.invoke('SubmitPrompt', promptEl.dataset.stamp, built.response)
            .then(ok => {
                if (!ok) {
                    showError(promptEl, 'That could not be accepted — your view may be out of date.');
                    disabled.forEach(b => { b.disabled = false; });
                }
                // ok: leave disabled — the next StateChanged frame swaps the prompt out.
            })
            .catch(err => {
                console.error('SubmitPrompt failed:', err);
                disabled.forEach(b => { b.disabled = false; });
            });
    });
})();
