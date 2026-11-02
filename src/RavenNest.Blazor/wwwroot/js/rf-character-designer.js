// The character customizer's view: the player's character as the game builds it, from the parts the game's
// tools/characters/export-characters-web.cs wrote to /characters (skinned meshes in three.js space, the shared
// skeleton, the game's idle and cheer, the Synty material's fixed colours baked and the picked ones as masks).
//
// Two views behind one API (show, color, focus, cheer, dispose):
// - the 3D one (three.js, WebGL): the character idles on a lit stage; drag to turn, scroll to zoom;
// - the flat one, for browsers without WebGL: layers the same parts were pre-rendered into
//   (tools/characters/bake-flat.html, eight directions), composited on a 2D canvas per pixel: each layer brings its
//   baked colour, its masks, its light and its depth, so any colour works and nearer layers hide farther ones.
import * as THREE from './three.module.min.js';

const BASE = '/characters/';
let manifestPromise = null;
let flatPromise = null;

function manifest() {
    manifestPromise ??= fetch(BASE + 'characters.json', { cache: 'no-cache' }).then(r => r.json());
    return manifestPromise;
}

function flatManifest() {
    flatPromise ??= fetch(BASE + 'flat/flat.json', { cache: 'no-cache' }).then(r => r.ok ? r.json() : null).catch(() => null);
    return flatPromise;
}

/** True when the browser can draw the 3D view. */
export function supports3d() {
    try {
        const c = document.createElement('canvas');
        return !!(window.WebGLRenderingContext && (c.getContext('webgl2') || c.getContext('webgl')));
    } catch {
        return false;
    }
}

// ---- The appearance ------------------------------------------------------------------------------------------------

/** The game's SyntyAppearance as the view wants it (Blazor sends either camelCase or PascalCase, numbers or names). */
export function normalize(a) {
    a = a || {};
    const get = (k, d) => a[k] ?? a[k[0].toUpperCase() + k.slice(1)] ?? d;
    const gender = get('gender', 0);
    const female = gender === 1 || gender === 'Female' || gender === 'female';
    return {
        gender: female ? 'female' : 'male',
        head: +get('head', 0),
        hair: +get('hair', 0),
        eyebrows: +get('eyebrows', 0),
        facialHair: female ? -1 : +get('facialHair', -1),
        skin: hex(get('skinColor', '#d6b8ae')),
        // the game colours the hair, the eyebrows and the beard all with the beard colour
        hair_: hex(get('beardColor', get('hairColor', '#3c2823'))),
        stubble: hex(get('stubbleColor', get('skinColor', '#d6b8ae'))),
        warPaint: hex(get('warPaintColor', '#000000')),
        eyes: hex(get('eyeColor', '#000000')),
    };
}

function hex(v) {
    if (!v) return '#000000';
    v = String(v).trim();
    if (v[0] !== '#') v = '#' + v;
    return /^#[0-9a-fA-F]{6}$/.test(v) ? v.toLowerCase() : '#000000';
}

// the parts an appearance is made of, by kind (index out of range: none)
function partsOf(m, a) {
    const g = m.parts[a.gender];
    const at = (list, i) => (i >= 0 && i < list.length ? list[i] : null);
    return {
        body: g.body.filter(Boolean),
        head: at(g.heads, a.head),
        eyebrows: at(g.eyebrows, a.eyebrows),
        beard: at(g.beards, a.facialHair),
        hair: at(m.parts.hairs, a.hair),
    };
}

/** How many of each part there are, for the page's pickers. */
export async function counts() {
    const m = await manifest();
    const n = list => {
        let last = -1;
        list.forEach((p, i) => { if (p) last = i; });
        return last + 1;
    };
    return {
        maleHeads: n(m.parts.male.heads), femaleHeads: n(m.parts.female.heads),
        maleEyebrows: n(m.parts.male.eyebrows), femaleEyebrows: n(m.parts.female.eyebrows),
        maleBeards: n(m.parts.male.beards), femaleBeards: n(m.parts.female.beards),
        hairs: n(m.parts.hairs),
    };
}

/** The thumbnails the bake wrote, by kind and gender ({ heads: { male: [url...] } ...}), or null before a bake. */
export async function thumbnails() {
    const f = await flatManifest();
    if (!f || !f.thumbs) return null;
    const out = {};
    for (const [kind, byGender] of Object.entries(f.thumbs)) {
        out[kind] = {};
        for (const [g, files] of Object.entries(byGender)) out[kind][g] = files.map(x => (x ? BASE + 'flat/' + x + '?v=' + f.version : null));
    }
    return out;
}

// ---- Meshes --------------------------------------------------------------------------------------------------------

const meshCache = new Map();

// one skinned mesh, unpacked: positions int16 over its bounds, normals int8, uvs uint16, 4 bone indices and 4 weights
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
        const skin = new Uint8Array(buf, info.skin, n * 8);
        const idx = new Uint16Array(n * 4), wt = new Float32Array(n * 4);
        for (let i = 0; i < n; i++) {
            for (let k = 0; k < 4; k++) {
                idx[i * 4 + k] = skin[i * 8 + k];
                wt[i * 4 + k] = skin[i * 8 + 4 + k] / 255;
            }
        }
        const count = info.subs.reduce((s, x) => s + x[1], 0);
        const index = new Uint16Array(buf.slice(info.indices, info.indices + count * 2));
        const g = new THREE.BufferGeometry();
        g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
        g.setAttribute('normal', new THREE.BufferAttribute(nrm, 3));
        g.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
        g.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(idx, 4));
        g.setAttribute('skinWeight', new THREE.Float32BufferAttribute(wt, 4));
        g.setIndex(new THREE.BufferAttribute(index, 1));
        g.computeBoundingBox();
        g.computeBoundingSphere();
        return g;
    });
    meshCache.set(id, promise);
    return promise;
}

// ---- The material: the game's Synty shader graph, its picked colours over the baked rest ----------------------------

function characterMaterial(m) {
    const loader = new THREE.TextureLoader();
    const tex = (file, color) => {
        const t = loader.load(BASE + file + '?v=' + m.version);
        t.colorSpace = color ? THREE.SRGBColorSpace : THREE.NoColorSpace;
        t.anisotropy = 4;
        return t;
    };
    const mat = new THREE.MeshStandardMaterial({
        map: tex(m.material.base, true),
        roughness: 1 - (m.material.smoothness ?? 0.2) * 0.8,
        metalness: m.material.metallic ?? 0,
    });
    const uniforms = {
        masksA: { value: tex(m.material.masksA, false) },
        masksB: { value: tex(m.material.masksB, false) },
        cHair: { value: new THREE.Color('#3c2823') },
        cSkin: { value: new THREE.Color('#d6b8ae') },
        cStubble: { value: new THREE.Color('#d6b8ae') },
        cScar: { value: new THREE.Color().setRGB(...m.material.scar, THREE.LinearSRGBColorSpace) },
        cBodyArt: { value: new THREE.Color('#000000') },
        // the fifth mask is the eyes (the game's graph has a constant red there, its eye colour left unconnected)
        cFifth: { value: new THREE.Color('#000000') },
        bodyArtAmount: { value: m.material.bodyArtAmount ?? 1 },
    };
    mat.onBeforeCompile = shader => {
        Object.assign(shader.uniforms, uniforms);
        shader.fragmentShader = shader.fragmentShader
            .replace('#include <map_pars_fragment>', `#include <map_pars_fragment>
uniform sampler2D masksA; uniform sampler2D masksB;
uniform vec3 cHair; uniform vec3 cSkin; uniform vec3 cStubble; uniform vec3 cScar; uniform vec3 cBodyArt; uniform vec3 cFifth;
uniform float bodyArtAmount;`)
            .replace('#include <map_fragment>', `
vec3 rfC = texture2D(map, vMapUv).rgb;
vec3 rfA = texture2D(masksA, vMapUv).rgb;
vec3 rfB = texture2D(masksB, vMapUv).rgb;
rfC = mix(rfC, cHair, rfA.b);
rfC = mix(rfC, cSkin, rfA.r);
rfC = mix(rfC, cStubble, rfA.g);
rfC = mix(rfC, cScar, rfB.g);
rfC = mix(rfC, cBodyArt, rfB.r * bodyArtAmount);
rfC = mix(rfC, cFifth, rfB.b);
diffuseColor.rgb *= rfC;`);
    };
    mat.customProgramCacheKey = () => 'rf-character';
    mat.userData.uniforms = uniforms;
    return mat;
}

function setColors(uniforms, a) {
    uniforms.cHair.value.set(a.hair_);
    uniforms.cSkin.value.set(a.skin);
    uniforms.cStubble.value.set(a.stubble);
    uniforms.cBodyArt.value.set(a.warPaint);
    uniforms.cFifth.value.set(a.eyes);
}

// ---- Animations ----------------------------------------------------------------------------------------------------

function clipOf(m, name, boneName) {
    const a = m.animations[name];
    if (!a) return null;
    const times = new Float32Array(a.frames);
    for (let f = 0; f < a.frames; f++) times[f] = f / a.fps;
    const tracks = a.tracks.map(t => new THREE.QuaternionKeyframeTrack(boneName(t.bone) + '.quaternion', times, t.q));
    if (a.hips >= 0) tracks.push(new THREE.VectorKeyframeTrack(boneName(a.hips) + '.position', times, a.hipsP));
    return new THREE.AnimationClip(name, (a.frames - 1) / a.fps, tracks);
}

/** The character's skeleton under a group (turned to face +z), its animations and the shared material. */
function buildRig(m) {
    // the game's characters face +z in Unity, -z here once mirrored: turned round to face the camera
    const rig = new THREE.Group();
    rig.rotation.y = Math.PI;
    const bones = m.skeleton.map((b, i) => {
        const bone = new THREE.Bone();
        bone.name = 'b' + i;
        bone.position.fromArray(b.p);
        bone.quaternion.fromArray(b.q);
        bone.scale.fromArray(b.s);
        return bone;
    });
    m.skeleton.forEach((b, i) => (b.parent >= 0 ? bones[b.parent] : rig).add(bones[i]));
    const skeleton = new THREE.Skeleton(bones, m.skeleton.map(b => new THREE.Matrix4().fromArray(b.inverse)));
    const mixer = new THREE.AnimationMixer(rig);
    const name = i => 'b' + i;
    const clips = { idle: clipOf(m, 'idle', name), bored: clipOf(m, 'bored', name), cheer: clipOf(m, 'cheer', name) };
    return { rig, bones, skeleton, mixer, clips, headBone: bones[m.skeleton.findIndex(b => b.name === 'Head')] };
}

/** A warm key from the front left, a cool rim from behind, the sky and the floor's bounce (the camera at +z). */
function stageLights() {
    const group = new THREE.Group();
    group.add(new THREE.HemisphereLight(0xc9d6ea, 0x3a3128, 1.15));
    const key = new THREE.DirectionalLight(0xffe7c4, 2.4);
    key.position.set(-1.6, 3.2, 2.6);
    const rim = new THREE.DirectionalLight(0x8fb4ff, 2.2);
    rim.position.set(1.8, 2.4, -2.8);
    const fill = new THREE.DirectionalLight(0xffc98a, 0.5);
    fill.position.set(2.5, 0.6, 1.5);
    group.add(key, rim, fill);
    return group;
}

/** A part's meshes, skinned to a rig. */
function skinned(geometry, material, skeleton) {
    const mesh = new THREE.SkinnedMesh(geometry, material);
    mesh.frustumCulled = false;
    mesh.bind(skeleton, new THREE.Matrix4());
    return mesh;
}

// ---- The 3D view ---------------------------------------------------------------------------------------------------

class Stage {
    constructor(container, m, options) {
        this.container = container;
        this.m = m;
        this.options = options || {};
        this.yaw = 0.35;
        this.pitch = 0.08;
        this.view = 'body';
        this.token = 0;
        this.shown = new Map(); // mesh id -> SkinnedMesh

        const renderer = this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: !!this.options.capture });
        renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
        renderer.outputColorSpace = THREE.SRGBColorSpace;
        renderer.toneMapping = THREE.NeutralToneMapping;
        renderer.toneMappingExposure = 1.05;
        container.appendChild(renderer.domElement);

        this.scene = new THREE.Scene();
        this.camera = new THREE.PerspectiveCamera(28, 1, 0.05, 100);

        this.scene.add(stageLights());

        if (!this.options.bare) this.scene.add(floor());

        const rig = buildRig(m);
        this.rig = rig.rig;
        this.scene.add(this.rig);
        this.bones = rig.bones;
        this.skeleton = rig.skeleton;
        this.mixer = rig.mixer;
        this.clips = rig.clips;
        this.headBone = rig.headBone;
        this.material = characterMaterial(m);
        if (this.clips.idle) {
            this.idle = this.mixer.clipAction(this.clips.idle);
            this.idle.play();
        }
        this.mixer.addEventListener('finished', () => this.backToIdle());
        this.nextFidget = performance.now() + 14000 + Math.random() * 10000;

        this.target = new THREE.Vector3(0, 1, 0);
        this.distance = 4.4;
        this.wanted = { target: new THREE.Vector3(0, 0.92, 0), distance: 4.7 };

        if (!this.options.still) this.bindInput();
        this.resize = () => this.fit();
        window.addEventListener('resize', this.resize);
        if (window.ResizeObserver) {
            this.observer = new ResizeObserver(this.resize);
            this.observer.observe(container);
        }
        this.fit();
        this.clock = new THREE.Clock();
        if (!this.options.still) {
            const loop = () => {
                if (this.disposed) return;
                this.tick();
                this.frame = requestAnimationFrame(loop);
            };
            loop();
        }
    }

    fit() {
        const w = Math.max(10, this.container.clientWidth), h = Math.max(10, this.container.clientHeight);
        this.renderer.setSize(w, h, false);
        this.renderer.domElement.style.width = '100%';
        this.renderer.domElement.style.height = '100%';
        this.camera.aspect = w / h;
        this.camera.updateProjectionMatrix();
    }

    tick() {
        const dt = Math.min(0.1, this.clock.getDelta());
        if (!this.options.still) {
            const now = performance.now();
            if (now > this.nextFidget && this.current !== 'cheer') {
                this.nextFidget = now + 18000 + Math.random() * 14000;
                if (this.clips.bored && !this.dragging) this.play('bored');
            }
        }
        this.mixer.update(dt);
        // the camera eases to the face or the whole figure
        const k = 1 - Math.exp(-dt * 6);
        if (this.view === 'face' && this.headBone) {
            this.rig.updateMatrixWorld(true);
            this.headBone.getWorldPosition(this.wanted.target);
            this.wanted.target.y += 0.1;
        }
        this.target.lerp(this.wanted.target, k);
        this.distance += (this.wanted.distance - this.distance) * k;
        if (!this.dragging && !this.options.still && this.options.spin) this.yaw += dt * 0.15;
        this.place();
        this.renderer.render(this.scene, this.camera);
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
            this.yaw = from.yaw - (e.clientX - from.x) * 0.01;
            this.pitch = Math.min(0.8, Math.max(-0.25, from.pitch + (e.clientY - from.y) * 0.005));
        });
        const end = () => { from = null; this.dragging = false; el.style.cursor = 'grab'; };
        el.addEventListener('pointerup', end);
        el.addEventListener('pointercancel', end);
        el.addEventListener('wheel', e => {
            e.preventDefault();
            const near = e.deltaY < 0;
            // zooming in far enough goes to the face, out again to the whole figure
            if (near && this.view === 'body' && this.wanted.distance < 2.6) return this.focus('face');
            if (!near && this.view === 'face' && this.wanted.distance > 1.5) return this.focus('body');
            const [lo, hi] = this.view === 'face' ? [0.8, 1.8] : [2.2, 6.5];
            this.wanted.distance = Math.min(hi, Math.max(lo, this.wanted.distance * (near ? 0.9 : 1.1)));
        }, { passive: false });
    }

    /** The camera on the face ('face') or the whole figure ('body'). */
    focus(view) {
        this.view = view === 'face' ? 'face' : 'body';
        if (this.view === 'body') {
            this.wanted.target.set(0, 0.92, 0);
            this.wanted.distance = 4.7;
        } else {
            this.wanted.distance = 1.25;
        }
    }

    play(name) {
        const clip = this.clips[name];
        if (!clip) return;
        const action = this.mixer.clipAction(clip);
        action.reset();
        action.setLoop(THREE.LoopOnce, 1);
        action.clampWhenFinished = true;
        action.fadeIn(0.25);
        if (this.idle) this.idle.fadeOut(0.25);
        action.play();
        this.current = name;
    }

    backToIdle() {
        if (!this.idle) return;
        const action = this.current && this.clips[this.current] ? this.mixer.clipAction(this.clips[this.current]) : null;
        this.idle.reset().fadeIn(0.35).play();
        if (action) action.fadeOut(0.35);
        this.current = null;
    }

    /** A little cheer (after a save). */
    cheer() {
        this.play('cheer');
    }

    getMode() {
        return '3d';
    }

    /** Dresses the character in an appearance; only the parts that changed are swapped. */
    show(appearance) {
        this.ready = this.dress(appearance);
        return this.ready;
    }

    async dress(appearance) {
        const token = ++this.token;
        const a = this.appearance = normalize(appearance);
        setColors(this.material.userData.uniforms, a);
        const parts = partsOf(this.m, a);
        const ids = new Set();
        for (const p of [...parts.body, parts.head, parts.eyebrows, parts.beard, parts.hair]) if (p) p.meshes.forEach(id => ids.add(id));
        const geometries = await Promise.all([...ids].map(id => loadMesh(this.m, id).then(g => [id, g])));
        if (token !== this.token || this.disposed) return;
        for (const [id, mesh] of [...this.shown]) {
            if (ids.has(id)) continue;
            this.rig.remove(mesh);
            this.shown.delete(id);
        }
        for (const [id, g] of geometries) {
            if (this.shown.has(id)) continue;
            const mesh = skinned(g, this.material, this.skeleton);
            this.rig.add(mesh);
            this.shown.set(id, mesh);
        }
        if (this.options.still) this.tick();
    }

    /**
     * Fills the page's option pictures (img data-rf-thumb="kind:index", kind heads|hairs|eyebrows|beards) with this
     * character wearing each option: rendered here, in the corner of the canvas between two frames, so nothing large
     * goes through the page's connection.
     */
    async paintThumbs(root, appearance) {
        await this.ready;
        const imgs = [...root.querySelectorAll('img[data-rf-thumb]')];
        if (!imgs.length || this.disposed) return;
        const base = normalize(appearance);
        const field = { heads: 'head', hairs: 'hair', eyebrows: 'eyebrows', beards: 'facialHair' };
        this.thumbCache ??= new Map();
        const todo = [];
        for (const img of imgs) {
            const [kind, i] = img.dataset.rfThumb.split(':');
            if (!field[kind]) continue;
            const a = { ...base, [field[kind]]: +i };
            const key = kind + '|' + JSON.stringify(a);
            if (this.thumbCache.has(key)) { img.src = this.thumbCache.get(key); continue; }
            todo.push({ img, kind, a, key });
        }
        if (!todo.length) return;
        // every part they need, loaded first: the renders then happen in one go
        const ids = new Set();
        for (const t of todo) {
            const p = partsOf(this.m, t.a);
            for (const part of [...p.body, p.head, p.eyebrows, p.beard, p.hair]) if (part) part.meshes.forEach(id => ids.add(id));
        }
        const geometries = new Map(await Promise.all([...ids].map(id => loadMesh(this.m, id).then(g => [id, g]))));
        if (this.disposed) return;

        const size = 112;
        const canvas = this.renderer.domElement;
        const ratio = this.renderer.getPixelRatio();
        const px = Math.round(size * ratio);
        const out = document.createElement('canvas');
        out.width = out.height = px;
        const g2 = out.getContext('2d');
        const cam = new THREE.PerspectiveCamera(24, 1, 0.05, 20);
        const saved = [...this.rig.children].filter(c => c.isSkinnedMesh);
        const floorObj = this.scene.children.find(c => c.isMesh && !c.isSkinnedMesh);
        const temp = new Map();
        const meshFor = id => {
            if (!temp.has(id)) temp.set(id, skinned(geometries.get(id), this.material, this.skeleton));
            return temp.get(id);
        };
        this.rig.updateMatrixWorld(true);
        const head = new THREE.Vector3();
        this.headBone.getWorldPosition(head);
        const shots = {
            heads: { up: 0.07, distance: 0.62, yaw: 0.3 },
            hairs: { up: 0.11, distance: 0.8, yaw: 0.55 },
            eyebrows: { up: 0.12, distance: 0.36, yaw: 0.2 },
            beards: { up: -0.02, distance: 0.55, yaw: 0.35 },
        };
        saved.forEach(m => this.rig.remove(m));
        if (floorObj) floorObj.visible = false;
        const uniforms = this.material.userData.uniforms;
        const keepColors = { ...this.appearance };
        try {
            this.renderer.setScissorTest(true);
            for (const t of todo) {
                const p = partsOf(this.m, t.a);
                const meshes = [];
                for (const part of [...p.body, p.head, p.eyebrows, p.beard, p.hair]) if (part) part.meshes.forEach(id => meshes.push(meshFor(id)));
                meshes.forEach(m => this.rig.add(m));
                setColors(uniforms, t.a);
                const shot = shots[t.kind];
                // the character faces the camera at yaw 0 (the rig is turned round)
                const target = head.clone().add(new THREE.Vector3(0, shot.up, 0));
                cam.position.set(target.x + Math.sin(shot.yaw) * shot.distance, target.y + 0.02, target.z + Math.cos(shot.yaw) * shot.distance);
                cam.lookAt(target);
                this.renderer.setViewport(0, 0, size, size);
                this.renderer.setScissor(0, 0, size, size);
                this.renderer.setClearColor(0x000000, 0);
                this.renderer.clear();
                this.renderer.render(this.scene, cam);
                g2.clearRect(0, 0, px, px);
                g2.drawImage(canvas, 0, canvas.height - px, px, px, 0, 0, px, px);
                const url = out.toDataURL('image/png');
                this.thumbCache.set(t.key, url);
                t.img.src = url;
                meshes.forEach(m => this.rig.remove(m));
            }
        } finally {
            this.renderer.setScissorTest(false);
            this.fit();
            setColors(uniforms, keepColors);
            saved.forEach(m => this.rig.add(m));
            if (floorObj) floorObj.visible = true;
            // the stage again before the browser shows the canvas
            this.place();
            this.renderer.render(this.scene, this.camera);
        }
    }

    /** One colour changed while a picker is dragged: no parts change, so no round trip to the page. */
    color(channel, value) {
        if (!this.appearance) return;
        const a = this.appearance;
        const map = { skin: 'skin', hair: 'hair_', stubble: 'stubble', warPaint: 'warPaint', eyes: 'eyes' };
        if (!map[channel]) return;
        a[map[channel]] = hex(value);
        setColors(this.material.userData.uniforms, a);
    }

    /** Colour pickers inside an element drive the view directly: inputs with data-rf-color="skin|hair|stubble|warPaint|eyes". */
    listen(root) {
        this.listener = e => {
            const channel = e.target?.dataset?.rfColor;
            if (channel) this.color(channel, e.target.value);
        };
        root.addEventListener('input', this.listener);
        this.listening = root;
    }

    dispose() {
        this.disposed = true;
        cancelAnimationFrame(this.frame);
        window.removeEventListener('resize', this.resize);
        this.observer?.disconnect();
        if (this.listening) this.listening.removeEventListener('input', this.listener);
        this.renderer.dispose();
        this.renderer.domElement.remove();
    }
}

// a dark stone floor fading into the backdrop, a soft shadow under the feet and a thin gold ring round them
function floor() {
    const size = 512;
    const c = document.createElement('canvas');
    c.width = c.height = size;
    const g = c.getContext('2d');
    const grad = g.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
    grad.addColorStop(0, 'rgba(0,0,0,0.55)');
    grad.addColorStop(0.12, 'rgba(0,0,0,0.35)');
    grad.addColorStop(0.3, 'rgba(20,24,32,0.5)');
    grad.addColorStop(1, 'rgba(20,24,32,0)');
    g.fillStyle = grad;
    g.fillRect(0, 0, size, size);
    g.strokeStyle = 'rgba(232,163,61,0.55)';
    g.lineWidth = 3;
    g.beginPath();
    g.arc(size / 2, size / 2, size * 0.2, 0, Math.PI * 2);
    g.stroke();
    g.strokeStyle = 'rgba(232,163,61,0.18)';
    g.lineWidth = 10;
    g.beginPath();
    g.arc(size / 2, size / 2, size * 0.2, 0, Math.PI * 2);
    g.stroke();
    const tex = new THREE.CanvasTexture(c);
    tex.colorSpace = THREE.SRGBColorSpace;
    const mesh = new THREE.Mesh(new THREE.PlaneGeometry(5, 5), new THREE.MeshBasicMaterial({ map: tex, transparent: true, depthWrite: false }));
    mesh.rotation.x = -Math.PI / 2;
    mesh.position.y = 0.002;
    return mesh;
}

// ---- The flat view (no WebGL) --------------------------------------------------------------------------------------

const imageCache = new Map();

function loadImage(url) {
    if (imageCache.has(url)) return imageCache.get(url);
    const p = new Promise((resolve, reject) => {
        const img = new Image();
        img.onload = () => {
            const c = document.createElement('canvas');
            c.width = img.width;
            c.height = img.height;
            const g = c.getContext('2d', { willReadFrequently: true });
            g.drawImage(img, 0, 0);
            resolve(g.getImageData(0, 0, img.width, img.height));
        };
        img.onerror = reject;
        img.src = url;
    });
    imageCache.set(url, p);
    return p;
}

function srgbToLinear(c) {
    return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
}

function linearColor(h) {
    const v = parseInt(hex(h).slice(1), 16);
    return [srgbToLinear(((v >> 16) & 255) / 255), srgbToLinear(((v >> 8) & 255) / 255), srgbToLinear((v & 255) / 255)];
}

// linear to sRGB bytes, a table over 4096 steps
const toSrgb = (() => {
    const t = new Uint8ClampedArray(4097);
    for (let i = 0; i <= 4096; i++) {
        const c = i / 4096;
        t[i] = Math.round(255 * (c <= 0.0031308 ? c * 12.92 : 1.055 * Math.pow(c, 1 / 2.4) - 0.055));
    }
    return t;
})();
const fromSrgb = (() => {
    const t = new Float32Array(256);
    for (let i = 0; i < 256; i++) t[i] = srgbToLinear(i / 255);
    return t;
})();

class Flat {
    constructor(container, m, f, options) {
        this.container = container;
        this.m = m;
        this.f = f;
        this.options = options || {};
        this.direction = 0; // three-quarters from the front
        this.view = 'body';
        this.token = 0;
        this.canvas = document.createElement('canvas');
        this.canvas.width = f.width;
        this.canvas.height = f.height;
        this.canvas.style.width = '100%';
        this.canvas.style.height = '100%';
        this.canvas.style.objectFit = 'contain';
        this.canvas.style.transition = 'transform 0.35s ease';
        // the pictures are waist up: faded out at the bottom rather than cut
        this.canvas.style.maskImage = this.canvas.style.webkitMaskImage = 'linear-gradient(to bottom, #000 72%, transparent 97%)';
        this.canvas.style.cursor = 'grab';
        this.canvas.style.touchAction = 'none';
        this.canvas.className = 'rf-character-flat';
        container.appendChild(this.canvas);
        this.g = this.canvas.getContext('2d');
        this.bindInput();
    }

    bindInput() {
        const el = this.canvas;
        let from = null;
        el.addEventListener('pointerdown', e => {
            from = { x: e.clientX, direction: this.direction };
            el.setPointerCapture(e.pointerId);
            el.style.cursor = 'grabbing';
        });
        el.addEventListener('pointermove', e => {
            if (!from) return;
            const steps = Math.round((e.clientX - from.x) / 40);
            const n = this.f.directions.length;
            const d = (((from.direction - steps) % n) + n) % n;
            if (d !== this.direction) {
                this.direction = d;
                this.draw();
            }
        });
        const end = () => { from = null; el.style.cursor = 'grab'; };
        el.addEventListener('pointerup', end);
        el.addEventListener('pointercancel', end);
    }

    focus(view) {
        this.view = view === 'face' ? 'face' : 'body';
        this.redraw();
    }

    getMode() {
        return '2d';
    }

    // the baked thumbnails (the default colours): this view cannot render new ones
    async paintThumbs(root, appearance) {
        const thumbs = await thumbnails();
        if (!thumbs) return;
        const g = normalize(appearance).gender;
        for (const img of root.querySelectorAll('img[data-rf-thumb]')) {
            const [kind, i] = img.dataset.rfThumb.split(':');
            const list = thumbs[kind]?.[g] || thumbs[kind]?.any;
            const url = list?.[+i];
            if (url && img.getAttribute('src') !== url) img.src = url;
        }
    }

    cheer() {
        // no animation here: a little hop
        this.canvas.animate?.([{ transform: 'translateY(0)' }, { transform: 'translateY(-6%)' }, { transform: 'translateY(0)' }], { duration: 420, easing: 'ease-out' });
    }

    async show(appearance) {
        this.appearance = normalize(appearance);
        await this.draw();
    }

    color(channel, value) {
        if (!this.appearance) return;
        const map = { skin: 'skin', hair: 'hair_', stubble: 'stubble', warPaint: 'warPaint', eyes: 'eyes' };
        if (!map[channel]) return;
        this.appearance[map[channel]] = hex(value);
        this.redraw();
    }

    listen(root) {
        this.listener = e => {
            const channel = e.target?.dataset?.rfColor;
            if (channel) this.color(channel, e.target.value);
        };
        root.addEventListener('input', this.listener);
        this.listening = root;
    }

    // a colour drag asks many times a second: one draw per frame
    redraw() {
        if (this.pending) return;
        this.pending = true;
        requestAnimationFrame(() => {
            this.pending = false;
            this.draw();
        });
    }

    // the layers of an appearance, nearest-first not needed: each pixel keeps the nearest layer's colour
    layerNames(a) {
        const parts = partsOf(this.m, a);
        return ['body-' + a.gender, ...[parts.head, parts.eyebrows, parts.beard, parts.hair].filter(Boolean).map(p => p.name)];
    }

    async draw() {
        const token = ++this.token;
        const a = this.appearance;
        if (!a) return;
        const d = this.direction;
        const wanted = this.layerNames(a).map(name => ({ name, layer: this.f.layers[name]?.[d] })).filter(x => x.layer);
        const loaded = await Promise.all(wanted.map(x => loadImage(BASE + 'flat/' + x.layer.file + '?v=' + this.f.version).then(img => ({ ...x.layer, name: x.name, img }))));
        if (token !== this.token || this.disposed) return;
        const W = this.f.width, H = this.f.height;
        const out = this.g.createImageData(W, H);
        const px = out.data;
        const depth = new Uint16Array(W * H); // 0 is empty, nearer is larger
        const hair = linearColor(a.hair_), skin = linearColor(a.skin), stubble = linearColor(a.stubble);
        const paint = linearColor(a.warPaint), eyes = linearColor(a.eyes), scar = this.m.material.scar;
        const amount = this.m.material.bodyArtAmount ?? 1;
        for (const l of loaded) {
            const s = l.img.data, w = l.w;
            for (let y = 0; y < l.h; y++) {
                const row = y * w * 4;
                for (let x = 0; x < w; x++) {
                    const i0 = (row + x) * 4;
                    const cover = s[i0 + 3];
                    if (cover < 8) continue;
                    const i3 = (row + 3 * w + x) * 4;
                    const z = (s[i3 + 1] << 8) | s[i3 + 2];
                    const o = (l.y + y) * W + l.x + x;
                    if (z <= depth[o]) continue;
                    depth[o] = z;
                    const i1 = (row + w + x) * 4, i2 = (row + 2 * w + x) * 4;
                    // the game's lerps, in linear light
                    let r = fromSrgb[s[i0]], g = fromSrgb[s[i0 + 1]], b = fromSrgb[s[i0 + 2]];
                    let k = s[i1 + 2] / 255; r += (hair[0] - r) * k; g += (hair[1] - g) * k; b += (hair[2] - b) * k;
                    k = s[i1] / 255; r += (skin[0] - r) * k; g += (skin[1] - g) * k; b += (skin[2] - b) * k;
                    k = s[i1 + 1] / 255; r += (stubble[0] - r) * k; g += (stubble[1] - g) * k; b += (stubble[2] - b) * k;
                    k = s[i2 + 1] / 255; r += (scar[0] - r) * k; g += (scar[1] - g) * k; b += (scar[2] - b) * k;
                    k = s[i2] / 255 * amount; r += (paint[0] - r) * k; g += (paint[1] - g) * k; b += (paint[2] - b) * k;
                    k = s[i2 + 2] / 255; r += (eyes[0] - r) * k; g += (eyes[1] - g) * k; b += (eyes[2] - b) * k;
                    const shade = s[i3] / 255 * 2 * 4096;
                    const q = o * 4;
                    px[q] = toSrgb[Math.min(4096, (r * shade) | 0)];
                    px[q + 1] = toSrgb[Math.min(4096, (g * shade) | 0)];
                    px[q + 2] = toSrgb[Math.min(4096, (b * shade) | 0)];
                    px[q + 3] = cover;
                }
            }
        }
        this.g.putImageData(out, 0, 0);
        this.headRect = loaded.find(l => this.m.parts[a.gender].heads.some(h => h && h.name === l.name)) || null;
        this.applyView();
    }

    applyView() {
        // the face view: the canvas scaled round the head (the pictures are waist up)
        const f = this.f, h = this.headRect;
        if (this.view === 'face' && h) {
            const cx = (h.x + h.w / 2) / f.width * 100, cy = (h.y + h.h * 0.55) / f.height * 100;
            this.canvas.style.transformOrigin = `${cx}% ${cy}%`;
            this.canvas.style.transform = 'scale(2.1)';
        } else {
            this.canvas.style.transform = '';
        }
    }

    dispose() {
        this.disposed = true;
        if (this.listening) this.listening.removeEventListener('input', this.listener);
        this.canvas.remove();
    }
}

/**
 * Opens the view in an element: the 3D one where the browser has WebGL, else the flat one (options.mode '3d' or
 * '2d' asks for one). Blazor keeps the returned object and calls show(), focus(), cheer() and dispose() on it.
 */
export async function create(container, options) {
    options = options || {};
    const m = await manifest();
    const want = options.mode || 'auto';
    if (want !== '2d' && supports3d()) {
        try {
            const stage = new Stage(container, m, options);
            stage.mode = '3d';
            return stage;
        } catch (e) {
            console.warn('The 3D view could not start, using the flat one', e);
        }
    }
    const f = await flatManifest();
    if (!f) throw new Error('The flat character pictures are missing (tools/characters/bake-flat.html).');
    const flat = new Flat(container, m, f, options);
    flat.mode = '2d';
    return flat;
}

/** The 3D view on its own, for the bake page (no input, renders on demand). */
export async function createStage(container, options) {
    const m = await manifest();
    return new Stage(container, m, options);
}

export { THREE, manifest, loadMesh, characterMaterial, setColors, partsOf, buildRig, skinned, stageLights };
