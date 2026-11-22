// The gear tabs of a character page (Components/PlayerGear.razor): dragging a piece from the bag onto its slot,
// lighting up where a piece goes while the mouse is over it, and the character standing in the middle.
//
// Everything here is a convenience on top of the page, which works by click and tap alone: a piece is put on with
// the Equip button too. So this only listens to a mouse or a pen. A finger scrolls the page as usual.
//
// It also shows the card of a piece or a worn slot beside it while the mouse is over it. The page draws every card
// (hidden, in .rf-gear-tips) with the same numbers as its item panel; this copies the one asked for into a layer of
// its own, and for a piece of the bag puts the card of the piece worn in its slot next to it. Nothing is asked of
// the server for a hover.
//
// The script never changes what is worn. A drop only tells the page (DropOnSlot, DropOnBag); the page asks the server
// and draws what the server answers. While dragging, the marks are attributes on the page's root (data-drag-slot and
// so on), which the stylesheet reads: Blazor does not render those, so a re-render in the middle leaves them alone.

const attachedRoots = new WeakMap();
let figureNow = null;

/** Starts listening on the gear page's root element. */
export function attach(root, dotnet) {
    if (!root || attachedRoots.has(root)) return;

    const s = { dotnet, start: null, drag: null };
    const hoverable = window.matchMedia && window.matchMedia('(hover: hover)').matches;

    const dropUnder = (x, y) => {
        const el = document.elementFromPoint(x, y);
        const target = el && el.closest ? el.closest('[data-gear-drop]') : null;
        if (!target || !root.contains(target) || !s.drag) return null;
        const key = target.dataset.gearDrop;
        // a piece from the bag goes onto its own slot; a worn piece goes back into the bag
        const fits = s.drag.from === 'worn' ? key === 'bag' : key === s.drag.slot;
        return fits ? target : null;
    };

    const setOver = target => {
        if (s.drag.over === target) return;
        if (s.drag.over) s.drag.over.removeAttribute('data-gear-over');
        s.drag.over = target;
        if (target) target.setAttribute('data-gear-over', '1');
    };

    // ---- The cards ---------------------------------------------------------------------------------------------
    const tip = { layer: null, anchor: null, showTimer: 0, hideTimer: 0 };
    const cardOf = id => (id ? document.querySelector('.rf-gear-tips [data-tip-card="' + id + '"]') : null);

    const hideTip = () => {
        clearTimeout(tip.showTimer);
        clearTimeout(tip.hideTimer);
        tip.anchor = null;
        tip.waiting = null;
        if (tip.layer) tip.layer.classList.remove('is-on');
    };

    // Where the card goes: beside what it is about, inside the window, and never over the slot the piece would go
    // in. A pair is laid side by side where that fits and one over the other where it does not.
    const placeTip = (pair, slotKey, fromBag) => {
        const layer = tip.layer;
        const a = tip.anchor.getBoundingClientRect();
        const vw = document.documentElement.clientWidth, vh = window.innerHeight;
        const pad = 8, gap = 10;
        const slotEl = fromBag && slotKey ? root.querySelector('.rf-gear-slot[data-slot="' + slotKey + '"]') : null;
        const avoid = slotEl ? slotEl.getBoundingClientRect() : null;
        const hits = (x, y, w, h, r) => r && x < r.right && x + w > r.left && y < r.bottom && y + h > r.top;
        const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));

        let order = ['below', 'above', 'left', 'right'];
        if (!fromBag) {
            // a worn slot: toward the middle of the figure, which keeps the other slots of its column in sight
            const figure = tip.anchor.closest('.rf-gear-figure');
            const f = figure ? figure.getBoundingClientRect() : null;
            const rightHalf = f && a.left + a.width / 2 > f.left + f.width / 2 + 4;
            order = rightHalf ? ['left', 'right', 'below', 'above'] : ['right', 'left', 'below', 'above'];
        }

        let best = null;
        for (const layout of pair ? ['is-row', 'is-stack'] : ['is-row']) {
            layer.classList.remove('is-row', 'is-stack');
            layer.classList.add(layout);
            const w = layer.offsetWidth, h = layer.offsetHeight;
            const cx = clamp(a.left + a.width / 2 - w / 2, pad, Math.max(pad, vw - w - pad));
            const cy = clamp(a.top - 12, pad, Math.max(pad, vh - h - pad));
            const spots = {
                below: [cx, a.bottom + gap],
                above: [cx, a.top - gap - h],
                left: [a.left - gap - w, cy],
                right: [a.right + gap, cy],
            };
            for (const side of order) {
                const [x, y] = spots[side];
                const over = Math.max(0, pad - x) + Math.max(0, pad - y) + Math.max(0, x + w - (vw - pad)) + Math.max(0, y + h - (vh - pad));
                const clear = !hits(x, y, w, h, avoid);
                if (over === 0 && clear) {
                    layer.style.left = Math.round(x) + 'px';
                    layer.style.top = Math.round(y) + 'px';
                    return;
                }
                const score = over + (clear ? 0 : 5000);
                if (!best || score < best.score) best = { score, layout, x, y, w, h };
            }
        }

        // nowhere is perfect (a very small window): the least bad, pulled inside
        layer.classList.remove('is-row', 'is-stack');
        layer.classList.add(best.layout);
        layer.style.left = Math.round(clamp(best.x, pad, Math.max(pad, vw - best.w - pad))) + 'px';
        layer.style.top = Math.round(clamp(best.y, pad, Math.max(pad, vh - best.h - pad))) + 'px';
    };

    const showTip = anchor => {
        clearTimeout(tip.showTimer);
        const card = anchor.isConnected ? cardOf(anchor.dataset.tip) : null;
        if (!card || s.drag) {
            hideTip();
            return;
        }
        if (!tip.layer) {
            tip.layer = document.createElement('div');
            tip.layer.className = 'rf-gear-tip';
            document.body.appendChild(tip.layer);
        }
        const other = cardOf(card.dataset.tipWith);
        tip.layer.innerHTML = card.innerHTML + (other ? other.innerHTML : '');
        tip.layer.querySelectorAll('img').forEach(img => { img.loading = 'eager'; });
        tip.layer.classList.toggle('is-pair', !!other);
        tip.anchor = anchor;
        placeTip(!!other, card.dataset.tipSlot, anchor.classList.contains('rf-gear-tile'));
        tip.layer.classList.add('is-on');
    };

    // after something was done the page draws its cards again: the one on show follows, or goes with its piece
    const refreshTip = () => {
        if (!tip.anchor) return;
        if (tip.anchor.isConnected && tip.anchor.dataset.tip && tip.anchor.matches(':hover')) showTip(tip.anchor);
        else hideTip();
    };

    s.tipOver = e => {
        if (e.pointerType === 'touch' || s.drag) return;
        const anchor = e.target.closest ? e.target.closest('[data-tip]') : null;
        if (!anchor || !root.contains(anchor)) return;
        clearTimeout(tip.hideTimer);
        if (tip.anchor === anchor) return;
        clearTimeout(tip.showTimer);
        // the first card waits a moment, so crossing the page does not flash cards; from one piece to the next
        // it changes at once, without going away in between
        if (tip.anchor) showTip(anchor);
        else tip.showTimer = setTimeout(() => showTip(anchor), 150);
    };

    // a scroll takes the card away; the mouse moving again over the same piece brings it back
    s.tipMove = e => {
        if (tip.anchor || s.drag || s.start || e.pointerType === 'touch') return;
        const anchor = e.target.closest ? e.target.closest('[data-tip]') : null;
        if (!anchor || !root.contains(anchor) || tip.waiting === anchor) return;
        tip.waiting = anchor;
        clearTimeout(tip.showTimer);
        tip.showTimer = setTimeout(() => { tip.waiting = null; showTip(anchor); }, 150);
    };

    s.tipOut = e => {
        const anchor = e.target.closest ? e.target.closest('[data-tip]') : null;
        if (!anchor) return;
        const to = e.relatedTarget && e.relatedTarget.closest ? e.relatedTarget.closest('[data-tip]') : null;
        if (to === anchor) return;
        clearTimeout(tip.showTimer);
        clearTimeout(tip.hideTimer);
        tip.hideTimer = setTimeout(hideTip, 110);
    };

    s.tipClick = () => {
        setTimeout(refreshTip, 350);
        setTimeout(refreshTip, 1000);
    };

    s.hideTip = hideTip;
    s.dropTip = () => {
        hideTip();
        if (tip.layer) tip.layer.remove();
        tip.layer = null;
    };

    const begin = e => {
        hideTip();
        const src = s.start.src;
        const img = src.querySelector('img');
        const ghost = document.createElement('div');
        ghost.className = 'rf-gear-ghost';
        if (img) {
            const copy = document.createElement('img');
            copy.src = img.currentSrc || img.src;
            copy.alt = '';
            ghost.appendChild(copy);
        }
        document.body.appendChild(ghost);
        s.drag = { ghost, src, id: s.start.id, from: s.start.from, slot: s.start.slot, over: null };
        src.setAttribute('data-gear-dragging', '1');
        root.setAttribute('data-drag-from', s.drag.from);
        if (s.drag.slot) root.setAttribute('data-drag-slot', s.drag.slot);
        root.removeAttribute('data-hover-slot');
        root.removeAttribute('data-hover-piece');
        document.body.classList.add('rf-gear-dragging');
    };

    const end = () => {
        if (s.drag) {
            setOver(null);
            s.drag.ghost.remove();
            s.drag.src.removeAttribute('data-gear-dragging');
            root.removeAttribute('data-drag-from');
            root.removeAttribute('data-drag-slot');
            document.body.classList.remove('rf-gear-dragging');
        }
        s.drag = null;
        s.start = null;
    };

    s.down = e => {
        if (e.button !== 0 || e.pointerType === 'touch') return;
        const src = e.target.closest ? e.target.closest('[data-gear-drag]') : null;
        if (!src || !root.contains(src)) return;
        s.start = { x: e.clientX, y: e.clientY, src, id: src.dataset.gearDrag, from: src.dataset.gearFrom || 'bag', slot: src.dataset.slot || '' };
    };

    s.move = e => {
        if (!s.start) return;
        if (!s.drag) {
            if (Math.hypot(e.clientX - s.start.x, e.clientY - s.start.y) < 6) return;
            if (!document.body.contains(s.start.src)) {
                s.start = null;
                return;
            }
            begin(e);
        }
        e.preventDefault();
        s.drag.ghost.style.transform = `translate(${e.clientX}px, ${e.clientY}px)`;
        setOver(dropUnder(e.clientX, e.clientY));
    };

    s.up = e => {
        if (!s.drag) {
            s.start = null;
            return;
        }
        const target = dropUnder(e.clientX, e.clientY);
        const { id, from } = s.drag;
        const key = target ? target.dataset.gearDrop : null;
        end();
        // the mouse button coming up after a drag is not a click on whatever lies under it
        const swallow = ev => { ev.stopPropagation(); ev.preventDefault(); };
        window.addEventListener('click', swallow, true);
        setTimeout(() => window.removeEventListener('click', swallow, true), 0);
        if (!key) return;
        if (from === 'worn') s.dotnet.invokeMethodAsync('DropOnBag', id);
        else s.dotnet.invokeMethodAsync('DropOnSlot', id, key);
        s.tipClick();
    };

    s.cancel = () => end();
    s.key = e => {
        if (e.key !== 'Escape') return;
        if (s.drag) end();
        hideTip();
    };

    // With nothing picked, the mouse over a slot lights up what goes in it, and over a piece the slot it goes in.
    s.over = e => {
        if (!hoverable || s.drag || root.hasAttribute('data-sel-slot')) return;
        const slot = e.target.closest ? e.target.closest('.rf-gear-slot[data-slot]') : null;
        const piece = slot ? null : (e.target.closest ? e.target.closest('.rf-gear-tile[data-slot]') : null);
        if (slot) root.setAttribute('data-hover-slot', slot.dataset.slot);
        else root.removeAttribute('data-hover-slot');
        if (piece && root.classList.contains('rf-gear--equipment')) root.setAttribute('data-hover-piece', piece.dataset.slot);
        else root.removeAttribute('data-hover-piece');
    };
    s.out = () => {
        root.removeAttribute('data-hover-slot');
        root.removeAttribute('data-hover-piece');
    };

    root.addEventListener('pointerdown', s.down);
    root.addEventListener('mouseover', s.over);
    root.addEventListener('mouseleave', s.out);
    root.addEventListener('pointerover', s.tipOver);
    root.addEventListener('pointerout', s.tipOut);
    root.addEventListener('pointermove', s.tipMove);
    root.addEventListener('click', s.tipClick);
    window.addEventListener('scroll', s.hideTip, true);
    window.addEventListener('pointermove', s.move, { passive: false });
    window.addEventListener('pointerup', s.up);
    window.addEventListener('pointercancel', s.cancel);
    window.addEventListener('keydown', s.key);
    attachedRoots.set(root, s);
}

/** Stops listening, and lets the character go. */
export function detach(root) {
    const s = root ? attachedRoots.get(root) : null;
    if (s) {
        root.removeEventListener('pointerdown', s.down);
        root.removeEventListener('mouseover', s.over);
        root.removeEventListener('mouseleave', s.out);
        root.removeEventListener('pointerover', s.tipOver);
        root.removeEventListener('pointerout', s.tipOut);
        root.removeEventListener('pointermove', s.tipMove);
        root.removeEventListener('click', s.tipClick);
        window.removeEventListener('scroll', s.hideTip, true);
        s.dropTip();
        window.removeEventListener('pointermove', s.move);
        window.removeEventListener('pointerup', s.up);
        window.removeEventListener('pointercancel', s.cancel);
        window.removeEventListener('keydown', s.key);
        attachedRoots.delete(root);
    }
    document.body.classList.remove('rf-gear-dragging');
    document.querySelectorAll('.rf-gear-ghost').forEach(g => g.remove());
    dropFigure();
}

/**
 * Stands the character in the middle of the Equipment tab: the customizer's own view (3D where the browser can,
 * the flat pictures where it cannot). Answers '3d', '2d', or 'none' when there is nothing to show; the page then
 * shows a still figure instead. The character wears its look (face, hair, colors), not its gear: the worn pieces
 * are the slots around it.
 */
export async function figure(host, designerUrl, look) {
    dropFigure();
    if (!host || !look) return 'none';
    try {
        // the page names the script from its own address; an import here would read it from this script's folder
        const designer = await import(new URL(designerUrl, document.baseURI).href);
        const view = await designer.create(host, { inline: true });
        if (!host.isConnected) {
            view.dispose();
            return 'none';
        }
        figureNow = view;
        // a little nearer than the customizer stands: this stage is narrower, and the figure should fill it
        if (view.wanted && typeof view.wanted.distance === 'number') {
            view.wanted.distance = 4.2;
            view.distance = 4.2;
        }
        await view.show(look);
        return view.mode || '3d';
    } catch (e) {
        console.warn('The character could not be shown on the gear page', e);
        dropFigure();
        return 'none';
    }
}

export function dropFigure() {
    if (figureNow) {
        try { figureNow.dispose(); } catch { /* already gone */ }
        figureNow = null;
    }
}
