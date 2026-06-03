// Auction-bid prompt handler — renders _AuctionBidPrompt.cshtml from the engine's
// AuctionBidPrompt. Builds one raise button per AllowedIncrements (each submits
// CurrentHighBid + increment as a Bid, gated against the bidder's balance) plus a
// Pass that drops the bidder out. Plugs into the game-play hub coordinator
// (../game-play-hub.js), which owns the connection and routes PromptOpened /
// PromptClosed here. The element ids / colour-map id below mirror _AuctionBidPrompt.cshtml.
(function () {
    'use strict';

    if (!window.GamePlayHub || typeof bootstrap === 'undefined') return;

    const modalEl = document.getElementById('auctionBidModal');
    if (!modalEl) return;   // partial not on this page — nothing to handle

    const modal = new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false });
    const contentEl = document.getElementById('auctionBidContent');
    const headerEl = document.getElementById('auctionBidHeader');
    const titleEl = document.getElementById('auctionBidTitle');
    const bodyEl = document.getElementById('auctionBidBody');
    const currentEl = document.getElementById('auctionBidCurrent');
    const balanceEl = document.getElementById('auctionBidBalance');
    const incrementsEl = document.getElementById('auctionBidIncrements');
    const errEl = document.getElementById('auctionBidError');
    const passBtn = document.getElementById('auctionBidPass');

    const currency = modalEl.dataset.currency || '£';

    // AuctionBidAction goes over the wire numerically — SignalR's default JSON
    // protocol serialises enums by value, not name (Bid = 0, Pass = 1).
    const ACTION_BID = 0;
    const ACTION_PASS = 1;

    // Board index → set slug, emitted server-side by the partial; drives card colour.
    let colours = {};
    try {
        const el = document.getElementById('auctionBidColours');
        if (el) colours = JSON.parse(el.textContent || '{}');
    } catch (e) {
        console.error('Failed to parse auction-bid colours:', e);
    }

    let current = null;   // { promptId, stamp, highBid, balance, increments }
    let ctx = null;       // hub context, captured when the prompt opens

    function showError(message) {
        errEl.textContent = message;
        errEl.classList.remove('d-none');
    }

    // Reset to base classes (drop any prior prop-colour), then colour by the
    // auctioned property's set — primary fallback if the index has no mapped set.
    function applyColour(boardIndex) {
        headerEl.className = 'modal-header border-0';
        contentEl.className = 'modal-content border border-2';

        const slug = colours[String(boardIndex)];
        if (slug) {
            headerEl.classList.add('text-bg-prop-' + slug);
            contentEl.classList.add('border-prop-' + slug);
        } else {
            headerEl.classList.add('text-bg-primary');
            contentEl.classList.add('border-primary');
        }
    }

    // One button per increment; the submitted bid is CurrentHighBid + increment.
    // game-rules.md Default rule 7: bids come from genuine cash, so a raise beyond
    // the bidder's balance renders disabled (the validator enforces this too). A
    // bidder who can't afford the smallest raise is left with only Pass.
    function renderIncrements() {
        incrementsEl.innerHTML = '';
        if (!current) return;

        (current.increments || []).forEach(inc => {
            const newBid = current.highBid + inc;
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'btn btn-outline-primary flex-fill';
            btn.textContent = '+' + currency + inc;
            btn.title = 'Bid ' + currency + newBid;
            btn.setAttribute('aria-label', 'Bid ' + currency + newBid);

            if (newBid > current.balance) btn.disabled = true;
            else btn.addEventListener('click', () => submit(ACTION_BID, newBid));

            incrementsEl.appendChild(btn);
        });
    }

    function setBusy(busy) {
        passBtn.disabled = busy;
        if (busy) incrementsEl.querySelectorAll('button').forEach(b => { b.disabled = true; });
        else renderIncrements();   // restores per-increment affordability gating
    }

    function onOpen(prompt, stamp, hubCtx) {
        // Phones (carry a userId) render only their own player's prompt; the host
        // tablet (no userId — the controller) renders every prompt and answers on a
        // player's behalf server-side via host-bypass.
        if (hubCtx.userId && prompt.playerId !== hubCtx.userId) return;

        ctx = hubCtx;
        current = {
            promptId: prompt.promptId,
            stamp: stamp,
            highBid: Number(prompt.currentHighBid) || 0,
            balance: Number(prompt.playerBalance) || 0,
            increments: prompt.allowedIncrements || []
        };

        titleEl.textContent = prompt.title || 'Property auction';
        bodyEl.textContent = prompt.body || '';
        currentEl.textContent = currency + current.highBid;
        balanceEl.textContent = currency + current.balance;
        errEl.classList.add('d-none');

        renderIncrements();
        applyColour(prompt.boardIndex);
        modal.show();
    }

    function onClose(promptId) {
        // Ignore a close for a prompt we're no longer showing.
        if (current && promptId && current.promptId !== promptId) return;
        current = null;
        modal.hide();
    }

    async function submit(action, bidAmount) {
        if (!current || !ctx) return;

        const response = {
            '$type': 'AuctionBid',
            promptId: current.promptId,
            action: action,
            bidAmount: action === ACTION_BID ? bidAmount : null
        };

        setBusy(true);
        try {
            const ok = await ctx.submit(current.stamp, response);
            if (ok) {
                onClose(current.promptId);
            } else {
                showError('That could not be accepted — your view may be out of date. Refreshing…');
                await ctx.refresh();
            }
        } catch (e) {
            console.error('SubmitPrompt failed:', e);
            showError('Something went wrong submitting your bid.');
        } finally {
            setBusy(false);
        }
    }

    passBtn.addEventListener('click', () => submit(ACTION_PASS, null));

    window.GamePlayHub.registerPrompt({
        type: 'AuctionBid',
        onOpen: onOpen,
        onClose: onClose
    });
})();