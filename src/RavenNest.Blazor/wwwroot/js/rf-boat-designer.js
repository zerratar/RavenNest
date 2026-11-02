// The boat designer's 3D view (the game's docs/own-boats.md, "Customizing boats", phase 4). It builds a boat the way
// the game's BoatAssembler does, from the parts the game's tools/sailing/export-boats-web.cs wrote to /boats: the
// ship's hull, front, middle, back, figurehead and ram of the look's variants, the rig every one of that ship has, the
// look's sails and planks by material, and its flag at the masthead (the small boats fly it from a pole at the stern).
// Drag to turn, scroll to zoom. Everything is in the game's (Unity's) space under a root mirrored in z, so the part
// matrices and the assembler's numbers are used as they are. paintThumbs draws each part's options on the boat itself,
// framed on that part, into the page's picker tiles (kept here, so the pictures never go through the server).
import * as THREE from './three.module.min.js';

const BASE = '/boats/';
// the clan's logo on the plain dark flag (the game's BoatAssembler.ClanFlag)
const CLAN_FLAG = 99, PLAIN_DARK_FLAG = 6;
let manifestPromise = null;
const meshCache = new Map();
const textureCache = new Map();
// textures still loading: a thumbnail waits for them, or it would be drawn untextured
const loading = new Set();

function manifest() {
    manifestPromise ??= fetch(BASE + 'boats.json', { cache: 'no-cache' }).then(r => r.json());
    return manifestPromise;
}

// one mesh, unpacked from its file: positions in int16 over the mesh's bounds, normals in int8, uvs in uint16
function loadMesh(m, id) {
    if (meshCache.has(id)) return meshCache.get(id);
    const info = m.meshes[id];
    const promise = fetch(BASE + 'meshes/' + id + '.bin?v=' + m.version).then(r => r.arrayBuffer()).then(buf => {
        const n = info.vertices;
        const q = new Int16Array(buf, 0, n * 3);
        const pos = new Float32Array(n * 3);
        for (let i = 0; i < n; i++) {
            for (let a = 0; a < 3; a++) {
                const lo = info.min[a], hi = info.max[a];
                pos[i * 3 + a] = lo + (q[i * 3 + a] + 32768) / 65535 * (hi - lo);
            }
        }
        const nq = new Int8Array(buf, info.normals, n * 4);
        const nrm = new Float32Array(n * 3);
        for (let i = 0; i < n; i++) for (let a = 0; a < 3; a++) nrm[i * 3 + a] = nq[i * 4 + a] / 127;
        const uq = new Uint16Array(buf, info.uvs, n * 2);
        const uv = new Float32Array(n * 2);
        for (let i = 0; i < n; i++) {
            uv[i * 2] = info.uvMin[0] + uq[i * 2] / 65535 * (info.uvMax[0] - info.uvMin[0]);
            uv[i * 2 + 1] = info.uvMin[1] + uq[i * 2 + 1] / 65535 * (info.uvMax[1] - info.uvMin[1]);
        }
        const count = info.subs.reduce((s, x) => s + x[1], 0);
        const index = info.wide ? new Uint32Array(buf, info.indices, count) : new Uint16Array(buf, info.indices, count);
        const g = new THREE.BufferGeometry();
        g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
        g.setAttribute('normal', new THREE.BufferAttribute(nrm, 3));
        g.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
        g.setIndex(new THREE.BufferAttribute(info.wide ? new Uint32Array(index) : new Uint16Array(index), 1));
        info.subs.forEach((s, i) => g.addGroup(s[0], s[1], i));
        g.computeBoundingBox();
        return g;
    });
    meshCache.set(id, promise);
    return promise;
}

function texture(file, version) {
    if (!file) return null;
    if (textureCache.has(file)) return textureCache.get(file);
    let done;
    const ready = new Promise(resolve => { done = resolve; });
    loading.add(ready);
    const finish = () => { loading.delete(ready); done(); };
    const t = new THREE.TextureLoader().load(BASE + file + '?v=' + version, finish, undefined, finish);
    t.colorSpace = THREE.SRGBColorSpace;
    t.anisotropy = 4;
    textureCache.set(file, t);
    return t;
}

class Designer {
    constructor(container, options) {
        this.container = container;
        this.options = options || {};
        this.materials = new Map();
        this.yaw = -0.7;
        this.pitch = 0.32;
        this.distance = 40;
        this.target = new THREE.Vector3(0, 6, 0);
        this.token = 0;
        this.thumbToken = 0;
        this.thumbs = new Map();
        this.clanFlags = new Map();
        this.clanLogoUrl = null;

        this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
        this.renderer.outputColorSpace = THREE.SRGBColorSpace;
        container.appendChild(this.renderer.domElement);
        this.scene = new THREE.Scene();
        this.camera = new THREE.PerspectiveCamera(35, 1, 0.1, 2000);
        this.scene.add(new THREE.HemisphereLight(0xdfeeff, 0x2b3a48, 1.4));
        const sun = new THREE.DirectionalLight(0xfff2dd, 2.2);
        sun.position.set(30, 60, 20);
        this.scene.add(sun);

        // the sea: a disc of water round the boat, its edge lost in the haze (the hull below the waterline hidden, as
        // in the game)
        const sky = 0xbcd6e4;
        this.scene.background = new THREE.Color(sky);
        this.scene.fog = new THREE.Fog(sky, 90, 380);
        const water = new THREE.Mesh(new THREE.CircleGeometry(400, 64), new THREE.MeshStandardMaterial({ color: 0x1d6a8c, roughness: 0.35, metalness: 0.05 }));
        water.rotation.x = -Math.PI / 2;
        water.position.y = 0.02;
        this.scene.add(water);

        // the game's space: Unity is left-handed, so z is mirrored once at the root
        this.root = new THREE.Group();
        this.root.scale.z = -1;
        this.scene.add(this.root);

        this.bindInput();
        this.resize = () => this.fit();
        window.addEventListener('resize', this.resize);
        this.fit();
        const loop = () => {
            if (this.disposed) return;
            if (!this.dragging && this.options.spin !== false) this.yaw += 0.0012;
            this.place();
            this.renderer.render(this.scene, this.camera);
            this.frame = requestAnimationFrame(loop);
        };
        loop();
    }

    fit() {
        const w = Math.max(10, this.container.clientWidth), h = Math.max(10, this.container.clientHeight);
        this.renderer.setSize(w, h, false);
        this.renderer.domElement.style.width = '100%';
        this.renderer.domElement.style.height = '100%';
        this.camera.aspect = w / h;
        this.camera.updateProjectionMatrix();
    }

    place() {
        const c = Math.cos(this.pitch);
        this.camera.position.set(
            this.target.x + Math.sin(this.yaw) * c * this.distance,
            this.target.y + Math.sin(this.pitch) * this.distance,
            this.target.z + Math.cos(this.yaw) * c * this.distance);
        this.camera.lookAt(this.target);
    }

    bindInput() {
        const el = this.renderer.domElement;
        el.style.cursor = 'grab';
        el.style.touchAction = 'none';
        let from = null;
        el.addEventListener('pointerdown', e => {
            from = { x: e.clientX, y: e.clientY, yaw: this.yaw, pitch: this.pitch };
            this.dragging = true;
            el.setPointerCapture(e.pointerId);
            el.style.cursor = 'grabbing';
        });
        el.addEventListener('pointermove', e => {
            if (!from) return;
            this.yaw = from.yaw - (e.clientX - from.x) * 0.008;
            this.pitch = Math.min(1.35, Math.max(0.02, from.pitch + (e.clientY - from.y) * 0.006));
        });
        const end = () => { from = null; this.dragging = false; el.style.cursor = 'grab'; };
        el.addEventListener('pointerup', end);
        el.addEventListener('pointercancel', end);
        el.addEventListener('wheel', e => {
            e.preventDefault();
            this.distance = Math.min(this.maxDistance || 200, Math.max(this.minDistance || 6, this.distance * (e.deltaY > 0 ? 1.1 : 0.9)));
        }, { passive: false });
    }

    material(m, name) {
        if (this.materials.has(name)) return this.materials.get(name);
        const info = m.materials[name] || { color: [0.7, 0.7, 0.7] };
        const mat = new THREE.MeshStandardMaterial({
            map: texture(info.texture, m.version),
            color: new THREE.Color(info.color[0], info.color[1], info.color[2]),
            roughness: 0.85,
            metalness: 0,
            alphaTest: info.clip ? 0.5 : 0,
            side: info.doubleSided ? THREE.DoubleSide : THREE.FrontSide,
        });
        this.materials.set(name, mat);
        return mat;
    }

    // a part's meshes under a group, with the look's sails and planks in place of the kit's own
    async addPart(m, part, parent, swap) {
        if (!part) return [];
        const meshes = [];
        await Promise.all(part.map(async item => {
            const g = await loadMesh(m, item.mesh);
            const mats = item.materials.map(n => this.material(m, swap(n)));
            const mesh = new THREE.Mesh(g, mats.length === 1 ? mats[0] : mats);
            mesh.name = item.name;
            mesh.matrixAutoUpdate = false;
            mesh.matrix.fromArray(item.matrix);
            parent.add(mesh);
            meshes.push(mesh);
        }));
        return meshes;
    }

    /** Shows a boat ("Sloop", "Brigantine", "Galleon", "Rowboat", "Fishing Boat") in a look ("look:1;sails=7;..."). */
    async show(boat, look) {
        const token = ++this.token;
        const built = await this.build(boat, look);
        if (token !== this.token) return; // a newer look was asked for meanwhile
        if (this.boat) this.root.remove(this.boat);
        this.boat = built.group;
        this.root.add(built.group);
        this.frame0(built.group);
    }

    // the boat in a look, with each part's meshes kept apart (for framing one in a thumbnail)
    async build(boat, look) {
        const m = await manifest();
        const l = parseLook(look, boat);
        const group = new THREE.Group();
        const sails = m.sails[l.sails - 1], wood = m.woods[l.wood - 1];
        const swap = n => n.startsWith('ShipSails_') && sails ? sails : n.startsWith('Ship_Planks_') && wood ? wood : n;
        const ship = m.ships[boat];
        const parts = {};
        if (ship) {
            const pick = (slot, v, fallback) => ship[slot][v - 1] || (fallback ? ship[slot][fallback - 1] : null);
            [parts.hull, parts.front, parts.middle, parts.back, parts.figurehead, parts.ram, parts.rig] = await Promise.all([
                this.addPart(m, pick('hull', l.hull, 1), group, swap),
                this.addPart(m, pick('front', l.front, 1), group, swap),
                this.addPart(m, pick('middle', l.middle, 1), group, swap),
                this.addPart(m, pick('back', l.back, boat === 'Sloop' ? 3 : 1), group, swap),
                this.addPart(m, pick('figurehead', l.figurehead), group, swap),
                this.addPart(m, pick('ram', l.ram), group, swap),
                this.addPart(m, ship.rig, group, swap),
            ]);
        } else {
            parts.hull = await this.addPart(m, m.boats[boat], group, swap);
        }
        const meshes = Object.values(parts).flat();
        parts.flag = await this.addFlag(m, group, meshes, l.flag, !!ship);
        return { group, parts };
    }

    /**
     * Draws the options of the parts in a picker: every img[data-slot][data-variant] under the element gets a picture
     * of the boat wearing that option, framed on the part. Drawn once per boat and option, then kept.
     */
    async paintThumbs(element, boat, look) {
        const token = ++this.thumbToken;
        const images = [...element.querySelectorAll('img[data-slot][data-variant]')];
        for (const img of images) {
            if (token !== this.thumbToken || this.disposed) return; // the page moved on
            const slot = img.dataset.slot, variant = +img.dataset.variant;
            const key = boat + '|' + slot + '|' + variant + (variant === CLAN_FLAG ? '|' + this.clanLogoUrl : '');
            let url = this.thumbs.get(key);
            if (!url) {
                url = await this.thumbnail(boat, withPart(look, slot, variant), slot);
                if (url) this.thumbs.set(key, url);
            }
            if (url && img.isConnected) img.src = url;
        }
    }

    async thumbnail(boat, look, slot) {
        const kit = thumbKit();
        const built = await this.build(boat, look);
        const parts = built.parts;
        const focus = focusOf(parts, slot);
        if (!focus.length) return null;
        // the rig and the flag hide the decks, the hull and the bow: only the sails and the flag keep them
        if (slot !== 'sails') for (const mesh of parts.rig || []) mesh.visible = false;
        if (slot !== 'flag') for (const mesh of parts.flag || []) mesh.visible = false;
        while (loading.size) await Promise.all([...loading]);
        kit.root.add(built.group);
        kit.root.updateMatrixWorld(true);
        const box = new THREE.Box3();
        for (const mesh of focus) box.expandByObject(mesh);
        const hull = new THREE.Box3();
        for (const mesh of parts.hull || focus) hull.expandByObject(mesh);
        const centre = box.getCenter(new THREE.Vector3());
        const radius = Math.max(0.3, box.getBoundingSphere(new THREE.Sphere()).radius);
        // the bow or the stern, whichever end the part is at, seen from the side and a little above
        const along = Math.sign(centre.z - hull.getCenter(new THREE.Vector3()).z) || 1;
        const dir = slot === 'figurehead' || slot === 'ram' || slot === 'front' || slot === 'back'
            ? new THREE.Vector3(-0.9, 0.35, 0.8 * along)
            : slot === 'wood' ? new THREE.Vector3(-0.7, 1.0, 0.3)
            : slot === 'sails' || slot === 'flag' ? new THREE.Vector3(-1, 0.12, 0.25)
            : new THREE.Vector3(-1, 0.4, 0.35);
        dir.normalize();
        const margin = slot === 'figurehead' || slot === 'ram' ? 1.25 : 1.0;
        const distance = radius / Math.sin(THREE.MathUtils.degToRad(kit.camera.fov / 2)) * margin;
        kit.camera.position.copy(centre).addScaledVector(dir, distance);
        kit.camera.near = distance * 0.05;
        kit.camera.far = distance * 4;
        kit.camera.updateProjectionMatrix();
        kit.camera.lookAt(centre);
        // sails and flags on a sky, or the dark ones would be lost on the page's dark tile
        kit.scene.background = slot === 'sails' || slot === 'flag' ? kit.sky : null;
        kit.renderer.render(kit.scene, kit.camera);
        const url = kit.renderer.domElement.toDataURL('image/webp', 0.9);
        kit.root.remove(built.group);
        return url;
    }

    // the flag as the game's BoatAssembler.Flag hangs it: from the tallest masthead (the ships and the fishing boat's
    // sail) or a pole at the rowboat's stern; its leading edge at the mast or pole, streaming aft
    async addFlag(m, group, meshes, flag, ship) {
        const clan = flag === CLAN_FLAG;
        const material = m.flags[(clan ? PLAIN_DARK_FLAG : flag) - 1];
        if (!material) return [];
        group.updateMatrixWorld(true);
        const boundsOf = list => {
            const b = new THREE.Box3();
            for (const mesh of list) {
                const box = mesh.geometry.boundingBox.clone().applyMatrix4(mesh.matrix);
                b.union(box);
            }
            return b;
        };
        const flagSwap = () => material;
        const HALF = 1.89; // half the flag cloth's width, hanging from the middle of its top edge
        let at, scale;
        const masts = meshes.filter(x => x.name.includes('_Mast_') && !x.name.includes('Boom') && !x.name.includes('Sail') && !x.name.includes('Locator'));
        if (masts.length) {
            let top = null;
            for (const mast of masts) { const b = boundsOf([mast]); if (!top || b.max.y > top.max.y) top = b; }
            const c = top.getCenter(new THREE.Vector3()), size = top.getSize(new THREE.Vector3());
            scale = ship ? 0.75 : 0.42;
            at = new THREE.Vector3(c.x, top.max.y, c.z - HALF * scale - Math.min(size.x, size.z) / 2);
        } else {
            // the kit's banded post stands upright from its foot, just inside the stern's gunwale
            const hulls = meshes.filter(x => x.name.includes('Hull') || x.name === 'Model');
            const b = boundsOf(hulls.length ? hulls : meshes);
            const poleScale = 0.7, poleHeight = 2.36 * poleScale, foot = b.max.y - 0.3, z = b.min.z + 0.35;
            const pole = new THREE.Group();
            pole.position.set(0, foot, z);
            pole.scale.setScalar(poleScale);
            await this.addPart(m, m.flagPole, pole, n => n);
            group.add(pole);
            scale = 0.32;
            at = new THREE.Vector3(0, foot + poleHeight - 0.05, z - HALF * scale - 0.06);
        }
        const cloth = new THREE.Group();
        cloth.position.copy(at);
        cloth.rotation.y = Math.PI / 2;
        cloth.scale.setScalar(scale);
        const clothMeshes = await this.addPart(m, m.flag, cloth, flagSwap);
        if (clan && this.clanLogoUrl) {
            const logo = await this.clanFlagMaterial(m, material, this.clanLogoUrl);
            if (logo) for (const mesh of clothMeshes) mesh.material = logo;
        }
        group.add(cloth);
        return clothMeshes;
    }

    /** The character's clan logo (its address), for the clan flag; null for none. */
    clanLogo(url) {
        this.clanLogoUrl = url || null;
    }

    // the plain dark flag's sheet with the logo in the middle of the shape the flag cloth shows (uv 0.40 to 0.73
    // across, 0.71 to 0.97 up), as the game lays its symbol; kept per logo
    clanFlagMaterial(m, plainName, url) {
        if (this.clanFlags.has(url)) return this.clanFlags.get(url);
        const info = m.materials[plainName];
        const load = src => new Promise(resolve => {
            const img = new Image();
            img.onload = () => resolve(img);
            img.onerror = () => resolve(null);
            img.src = src;
        });
        const promise = Promise.all([load(BASE + info.texture + '?v=' + m.version), load(url)]).then(([sheet, logo]) => {
            if (!sheet || !logo) return null;
            const size = sheet.width;
            const canvas = document.createElement('canvas');
            canvas.width = canvas.height = size;
            const g = canvas.getContext('2d');
            g.drawImage(sheet, 0, 0, size, size);
            const u0 = 0.4014, u1 = 0.7325, v0 = 0.7078, v1 = 0.9694;
            const side = (v1 - v0) * size * 0.72;
            const cx = (u0 + u1) / 2 * size, cy = (1 - (v0 + v1) / 2) * size;
            g.drawImage(logo, cx - side / 2, cy - side / 2, side, side);
            const map = new THREE.CanvasTexture(canvas);
            map.colorSpace = THREE.SRGBColorSpace;
            map.anisotropy = 4;
            return new THREE.MeshStandardMaterial({ map, roughness: 0.85, metalness: 0, alphaTest: 0.5, side: THREE.DoubleSide });
        });
        this.clanFlags.set(url, promise);
        return promise;
    }

    // the camera round the whole boat, the first time and when its size changes much
    frame0(group) {
        const box = new THREE.Box3().setFromObject(group);
        const size = box.getSize(new THREE.Vector3());
        const centre = box.getCenter(new THREE.Vector3());
        const reach = Math.max(size.x, size.y, size.z);
        this.target.set(centre.x, Math.max(1, centre.y), centre.z);
        this.minDistance = reach * 0.4;
        this.maxDistance = reach * 4;
        // the whole boat, masthead flag and all, in the view's height
        const radius = box.getBoundingSphere(new THREE.Sphere()).radius;
        if (!this.reach || Math.abs(this.reach - reach) / reach > 0.2)
            this.distance = radius / Math.tan(THREE.MathUtils.degToRad(this.camera.fov / 2)) * 0.75;
        this.reach = reach;
    }

    dispose() {
        this.disposed = true;
        cancelAnimationFrame(this.frame);
        window.removeEventListener('resize', this.resize);
        this.renderer.dispose();
        this.renderer.domElement.remove();
    }
}

// the meshes a thumbnail frames: the part itself, the main mast's sails for sails, the middle deck for planks
function focusOf(parts, slot) {
    switch (slot) {
        case 'sails': {
            // the main mast's sails, big enough to tell apart (the fishing boat's sail is among its own meshes)
            const cloth = (parts.rig || parts.hull || []).filter(mesh => mesh.name.includes('Sails') || mesh.name.includes('_Sail_'));
            const main = cloth.filter(mesh => mesh.name.includes('Middle'));
            return main.length ? main : cloth.length ? cloth : parts.rig || [];
        }
        case 'wood': return parts.middle || [];
        default: return parts[slot] || [];
    }
}

function withPart(look, slot, variant) {
    const parts = (look || 'look:1').split(';').filter(p => !p.startsWith(slot + '='));
    parts.push(slot + '=' + variant);
    return parts.join(';');
}

// one small renderer for every thumbnail, on a clear background so the page's tile shows through
const THUMB = 220;
let kitCache = null;
function thumbKit() {
    if (kitCache) return kitCache;
    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
    renderer.setPixelRatio(1);
    renderer.setSize(THUMB, THUMB, false);
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    const scene = new THREE.Scene();
    scene.add(new THREE.HemisphereLight(0xdfeeff, 0x2b3a48, 1.6));
    const sun = new THREE.DirectionalLight(0xfff2dd, 2.4);
    sun.position.set(-30, 60, 40);
    scene.add(sun);
    const rim = new THREE.DirectionalLight(0xcfe3ff, 1.6);
    rim.position.set(40, 30, -50);
    scene.add(rim);
    const root = new THREE.Group();
    root.scale.z = -1;
    scene.add(root);
    const canvas = document.createElement('canvas');
    canvas.width = 4;
    canvas.height = 64;
    const g = canvas.getContext('2d');
    const fade = g.createLinearGradient(0, 0, 0, 64);
    fade.addColorStop(0, '#d4e4ee');
    fade.addColorStop(1, '#7f9db1');
    g.fillStyle = fade;
    g.fillRect(0, 0, 4, 64);
    const sky = new THREE.CanvasTexture(canvas);
    sky.colorSpace = THREE.SRGBColorSpace;
    kitCache = { renderer, scene, root, sky, camera: new THREE.PerspectiveCamera(30, 1, 0.05, 500) };
    return kitCache;
}

function parseLook(tag, boat) {
    const look = { hull: 1, front: 1, middle: 1, back: boat === 'Sloop' ? 3 : 1, figurehead: 0, ram: 0, sails: 1, flag: 0, wood: 1 };
    if (tag && tag.startsWith('look:')) {
        for (const part of tag.split(';')) {
            const [k, v] = part.split('=');
            if (k in look && !isNaN(+v)) look[k] = +v;
        }
    }
    return look;
}

/** The pictures of the sails, flags and planks (their baked textures), for the page's swatches. Index 0 is variant 1. */
export async function swatches() {
    const m = await manifest();
    const of = names => names.map(n => m.materials[n]?.texture ? BASE + m.materials[n].texture + '?v=' + m.version : null);
    return { sails: of(m.sails), flag: of(m.flags), wood: of(m.woods) };
}

/** Opens the 3D view in an element; Blazor keeps the returned object and calls show() and dispose() on it. */
export function create(container, options) {
    return new Designer(container, options);
}
