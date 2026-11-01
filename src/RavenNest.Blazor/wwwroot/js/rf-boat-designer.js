// The boat designer's 3D view (the game's docs/own-boats.md, "Customizing boats", phase 4). It builds a boat the way
// the game's BoatAssembler does, from the parts the game's tools/sailing/export-boats-web.cs wrote to /boats: the
// ship's hull, front, middle, back, figurehead and ram of the look's variants, the rig every one of that ship has, the
// look's sails and planks by material, and its flag at the masthead (the small boats fly it from a pole at the stern).
// Drag to turn, scroll to zoom. Everything is in the game's (Unity's) space under a root mirrored in z, so the part
// matrices and the assembler's numbers are used as they are.
import * as THREE from './three.module.min.js';

const BASE = '/boats/';
let manifestPromise = null;
const meshCache = new Map();
const textureCache = new Map();

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
    const t = new THREE.TextureLoader().load(BASE + file + '?v=' + version);
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
        const m = await manifest();
        const l = parseLook(look, boat);
        const group = new THREE.Group();
        const sails = m.sails[l.sails - 1], wood = m.woods[l.wood - 1];
        const swap = n => n.startsWith('ShipSails_') && sails ? sails : n.startsWith('Ship_Planks_') && wood ? wood : n;
        const ship = m.ships[boat];
        let meshes = [];
        if (ship) {
            const pick = (slot, v, fallback) => ship[slot][v - 1] || (fallback ? ship[slot][fallback - 1] : null);
            const lists = await Promise.all([
                this.addPart(m, pick('hull', l.hull, 1), group, swap),
                this.addPart(m, pick('front', l.front, 1), group, swap),
                this.addPart(m, pick('middle', l.middle, 1), group, swap),
                this.addPart(m, pick('back', l.back, boat === 'Sloop' ? 3 : 1), group, swap),
                this.addPart(m, pick('figurehead', l.figurehead), group, swap),
                this.addPart(m, pick('ram', l.ram), group, swap),
                this.addPart(m, ship.rig, group, swap),
            ]);
            meshes = lists.flat();
        } else {
            meshes = await this.addPart(m, m.boats[boat], group, swap);
        }
        if (token !== this.token) return; // a newer look was asked for meanwhile

        await this.addFlag(m, group, meshes, l.flag, !!ship);
        if (token !== this.token) return;
        if (this.boat) this.root.remove(this.boat);
        this.boat = group;
        this.root.add(group);
        this.frame0(group);
    }

    async addFlag(m, group, meshes, flag, ship) {
        const material = m.flags[flag - 1];
        if (!material) return;
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
        let at, scale;
        const masts = meshes.filter(x => x.name.includes('_Mast_') && !x.name.includes('Boom') && !x.name.includes('Sail') && !x.name.includes('Locator'));
        if (ship && masts.length) {
            let top = null;
            for (const mast of masts) { const b = boundsOf([mast]); if (!top || b.max.y > top.max.y) top = b; }
            const c = top.getCenter(new THREE.Vector3());
            at = new THREE.Vector3(c.x, top.max.y, c.z - 0.9);
            scale = 0.75;
        } else {
            const hulls = meshes.filter(x => x.name.includes('Hull') || x.name === 'Model');
            const b = boundsOf(hulls.length ? hulls : meshes);
            // the kit's post lies along x: stood upright, its foot just inside the stern's gunwale
            const poleLength = 2.34 * 0.7, foot = b.max.y - 0.3, z = b.min.z + 0.35;
            const pole = new THREE.Group();
            pole.position.set(0, foot + poleLength * 0.5, z);
            pole.rotation.z = -Math.PI / 2;
            pole.scale.setScalar(0.7);
            await this.addPart(m, m.flagPole, pole, n => n);
            group.add(pole);
            at = new THREE.Vector3(0, foot + poleLength, z - 0.05);
            scale = 0.32;
        }
        const cloth = new THREE.Group();
        cloth.position.copy(at);
        cloth.rotation.y = Math.PI / 2;
        cloth.scale.setScalar(scale);
        await this.addPart(m, m.flag, cloth, flagSwap);
        group.add(cloth);
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
