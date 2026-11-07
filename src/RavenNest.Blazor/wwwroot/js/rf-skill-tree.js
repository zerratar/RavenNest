// The skill trees as a map (combat v2, the game's docs/combat-skills-design.md): the eight trees as wedges around a
// centre, their tiers as rings, every skill a node to tap. Drag (or one finger) pans, the wheel or a pinch zooms, a
// tap picks a node and tells the page (PlayerSkillTrees.razor), which shows it with its Learn button.
//
// Two ways to draw the same map: WebGL (three.js: glowing nodes and links, a slow drifting field of motes behind
// them, learnable skills breathing) where the browser has it, and a plain 2D canvas where it does not (a good share of
// players have WebGL turned off). The names and numbers are always drawn on a 2D layer on top, so both read the same.
import * as THREE from './three.module.min.js';

// ---- the map's shape ----------------------------------------------------------------------------------------------

const HUB_RADIUS = 150;          // where each tree's name sits
const FIRST_RING = 340;          // tier 0
const RING_STEP = 190;           // between tiers
const NODE_RADIUS = { active: 26, passive: 20, special: 30 };
const WEDGE_PADDING = 0.07;      // radians kept free at each side of a wedge
const LABEL_MIN_ZOOM = 0.42;     // names show from this zoom up
const TITLE_RADIUS = FIRST_RING + 3 * RING_STEP + 95; // the trees' names, past the outer ring
const MIN_ZOOM = 0.1, MAX_ZOOM = 3.2;

const STATE_ALPHA = { learned: 1, maxed: 1, available: 0.95, open: 0.6, locked: 0.32 };

function hexToRgb(hex) {
    const v = parseInt(hex.replace('#', ''), 16);
    return [(v >> 16) & 255, (v >> 8) & 255, v & 255];
}

function rgba(hex, a) {
    const [r, g, b] = hexToRgb(hex);
    return `rgba(${r},${g},${b},${a})`;
}

/** Places the trees as wedges (tree 0 at the top, clockwise), their tiers as rings, the links from tier to tier. */
function layout(data) {
    const trees = data.trees;
    const wedge = (Math.PI * 2) / trees.length;
    const nodes = data.nodes.map(n => ({ ...n }));
    const byTree = trees.map(() => [[], [], [], []]);
    for (const n of nodes) byTree[n.tree][n.tier].push(n);

    const hubs = trees.map((t, i) => {
        const angle = -Math.PI / 2 + i * wedge;
        return { ...t, index: i, angle, x: Math.cos(angle) * HUB_RADIUS, y: Math.sin(angle) * HUB_RADIUS };
    });

    for (let t = 0; t < trees.length; t++) {
        const centre = hubs[t].angle;
        for (let tier = 0; tier < 4; tier++) {
            const ring = byTree[t][tier];
            const radius = FIRST_RING + tier * RING_STEP;
            const span = wedge - WEDGE_PADDING * 2;
            ring.forEach((n, i) => {
                const a = ring.length === 1 ? centre : centre - span / 2 + span * (i + 0.5) / ring.length;
                n.angle = a;
                n.x = Math.cos(a) * radius;
                n.y = Math.sin(a) * radius;
                n.r = NODE_RADIUS[n.kind] || 22;
            });
        }
    }

    // links: the hub to its first ring, then each node to the nearest of the ring below it (the tiers' order)
    const links = [];
    for (let t = 0; t < trees.length; t++) {
        for (const n of byTree[t][0]) links.push({ a: hubs[t], b: n, tree: t });
        for (let tier = 1; tier < 4; tier++) {
            const below = byTree[t][tier - 1].length ? byTree[t][tier - 1] : byTree[t][Math.max(0, tier - 2)];
            for (const n of byTree[t][tier]) {
                let best = null, bestD = Infinity;
                for (const m of below) {
                    const d = Math.abs(m.angle - n.angle);
                    if (d < bestD) { bestD = d; best = m; }
                }
                if (best) links.push({ a: best, b: n, tree: t });
            }
        }
    }

    for (const hub of hubs) {
        hub.tx = Math.cos(hub.angle) * TITLE_RADIUS;
        hub.ty = Math.sin(hub.angle) * TITLE_RADIUS;
    }
    const extent = TITLE_RADIUS + 40;
    return { hubs, nodes, links, extent };
}

function lit(node) { return node.state === 'learned' || node.state === 'maxed'; }

// ---- the camera and the pointer, shared by both ways of drawing ----------------------------------------------------

class Camera {
    constructor() { this.x = 0; this.y = 0; this.zoom = 0.5; }
    toScreen(wx, wy, w, h) { return [w / 2 + (wx - this.x) * this.zoom, h / 2 + (wy - this.y) * this.zoom]; }
    toWorld(sx, sy, w, h) { return [this.x + (sx - w / 2) / this.zoom, this.y + (sy - h / 2) / this.zoom]; }
    zoomAt(factor, sx, sy, w, h) {
        const [wx, wy] = this.toWorld(sx, sy, w, h);
        this.zoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, this.zoom * factor));
        const [nx, ny] = this.toWorld(sx, sy, w, h);
        this.x += wx - nx;
        this.y += wy - ny;
    }
}

// ---- drawing: shapes on a 2D canvas (the fallback) ----------------------------------------------------------------

function drawShapes2d(g, view) {
    const { w, h, cam, map, selected, time } = view;
    g.clearRect(0, 0, w, h);

    // the sky
    const sky = g.createRadialGradient(w / 2, h / 2, 0, w / 2, h / 2, Math.max(w, h) * 0.75);
    sky.addColorStop(0, '#1d2230');
    sky.addColorStop(1, '#0d0f15');
    g.fillStyle = sky;
    g.fillRect(0, 0, w, h);

    // the rings and the wedge borders
    const [cx, cy] = cam.toScreen(0, 0, w, h);
    g.lineWidth = 1;
    for (let tier = 0; tier < 4; tier++) {
        g.strokeStyle = 'rgba(236,228,214,0.07)';
        g.beginPath();
        g.arc(cx, cy, (FIRST_RING + tier * RING_STEP) * cam.zoom, 0, Math.PI * 2);
        g.stroke();
    }
    const wedge = (Math.PI * 2) / map.hubs.length;
    for (let i = 0; i < map.hubs.length; i++) {
        const a = -Math.PI / 2 + (i + 0.5) * wedge;
        g.strokeStyle = 'rgba(236,228,214,0.05)';
        g.beginPath();
        g.moveTo(cx + Math.cos(a) * 60 * cam.zoom, cy + Math.sin(a) * 60 * cam.zoom);
        g.lineTo(cx + Math.cos(a) * map.extent * cam.zoom, cy + Math.sin(a) * map.extent * cam.zoom);
        g.stroke();
    }

    // links
    for (const l of map.links) {
        const color = map.hubs[l.tree].color;
        const on = (l.a.state === undefined || lit(l.a)) && lit(l.b);
        const [ax, ay] = cam.toScreen(l.a.x, l.a.y, w, h);
        const [bx, by] = cam.toScreen(l.b.x, l.b.y, w, h);
        g.strokeStyle = on ? rgba(color, 0.9) : rgba(color, 0.18);
        g.lineWidth = Math.max(1, (on ? 4 : 2) * cam.zoom);
        g.beginPath();
        g.moveTo(ax, ay);
        g.lineTo(bx, by);
        g.stroke();
    }

    // hubs
    for (const hub of map.hubs) {
        const [x, y] = cam.toScreen(hub.x, hub.y, w, h);
        g.fillStyle = rgba(hub.color, 0.18);
        g.beginPath();
        g.arc(x, y, 34 * cam.zoom, 0, Math.PI * 2);
        g.fill();
        g.strokeStyle = rgba(hub.color, 0.8);
        g.lineWidth = Math.max(1, 2 * cam.zoom);
        g.stroke();
    }

    // nodes
    for (const n of map.nodes) {
        const [x, y] = cam.toScreen(n.x, n.y, w, h);
        const r = n.r * cam.zoom;
        if (x < -r * 3 || y < -r * 3 || x > w + r * 3 || y > h + r * 3) continue;
        const color = map.hubs[n.tree].color;
        const alpha = STATE_ALPHA[n.state] ?? 0.5;

        if (lit(n)) {
            const glow = g.createRadialGradient(x, y, r * 0.5, x, y, r * 2.2);
            glow.addColorStop(0, rgba(color, 0.45));
            glow.addColorStop(1, rgba(color, 0));
            g.fillStyle = glow;
            g.beginPath();
            g.arc(x, y, r * 2.2, 0, Math.PI * 2);
            g.fill();
        }

        g.beginPath();
        nodePath(g, n.kind, x, y, r);
        g.fillStyle = lit(n) ? rgba(color, 0.85) : 'rgba(18,20,27,0.95)';
        g.fill();
        const breathe = n.state === 'available' ? 0.65 + 0.35 * Math.sin(time * 3) : 1;
        g.strokeStyle = rgba(color, alpha * breathe);
        g.lineWidth = Math.max(1.5, (n.state === 'available' ? 5 : 4) * cam.zoom);
        g.stroke();

        drawRank(g, n, x, y, r, color);

        if (selected === n.id) {
            g.strokeStyle = '#fff4d6';
            g.lineWidth = Math.max(2, 3 * cam.zoom);
            g.beginPath();
            g.arc(x, y, r + 7 * cam.zoom, 0, Math.PI * 2);
            g.stroke();
        }
    }
}

/** Actives are diamonds, passives circles, the specials (Battle Rhythm, Last Stand) hexagons. */
function nodePath(g, kind, x, y, r) {
    if (kind === 'active') {
        g.moveTo(x, y - r * 1.15);
        g.lineTo(x + r * 1.15, y);
        g.lineTo(x, y + r * 1.15);
        g.lineTo(x - r * 1.15, y);
        g.closePath();
    } else if (kind === 'special') {
        for (let i = 0; i < 6; i++) {
            const a = Math.PI / 6 + i * Math.PI / 3;
            const px = x + Math.cos(a) * r * 1.1, py = y + Math.sin(a) * r * 1.1;
            if (i === 0) g.moveTo(px, py); else g.lineTo(px, py);
        }
        g.closePath();
    } else {
        g.arc(x, y, r, 0, Math.PI * 2);
    }
}

/** A passive's ranks as an arc around it (3 of 5 is three fifths). */
function drawRank(g, n, x, y, r, color) {
    if (n.max <= 1 || n.rank <= 0) return;
    g.strokeStyle = rgba(color, 1);
    g.lineWidth = Math.max(2, r * 0.18);
    g.beginPath();
    g.arc(x, y, r + Math.max(3, r * 0.22), -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * (n.rank / n.max));
    g.stroke();
}

// ---- drawing: the names and numbers, on top in both ways ----------------------------------------------------------

function drawLabels(g, view) {
    const { w, h, cam, map, selected } = view;
    g.textAlign = 'center';
    g.textBaseline = 'middle';

    // tree names past the outer ring, readable at every zoom
    for (const hub of map.hubs) {
        let [x, y] = cam.toScreen(hub.tx, hub.ty, w, h);
        const size = Math.max(14, Math.min(26, 34 * cam.zoom));
        g.font = `800 ${size}px "FOT-NewRodin Pro B", "Segoe UI", system-ui, sans-serif`;
        // kept inside the view, so a phone shows the side trees' names whole
        const titleWidth = g.measureText(hub.name).width;
        g.font = `700 ${Math.max(11, size * 0.6)}px "Segoe UI", system-ui, sans-serif`;
        const half = Math.max(titleWidth, g.measureText(`${hub.skill} ${hub.level}`).width) / 2 + 6;
        g.font = `800 ${size}px "FOT-NewRodin Pro B", "Segoe UI", system-ui, sans-serif`;
        if (half * 2 < w) x = Math.min(w - half, Math.max(half, x));
        shadowText(g, hub.name, x, y - size * 0.35, hub.color);
        g.font = `700 ${Math.max(11, size * 0.6)}px "Segoe UI", system-ui, sans-serif`;
        shadowText(g, `${hub.skill} ${hub.level}`, x, y + size * 0.55, 'rgba(236,228,214,0.8)');
    }

    if (cam.zoom < LABEL_MIN_ZOOM) return;
    const size = Math.max(11, Math.min(16, 15 * cam.zoom));
    for (const n of map.nodes) {
        const [x, y] = cam.toScreen(n.x, n.y, w, h);
        const r = n.r * cam.zoom;
        if (x < -80 || y < -40 || x > w + 80 || y > h + 40) continue;
        g.font = `${selected === n.id ? 800 : 700} ${size}px "Segoe UI", system-ui, sans-serif`;
        const color = n.state === 'locked' ? 'rgba(236,228,214,0.45)' : 'rgba(255,244,222,0.95)';
        shadowText(g, n.name, x, y + r + 6 + size * 0.6, color);
        if (n.max > 1) {
            g.font = `800 ${Math.max(10, size * 0.8)}px "Segoe UI", system-ui, sans-serif`;
            shadowText(g, `${n.rank}/${n.max}`, x, y, lit(n) ? '#10131a' : 'rgba(255,244,222,0.85)', !lit(n));
        }
        if (n.slot) {
            const bx = x + r * 0.95, by = y - r * 0.95;
            g.fillStyle = '#e8a33d';
            g.beginPath();
            g.arc(bx, by, Math.max(8, 10 * cam.zoom), 0, Math.PI * 2);
            g.fill();
            g.font = `800 ${Math.max(10, 12 * cam.zoom)}px "Segoe UI", system-ui, sans-serif`;
            g.fillStyle = '#10131a';
            g.fillText(String(n.slot), bx, by + 0.5);
        }
    }
}

function shadowText(g, text, x, y, color, shadow = true) {
    if (shadow) {
        g.lineWidth = 3;
        g.strokeStyle = 'rgba(8,9,12,0.9)';
        g.strokeText(text, x, y);
    }
    g.fillStyle = color;
    g.fillText(text, x, y);
}

// ---- drawing: shapes with WebGL (three.js) -----------------------------------------------------------------------

function webglAvailable() {
    try {
        const c = document.createElement('canvas');
        return !!(window.WebGLRenderingContext && (c.getContext('webgl2') || c.getContext('webgl')));
    } catch { return false; }
}

const GLOW_VERT = `
attribute vec3 color;
attribute float size;
attribute float strength;
varying vec3 vColor;
varying float vStrength;
uniform float zoom;
uniform float pixelRatio;
void main() {
    vColor = color;
    vStrength = strength;
    gl_PointSize = size * zoom * pixelRatio;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
}`;

const GLOW_FRAG = `
varying vec3 vColor;
varying float vStrength;
void main() {
    vec2 p = gl_PointCoord * 2.0 - 1.0;
    float d = dot(p, p);
    if (d > 1.0) discard;
    float a = exp(-d * 4.0) * vStrength;
    gl_FragColor = vec4(vColor * a, a);
}`;

class GlScene {
    constructor(canvas) {
        this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
        this.renderer.setClearColor(0x0d0f15, 1);
        this.scene = new THREE.Scene();
        this.camera = new THREE.OrthographicCamera(-1, 1, 1, -1, -10, 10);
        this.groups = new THREE.Group();
        this.scene.add(this.groups);
        this.motes = this.makeMotes();
        this.scene.add(this.motes);
    }

    makeMotes() {
        const count = 420;
        const pos = new Float32Array(count * 3), col = new Float32Array(count * 3), size = new Float32Array(count), str = new Float32Array(count);
        for (let i = 0; i < count; i++) {
            const r = Math.sqrt(Math.random()) * 1400, a = Math.random() * Math.PI * 2;
            pos[i * 3] = Math.cos(a) * r;
            pos[i * 3 + 1] = Math.sin(a) * r;
            pos[i * 3 + 2] = -1;
            const warm = Math.random() < 0.5;
            col.set(warm ? [0.95, 0.75, 0.45] : [0.55, 0.65, 0.95], i * 3);
            size[i] = 6 + Math.random() * 14;
            str[i] = 0.15 + Math.random() * 0.35;
        }
        const geom = new THREE.BufferGeometry();
        geom.setAttribute('position', new THREE.BufferAttribute(pos, 3));
        geom.setAttribute('color', new THREE.BufferAttribute(col, 3));
        geom.setAttribute('size', new THREE.BufferAttribute(size, 1));
        geom.setAttribute('strength', new THREE.BufferAttribute(str, 1));
        this.moteMaterial = this.glowMaterial();
        return new THREE.Points(geom, this.moteMaterial);
    }

    glowMaterial() {
        return new THREE.ShaderMaterial({
            uniforms: { zoom: { value: 1 }, pixelRatio: { value: window.devicePixelRatio || 1 } },
            vertexShader: GLOW_VERT, fragmentShader: GLOW_FRAG,
            transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
        });
    }

    /** Builds the map's meshes: links as lines, nodes as filled shapes with rims, glows as soft points. */
    build(map) {
        this.groups.clear();
        this.nodeMeshes = new Map();

        // rings
        for (let tier = 0; tier < 4; tier++) {
            const ring = new THREE.RingGeometry(FIRST_RING + tier * RING_STEP - 0.8, FIRST_RING + tier * RING_STEP + 0.8, 160);
            const mesh = new THREE.Mesh(ring, new THREE.MeshBasicMaterial({ color: 0xece4d6, transparent: true, opacity: 0.06 }));
            mesh.position.z = -0.8;
            this.groups.add(mesh);
        }

        // links
        for (const l of map.links) {
            const on = (l.a.state === undefined || lit(l.a)) && lit(l.b);
            const color = new THREE.Color(map.hubs[l.tree].color);
            const width = on ? 4 : 2;
            const dx = l.b.x - l.a.x, dy = l.b.y - l.a.y, len = Math.hypot(dx, dy);
            const geom = new THREE.PlaneGeometry(len, width);
            const mesh = new THREE.Mesh(geom, new THREE.MeshBasicMaterial({ color, transparent: true, opacity: on ? 0.9 : 0.2 }));
            mesh.position.set((l.a.x + l.b.x) / 2, -(l.a.y + l.b.y) / 2, -0.5);
            mesh.rotation.z = -Math.atan2(dy, dx);
            this.groups.add(mesh);
        }

        // glows behind the lit nodes, the breathing ones and the hubs
        const glowing = map.nodes.filter(n => lit(n) || n.state === 'available');
        const count = glowing.length + map.hubs.length;
        const pos = new Float32Array(count * 3), col = new Float32Array(count * 3), size = new Float32Array(count), str = new Float32Array(count);
        glowing.forEach((n, i) => {
            const c = new THREE.Color(map.hubs[n.tree].color);
            pos.set([n.x, -n.y, -0.3], i * 3);
            col.set([c.r, c.g, c.b], i * 3);
            size[i] = n.r * (lit(n) ? 6 : 4.2);
            str[i] = lit(n) ? 0.8 : 0.4;
        });
        map.hubs.forEach((hub, j) => {
            const i = glowing.length + j;
            const c = new THREE.Color(hub.color);
            pos.set([hub.x, -hub.y, -0.3], i * 3);
            col.set([c.r, c.g, c.b], i * 3);
            size[i] = 180;
            str[i] = 0.35;
        });
        const geom = new THREE.BufferGeometry();
        geom.setAttribute('position', new THREE.BufferAttribute(pos, 3));
        geom.setAttribute('color', new THREE.BufferAttribute(col, 3));
        geom.setAttribute('size', new THREE.BufferAttribute(size, 1));
        geom.setAttribute('strength', new THREE.BufferAttribute(str, 1));
        this.glowMat = this.glowMaterial();
        this.glows = new THREE.Points(geom, this.glowMat);
        this.glowStrength = str.slice();
        this.glowAvailable = glowing.map(n => n.state === 'available');
        this.groups.add(this.glows);

        // nodes: a dark (or lit) face and a rim in the tree's colour
        for (const n of map.nodes) {
            const color = new THREE.Color(map.hubs[n.tree].color);
            const shape = shapeOf(n.kind, n.r);
            const face = new THREE.Mesh(new THREE.ShapeGeometry(shape, 24), new THREE.MeshBasicMaterial({ color: lit(n) ? color.clone().multiplyScalar(0.85) : new THREE.Color(0x12141b) }));
            face.position.set(n.x, -n.y, 0);
            const rimShape = shapeOf(n.kind, n.r + 4.5);
            rimShape.holes.push(pathOf(n.kind, n.r));
            const rim = new THREE.Mesh(new THREE.ShapeGeometry(rimShape, 24), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: STATE_ALPHA[n.state] ?? 0.5 }));
            rim.position.set(n.x, -n.y, 0.1);
            this.groups.add(face);
            this.groups.add(rim);
            this.nodeMeshes.set(n.id, { face, rim, node: n });

            if (n.max > 1 && n.rank > 0) {
                const arc = new THREE.RingGeometry(n.r + 5, n.r + 9, 48, 1, Math.PI / 2 - Math.PI * 2 * (n.rank / n.max), Math.PI * 2 * (n.rank / n.max));
                const arcMesh = new THREE.Mesh(arc, new THREE.MeshBasicMaterial({ color }));
                arcMesh.position.set(n.x, -n.y, 0.2);
                this.groups.add(arcMesh);
            }
        }

        // hubs
        for (const hub of map.hubs) {
            const color = new THREE.Color(hub.color);
            const disc = new THREE.Mesh(new THREE.CircleGeometry(34, 40), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.16 }));
            disc.position.set(hub.x, -hub.y, -0.1);
            const rim = new THREE.Mesh(new THREE.RingGeometry(33, 36, 40), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.8 }));
            rim.position.set(hub.x, -hub.y, 0);
            this.groups.add(disc);
            this.groups.add(rim);
        }

        this.selectRing = new THREE.Mesh(new THREE.RingGeometry(1, 1.12, 48), new THREE.MeshBasicMaterial({ color: 0xfff4d6 }));
        this.selectRing.visible = false;
        this.selectRing.position.z = 0.3;
        this.groups.add(this.selectRing);
    }

    render(view) {
        const { w, h, cam, time, selected } = view;
        const ratio = window.devicePixelRatio || 1;
        if (this.w !== w || this.h !== h) {
            this.renderer.setPixelRatio(ratio);
            this.renderer.setSize(w, h, false);
            this.w = w; this.h = h;
        }
        this.camera.left = cam.x - w / 2 / cam.zoom;
        this.camera.right = cam.x + w / 2 / cam.zoom;
        this.camera.top = -cam.y + h / 2 / cam.zoom;
        this.camera.bottom = -cam.y - h / 2 / cam.zoom;
        this.camera.updateProjectionMatrix();

        this.moteMaterial.uniforms.zoom.value = Math.max(0.4, cam.zoom);
        this.motes.rotation.z = time * 0.004;
        if (this.glows) {
            this.glowMat.uniforms.zoom.value = cam.zoom;
            const str = this.glows.geometry.attributes.strength;
            for (let i = 0; i < this.glowAvailable.length; i++) {
                if (this.glowAvailable[i]) str.array[i] = this.glowStrength[i] * (0.55 + 0.45 * Math.sin(time * 3 + i));
            }
            str.needsUpdate = true;
        }

        const pick = selected && this.nodeMeshes ? this.nodeMeshes.get(selected) : null;
        if (pick) {
            this.selectRing.visible = true;
            const s = pick.node.r + 8;
            this.selectRing.scale.set(s, s, 1);
            this.selectRing.position.set(pick.node.x, -pick.node.y, 0.3);
        } else if (this.selectRing) {
            this.selectRing.visible = false;
        }

        this.renderer.render(this.scene, this.camera);
    }

    dispose() {
        this.renderer.dispose();
        this.renderer.forceContextLoss?.();
    }
}

function pathOf(kind, r) {
    const p = new THREE.Path();
    tracePath(p, kind, r);
    return p;
}

function shapeOf(kind, r) {
    const s = new THREE.Shape();
    tracePath(s, kind, r);
    return s;
}

// the same outlines as nodePath, in three.js (y up)
function tracePath(p, kind, r) {
    if (kind === 'active') {
        p.moveTo(0, r * 1.15); p.lineTo(r * 1.15, 0); p.lineTo(0, -r * 1.15); p.lineTo(-r * 1.15, 0); p.lineTo(0, r * 1.15);
    } else if (kind === 'special') {
        for (let i = 0; i <= 6; i++) {
            const a = Math.PI / 6 + i * Math.PI / 3;
            if (i === 0) p.moveTo(Math.cos(a) * r * 1.1, Math.sin(a) * r * 1.1); else p.lineTo(Math.cos(a) * r * 1.1, Math.sin(a) * r * 1.1);
        }
    } else {
        p.absarc(0, 0, r, 0, Math.PI * 2, false);
    }
}

// ---- the view -----------------------------------------------------------------------------------------------------

class SkillTreeView {
    constructor(element, data, dotnet, options) {
        this.element = element;
        this.dotnet = dotnet;
        this.options = options || {};
        this.cam = new Camera();
        this.selected = null;
        this.pointers = new Map();
        this.dirty = true;
        this.start = performance.now();

        element.classList.add('rf-skilltree');
        element.style.touchAction = 'none';
        this.useGl = this.options.mode !== '2d' && webglAvailable();
        if (this.useGl) {
            this.glCanvas = document.createElement('canvas');
            this.glCanvas.className = 'rf-skilltree__gl';
            element.appendChild(this.glCanvas);
            try { this.gl = new GlScene(this.glCanvas); }
            catch { this.useGl = false; this.glCanvas.remove(); }
        }
        this.canvas = document.createElement('canvas');
        this.canvas.className = 'rf-skilltree__ui';
        element.appendChild(this.canvas);
        this.g = this.canvas.getContext('2d');
        element.dataset.mode = this.useGl ? 'webgl' : '2d';

        this.bindInput();
        this.resizeObserver = new ResizeObserver(() => { this.dirty = true; });
        this.resizeObserver.observe(element);
        this.needsFit = true; // the element may not have its size yet: fit on the first frame that has one
        this.update(data, false);
        this.loop = this.loop.bind(this);
        this.frame = requestAnimationFrame(this.loop);
    }

    get mode() { return this.useGl ? 'webgl' : '2d'; }

    update(data, fit) {
        this.data = data;
        this.map = layout(data);
        if (this.gl) this.gl.build(this.map);
        if (fit) this.fit();
        this.dirty = true;
    }

    fit() {
        const r = this.element.getBoundingClientRect();
        const size = Math.max(200, Math.min(r.width, r.height || r.width));
        this.cam.x = 0;
        this.cam.y = 0;
        // the trees' names stick out past the extent on the sides: room for them
        this.cam.zoom = Math.max(MIN_ZOOM, size / ((this.map.extent + 90) * 2));
        this.dirty = true;
    }

    zoomBy(factor) {
        const r = this.element.getBoundingClientRect();
        this.cam.zoomAt(factor, r.width / 2, r.height / 2, r.width, r.height);
        this.dirty = true;
    }

    select(id, notify) {
        this.selected = id;
        this.dirty = true;
        if (notify && this.dotnet) this.dotnet.invokeMethodAsync('OnNodeSelected', id || '');
    }

    /** Centres the camera on a node (from the page's list or search). */
    focus(id) {
        const n = this.map.nodes.find(x => x.id === id);
        if (!n) return;
        this.cam.x = n.x;
        this.cam.y = n.y;
        this.cam.zoom = Math.max(this.cam.zoom, 1.1);
        this.select(id, false);
    }

    hit(sx, sy) {
        const r = this.element.getBoundingClientRect();
        const [wx, wy] = this.cam.toWorld(sx, sy, r.width, r.height);
        let best = null, bestD = Infinity;
        for (const n of this.map.nodes) {
            const d = Math.hypot(n.x - wx, n.y - wy);
            const reach = n.r * 1.35 + 14 / this.cam.zoom; // fingers are bigger than mice
            if (d < reach && d < bestD) { best = n; bestD = d; }
        }
        return best;
    }

    bindInput() {
        const el = this.element;
        const local = e => { const r = el.getBoundingClientRect(); return [e.clientX - r.left, e.clientY - r.top]; };
        this.onDown = e => {
            el.setPointerCapture?.(e.pointerId);
            this.pointers.set(e.pointerId, local(e));
            this.moved = 0;
            this.pinch = null;
        };
        this.onMove = e => {
            if (!this.pointers.has(e.pointerId)) return;
            const prev = this.pointers.get(e.pointerId);
            const now = local(e);
            this.pointers.set(e.pointerId, now);
            if (this.pointers.size === 1) {
                this.cam.x -= (now[0] - prev[0]) / this.cam.zoom;
                this.cam.y -= (now[1] - prev[1]) / this.cam.zoom;
                this.moved += Math.abs(now[0] - prev[0]) + Math.abs(now[1] - prev[1]);
                this.dirty = true;
            } else if (this.pointers.size === 2) {
                const [a, b] = [...this.pointers.values()];
                const dist = Math.hypot(a[0] - b[0], a[1] - b[1]);
                const mid = [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2];
                if (this.pinch) {
                    const r = el.getBoundingClientRect();
                    this.cam.zoomAt(dist / this.pinch.dist, mid[0], mid[1], r.width, r.height);
                    this.cam.x -= (mid[0] - this.pinch.mid[0]) / this.cam.zoom;
                    this.cam.y -= (mid[1] - this.pinch.mid[1]) / this.cam.zoom;
                }
                this.pinch = { dist, mid };
                this.moved += 100;
                this.dirty = true;
            }
        };
        this.onUp = e => {
            const p = this.pointers.get(e.pointerId);
            this.pointers.delete(e.pointerId);
            if (this.pointers.size < 2) this.pinch = null;
            if (p && this.pointers.size === 0 && this.moved < 8) {
                const n = this.hit(p[0], p[1]);
                this.select(n ? n.id : null, true);
            }
        };
        this.onWheel = e => {
            e.preventDefault();
            const [x, y] = local(e);
            const r = el.getBoundingClientRect();
            this.cam.zoomAt(Math.exp(-e.deltaY * 0.0015), x, y, r.width, r.height);
            this.dirty = true;
        };
        this.onDouble = e => {
            const [x, y] = local(e);
            const r = el.getBoundingClientRect();
            this.cam.zoomAt(1.8, x, y, r.width, r.height);
            this.dirty = true;
        };
        el.addEventListener('pointerdown', this.onDown);
        el.addEventListener('pointermove', this.onMove);
        el.addEventListener('pointerup', this.onUp);
        el.addEventListener('pointercancel', this.onUp);
        el.addEventListener('wheel', this.onWheel, { passive: false });
        el.addEventListener('dblclick', this.onDouble);
    }

    loop(now) {
        this.frame = requestAnimationFrame(this.loop);
        if (document.hidden) return;
        const time = (now - this.start) / 1000;
        const breathing = this.map.nodes.some(n => n.state === 'available');
        if (!this.dirty && !breathing && !this.useGl) return;
        if (!this.dirty && !breathing && this.useGl && time - (this.lastGl || 0) < 0.1) return; // the motes drift slowly
        this.lastGl = time;

        const r = this.element.getBoundingClientRect();
        if (r.width < 2 || r.height < 2) return;
        if (this.needsFit) { this.needsFit = false; this.fit(); }
        const w = Math.max(1, Math.round(r.width)), h = Math.max(1, Math.round(r.height));
        const ratio = window.devicePixelRatio || 1;
        if (this.canvas.width !== Math.round(w * ratio) || this.canvas.height !== Math.round(h * ratio)) {
            this.canvas.width = Math.round(w * ratio);
            this.canvas.height = Math.round(h * ratio);
        }
        const view = { w, h, cam: this.cam, map: this.map, selected: this.selected, time };
        const g = this.g;
        g.setTransform(ratio, 0, 0, ratio, 0, 0);
        if (this.useGl) {
            this.gl.render(view);
            g.clearRect(0, 0, w, h);
        } else {
            drawShapes2d(g, view);
        }
        drawLabels(g, view);
        this.dirty = false;
    }

    dispose() {
        cancelAnimationFrame(this.frame);
        this.resizeObserver.disconnect();
        const el = this.element;
        el.removeEventListener('pointerdown', this.onDown);
        el.removeEventListener('pointermove', this.onMove);
        el.removeEventListener('pointerup', this.onUp);
        el.removeEventListener('pointercancel', this.onUp);
        el.removeEventListener('wheel', this.onWheel);
        el.removeEventListener('dblclick', this.onDouble);
        this.gl?.dispose();
        el.innerHTML = '';
    }
}

/**
 * Opens the map in an element. data: { trees: [{ name, skill, level, color }], nodes: [{ id, name, tree, tier,
 * kind ('active' | 'passive' | 'special'), rank, max, state ('learned' | 'maxed' | 'available' | 'open' | 'locked'),
 * slot (loadout slot from 1, or 0) }] }. options.mode '2d' forces the canvas. dotnet gets OnNodeSelected(id).
 */
export function open(element, data, dotnet, options) {
    return new SkillTreeView(element, data, dotnet, options);
}
