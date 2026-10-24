// The clan page's image editor: a clan logo (square) or a cape image (1:2), cropped, zoomed, rotated and placed
// in the browser, so the server only checks and re-encodes what it gets. The cape editor shows the chosen cape
// shape's outline over the image and a 3D preview of the game's cape mesh with the image on it, updated as you
// edit. See docs/clan-images-design.md in the game repository.
//
// openEditor(options, dotnet) shows it; when it closes it calls dotnet.EditorDone({ ok, waiting, error, cancelled }).
// options: { kind: "logo" | "cape", capeStyle: 0-3, logoUrl, requireReview }

import * as THREE from './three.module.min.js';

const OUT = { logo: [512, 512], cape: [512, 1024] };
const VIEW = { logo: [400, 400], cape: [230, 460] };
const STYLES = ['long', 'pointed', 'short', 'tattered'];
const STYLE_NAMES = ['Long', 'Pointed', 'Short', 'Tattered'];
// the same cloth colour the server and the game put behind a cape
const CLOTH = '#2b2a33';
const MAX_FILE = 10 * 1024 * 1024;

// the game's cape meshes: asked for once, kept once they are here
let capeRequest = null;
let capeData = null;
function loadCapes() {
    if (!capeRequest) capeRequest = fetch('/models/clan-capes.json').then(r => r.json()).then(d => (capeData = d));
    return capeRequest;
}

export function openEditor(options, dotnet) {
    new Editor(options || {}, dotnet).open();
}

function el(tag, cls, text) {
    const e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
}

function button(text, cls, onClick, title) {
    const b = el('button', 'rf-btn rf-btn--small' + (cls ? ' ' + cls : ''), text);
    b.type = 'button';
    if (title) b.title = title;
    b.addEventListener('click', onClick);
    return b;
}

class Editor {
    constructor(o, dotnet) {
        this.o = o;
        this.dotnet = dotnet;
        this.kind = o.kind === 'cape' ? 'cape' : 'logo';
        this.style = Math.max(0, Math.min(3, o.capeStyle | 0));
        this.img = null;
        this.tx = { x: 0, y: 0, zoom: 1, rot: 0, flip: false };
        this.bg = this.kind === 'cape' ? CLOTH : null;
        this.busy = false;
        this.masks = {};
        [this.ow, this.oh] = OUT[this.kind];
        [this.vw, this.vh] = VIEW[this.kind];
    }

    // ---- building the dialog ---------------------------------------------------------------------------------

    open() {
        const back = this.back = el('div', 'rf-imged');
        back.setAttribute('role', 'dialog');
        back.setAttribute('aria-modal', 'true');
        const box = el('div', 'rf-imged__box' + (this.kind === 'cape' ? ' rf-imged__box--cape' : ''));
        back.appendChild(box);

        const head = el('div', 'rf-imged__head');
        head.appendChild(el('h2', 'rf-imged__title', this.kind === 'cape' ? 'Cape image' : 'Clan logo'));
        const close = el('button', 'rf-modal__close', '×');
        close.type = 'button';
        close.setAttribute('aria-label', 'Close');
        close.addEventListener('click', () => this.finish({ cancelled: true }));
        head.appendChild(close);
        box.appendChild(head);

        const body = el('div', 'rf-imged__body');
        box.appendChild(body);

        // the image and the frame
        const stageCol = el('div', 'rf-imged__stagecol');
        const stage = this.stage = el('div', 'rf-imged__stage');
        stage.style.width = this.vw + 'px';
        stage.style.height = this.vh + 'px';
        this.view = el('canvas', 'rf-imged__view');
        this.view.width = this.vw * devicePixelRatio;
        this.view.height = this.vh * devicePixelRatio;
        this.overlay = el('canvas', 'rf-imged__overlay');
        this.overlay.width = this.view.width;
        this.overlay.height = this.view.height;
        this.empty = el('div', 'rf-imged__empty', this.kind === 'cape'
            ? 'Choose or drop an image. The whole back of the cape is this frame.'
            : 'Choose or drop an image.');
        stage.append(this.view, this.overlay, this.empty);
        stageCol.appendChild(stage);
        stageCol.appendChild(el('p', 'rf-meta rf-imged__hint', 'Drag to move, scroll to zoom.'));
        body.appendChild(stageCol);

        // the controls
        const controls = el('div', 'rf-imged__controls');
        const file = this.file = el('input');
        file.type = 'file';
        file.accept = 'image/png,image/jpeg,image/webp,image/gif';
        file.hidden = true;
        file.addEventListener('change', () => file.files[0] && this.loadFile(file.files[0]));
        const pick = el('div', 'rf-imged__row');
        pick.appendChild(button('Choose image', 'rf-btn--primary', () => file.click()));
        if (this.kind === 'cape' && this.o.logoUrl)
            pick.appendChild(button('Start from the clan logo', '', () => this.loadUrl(this.o.logoUrl)));
        controls.append(file, pick);

        this.zoom = this.slider(controls, 'Zoom', 10, 400, 100, v => { this.tx.zoom = v / 100; this.changed(); }, v => v + '%');
        this.rot = this.slider(controls, 'Rotate', -180, 180, 0, v => { this.tx.rot = v; this.changed(); }, v => v + '°');

        const tools = el('div', 'rf-imged__row');
        tools.append(
            button('↺ 90°', '', () => this.turn(-90), 'Turn left'),
            button('↻ 90°', '', () => this.turn(90), 'Turn right'),
            button('Flip', '', () => { this.tx.flip = !this.tx.flip; this.changed(); }, 'Mirror left to right'),
            button('Fit', '', () => this.place('fit'), 'The whole image inside the frame'),
            button('Fill', '', () => this.place('fill'), 'The frame covered, edges cut off'),
            button('Centre', '', () => { this.tx.x = 0; this.tx.y = 0; this.changed(); }));
        controls.appendChild(tools);

        const colour = el('label', 'rf-imged__colour');
        colour.appendChild(el('span', 'rf-label', 'Background'));
        const picker = el('input');
        picker.type = 'color';
        picker.value = CLOTH;
        colour.appendChild(picker);
        controls.appendChild(colour);
        if (this.kind === 'logo') {
            const clear = el('label', 'rf-imged__check');
            const box2 = el('input');
            box2.type = 'checkbox';
            box2.checked = true;
            clear.append(box2, el('span', null, 'Transparent'));
            controls.appendChild(clear);
            const set = () => { this.bg = box2.checked ? null : picker.value; picker.disabled = box2.checked; this.changed(); };
            box2.addEventListener('change', set);
            picker.addEventListener('input', set);
            picker.disabled = true;
        } else {
            picker.addEventListener('input', () => { this.bg = picker.value; this.changed(); });
            controls.appendChild(el('p', 'rf-meta', 'Capes cannot be see-through: the background shows wherever the image does not reach.'));
        }

        if (this.kind === 'logo') {
            const round = el('div', 'rf-imged__round');
            this.roundView = el('canvas');
            this.roundView.width = this.roundView.height = 96 * devicePixelRatio;
            round.append(this.roundView, el('span', 'rf-meta', 'Some places show it round.'));
            controls.appendChild(round);
        }

        body.appendChild(controls);

        // the cape on the game's mesh
        if (this.kind === 'cape') {
            const side = el('div', 'rf-imged__previewcol');
            const holder = el('div', 'rf-imged__preview');
            side.appendChild(holder);
            const chips = el('div', 'rf-imged__row rf-imged__styles');
            this.styleButtons = STYLE_NAMES.map((name, i) => {
                const b = button(name, i === this.style ? 'is-active' : '', () => this.setStyle(i));
                chips.appendChild(b);
                return b;
            });
            side.appendChild(chips);
            side.appendChild(el('p', 'rf-meta', 'Drag the cape to turn it. The shape your clan wears is set on the Look tab.'));
            body.appendChild(side);
            this.out = el('canvas');
            this.out.width = this.ow;
            this.out.height = this.oh;
            this.preview = new CapePreview(holder, this.out, this.style);
        }

        const foot = el('div', 'rf-imged__foot');
        this.status = el('p', 'rf-imged__status');
        if (this.o.requireReview)
            this.status.textContent = 'New images are checked before they are used. It can take up to 24 hours.';
        this.apply = button('Apply', 'rf-btn--primary', () => this.upload());
        this.apply.disabled = true;
        foot.append(this.status, button('Cancel', '', () => this.finish({ cancelled: true })), this.apply);
        box.appendChild(foot);

        this.bindPointer();
        this.onKey = e => { if (e.key === 'Escape' && !this.busy) this.finish({ cancelled: true }); };
        document.addEventListener('keydown', this.onKey);
        document.body.appendChild(back);
        if (this.kind === 'cape') loadCapes().then(() => this.changed());
        this.changed();
    }

    slider(parent, label, min, max, value, onInput, format) {
        const row = el('label', 'rf-imged__slider');
        const name = el('span', 'rf-label', label);
        const input = el('input');
        input.type = 'range';
        input.min = min;
        input.max = max;
        input.value = value;
        const shown = el('span', 'rf-meta rf-imged__value', format(value));
        input.addEventListener('input', () => { shown.textContent = format(+input.value); onInput(+input.value); });
        row.append(name, input, shown);
        parent.appendChild(row);
        return { set: v => { input.value = v; shown.textContent = format(v); } };
    }

    // ---- the image -----------------------------------------------------------------------------------------

    loadFile(file) {
        if (file.size > MAX_FILE) { this.say('That file is larger than 10 MB.', true); return; }
        this.loadUrl(URL.createObjectURL(file), true);
    }

    loadUrl(url, revoke) {
        const img = new Image();
        img.onload = () => {
            if (revoke) URL.revokeObjectURL(url);
            this.img = img;
            this.empty.style.display = "none";
            this.tx = { x: 0, y: 0, zoom: 1, rot: 0, flip: false };
            this.rot.set(0);
            this.place(this.kind === 'cape' && url === this.o.logoUrl ? 'logo' : (this.kind === 'cape' ? 'fill' : 'fit'));
            this.apply.disabled = false;
            this.say(this.o.requireReview ? 'New images are checked before they are used. It can take up to 24 hours.' : '');
        };
        img.onerror = () => this.say('That image could not be opened.', true);
        img.src = url;
    }

    // the scale at zoom 100%: the image fitted inside the frame
    baseScale() {
        if (!this.img) return 1;
        return Math.min(this.ow / this.img.width, this.oh / this.img.height);
    }

    place(how) {
        if (!this.img) return;
        const fit = Math.min(this.ow / this.img.width, this.oh / this.img.height);
        const fill = Math.max(this.ow / this.img.width, this.oh / this.img.height);
        this.tx.x = 0;
        this.tx.y = 0;
        if (how === 'fill') this.tx.zoom = fill / fit;
        else if (how === 'logo') {
            // the clan logo as the game places it on a default cape: on the upper back, inside what the
            // short cape shows
            this.tx.zoom = (384 / Math.max(this.img.width, this.img.height)) / fit;
            this.tx.y = 300 - this.oh / 2;
        }
        else this.tx.zoom = 1;
        this.tx.zoom = Math.max(0.1, Math.min(4, this.tx.zoom));
        this.zoom.set(Math.round(this.tx.zoom * 100));
        this.changed();
    }

    turn(deg) {
        let r = Math.round((this.tx.rot + deg) / 90) * 90;
        if (r > 180) r -= 360;
        if (r < -180) r += 360;
        this.tx.rot = r;
        this.rot.set(r);
        this.changed();
    }

    setStyle(i) {
        this.style = i;
        this.styleButtons.forEach((b, k) => b.classList.toggle('is-active', k === i));
        if (this.preview) this.preview.setStyle(i);
        this.changed();
    }

    // draws the result at output size onto a context scaled to its canvas
    draw(ctx, width) {
        const k = width / this.ow;
        ctx.save();
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.clearRect(0, 0, ctx.canvas.width, ctx.canvas.height);
        ctx.scale(k, k);
        if (this.bg) {
            ctx.fillStyle = this.bg;
            ctx.fillRect(0, 0, this.ow, this.oh);
        }
        if (this.img) {
            const s = this.baseScale() * this.tx.zoom;
            ctx.translate(this.ow / 2 + this.tx.x, this.oh / 2 + this.tx.y);
            ctx.rotate(this.tx.rot * Math.PI / 180);
            ctx.scale(this.tx.flip ? -s : s, s);
            ctx.imageSmoothingQuality = 'high';
            ctx.drawImage(this.img, -this.img.width / 2, -this.img.height / 2);
        }
        ctx.restore();
    }

    changed() {
        this.draw(this.view.getContext('2d'), this.view.width);
        this.drawOverlay();
        if (this.roundView) {
            const c = this.roundView.getContext('2d');
            c.save();
            c.clearRect(0, 0, this.roundView.width, this.roundView.height);
            c.beginPath();
            c.arc(this.roundView.width / 2, this.roundView.height / 2, this.roundView.width / 2, 0, Math.PI * 2);
            c.clip();
            c.drawImage(this.view, 0, 0, this.roundView.width, this.roundView.height);
            c.restore();
        }
        if (this.out) {
            this.draw(this.out.getContext('2d'), this.ow);
            this.preview.update();
        }
    }

    // the logo's round crop, or what the chosen cape shape covers; outside it is dimmed
    drawOverlay() {
        const c = this.overlay.getContext('2d');
        const w = this.overlay.width, h = this.overlay.height;
        c.clearRect(0, 0, w, h);
        if (this.kind === 'logo') {
            c.save();
            c.strokeStyle = 'rgba(255,255,255,0.55)';
            c.setLineDash([6 * devicePixelRatio, 6 * devicePixelRatio]);
            c.lineWidth = devicePixelRatio;
            c.beginPath();
            c.arc(w / 2, h / 2, w / 2 - devicePixelRatio, 0, Math.PI * 2);
            c.stroke();
            c.restore();
            return;
        }
        const mask = this.maskFor(this.style, w, h);
        if (!mask) return;
        c.fillStyle = 'rgba(10,10,14,0.62)';
        c.fillRect(0, 0, w, h);
        c.globalCompositeOperation = 'destination-out';
        c.drawImage(mask, 0, 0);
        c.globalCompositeOperation = 'source-over';
    }

    // the shape's triangles drawn in image space: exactly what the game shows of the image
    maskFor(style, w, h) {
        const key = style + ':' + w;
        if (this.masks[key]) return this.masks[key];
        if (!capeData) {
            loadCapes().then(() => this.changed());
            return null;
        }
        const cape = capeData.capes[STYLES[style]];
        const m = document.createElement('canvas');
        m.width = w;
        m.height = h;
        const c = m.getContext('2d');
        c.fillStyle = '#fff';
        c.strokeStyle = '#fff';
        c.lineWidth = 1;
        const uv = cape.uv, idx = cape.index;
        for (let i = 0; i < idx.length; i += 3) {
            c.beginPath();
            for (let j = 0; j < 3; ++j) {
                const v = idx[i + j];
                const x = uv[v * 2] * w, y = (1 - uv[v * 2 + 1]) * h;
                if (j === 0) c.moveTo(x, y); else c.lineTo(x, y);
            }
            c.closePath();
            c.fill();
            c.stroke();
        }
        return this.masks[key] = m;
    }

    bindPointer() {
        let drag = null;
        const target = this.overlay;
        target.addEventListener('pointerdown', e => {
            if (!this.img) return;
            drag = { x: e.clientX, y: e.clientY, tx: this.tx.x, ty: this.tx.y };
            target.setPointerCapture(e.pointerId);
        });
        target.addEventListener('pointermove', e => {
            if (!drag) return;
            const k = this.ow / this.vw;
            this.tx.x = drag.tx + (e.clientX - drag.x) * k;
            this.tx.y = drag.ty + (e.clientY - drag.y) * k;
            this.changed();
        });
        const end = () => { drag = null; };
        target.addEventListener('pointerup', end);
        target.addEventListener('pointercancel', end);
        target.addEventListener('wheel', e => {
            if (!this.img) return;
            e.preventDefault();
            this.tx.zoom = Math.max(0.1, Math.min(4, this.tx.zoom * (e.deltaY < 0 ? 1.08 : 1 / 1.08)));
            this.zoom.set(Math.round(this.tx.zoom * 100));
            this.changed();
        }, { passive: false });

        // a file dropped anywhere on the image
        this.stage.addEventListener('dragover', e => { e.preventDefault(); this.stage.classList.add('is-over'); });
        this.stage.addEventListener('dragleave', () => this.stage.classList.remove('is-over'));
        this.stage.addEventListener('drop', e => {
            e.preventDefault();
            this.stage.classList.remove('is-over');
            const f = e.dataTransfer.files && e.dataTransfer.files[0];
            if (f) this.loadFile(f);
        });
    }

    // ---- sending it --------------------------------------------------------------------------------------------

    say(text, bad) {
        this.status.textContent = text || '';
        this.status.classList.toggle('is-bad', !!bad);
    }

    async upload() {
        if (!this.img || this.busy) return;
        this.busy = true;
        this.apply.disabled = true;
        this.say('Uploading...');
        try {
            const canvas = document.createElement('canvas');
            canvas.width = this.ow;
            canvas.height = this.oh;
            this.draw(canvas.getContext('2d'), this.ow);
            const blob = await new Promise(r => canvas.toBlob(r, 'image/png'));
            const response = await fetch('/api/clan/images/' + this.kind, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'image/png', 'X-Ravenfall-Upload': '1' },
                body: blob,
            });
            if (response.status === 401) throw new Error('You are signed out. Sign in again and retry.');
            if (!response.ok) throw new Error('The upload did not go through (' + response.status + ').');
            const result = await response.json();
            const ok = result.ok ?? result.Ok;
            if (!ok) throw new Error(result.error ?? result.Error ?? 'The upload did not go through.');
            this.finish({ ok: true, waiting: !!(result.waiting ?? result.Waiting) });
        } catch (err) {
            this.busy = false;
            this.apply.disabled = false;
            this.say(err.message || String(err), true);
        }
    }

    finish(result) {
        document.removeEventListener('keydown', this.onKey);
        if (this.preview) this.preview.dispose();
        this.back.remove();
        if (this.dotnet) this.dotnet.invokeMethodAsync('EditorDone', result).catch(() => { });
    }
}

// ---- the 3D preview ------------------------------------------------------------------------------------------

class CapePreview {
    constructor(holder, source, style) {
        this.holder = holder;
        this.style = style;
        const w = holder.clientWidth || 300, h = holder.clientHeight || 420;
        try {
            this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
        } catch (e) {
            holder.appendChild(el('p', 'rf-meta', 'Your browser cannot show the 3D preview.'));
            return;
        }
        this.renderer.setPixelRatio(devicePixelRatio);
        this.renderer.setSize(w, h);
        holder.appendChild(this.renderer.domElement);

        this.scene = new THREE.Scene();
        this.camera = new THREE.PerspectiveCamera(30, w / h, 0.05, 20);
        // behind the character, a little above, looking at the middle of the back
        this.camera.position.set(0, 1.25, -3.4);
        this.camera.lookAt(0, 0.98, 0);
        this.scene.add(new THREE.HemisphereLight(0xffffff, 0x3a3a48, 1.5));
        const sun = new THREE.DirectionalLight(0xffffff, 1.9);
        sun.position.set(-1.6, 2.6, -2.4);
        this.scene.add(sun);

        this.figure = new THREE.Group();
        this.scene.add(this.figure);
        this.addBody();

        this.texture = new THREE.CanvasTexture(source);
        this.texture.colorSpace = THREE.SRGBColorSpace;
        this.texture.anisotropy = 4;
        this.material = new THREE.MeshStandardMaterial({ map: this.texture, side: THREE.DoubleSide, roughness: 0.9 });
        this.meshes = {};

        this.yaw = 0;
        this.spin = true;
        this.bindDrag();
        loadCapes().then(data => { this.data = data; this.setStyle(this.style); });

        const loop = t => {
            this.frame = requestAnimationFrame(loop);
            const dt = this.last ? Math.min(0.1, (t - this.last) / 1000) : 0;
            this.last = t;
            // a slow sway either side of the back until it is dragged
            if (this.spin) { this.time = (this.time || 0) + dt; this.yaw = Math.sin(this.time * 0.6) * 0.7; }
            this.figure.rotation.y = this.yaw;
            this.renderer.render(this.scene, this.camera);
        };
        this.frame = requestAnimationFrame(loop);
    }

    // a plain figure for the cape to hang on: the cape is the point, not the character
    addBody() {
        const skin = new THREE.MeshStandardMaterial({ color: 0x8b8f9a, roughness: 0.85 });
        const add = (geometry, x, y, z, rx, rz, sx, sz) => {
            const m = new THREE.Mesh(geometry, skin);
            m.position.set(x, y, z);
            if (rx) m.rotation.x = rx;
            if (rz) m.rotation.z = rz;
            m.scale.set(sx || 1, 1, sz || 1);
            this.figure.add(m);
        };
        add(new THREE.CapsuleGeometry(0.17, 0.44, 6, 16), 0, 1.21, 0.03, 0, 0, 1, 0.6);
        add(new THREE.SphereGeometry(0.12, 20, 14), 0, 1.72, 0.03);
        add(new THREE.CapsuleGeometry(0.075, 0.32, 4, 10), 0, 1.48, 0.03, 0, Math.PI / 2);
        add(new THREE.CapsuleGeometry(0.06, 0.55, 4, 10), 0.25, 1.15, 0.03, 0, 0.12);
        add(new THREE.CapsuleGeometry(0.06, 0.55, 4, 10), -0.25, 1.15, 0.03, 0, -0.12);
        add(new THREE.CapsuleGeometry(0.08, 0.68, 4, 10), 0.1, 0.48, 0.03);
        add(new THREE.CapsuleGeometry(0.08, 0.68, 4, 10), -0.1, 0.48, 0.03);
    }

    geometryFor(style) {
        if (this.meshes[style]) return this.meshes[style];
        const cape = this.data.capes[STYLES[style]];
        const g = new THREE.BufferGeometry();
        g.setAttribute('position', new THREE.Float32BufferAttribute(cape.position, 3));
        g.setAttribute('normal', new THREE.Float32BufferAttribute(cape.normal, 3));
        g.setAttribute('uv', new THREE.Float32BufferAttribute(cape.uv, 2));
        g.setIndex(cape.index);
        const mesh = new THREE.Mesh(g, this.material);
        return this.meshes[style] = mesh;
    }

    setStyle(style) {
        this.style = style;
        if (!this.data || !this.figure) return;
        if (this.cape) this.figure.remove(this.cape);
        this.cape = this.geometryFor(style);
        this.figure.add(this.cape);
    }

    update() {
        if (this.texture) this.texture.needsUpdate = true;
    }

    bindDrag() {
        const canvas = this.renderer.domElement;
        let from = null;
        canvas.addEventListener('pointerdown', e => {
            this.spin = false;
            from = { x: e.clientX, yaw: this.yaw };
            canvas.setPointerCapture(e.pointerId);
        });
        canvas.addEventListener('pointermove', e => {
            if (!from) return;
            this.yaw = from.yaw + (e.clientX - from.x) * 0.012;
        });
        const end = () => { from = null; };
        canvas.addEventListener('pointerup', end);
        canvas.addEventListener('pointercancel', end);
    }

    dispose() {
        if (!this.renderer) return;
        cancelAnimationFrame(this.frame);
        Object.values(this.meshes).forEach(m => m.geometry.dispose());
        this.figure.traverse(o => { if (o.isMesh && o.geometry) o.geometry.dispose(); });
        if (this.texture) this.texture.dispose();
        if (this.material) this.material.dispose();
        this.renderer.dispose();
    }
}
