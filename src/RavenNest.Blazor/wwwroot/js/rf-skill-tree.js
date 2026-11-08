// The skill trees as a map (combat v2, the game's docs/combat-skills-design.md): the eight trees as wedges around a
// centre, their tiers as rings, every skill a node to tap. Drag (or one finger) pans, the wheel or a pinch zooms, a
// tap picks a node and tells the page (PlayerSkillTrees.razor), which shows it with its Learn button.
//
// The look is a star map (from the mockup the user picked on 2026-11-08, docs/mockups/skilltree-constellation.png in
// the game): a deep blue night sky with a soft nebula and twinkling stars, a gold raven medallion in the middle with
// compass rays to each tree, the skills as stars (lit and gold rimmed when learned, dim stone when locked), starlight
// links, and the trees' names on gold framed plates.
//
// Two ways to draw the same map: WebGL (three.js) where the browser has it, and a plain 2D canvas where it does not (a
// good share of players have WebGL turned off). The names and numbers are always drawn on a 2D layer on top, so both
// read the same.
import * as THREE from './three.module.min.js';

// ---- the map's shape ----------------------------------------------------------------------------------------------

const HUB_RADIUS = 185;          // where each tree's name sits
const FIRST_RING = 340;          // tier 0
const RING_STEP = 190;           // between tiers
const NODE_RADIUS = { active: 26, passive: 20, special: 30 };
const WEDGE_PADDING = 0.07;      // radians kept free at each side of a wedge
const LABEL_MIN_ZOOM = 0.42;     // names show from this zoom up
const TITLE_RADIUS = FIRST_RING + 3 * RING_STEP + 95; // the trees' names, past the outer ring
const MIN_ZOOM = 0.1, MAX_ZOOM = 3.2;

const STATE_ALPHA = { learned: 1, maxed: 1, available: 0.95, open: 0.6, locked: 0.32 };

// the star map's colours
const SKY = 0x060a1c;
const SKY_CSS = '#060a1c';
const GOLD = '#e9b85c';
const GOLD_DEEP = '#a8762c';
const STONE = '#4a5170';
const NODE_DARK = '#0c1230';
const MEDALLION_RADIUS = 118;

/** A low poly raven perched, facing right, in a box of -1..1 (y down): the medallion's emblem. */
const RAVEN = [[-1.0, 0.12], [-0.6, 0.02], [-0.36, -0.2], [-0.02, -0.36], [0.24, -0.32], [0.38, -0.5], [0.56, -0.56],
    [0.7, -0.48], [0.98, -0.4], [0.7, -0.32], [0.64, -0.16], [0.56, 0.04], [0.36, 0.26], [0.12, 0.38], [0.08, 0.66],
    [0.02, 0.66], [-0.01, 0.44], [-0.14, 0.44], [-0.17, 0.66], [-0.23, 0.66], [-0.22, 0.42], [-0.46, 0.3], [-1.0, 0.24]];
const RAVEN_WING = [[-0.62, 0.1], [-0.3, -0.12], [0.18, -0.16], [0.34, 0.02], [0.08, 0.2], [-0.3, 0.2]];

/** The raven emblem on a canvas (gold on dark), for the WebGL medallion's texture and the 2D map. */
function drawRaven(g, cx, cy, size) {
    const trace = pts => { g.beginPath(); pts.forEach(([x, y], i) => { const px = cx + x * size, py = cy + y * size; if (i === 0) g.moveTo(px, py); else g.lineTo(px, py); }); g.closePath(); };
    trace(RAVEN);
    const fill = g.createLinearGradient(cx - size, cy - size, cx + size, cy + size);
    fill.addColorStop(0, '#2a1d4a');
    fill.addColorStop(1, '#120c24');
    g.fillStyle = fill;
    g.fill();
    g.lineJoin = 'round';
    g.lineWidth = Math.max(1.5, size * 0.05);
    g.strokeStyle = GOLD;
    g.stroke();
    trace(RAVEN_WING);
    g.fillStyle = 'rgba(160,120,230,0.35)';
    g.fill();
    g.lineWidth = Math.max(1, size * 0.03);
    g.strokeStyle = 'rgba(233,184,92,0.7)';
    g.stroke();
    // the eye
    g.fillStyle = '#ffe9a8';
    g.beginPath();
    g.arc(cx + 0.6 * size, cy - 0.42 * size, Math.max(1, size * 0.045), 0, Math.PI * 2);
    g.fill();
}

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

    // the sky: deep blue with a violet glow round the middle, and still stars
    const sky = g.createRadialGradient(w / 2, h / 2, 0, w / 2, h / 2, Math.max(w, h) * 0.75);
    sky.addColorStop(0, '#1a1d48');
    sky.addColorStop(0.5, '#0c1230');
    sky.addColorStop(1, SKY_CSS);
    g.fillStyle = sky;
    g.fillRect(0, 0, w, h);
    view.stars = view.stars || Array.from({ length: 260 }, (_, i) => [((i * 7919) % 1000) / 1000, ((i * 104729) % 997) / 997, 0.4 + ((i * 31) % 10) / 10]);
    for (const [sx, sy, b] of view.stars) {
        g.fillStyle = `rgba(220,230,255,${0.25 + 0.5 * b * (0.7 + 0.3 * Math.sin(time * 2 + sx * 40))})`;
        g.fillRect(sx * w, sy * h, b > 1 ? 2 : 1.2, b > 1 ? 2 : 1.2);
    }

    // the rings and the wedge borders
    const [cx, cy] = cam.toScreen(0, 0, w, h);
    g.lineWidth = 1;
    for (let tier = 0; tier < 4; tier++) {
        g.strokeStyle = 'rgba(150,170,255,0.08)';
        g.setLineDash([2, 6]);
        g.beginPath();
        g.arc(cx, cy, (FIRST_RING + tier * RING_STEP) * cam.zoom, 0, Math.PI * 2);
        g.stroke();
    }
    g.setLineDash([]);
    // the compass rays: long gold ones to each tree, short ones between
    for (let i = 0; i < map.hubs.length * 2; i++) {
        const a = -Math.PI / 2 + i * Math.PI / map.hubs.length;
        const long = i % 2 === 0;
        const reach = (long ? HUB_RADIUS + 40 : MEDALLION_RADIUS + 40) * cam.zoom;
        const side = (long ? 10 : 6) * cam.zoom;
        const ox = Math.cos(a), oy = Math.sin(a), px = -oy, py = ox;
        const base = MEDALLION_RADIUS * 0.8 * cam.zoom;
        g.beginPath();
        g.moveTo(cx + ox * base + px * side, cy + oy * base + py * side);
        g.lineTo(cx + ox * reach, cy + oy * reach);
        g.lineTo(cx + ox * base - px * side, cy + oy * base - py * side);
        g.closePath();
        g.fillStyle = long ? 'rgba(233,184,92,0.85)' : 'rgba(233,184,92,0.45)';
        g.fill();
    }
    // the medallion
    {
        const r = MEDALLION_RADIUS * cam.zoom;
        const glow = g.createRadialGradient(cx, cy, r * 0.5, cx, cy, r * 2.2);
        glow.addColorStop(0, 'rgba(233,184,92,0.35)');
        glow.addColorStop(1, 'rgba(233,184,92,0)');
        g.fillStyle = glow;
        g.beginPath(); g.arc(cx, cy, r * 2.2, 0, Math.PI * 2); g.fill();
        g.fillStyle = '#100c26';
        g.beginPath(); g.arc(cx, cy, r, 0, Math.PI * 2); g.fill();
        g.strokeStyle = GOLD; g.lineWidth = Math.max(2, 5 * cam.zoom); g.stroke();
        g.strokeStyle = GOLD_DEEP; g.lineWidth = Math.max(1, 2 * cam.zoom);
        g.beginPath(); g.arc(cx, cy, r * 0.84, 0, Math.PI * 2); g.stroke();
        drawRaven(g, cx, cy + r * 0.02, r * 0.66);
    }

    // links
    for (const l of map.links) {
        const color = map.hubs[l.tree].color;
        const on = (l.a.state === undefined || lit(l.a)) && lit(l.b);
        const [ax, ay] = cam.toScreen(l.a.x, l.a.y, w, h);
        const [bx, by] = cam.toScreen(l.b.x, l.b.y, w, h);
        g.strokeStyle = on ? rgba(color, 0.95) : 'rgba(170,185,255,0.16)';
        g.lineWidth = Math.max(1, (on ? 3.5 : 1.6) * cam.zoom);
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
        g.fillStyle = lit(n) ? rgba(color, 0.9) : NODE_DARK;
        g.fill();
        const breathe = n.state === 'available' ? 0.65 + 0.35 * Math.sin(time * 3) : 1;
        g.strokeStyle = n.state === 'locked' ? rgba(STONE, 0.9) : rgba(color, alpha * breathe);
        g.lineWidth = Math.max(1.5, (n.state === 'available' ? 4 : 3) * cam.zoom);
        g.stroke();
        if (lit(n)) {
            g.beginPath();
            nodePath(g, n.kind, x, y, r + 5 * cam.zoom);
            g.strokeStyle = GOLD;
            g.lineWidth = Math.max(1.5, 2.5 * cam.zoom);
            g.stroke();
        }

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

// ---- drawing: the icons, on top in both ways -----------------------------------------------------------------------

const ICON_ALPHA = { learned: 1, maxed: 1, available: 0.9, open: 0.55, locked: 0.28 };

/** The painted sigils (imgs/skills): inside actives and specials, and the trees' emblems on their discs by the medallion. */
function drawIcons(g, view, icons) {
    const { w, h, cam, map } = view;
    for (const n of map.nodes) {
        const img = icons.get(n.id);
        if (!img) continue;
        const [x, y] = cam.toScreen(n.x, n.y, w, h);
        const r = n.r * cam.zoom;
        if (r < 6 || x < -r * 2 || y < -r * 2 || x > w + r * 2 || y > h + r * 2) continue;
        const size = r * (n.kind === 'active' ? 1.55 : 1.6);
        g.globalAlpha = ICON_ALPHA[n.state] ?? 0.6;
        g.drawImage(img, x - size / 2, y - size / 2, size, size);
    }
    // the painted medallion over the drawn one, at the centre
    const medallion = icons.get('medallion');
    if (medallion) {
        const [x, y] = cam.toScreen(0, 0, w, h);
        const size = MEDALLION_RADIUS * 2.35 * cam.zoom;
        g.globalAlpha = 1;
        g.drawImage(medallion, x - size / 2, y - size / 2, size, size);
    }
    map.hubs.forEach((hub, i) => {
        const img = icons.get('tree:' + i);
        if (!img) return;
        const [x, y] = cam.toScreen(hub.x, hub.y, w, h);
        const size = 54 * cam.zoom;
        if (size < 8) return;
        g.globalAlpha = 1;
        g.drawImage(img, x - size / 2, y - size / 2, size, size);
    });
    g.globalAlpha = 1;
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
        const titleWidth = g.measureText(hub.name.toUpperCase()).width;
        g.font = `700 ${Math.max(11, size * 0.6)}px "Segoe UI", system-ui, sans-serif`;
        const half = Math.max(titleWidth, g.measureText(`${hub.skill} ${hub.level}`).width) / 2 + 6;
        g.font = `800 ${size}px "FOT-NewRodin Pro B", "Segoe UI", system-ui, sans-serif`;
        if (half * 2 < w && cam.zoom < LABEL_MIN_ZOOM) x = Math.min(w - half, Math.max(half, x)); // only in the overview: up close it would sit on the nodes
        drawPlate(g, x, y - size * 0.35, titleWidth + size * 1.6, size * 1.5);
        shadowText(g, hub.name.toUpperCase(), x, y - size * 0.32, '#fbe7b8');
        g.font = `700 ${Math.max(11, size * 0.6)}px "Segoe UI", system-ui, sans-serif`;
        shadowText(g, `${hub.skill} ${hub.level}`, x, y + size * 0.95, hub.color);
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

/** A dark plate with a gold frame and pointed ends, behind a tree's name (as the mockup's banners). */
function drawPlate(g, x, y, width, height) {
    const hw = width / 2, hh = height / 2, tip = hh * 0.9;
    g.beginPath();
    g.moveTo(x - hw - tip, y);
    g.lineTo(x - hw, y - hh);
    g.lineTo(x + hw, y - hh);
    g.lineTo(x + hw + tip, y);
    g.lineTo(x + hw, y + hh);
    g.lineTo(x - hw, y + hh);
    g.closePath();
    g.fillStyle = 'rgba(14,12,30,0.92)';
    g.fill();
    g.lineWidth = 2;
    g.strokeStyle = GOLD;
    g.stroke();
    g.beginPath();
    g.moveTo(x - hw + 4, y - hh + 4);
    g.lineTo(x + hw - 4, y - hh + 4);
    g.lineWidth = 1;
    g.strokeStyle = 'rgba(233,184,92,0.35)';
    g.stroke();
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
        this.renderer.setClearColor(SKY, 1);
        this.scene = new THREE.Scene();
        this.camera = new THREE.OrthographicCamera(-1, 1, 1, -1, -10, 10);
        this.groups = new THREE.Group();
        this.scene.add(this.groups);
        this.nebula = this.makeNebula();
        this.scene.add(this.nebula);
        this.stars = this.makeStars();
        this.scene.add(this.stars);
        this.constellations = this.makeConstellations();
        this.scene.add(this.constellations);
        this.motes = this.makeMotes();
        this.scene.add(this.motes);
    }

    /** Big soft clouds of violet, blue and teal behind everything. */
    makeNebula() {
        const blobs = [[0, 0, 900, [0.35, 0.22, 0.75], 0.5], [-700, -500, 1100, [0.15, 0.25, 0.7], 0.45], [800, 400, 1000, [0.1, 0.45, 0.55], 0.35],
            [600, -800, 800, [0.5, 0.2, 0.6], 0.3], [-900, 700, 900, [0.2, 0.3, 0.75], 0.35], [200, 1100, 700, [0.45, 0.25, 0.65], 0.25]];
        const pos = new Float32Array(blobs.length * 3), col = new Float32Array(blobs.length * 3), size = new Float32Array(blobs.length), str = new Float32Array(blobs.length);
        blobs.forEach(([x, y, s, c, a], i) => { pos.set([x, y, -2], i * 3); col.set(c, i * 3); size[i] = s; str[i] = a; });
        const geom = new THREE.BufferGeometry();
        geom.setAttribute('position', new THREE.BufferAttribute(pos, 3));
        geom.setAttribute('color', new THREE.BufferAttribute(col, 3));
        geom.setAttribute('size', new THREE.BufferAttribute(size, 1));
        geom.setAttribute('strength', new THREE.BufferAttribute(str, 1));
        this.nebulaMaterial = this.glowMaterial(true);
        return new THREE.Points(geom, this.nebulaMaterial);
    }

    /** Sharp little stars of a fixed size on screen, twinkling. */
    makeStars() {
        const count = 1600;
        const pos = new Float32Array(count * 3), seed = new Float32Array(count), size = new Float32Array(count);
        for (let i = 0; i < count; i++) {
            const r = Math.sqrt(Math.random()) * 2600, a = Math.random() * Math.PI * 2;
            pos.set([Math.cos(a) * r, Math.sin(a) * r, -1.5], i * 3);
            seed[i] = Math.random() * 100;
            size[i] = Math.random() < 0.06 ? 3.2 + Math.random() * 1.5 : 1.2 + Math.random() * 1.4;
        }
        this.starPositions = pos;
        const geom = new THREE.BufferGeometry();
        geom.setAttribute('position', new THREE.BufferAttribute(pos, 3));
        geom.setAttribute('seed', new THREE.BufferAttribute(seed, 1));
        geom.setAttribute('size', new THREE.BufferAttribute(size, 1));
        this.starMaterial = new THREE.ShaderMaterial({
            uniforms: { time: { value: 0 }, pixelRatio: { value: window.devicePixelRatio || 1 } },
            vertexShader: `attribute float seed; attribute float size; varying float vTwinkle; uniform float time; uniform float pixelRatio;
                void main() { vTwinkle = 0.55 + 0.45 * sin(time * (0.8 + fract(seed) * 2.2) + seed); gl_PointSize = size * pixelRatio * (0.8 + 0.4 * vTwinkle);
                gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`,
            fragmentShader: `varying float vTwinkle; void main() { vec2 p = gl_PointCoord * 2.0 - 1.0; float d = dot(p, p); if (d > 1.0) discard;
                float a = (1.0 - d) * vTwinkle; gl_FragColor = vec4(vec3(0.85, 0.9, 1.0) * a, a); }`,
            transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
        });
        return new THREE.Points(geom, this.starMaterial);
    }

    /** Faint lines joining a few of the background stars, as on a star chart. */
    makeConstellations() {
        const pos = this.starPositions, pts = [];
        for (let k = 0; k < 40; k++) {
            let i = Math.floor(Math.random() * (pos.length / 3));
            for (let step = 0; step < 4; step++) {
                let best = -1, bestD = Infinity;
                for (let tries = 0; tries < 60; tries++) {
                    const j = Math.floor(Math.random() * (pos.length / 3));
                    const d = Math.hypot(pos[j * 3] - pos[i * 3], pos[j * 3 + 1] - pos[i * 3 + 1]);
                    if (d > 60 && d < 240 && d < bestD) { best = j; bestD = d; }
                }
                if (best < 0) break;
                pts.push(pos[i * 3], pos[i * 3 + 1], -1.4, pos[best * 3], pos[best * 3 + 1], -1.4);
                i = best;
            }
        }
        const geom = new THREE.BufferGeometry();
        geom.setAttribute('position', new THREE.BufferAttribute(new Float32Array(pts), 3));
        return new THREE.LineSegments(geom, new THREE.LineBasicMaterial({ color: 0x8fa2ff, transparent: true, opacity: 0.12 }));
    }

    makeMotes() {
        const count = 420;
        const pos = new Float32Array(count * 3), col = new Float32Array(count * 3), size = new Float32Array(count), str = new Float32Array(count);
        for (let i = 0; i < count; i++) {
            const r = Math.sqrt(Math.random()) * 1400, a = Math.random() * Math.PI * 2;
            pos[i * 3] = Math.cos(a) * r;
            pos[i * 3 + 1] = Math.sin(a) * r;
            pos[i * 3 + 2] = -1;
            const warm = Math.random() < 0.25;
            col.set(warm ? [0.95, 0.78, 0.45] : [0.55, 0.65, 1.0], i * 3);
            size[i] = 5 + Math.random() * 10;
            str[i] = 0.08 + Math.random() * 0.2;
        }
        const geom = new THREE.BufferGeometry();
        geom.setAttribute('position', new THREE.BufferAttribute(pos, 3));
        geom.setAttribute('color', new THREE.BufferAttribute(col, 3));
        geom.setAttribute('size', new THREE.BufferAttribute(size, 1));
        geom.setAttribute('strength', new THREE.BufferAttribute(str, 1));
        this.moteMaterial = this.glowMaterial();
        return new THREE.Points(geom, this.moteMaterial);
    }

    glowMaterial(soft) {
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

        // rings: faint orbits
        for (let tier = 0; tier < 4; tier++) {
            const ring = new THREE.RingGeometry(FIRST_RING + tier * RING_STEP - 0.8, FIRST_RING + tier * RING_STEP + 0.8, 160);
            const mesh = new THREE.Mesh(ring, new THREE.MeshBasicMaterial({ color: 0x96aaff, transparent: true, opacity: 0.07 }));
            mesh.position.z = -0.8;
            this.groups.add(mesh);
        }

        // the compass rays: long gold ones to each tree, short ones between
        const gold = new THREE.Color(GOLD);
        for (let i = 0; i < map.hubs.length * 2; i++) {
            const a = -Math.PI / 2 + i * Math.PI / map.hubs.length;
            const long = i % 2 === 0;
            const reach = long ? HUB_RADIUS + 40 : MEDALLION_RADIUS + 40, side = long ? 10 : 6, base = MEDALLION_RADIUS * 0.8;
            const ox = Math.cos(a), oy = -Math.sin(a), px = -oy, py = ox;
            const shape = new THREE.Shape();
            shape.moveTo(ox * base + px * side, oy * base + py * side);
            shape.lineTo(ox * reach, oy * reach);
            shape.lineTo(ox * base - px * side, oy * base - py * side);
            const ray = new THREE.Mesh(new THREE.ShapeGeometry(shape), new THREE.MeshBasicMaterial({ color: gold, transparent: true, opacity: long ? 0.85 : 0.45 }));
            ray.position.z = -0.2;
            this.groups.add(ray);
        }

        // the medallion: a dark disc in a double gold ring with the raven, glowing softly
        {
            const disc = new THREE.Mesh(new THREE.CircleGeometry(MEDALLION_RADIUS, 64), new THREE.MeshBasicMaterial({ color: 0x100c26 }));
            disc.position.z = 0.05;
            const outer = new THREE.Mesh(new THREE.RingGeometry(MEDALLION_RADIUS - 2, MEDALLION_RADIUS + 4, 64), new THREE.MeshBasicMaterial({ color: gold }));
            outer.position.z = 0.1;
            const inner = new THREE.Mesh(new THREE.RingGeometry(MEDALLION_RADIUS * 0.84 - 1, MEDALLION_RADIUS * 0.84 + 1, 64), new THREE.MeshBasicMaterial({ color: new THREE.Color(GOLD_DEEP) }));
            inner.position.z = 0.1;
            const c = document.createElement('canvas');
            c.width = c.height = 256;
            drawRaven(c.getContext('2d'), 128, 130, 84);
            const texture = new THREE.CanvasTexture(c);
            texture.colorSpace = THREE.SRGBColorSpace;
            const emblem = new THREE.Mesh(new THREE.PlaneGeometry(MEDALLION_RADIUS * 2, MEDALLION_RADIUS * 2), new THREE.MeshBasicMaterial({ map: texture, transparent: true }));
            emblem.position.z = 0.15;
            this.groups.add(disc, outer, inner, emblem);
        }

        // links
        for (const l of map.links) {
            const on = (l.a.state === undefined || lit(l.a)) && lit(l.b);
            const color = new THREE.Color(map.hubs[l.tree].color);
            const width = on ? 3.5 : 1.6;
            const dx = l.b.x - l.a.x, dy = l.b.y - l.a.y, len = Math.hypot(dx, dy);
            const geom = new THREE.PlaneGeometry(len, width);
            const mesh = new THREE.Mesh(geom, new THREE.MeshBasicMaterial({ color: on ? color : new THREE.Color(0xaab9ff), transparent: true, opacity: on ? 0.95 : 0.16 }));
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
            size[i] = 150;
            str[i] = 0.28;
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
            const face = new THREE.Mesh(new THREE.ShapeGeometry(shape, 24), new THREE.MeshBasicMaterial({ color: lit(n) ? color.clone().multiplyScalar(0.9) : new THREE.Color(NODE_DARK) }));
            face.position.set(n.x, -n.y, 0);
            const rimShape = shapeOf(n.kind, n.r + 4);
            rimShape.holes.push(pathOf(n.kind, n.r));
            const locked = n.state === 'locked';
            const rim = new THREE.Mesh(new THREE.ShapeGeometry(rimShape, 24), new THREE.MeshBasicMaterial({ color: locked ? new THREE.Color(STONE) : color, transparent: true, opacity: locked ? 0.85 : STATE_ALPHA[n.state] ?? 0.5 }));
            rim.position.set(n.x, -n.y, 0.1);
            this.groups.add(face);
            this.groups.add(rim);
            if (lit(n)) {
                // learned: a gold ring round the star, and a sparkle in its middle
                const ringShape = shapeOf(n.kind, n.r + 8.5);
                ringShape.holes.push(pathOf(n.kind, n.r + 6));
                const ring = new THREE.Mesh(new THREE.ShapeGeometry(ringShape, 24), new THREE.MeshBasicMaterial({ color: gold }));
                ring.position.set(n.x, -n.y, 0.12);
                this.groups.add(ring);
                const spark = new THREE.Shape();
                const s = n.r * 0.55;
                spark.moveTo(0, s); spark.lineTo(s * 0.22, s * 0.22); spark.lineTo(s, 0); spark.lineTo(s * 0.22, -s * 0.22);
                spark.lineTo(0, -s); spark.lineTo(-s * 0.22, -s * 0.22); spark.lineTo(-s, 0); spark.lineTo(-s * 0.22, s * 0.22);
                const sparkle = new THREE.Mesh(new THREE.ShapeGeometry(spark), new THREE.MeshBasicMaterial({ color: 0xfff6dc, transparent: true, opacity: 0.9 }));
                sparkle.position.set(n.x, -n.y, 0.13);
                this.groups.add(sparkle);
            }
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
            const disc = new THREE.Mesh(new THREE.CircleGeometry(30, 40), new THREE.MeshBasicMaterial({ color: color.clone().multiplyScalar(0.35) }));
            disc.position.set(hub.x, -hub.y, -0.1);
            const rim = new THREE.Mesh(new THREE.RingGeometry(30, 35, 40), new THREE.MeshBasicMaterial({ color: gold }));
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
        this.starMaterial.uniforms.time.value = time;
        this.nebulaMaterial.uniforms.zoom.value = cam.zoom;
        this.nebula.rotation.z = time * 0.002;
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

        this.icons = new Map();
        this.loadIcons(data);
        this.root = this.options.root || element;
        this.bindInput();
        // a new size keeps the whole map in view until the reader moves or zooms it themselves
        this.resizeObserver = new ResizeObserver(() => { if (this.fitted) this.needsFit = true; this.dirty = true; });
        this.resizeObserver.observe(element);
        this.needsFit = true; // the element may not have its size yet: fit on the first frame that has one
        this.update(data, false);
        this.loop = this.loop.bind(this);
        this.frame = requestAnimationFrame(this.loop);
    }

    get mode() { return this.useGl ? 'webgl' : '2d'; }

    /** Icons by node id (imgs/skills/<id>.webp) and the trees' emblems (tree-<name>.webp); missing ones are left out. */
    loadIcons(data) {
        const base = this.options.iconBase || '/imgs/skills/';
        const want = data.nodes.filter(n => n.kind !== 'passive').map(n => [n.id, n.id]);
        data.trees.forEach((t, i) => want.push(['tree:' + i, 'tree-' + t.name.toLowerCase()]));
        want.push(['medallion', 'medallion']);
        for (const [key, file] of want) {
            if (this.icons.has(key)) continue;
            const img = new Image();
            img.onload = () => { this.icons.set(key, img); this.dirty = true; };
            img.src = base + file + '.webp';
        }
    }

    /**
     * The page's panel for the picked skill, kept beside it as the map moves (to its right, or its left near the edge;
     * on a narrow screen along the bottom). Hidden while nothing is picked.
     */
    setPopover(el) { this.popover = el; this.dirty = true; }

    placePopover(w, h) {
        const el = this.popover;
        if (!el) return;
        const n = this.selected ? this.map.nodes.find(x => x.id === this.selected) : null;
        if (!n) { el.style.visibility = 'hidden'; return; }
        const pw = el.offsetWidth, ph = el.offsetHeight;
        const [x, y] = this.cam.toScreen(n.x, n.y, w, h);
        const r = n.r * this.cam.zoom + 18;
        let left, top;
        if (w < 620) {
            left = 8; top = h - ph - (this.options.bottomInset || 0) - 8;
        } else {
            left = x + r + pw + 12 < w ? x + r : x - r - pw;
            top = y - ph / 2;
        }
        left = Math.max(8, Math.min(w - pw - 8, left));
        top = Math.max(8, Math.min(h - ph - 8, top));
        el.style.transform = `translate(${Math.round(left)}px, ${Math.round(top)}px)`;
        el.style.visibility = 'visible';
    }

    update(data, fit) {
        this.data = data;
        if (this.icons) this.loadIcons(data);
        this.map = layout(data);
        if (this.gl) this.gl.build(this.map);
        if (fit) this.fit();
        this.dirty = true;
    }

    fit() {
        const r = this.element.getBoundingClientRect();
        // room kept free for what lies over the map (the loadout along the bottom)
        const narrow = r.width < 620;
        const top = (narrow ? this.options.fitTopNarrow : this.options.fitTop) || 0;
        const bottom = (narrow ? this.options.fitBottomNarrow : this.options.fitBottom) || 0;
        const height = Math.max(200, (r.height || r.width) - top - bottom);
        const size = Math.max(200, Math.min(r.width, height));
        // the trees' names stick out past the extent on the sides: room for them
        this.cam.zoom = Math.max(MIN_ZOOM, size / ((this.map.extent + 90) * 2));
        this.cam.x = 0;
        this.cam.y = (bottom - top) / 2 / this.cam.zoom;
        this.fitted = true;
        this.dirty = true;
    }

    zoomBy(factor) {
        this.fitted = false;
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
                this.fitted = false;
                this.cam.x -= (now[0] - prev[0]) / this.cam.zoom;
                this.cam.y -= (now[1] - prev[1]) / this.cam.zoom;
                this.moved += Math.abs(now[0] - prev[0]) + Math.abs(now[1] - prev[1]);
                this.dirty = true;
            } else if (this.pointers.size === 2) {
                this.fitted = false;
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
            this.fitted = false;
            const [x, y] = local(e);
            const r = el.getBoundingClientRect();
            this.cam.zoomAt(Math.exp(-e.deltaY * 0.0015), x, y, r.width, r.height);
            this.dirty = true;
        };
        this.onDouble = e => {
            this.fitted = false;
            const [x, y] = local(e);
            const r = el.getBoundingClientRect();
            this.cam.zoomAt(1.8, x, y, r.width, r.height);
            this.dirty = true;
        };
        // a press outside the map and its panels, or Escape, puts the picked skill down
        this.onOutside = e => {
            if (!this.selected || this.root.contains(e.target)) return;
            this.select(null, true);
        };
        this.onKey = e => { if (e.key === 'Escape' && this.selected) this.select(null, true); };
        document.addEventListener('pointerdown', this.onOutside, true);
        document.addEventListener('keydown', this.onKey);
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
        const box = this.element.getBoundingClientRect();
        if (box.width >= 2) this.placePopover(box.width, box.height);
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
        drawIcons(g, view, this.icons);
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
        document.removeEventListener('pointerdown', this.onOutside, true);
        document.removeEventListener('keydown', this.onKey);
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

// saved plans (the planner's "Save"), kept in this browser only; storage can be missing or refuse (private windows)
const PLANS_KEY = 'rf-skill-plans';

/** Every saved plan: [{ name, build, levels }]. */
export function loadPlans() {
    try { return JSON.parse(localStorage.getItem(PLANS_KEY) || '[]'); } catch { return []; }
}

export function savePlans(plans) {
    try { localStorage.setItem(PLANS_KEY, JSON.stringify(plans || [])); return true; } catch { return false; }
}
