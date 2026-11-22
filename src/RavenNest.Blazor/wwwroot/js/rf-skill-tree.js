// The skill trees as a map (combat v2, the game's docs/combat-skills-design.md): the trees (nine with Beastmaster) as wedges around a
// centre, their tiers as rings, every skill a node to tap. Drag (or one finger) pans, the wheel or a pinch zooms, a
// tap picks a node and tells the page (PlayerSkillTrees.razor), which shows it with its Learn button.
// Two or three trees (the trade trees: Gathering and Artisan) do not fill the circle: they open as fans from the same
// centre, facing each other, side by side on a wide view and one above the other on a tall one.
//
// The look is a star map (from the mockup the user picked on 2026-11-08, docs/mockups/skilltree-constellation.png in
// the game): a deep blue night sky with a soft nebula and twinkling stars, a gold raven medallion in the middle with
// compass rays to each tree, the skills as stars (lit and gold rimmed when learned, dim stone when locked), starlight
// links, and the trees' names on gold framed plates.
//
// Two layers. The sky behind (the nebula, the stars, each tree's wedge of light) is WebGL (three.js) where the browser
// has it and a plain 2D canvas where it does not (a good share of players have WebGL turned off). The map itself (rings,
// links, nodes, names) is always drawn on a 2D canvas on top, by the same code, so both read the same.
import * as THREE from './three.module.min.js';

// ---- the map's shape ----------------------------------------------------------------------------------------------

const HUB_RADIUS = 185;          // where each tree's emblem sits
const FIRST_RING = 345;          // tier 0
const RING_STEP = 172;           // between tiers
const CAPSTONE_GAP = 26;         // a little more room before the last ring: the crowns are big
const TIERS = 5;                 // rings: skill level 1, 100, 300, 600 and the capstone at 999
const TIER_LEVELS = [1, 100, 300, 600, 999];
const NODE_RADIUS = { active: 27, passive: 21, special: 30, capstone: 44 };
const CROWN_RAYS = 1.72;         // how far a capstone's rays reach, in its radius
const WEDGE_PADDING = 0.045;     // radians kept free at each side of a wedge
const MAX_WEDGE = 1.3;           // a wedge is never wider than this: two or three trees are fans, not halves of the circle
// Two fans open as wide as the view lets them without making the map smaller: the widest of these that fits best.
const PAIR_WEDGES_WIDE = [1.4, 1.3, 1.2, 1.1, 1.0, 0.9];  // side by side
const PAIR_WEDGES_TALL = [1.1, 1.0, 0.9, 0.8];            // one above the other
const FAN_LABEL_MIN_ZOOM = 0.16; // a few trees have few skills: their names show from much further out
const FAN_NODE_SCALE = 1.8;      // and room for bigger skills, so their pictures can be seen from afar
const FAN_CROWN_SCALE = 1.35;    // (the capstones a little less: they are big already)
const LABEL_MIN_ZOOM = 0.42;     // names show from this zoom up
const MIN_ZOOM = 0.1, MAX_ZOOM = 3.2;
const LEGEND = true;             // the key in the map's corner (wide screens only)

function ringRadius(tier) { return FIRST_RING + tier * RING_STEP + (tier >= TIERS - 1 ? CAPSTONE_GAP : 0); }
const OUTER_RING = ringRadius(TIERS - 1);

/** Zoomed out, the nodes are drawn bigger than the map's scale, so the whole map still reads (up to half again). */
function boostOf(zoom) { return Math.min(1.5, Math.max(1, 1 + (0.8 - zoom) * 1.15)); }

// the star map's colours
const SKY = 0x060a1c;
const SKY_CSS = '#060a1c';
const GOLD = '#e9b85c';
const GOLD_BRIGHT = '#ffd98a';
const GOLD_DEEP = '#a8762c';
const STONE = '#5a6388';
const NODE_DARK = '#0c1230';
const NODE_LOCKED = '#090c1e';
const CREAM = '#fff4d6';
const MEDALLION_RADIUS = 118;
const FONT = '"Segoe UI", system-ui, sans-serif';
const TITLE_FONT = '"FOT-NewRodin Pro B", "Segoe UI", system-ui, sans-serif';

const ICON_ALPHA = { learned: 1, maxed: 1, available: 1, open: 0.62, locked: 0.36 };

/** A low poly raven perched, facing right, in a box of -1..1 (y down): the medallion's emblem. */
const RAVEN = [[-1.0, 0.12], [-0.6, 0.02], [-0.36, -0.2], [-0.02, -0.36], [0.24, -0.32], [0.38, -0.5], [0.56, -0.56],
    [0.7, -0.48], [0.98, -0.4], [0.7, -0.32], [0.64, -0.16], [0.56, 0.04], [0.36, 0.26], [0.12, 0.38], [0.08, 0.66],
    [0.02, 0.66], [-0.01, 0.44], [-0.14, 0.44], [-0.17, 0.66], [-0.23, 0.66], [-0.22, 0.42], [-0.46, 0.3], [-1.0, 0.24]];
const RAVEN_WING = [[-0.62, 0.1], [-0.3, -0.12], [0.18, -0.16], [0.34, 0.02], [0.08, 0.2], [-0.3, 0.2]];

/** The raven emblem on a canvas (gold on dark): the medallion until (or without) its painted picture. */
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

/** A colour between two (t of the way from a to b). */
function mix(a, b, t) {
    const A = hexToRgb(a), B = hexToRgb(b);
    return `rgb(${Math.round(A[0] + (B[0] - A[0]) * t)},${Math.round(A[1] + (B[1] - A[1]) * t)},${Math.round(A[2] + (B[2] - A[2]) * t)})`;
}

/** The shades of a tree's colour the map draws with, made once. */
function paletteOf(color) {
    return {
        c: color,
        light: mix(color, '#ffffff', 0.45),
        mid: mix(color, NODE_DARK, 0.42),
        deep: mix(color, NODE_DARK, 0.74),
        rim: rgba(color, 0.66),
        faint: rgba(color, 0.4),
    };
}

function angleBetween(a, b) {
    let d = (a - b) % (Math.PI * 2);
    if (d > Math.PI) d -= Math.PI * 2;
    if (d < -Math.PI) d += Math.PI * 2;
    return Math.abs(d);
}

function lit(node) { return node.state === 'learned' || node.state === 'maxed'; }

/** How far a node's drawing reaches from its middle, in map units (its rank ring or gold ring included). */
function outerOf(n) {
    if (n.capstone) return n.r * 1.12;
    if (n.kind === 'active') return n.r * 1.15 + 7;
    if (n.kind === 'special') return n.r * 1.1 + 7;
    return n.r + (n.max > 1 ? 10 : 7);
}

/**
 * A path's line. walked: both ends learned. next: from a learned skill (or the tree's emblem) to one that can be
 * learned now. reach: the same, but the points are missing. dim: the level is there, the path is not. locked: the
 * level is not there.
 */
function linkKind(l) {
    const from = l.a.state === undefined || lit(l.a);
    if (l.b.state === 'locked') return 'locked';
    if (from && lit(l.b)) return 'walked';
    if (from && l.b.state === 'available') return 'next';
    if (from) return 'reach';
    return 'dim';
}
const LINK_ORDER = ['locked', 'dim', 'reach', 'next', 'walked'];

/**
 * Places the trees as wedges (tree 0 at the top, clockwise), their tiers as rings, the links from tier to tier. A few
 * trees (their wedges capped at MAX_WEDGE) are fans with sky between them; two of them lie left and right on a wide
 * view (landscape), above and below on a tall one.
 */
function layout(data, landscape, pairWedge) {
    const trees = data.trees;
    const step = (Math.PI * 2) / Math.max(1, trees.length);
    const wedge = Math.min(step, trees.length === 2 ? pairWedge || (landscape ? PAIR_WEDGES_WIDE[0] : PAIR_WEDGES_TALL[0]) : MAX_WEDGE);
    const fans = wedge < step;
    const nodeScale = fans ? FAN_NODE_SCALE : 1, crownScale = fans ? FAN_CROWN_SCALE : 1;
    const first = trees.length === 2 && landscape ? Math.PI : -Math.PI / 2;
    const nodes = data.nodes.map(n => ({ ...n }));
    const byTree = trees.map(() => Array.from({ length: TIERS }, () => []));
    for (const n of nodes) {
        n.tier = Math.max(0, Math.min(TIERS - 1, n.tier | 0));
        if (byTree[n.tree]) byTree[n.tree][n.tier].push(n);
    }

    const hubs = trees.map((t, i) => {
        const angle = first + i * step;
        // how far out the tree's level reaches: past the ring it has opened, and on towards the next one
        let tier = 0;
        while (tier < TIERS - 1 && t.level >= TIER_LEVELS[tier + 1]) tier++;
        const part = tier < TIERS - 1 ? Math.max(0, (t.level - TIER_LEVELS[tier]) / (TIER_LEVELS[tier + 1] - TIER_LEVELS[tier])) : 0;
        const frontier = tier < TIERS - 1 ? ringRadius(tier) + (ringRadius(tier + 1) - ringRadius(tier)) * (0.4 + 0.42 * part) : OUTER_RING + 78;
        return {
            ...t, index: i, angle, x: Math.cos(angle) * HUB_RADIUS, y: Math.sin(angle) * HUB_RADIUS, pal: paletteOf(t.color), frontier,
            cos: Math.cos(angle), sin: Math.sin(angle),
            rings: TIER_LEVELS.map((level, k) => k === TIERS - 1 ? rgba(GOLD, t.level >= level ? 0.6 : 0.24) : rgba(t.color, t.level >= level ? 0.4 : 0.13)),
        };
    });

    const byId = new Map(nodes.map(n => [n.id, n]));
    for (let t = 0; t < trees.length; t++) {
        const centre = hubs[t].angle;
        for (let tier = 0; tier < TIERS; tier++) {
            let ring = byTree[t][tier];
            // above the first ring, each node sits under the middle of the nodes its path comes from, so links rarely cross
            if (tier > 0 && ring.some(n => n.requires && n.requires.length)) {
                const placeOf = n => {
                    const from = (n.requires || []).map(id => byId.get(id)).filter(m => m && m.angle !== undefined);
                    return from.length ? from.reduce((s, m) => s + m.angle, 0) / from.length : centre;
                };
                ring = byTree[t][tier] = ring.map((n, i) => ({ n, key: placeOf(n), i })).sort((a, b) => a.key - b.key || a.i - b.i).map(x => x.n);
            }
            const radius = ringRadius(tier);
            const span = wedge - WEDGE_PADDING * 2;
            ring.forEach((n, i) => {
                const a = ring.length === 1 ? centre : centre - span / 2 + span * (i + 0.5) / ring.length;
                n.angle = a;
                n.x = Math.cos(a) * radius;
                n.y = Math.sin(a) * radius;
                n.r = n.capstone ? NODE_RADIUS.capstone * crownScale : (NODE_RADIUS[n.kind] || 22) * nodeScale;
                n.outer = outerOf(n);
            });
        }
    }

    // links: the hub to its first ring, then the paths (data.nodes[].requires: one of them learned opens the node);
    // without paths in the data, each node to the nearest of the ring below it
    const links = [];
    const hasPaths = nodes.some(n => n.requires && n.requires.length);
    for (let t = 0; t < trees.length; t++) {
        for (const n of byTree[t][0]) links.push({ a: hubs[t], b: n, tree: t });
        if (hasPaths) {
            for (const n of nodes) {
                if (n.tree !== t) continue;
                for (const id of n.requires || []) { const m = byId.get(id); if (m) links.push({ a: m, b: n, tree: t }); }
            }
            continue;
        }
        for (let tier = 1; tier < TIERS; tier++) {
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
    // drawn kind by kind (the walked ones last, on top), a tree at a time
    for (const l of links) l.kind = linkKind(l);
    const linkGroups = [];
    for (const kind of LINK_ORDER) {
        for (let t = 0; t < trees.length; t++) {
            const of = links.filter(l => l.kind === kind && l.tree === t);
            if (of.length) linkGroups.push({ kind, tree: t, links: of });
        }
    }

    // the wedges' borders, and the room each ring has there for its level's label
    const borders = hubs.map(hub => {
        const angle = hub.angle + wedge / 2;
        const room = Array.from({ length: TIERS }, (_, tier) => {
            let best = null;
            for (const n of nodes) {
                if (n.tier !== tier || n.angle === undefined) continue;
                const d = angleBetween(n.angle, angle) * ringRadius(tier);
                if (!best || d - n.outer * 1.5 < best.d - best.outer * 1.5) best = { d, outer: n.outer };
            }
            return best;
        });
        return { angle, cos: Math.cos(angle), sin: Math.sin(angle), room };
    });

    // whose name is written first where names crowd: learned, then what can be learned, then the rest; crowns before others
    // (among a few fans every name is wanted, and a crown's has the fewest places to go: it goes first)
    const weight = n => fans && n.capstone ? -1 : (lit(n) ? 0 : n.state === 'available' ? 2 : n.state === 'open' ? 4 : 6) + (n.capstone ? 0 : 1);
    const named = nodes.filter(n => n.angle !== undefined).sort((a, b) => weight(a) - weight(b) || a.tier - b.tier);

    const lively = nodes.some(n => n.state === 'available' || (n.capstone && lit(n)));
    return { hubs, nodes, links, linkGroups, borders, byId, wedge, fans, nodeScale, crownScale, lively, named, extent: OUTER_RING + 150 };
}

// ---- the camera and the pointer ------------------------------------------------------------------------------------

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

// ---- small pictures made once ---------------------------------------------------------------------------------------

const glowSprites = new Map();

/** A soft round glow in a colour, to stamp behind lit things (far cheaper than a gradient each frame). */
function glowSprite(color) {
    let c = glowSprites.get(color);
    if (c) return c;
    c = document.createElement('canvas');
    c.width = c.height = 128;
    const g = c.getContext('2d');
    const fill = g.createRadialGradient(64, 64, 0, 64, 64, 64);
    [[0, 1], [0.15, 0.82], [0.3, 0.55], [0.45, 0.32], [0.6, 0.17], [0.8, 0.05], [1, 0]].forEach(([at, a]) => fill.addColorStop(at, rgba(color, a)));
    g.fillStyle = fill;
    g.fillRect(0, 0, 128, 128);
    glowSprites.set(color, c);
    return c;
}

function stampGlow(g, color, x, y, size, alpha) {
    if (alpha <= 0.01 || size < 2) return;
    g.globalAlpha = Math.min(1, alpha);
    g.drawImage(glowSprite(color), x - size / 2, y - size / 2, size, size);
}

/**
 * Each tree's wedge of light, the same in both skies: a whisper of the tree's colour over the whole wedge, brighter out
 * to where the tree's level reaches, with a bright edge there. So the map shows at a glance how far every tree is
 * open. As bands of the wedge: [from, to, alpha at from, alpha at to], in map units from the middle.
 */
const WEDGE_INNER = HUB_RADIUS + 62, WEDGE_OUTER = OUTER_RING + 78, WEDGE_GAP = 0.007;
function wedgeBands(hub) {
    const reach = Math.max(WEDGE_INNER + 20, hub.frontier), knee = WEDGE_INNER + (reach - WEDGE_INNER) * 0.7;
    return [[WEDGE_INNER, WEDGE_OUTER, 0.04, 0.04], [WEDGE_INNER, knee, 0.03, 0.09], [knee, reach, 0.09, 0.17], [reach - 2.5, reach + 2.5, 0.45, 0.45]];
}

// ---- drawing: the sky on a 2D canvas (the fallback for WebGL) -----------------------------------------------------

const NEBULA_2D = [[0, 0, 1700, '#5a38c0', 0.42], [-700, -500, 2000, '#2640b4', 0.36], [800, 400, 1900, '#1a738c', 0.3],
    [600, -800, 1500, '#80339a', 0.26], [-900, 700, 1700, '#334dc0', 0.3]];
const STARS_2D = Array.from({ length: 260 }, (_, i) => [((i * 7919) % 1000) / 1000, ((i * 104729) % 997) / 997, 0.4 + ((i * 31) % 10) / 10]);

/** Still: drawn again only when the map moves or changes, so the fallback stays light. */
function drawSky2d(g, view) {
    const { w, h, cam } = view;
    const sky = g.createRadialGradient(w / 2, h / 2, 0, w / 2, h / 2, Math.max(w, h) * 0.75);
    sky.addColorStop(0, '#15183f');
    sky.addColorStop(0.5, '#0b102c');
    sky.addColorStop(1, SKY_CSS);
    g.fillStyle = sky;
    g.fillRect(0, 0, w, h);
    const [cx, cy] = cam.toScreen(0, 0, w, h);
    g.globalCompositeOperation = 'lighter';
    for (const [x, y, size, color, a] of NEBULA_2D) stampGlow(g, color, cx + x * cam.zoom, cy + y * cam.zoom, size * cam.zoom, a * 0.5);
    g.globalCompositeOperation = 'source-over';
    for (const [sx, sy, b] of STARS_2D) {
        g.globalAlpha = Math.min(1, 0.25 + 0.5 * b);
        g.fillStyle = '#dce6ff';
        g.fillRect(sx * w, sy * h, b > 1 ? 2 : 1.3, b > 1 ? 2 : 1.3);
    }
    g.globalAlpha = 1;
    for (const hub of view.map.hubs) {
        const a0 = hub.angle - view.map.wedge / 2 + WEDGE_GAP, a1 = hub.angle + view.map.wedge / 2 - WEDGE_GAP;
        for (const [from, to, alphaFrom, alphaTo] of wedgeBands(hub)) {
            g.beginPath();
            g.arc(cx, cy, to * cam.zoom, a0, a1);
            g.arc(cx, cy, from * cam.zoom, a1, a0, true);
            g.closePath();
            if (alphaFrom === alphaTo) {
                g.fillStyle = rgba(hub.color, alphaFrom);
            } else {
                const fill = g.createRadialGradient(cx, cy, from * cam.zoom, cx, cy, to * cam.zoom);
                fill.addColorStop(0, rgba(hub.color, alphaFrom));
                fill.addColorStop(1, rgba(hub.color, alphaTo));
                g.fillStyle = fill;
            }
            g.fill();
        }
    }
}

// ---- drawing: the map (rings, links, nodes), the same over both skies ---------------------------------------------

/** Actives are diamonds, passives and capstones round, the specials (Battle Rhythm, Last Stand) hexagons. */
function nodePath(g, shape, x, y, r) {
    if (shape === 'active') {
        g.moveTo(x, y - r * 1.15);
        g.lineTo(x + r * 1.15, y);
        g.lineTo(x, y + r * 1.15);
        g.lineTo(x - r * 1.15, y);
        g.closePath();
    } else if (shape === 'special') {
        for (let i = 0; i < 6; i++) {
            const a = Math.PI / 6 + i * Math.PI / 3;
            const px = x + Math.cos(a) * r * 1.1, py = y + Math.sin(a) * r * 1.1;
            if (i === 0) g.moveTo(px, py); else g.lineTo(px, py);
        }
        g.closePath();
    } else {
        g.moveTo(x + r, y);
        g.arc(x, y, r, 0, Math.PI * 2);
    }
}

function shapeOf(n) { return n.capstone ? 'capstone' : n.kind; }

/** A four pointed sparkle. */
function sparklePath(g, x, y, s) {
    const t = s * 0.24;
    g.moveTo(x, y - s); g.lineTo(x + t, y - t); g.lineTo(x + s, y); g.lineTo(x + t, y + t);
    g.lineTo(x, y + s); g.lineTo(x - t, y + t); g.lineTo(x - s, y); g.lineTo(x - t, y - t);
    g.closePath();
}

/** A little padlock on a dark disc: the level is not there yet. */
function drawLock(g, x, y, s) {
    g.beginPath();
    g.arc(x, y, s, 0, Math.PI * 2);
    g.fillStyle = NODE_LOCKED;
    g.fill();
    g.lineWidth = Math.max(1, s * 0.16);
    g.strokeStyle = STONE;
    g.stroke();
    const bw = s * 0.92, bh = s * 0.66;
    g.fillStyle = '#aab3d6';
    g.fillRect(x - bw / 2, y - bh * 0.22, bw, bh);
    g.beginPath();
    g.arc(x, y - bh * 0.22, bw * 0.3, Math.PI, 0);
    g.lineWidth = Math.max(1, s * 0.2);
    g.strokeStyle = '#aab3d6';
    g.stroke();
}

/** A crown, its base's middle at x, y. */
function drawCrown(g, x, y, width, fill, edge, jewel) {
    const hw = width / 2, hgt = width * 0.52, band = width * 0.16;
    g.beginPath();
    g.moveTo(x - hw, y);
    g.lineTo(x - hw * 1.08, y - hgt * 0.78);
    g.lineTo(x - hw * 0.46, y - hgt * 0.36);
    g.lineTo(x, y - hgt);
    g.lineTo(x + hw * 0.46, y - hgt * 0.36);
    g.lineTo(x + hw * 1.08, y - hgt * 0.78);
    g.lineTo(x + hw, y);
    g.lineTo(x + hw, y + band);
    g.lineTo(x - hw, y + band);
    g.closePath();
    g.fillStyle = fill;
    g.fill();
    g.lineJoin = 'round';
    g.lineWidth = Math.max(1, width * 0.055);
    g.strokeStyle = edge;
    g.stroke();
    if (width < 12) return;
    g.fillStyle = jewel;
    for (const [dx, dy] of [[-hw * 1.08, -hgt * 0.78], [0, -hgt], [hw * 1.08, -hgt * 0.78]]) {
        g.beginPath();
        g.arc(x + dx, y + dy, width * 0.075, 0, Math.PI * 2);
        g.fill();
    }
}

/** 0 to 1 and round again, every two seconds: the ring that spreads from a skill that can be learned now. */
function pulseOf(time) { return (time * 0.5) % 1; }

/**
 * A capstone, the crown of its tree: a big gold medallion with the tree's emblem, a crown on top and a burst of rays
 * behind. Dull bronze while its level is far, gold when it can be learned, ablaze and slowly turning once it is.
 */
function drawCapstone(g, n, x, y, r, pal, time, emblem) {
    const st = n.state || 'open';
    const on = lit(n), locked = st === 'locked', ready = st === 'available';
    const gold = locked ? '#6f6a5c' : st === 'open' ? '#b08f55' : on ? GOLD_BRIGHT : GOLD;
    const beat = 0.5 + 0.5 * Math.sin(time * 2.4);

    // the rays
    const turn = on || ready ? time * 0.06 : 0;
    g.globalAlpha = locked ? 0.3 : st === 'open' ? 0.5 : ready ? 0.62 + 0.3 * beat : 1;
    g.fillStyle = on ? '#ffe7ad' : gold;
    g.beginPath();
    for (let i = 0; i < 16; i++) {
        const a = turn + i * Math.PI / 8;
        const long = i % 2 === 0;
        const reach = r * (long ? CROWN_RAYS : 1.38), side = r * (long ? 0.1 : 0.07), base = r * 0.9;
        const ox = Math.cos(a), oy = Math.sin(a);
        g.moveTo(x + ox * base - oy * side, y + oy * base + ox * side);
        g.lineTo(x + ox * reach, y + oy * reach);
        g.lineTo(x + ox * base + oy * side, y + oy * base - ox * side);
        g.closePath();
    }
    g.fill();
    g.globalAlpha = 1;

    if (ready) {
        const p = pulseOf(time);
        g.beginPath();
        g.arc(x, y, r * (1.12 + 0.5 * p), 0, Math.PI * 2);
        g.globalAlpha = 0.8 * (1 - p) * (1 - p);
        g.lineWidth = Math.max(1.2, r * 0.07);
        g.strokeStyle = GOLD_BRIGHT;
        g.stroke();
        g.globalAlpha = 1;
    }

    // the medallion
    g.beginPath();
    g.arc(x, y, r, 0, Math.PI * 2);
    if (on || ready) {
        const face = g.createRadialGradient(x, y - r * 0.2, r * 0.1, x, y, r);
        face.addColorStop(0, on ? pal.mid : pal.deep);
        face.addColorStop(1, on ? pal.deep : NODE_DARK);
        g.fillStyle = face;
    } else {
        g.fillStyle = locked ? NODE_LOCKED : NODE_DARK;
    }
    g.fill();
    g.lineWidth = Math.max(2, r * 0.14);
    g.strokeStyle = gold;
    g.stroke();
    g.beginPath();
    g.arc(x, y, r * 1.07, 0, Math.PI * 2);
    g.lineWidth = Math.max(1, r * 0.035);
    g.strokeStyle = locked ? '#3c3a36' : GOLD_DEEP;
    g.stroke();
    g.beginPath();
    g.arc(x, y, r * 0.8, 0, Math.PI * 2);
    g.lineWidth = Math.max(1, r * 0.045);
    g.strokeStyle = locked ? rgba(STONE, 0.7) : on || ready ? pal.c : pal.rim;
    g.stroke();

    if (emblem) {
        const size = r * 1.22;
        g.globalAlpha = locked ? 0.36 : st === 'open' ? 0.62 : 1;
        g.drawImage(emblem, x - size / 2, y - size / 2, size, size);
        g.globalAlpha = 1;
    } else {
        g.beginPath();
        sparklePath(g, x, y, r * 0.5);
        g.fillStyle = locked ? STONE : pal.c;
        g.fill();
    }

    drawCrown(g, x, y - r * 0.99, r * 1.02, gold, locked ? '#2a2822' : '#3a2608', locked ? '#8d8a80' : on || ready ? pal.light : pal.c);
    if (locked && r >= 14) drawLock(g, x, y + r * 0.98, Math.min(12, Math.max(6, r * 0.3)));
}

/**
 * One skill. Its state reads from afar: stone and dark with a padlock while the level is missing, the tree's colour
 * dimly once the level is there, bright with a spreading ring when it can be learned now, lit with gold round it
 * when learned. A passive with ranks wears them as a ring of segments that turn gold one by one.
 */
function drawNode(g, n, x, y, r, pal, time, picture) {
    if (n.capstone) { drawCapstone(g, n, x, y, r, pal, time, picture); return; }
    const u = r / n.r;
    const st = n.state || 'open';
    const on = lit(n), locked = st === 'locked', ready = st === 'available';
    const shape = n.kind;
    const ranks = n.kind === 'passive' && n.max > 1;

    if (ready) {
        const p = pulseOf(time);
        g.beginPath();
        nodePath(g, shape, x, y, r + (n.outer - n.r + 1 + 15 * p) * u);
        g.globalAlpha = 0.8 * (1 - p) * (1 - p);
        g.lineWidth = Math.max(1.2, 2.6 * u);
        g.strokeStyle = pal.c;
        g.stroke();
        g.globalAlpha = 1;
    }

    // the face and its rim
    g.beginPath();
    nodePath(g, shape, x, y, r);
    if (on || ready) {
        const face = g.createRadialGradient(x, y - r * 0.3, r * 0.1, x, y, r * 1.2);
        face.addColorStop(0, on ? pal.mid : pal.deep);
        face.addColorStop(1, on ? pal.deep : NODE_DARK);
        g.fillStyle = face;
    } else {
        g.fillStyle = locked ? NODE_LOCKED : NODE_DARK;
    }
    g.fill();
    g.lineWidth = Math.max(1.5, (ready ? 3.8 : 3) * u);
    g.strokeStyle = locked ? STONE : on ? pal.light : ready ? pal.c : pal.rim;
    g.stroke();

    // the picture: the skill's own, or for a passive its tree's emblem
    if (picture) {
        const size = r * (n.kind === 'passive' ? 1.5 : n.kind === 'active' ? 1.55 : 1.6);
        g.globalAlpha = ICON_ALPHA[st] ?? 0.6;
        g.drawImage(picture, x - size / 2, y - size / 2, size, size);
        g.globalAlpha = 1;
    } else if (n.kind === 'passive') {
        g.beginPath();
        sparklePath(g, x, y, r * 0.5);
        g.fillStyle = locked ? STONE : on ? pal.light : pal.c;
        g.globalAlpha = ICON_ALPHA[st] ?? 0.6;
        g.fill();
        g.globalAlpha = 1;
    }

    if (ranks) {
        // the ranks: a ring of segments, gold for each one learned
        const rr = r + 6 * u, gap = n.max > 6 ? 0.1 : 0.2, seg = Math.PI * 2 / n.max;
        g.lineWidth = Math.max(1.6, 4.4 * u);
        const arcs = (from, to) => {
            g.beginPath();
            for (let i = from; i < to; i++) {
                const a0 = -Math.PI / 2 + i * seg + gap / 2, a1 = -Math.PI / 2 + (i + 1) * seg - gap / 2;
                g.moveTo(x + Math.cos(a0) * rr, y + Math.sin(a0) * rr);
                g.arc(x, y, rr, a0, a1);
            }
            g.stroke();
        };
        if (n.rank < n.max) { g.strokeStyle = locked ? rgba(STONE, 0.6) : pal.faint; arcs(Math.max(0, n.rank), n.max); }
        if (n.rank > 0) { g.strokeStyle = GOLD_BRIGHT; arcs(0, Math.min(n.rank, n.max)); }
    } else if (on) {
        // learned: gold round it
        g.beginPath();
        nodePath(g, shape, x, y, r + 5.5 * u);
        g.lineWidth = Math.max(1.5, 2.8 * u);
        g.strokeStyle = GOLD_BRIGHT;
        g.stroke();
    }

    if (locked && r >= 13) drawLock(g, x, y + r * (shape === 'passive' ? 0.95 : 1.05), Math.min(11, Math.max(5.5, r * 0.36)));
}

function linkPath(g, view, links) {
    const { w, h, cam } = view;
    g.beginPath();
    for (const l of links) {
        const [ax, ay] = cam.toScreen(l.a.x, l.a.y, w, h);
        const [bx, by] = cam.toScreen(l.b.x, l.b.y, w, h);
        g.moveTo(ax, ay);
        g.lineTo(bx, by);
    }
}

/** The whole map over the sky: rings, the medallion, links, the trees' emblems and every skill. */
function drawMap(g, view, icons) {
    const { w, h, cam, map, selected, hover, time } = view;
    const zoom = cam.zoom, u = zoom * boostOf(zoom);
    const [cx, cy] = cam.toScreen(0, 0, w, h);
    g.lineJoin = 'round';
    g.lineCap = 'butt';

    // the rings, each wedge's arc in its tree's colour (brighter where the tree's level has opened it), the last in gold
    g.lineWidth = 1.4;
    for (const hub of map.hubs) {
        for (let tier = 0; tier < TIERS; tier++) {
            g.strokeStyle = hub.rings[tier];
            g.beginPath();
            g.arc(cx, cy, ringRadius(tier) * zoom, hub.angle - map.wedge / 2 + 0.007, hub.angle + map.wedge / 2 - 0.007);
            g.stroke();
        }
    }

    // the compass rays: long gold ones to each tree, short ones between (at least eight, so a few trees still get a star)
    const hubCount = Math.max(1, map.hubs.length), rays = hubCount >= 4 ? hubCount * 2 : Math.max(8, hubCount * 4);
    const firstRay = map.hubs.length ? map.hubs[0].angle : -Math.PI / 2;
    for (let i = 0; i < rays; i++) {
        const a = firstRay + i * Math.PI * 2 / rays;
        const long = i % (rays / hubCount) === 0;
        const reach = (long ? HUB_RADIUS + 40 : MEDALLION_RADIUS + 40) * zoom;
        const side = (long ? 10 : 6) * zoom;
        const ox = Math.cos(a), oy = Math.sin(a), px = -oy, py = ox;
        const base = MEDALLION_RADIUS * 0.8 * zoom;
        g.beginPath();
        g.moveTo(cx + ox * base + px * side, cy + oy * base + py * side);
        g.lineTo(cx + ox * reach, cy + oy * reach);
        g.lineTo(cx + ox * base - px * side, cy + oy * base - py * side);
        g.closePath();
        g.fillStyle = long ? 'rgba(233,184,92,0.85)' : 'rgba(233,184,92,0.45)';
        g.fill();
    }

    // links, the faint ones first
    for (const group of map.linkGroups) {
        const pal = map.hubs[group.tree].pal;
        const width = px => Math.max(1, px * u);
        linkPath(g, view, group.links);
        if (group.kind === 'walked') {
            g.strokeStyle = rgba(pal.c, 0.3); g.lineWidth = Math.max(3, 11 * u); g.stroke();
            g.strokeStyle = pal.light; g.lineWidth = Math.max(1.8, 4.2 * u); g.stroke();
        } else if (group.kind === 'next') {
            g.strokeStyle = rgba(pal.c, 0.2); g.lineWidth = Math.max(2.5, 8 * u); g.stroke();
            const dash = Math.max(5, 11 * u);
            g.setLineDash([dash, dash * 0.7]);
            g.lineDashOffset = -time * dash * 1.5;
            g.strokeStyle = pal.c; g.lineWidth = Math.max(1.6, 3.2 * u); g.stroke();
            g.setLineDash([]);
            g.lineDashOffset = 0;
        } else if (group.kind === 'reach') {
            g.strokeStyle = rgba(pal.c, 0.6); g.lineWidth = width(2.6); g.stroke();
        } else if (group.kind === 'dim') {
            g.strokeStyle = rgba(pal.c, 0.34); g.lineWidth = width(2.1); g.stroke();
        } else {
            g.strokeStyle = rgba(pal.c, 0.19); g.lineWidth = width(1.7); g.stroke();
        }
    }
    // the paths of the skill under the pointer, or the picked one: what it needs and what it opens
    const focus = hover || selected;
    if (focus) {
        const of = map.links.filter(l => l.b.id === focus || l.a.id === focus);
        if (of.length) {
            linkPath(g, view, of);
            g.strokeStyle = 'rgba(255,244,214,0.7)';
            g.lineWidth = Math.max(1.4, 2 * u);
            g.stroke();
        }
    }

    // the glows, behind everything they belong to
    g.globalCompositeOperation = 'lighter';
    stampGlow(g, GOLD, cx, cy, MEDALLION_RADIUS * 4.6 * zoom, 0.5);
    for (const hub of map.hubs) {
        const [x, y] = cam.toScreen(hub.x, hub.y, w, h);
        stampGlow(g, hub.color, x, y, 170 * zoom, 0.4);
    }
    const beat = 0.5 + 0.5 * Math.sin(time * Math.PI);
    for (const n of map.nodes) {
        const on = lit(n);
        if (!on && n.state !== 'available') continue;
        const [x, y] = cam.toScreen(n.x, n.y, w, h);
        const r = n.r * u;
        if (x < -r * 4 || y < -r * 4 || x > w + r * 4 || y > h + r * 4) continue;
        if (n.capstone) {
            stampGlow(g, GOLD, x, y, r * 6.2, on ? 0.85 : 0.3 + 0.2 * beat);
            if (on) stampGlow(g, n.pal.c, x, y, r * 4, 0.6);
        } else {
            stampGlow(g, n.pal.c, x, y, r * (on ? 5.4 : 4.6), on ? (n.state === 'maxed' ? 0.75 : 0.55) : 0.24 + 0.14 * beat);
        }
    }
    g.globalCompositeOperation = 'source-over';
    g.globalAlpha = 1;

    // the medallion: the painted one, or the drawn one until it has loaded
    const medallion = icons.get('medallion');
    if (medallion) {
        const size = MEDALLION_RADIUS * 2.35 * zoom;
        g.drawImage(medallion, cx - size / 2, cy - size / 2, size, size);
    } else {
        const r = MEDALLION_RADIUS * zoom;
        g.fillStyle = '#100c26';
        g.beginPath(); g.arc(cx, cy, r, 0, Math.PI * 2); g.fill();
        g.strokeStyle = GOLD; g.lineWidth = Math.max(2, 5 * zoom); g.stroke();
        g.strokeStyle = GOLD_DEEP; g.lineWidth = Math.max(1, 2 * zoom);
        g.beginPath(); g.arc(cx, cy, r * 0.84, 0, Math.PI * 2); g.stroke();
        drawRaven(g, cx, cy + r * 0.02, r * 0.66);
    }

    // the trees' emblems on their discs by the medallion
    for (const hub of map.hubs) {
        const [x, y] = cam.toScreen(hub.x, hub.y, w, h);
        const r = 34 * zoom;
        const face = g.createRadialGradient(x, y - r * 0.3, r * 0.1, x, y, r);
        face.addColorStop(0, hub.pal.mid);
        face.addColorStop(1, hub.pal.deep);
        g.beginPath();
        g.arc(x, y, r, 0, Math.PI * 2);
        g.fillStyle = face;
        g.fill();
        g.strokeStyle = GOLD;
        g.lineWidth = Math.max(1.5, 4 * zoom);
        g.stroke();
        const img = icons.get('tree:' + hub.name);
        const size = 56 * zoom;
        if (img && size >= 8) g.drawImage(img, x - size / 2, y - size / 2, size, size);
    }

    // the skills (the one under the pointer a touch bigger, and last so it lies on top)
    let top = null;
    for (const n of map.nodes) {
        if (n.id === hover) { top = n; continue; }
        const [x, y] = cam.toScreen(n.x, n.y, w, h);
        const r = n.r * u;
        const reach = r * (n.capstone ? CROWN_RAYS : 2);
        if (x < -reach || y < -reach || x > w + reach || y > h + reach) continue;
        drawNode(g, n, x, y, r, n.pal, time, icons.get(n.picture));
    }
    if (top) {
        const [x, y] = cam.toScreen(top.x, top.y, w, h);
        const r = top.r * u * 1.1;
        drawNode(g, top, x, y, r, top.pal, time, icons.get(top.picture));
        g.beginPath();
        nodePath(g, shapeOf(top) === 'capstone' ? 'passive' : top.kind, x, y, r + (top.outer - top.r + 4) * u * 1.1);
        g.strokeStyle = 'rgba(255,255,255,0.85)';
        g.lineWidth = 2;
        g.stroke();
    }

    // the picked skill: a slowly turning dashed ring, cream on a soft halo
    const pick = selected ? map.byId.get(selected) : null;
    if (pick) {
        const [x, y] = cam.toScreen(pick.x, pick.y, w, h);
        const r = (pick.outer + 9) * u * (pick.id === hover ? 1.1 : 1);
        g.beginPath();
        g.arc(x, y, r, 0, Math.PI * 2);
        g.strokeStyle = 'rgba(255,244,214,0.22)';
        g.lineWidth = Math.max(5, 8 * u);
        g.stroke();
        const dash = Math.PI * 2 * r / 12;
        g.setLineDash([dash * 0.62, dash * 0.38]);
        g.lineDashOffset = -time * dash * 0.5;
        g.strokeStyle = CREAM;
        g.lineWidth = Math.max(2, 3 * u);
        g.stroke();
        g.setLineDash([]);
        g.lineDashOffset = 0;
    }

    // a skill the page points at (the locked loadout slot's unlock): a gold ring with a halo and rings spreading from
    // it, and when it is out of view an arrow at the edge towards it
    const mark = view.highlight ? map.byId.get(view.highlight) : null;
    if (mark) {
        const [x, y] = cam.toScreen(mark.x, mark.y, w, h);
        const base = Math.max(13, (mark.outer + 12) * u);
        g.globalCompositeOperation = 'lighter';
        stampGlow(g, GOLD, x, y, base * 5.5, 0.75);
        g.globalCompositeOperation = 'source-over';
        g.globalAlpha = 1;
        g.strokeStyle = GOLD_BRIGHT;
        g.beginPath();
        g.arc(x, y, base, 0, Math.PI * 2);
        g.lineWidth = Math.max(3, 4.5 * u);
        g.stroke();
        for (const offset of [0, 0.5]) {
            const p = (time * 0.75 + offset) % 1;
            g.globalAlpha = 0.9 * (1 - p) * (1 - p);
            g.beginPath();
            g.arc(x, y, base + (10 + 44 * p) * Math.max(0.55, u), 0, Math.PI * 2);
            g.lineWidth = Math.max(2, 3 * u);
            g.stroke();
        }
        g.globalAlpha = 1;
        const pad = 30;
        if (x < pad || y < pad || x > w - pad || y > h - pad) {
            const dx = x - w / 2, dy = y - h / 2;
            const k = Math.min((w / 2 - pad) / Math.max(1, Math.abs(dx)), (h / 2 - pad) / Math.max(1, Math.abs(dy)));
            const ex = w / 2 + dx * k, ey = h / 2 + dy * k, a = Math.atan2(dy, dx), bob = 4 * Math.sin(time * 5);
            g.save();
            g.translate(ex + Math.cos(a) * bob, ey + Math.sin(a) * bob);
            g.rotate(a);
            g.beginPath();
            g.moveTo(14, 0); g.lineTo(-10, -12); g.lineTo(-4, 0); g.lineTo(-10, 12);
            g.closePath();
            g.fillStyle = GOLD_BRIGHT;
            g.fill();
            g.lineWidth = 2;
            g.strokeStyle = 'rgba(6,8,18,0.9)';
            g.stroke();
            g.restore();
        }
    }
}

// ---- drawing: the names and numbers --------------------------------------------------------------------------------

function titleSize(zoom) { return Math.max(15, Math.min(26, 34 * zoom)); }

/** A tree's name plate and the line under it: half its width and height, and the name's own width. */
function titleBox(g, hub, size) {
    g.font = `800 ${size}px ${TITLE_FONT}`;
    const nameWidth = g.measureText(hub.name.toUpperCase()).width;
    g.font = `800 ${Math.max(12, size * 0.62)}px ${FONT}`;
    const subWidth = g.measureText(`${hub.skill} ${hub.level}`).width;
    return { nameWidth, hw: Math.max(nameWidth / 2 + size * 1.5, subWidth / 2 + 4), hh: size * 1.25 };
}

/**
 * Where a tree's name plate lies from its capstone, in screen pixels: past the crown, outwards. A fan lying on its side
 * (two trees side by side) carries its name above its crown instead, so the pair may use the view's whole width.
 */
function titleOffset(map, hub, box, crown) {
    if (map.fans && Math.abs(hub.cos) > 0.7) return [0, -(crown + 10 + box.hh)];
    const away = crown + 8 + box.hw * Math.abs(hub.cos) + box.hh * Math.abs(hub.sin);
    return [hub.cos * away, hub.sin * away];
}

function roundRect(g, x, y, width, height, radius) {
    const r = Math.min(radius, width / 2, height / 2);
    g.beginPath();
    g.moveTo(x + r, y);
    g.lineTo(x + width - r, y);
    g.arcTo(x + width, y, x + width, y + r, r);
    g.lineTo(x + width, y + height - r);
    g.arcTo(x + width, y + height, x + width - r, y + height, r);
    g.lineTo(x + r, y + height);
    g.arcTo(x, y + height, x, y + height - r, r);
    g.lineTo(x, y + r);
    g.arcTo(x, y, x + r, y, r);
    g.closePath();
}

/** A small dark panel with a gold line round it, as the page's own panels over the map. */
function panel(g, x, y, width, height) {
    roundRect(g, x, y, width, height, 9);
    g.fillStyle = 'rgba(12,16,40,0.93)';
    g.fill();
    g.lineWidth = 1;
    g.strokeStyle = 'rgba(233,184,92,0.5)';
    g.stroke();
}

function badge(g, text, x, y, size, fill, ink) {
    g.font = `800 ${size}px ${FONT}`;
    const width = Math.max(size * 1.5, g.measureText(text).width + size * 0.8), height = size * 1.5;
    roundRect(g, x - width / 2, y - height / 2, width, height, height / 2);
    g.fillStyle = fill;
    g.fill();
    g.lineWidth = 1;
    g.strokeStyle = 'rgba(8,9,12,0.8)';
    g.stroke();
    g.fillStyle = ink;
    g.fillText(text, x, y + 0.5);
}

function drawLabels(g, view) {
    const { w, h, cam, map, selected, hover } = view;
    const zoom = cam.zoom, u = zoom * boostOf(zoom);
    g.textAlign = 'center';
    g.textBaseline = 'middle';

    // the rings' levels, on the borders between the trees where there is room. From afar only on the borders that have
    // room for every ring (a whole scale, the short way: "Lv" on the crown ring only); up close on every other border,
    // wherever one fits. Found here, drawn last: a skill's name goes first where the two would meet.
    const levels = [];
    {
        const close = zoom >= 0.6;
        const size = close ? 13 : 12, height = size + 8;
        g.font = `800 ${size}px ${FONT}`;
        const texts = TIER_LEVELS.map((level, tier) => close || tier === TIERS - 1 ? 'Lv ' + level : String(level));
        const widths = texts.map(text => g.measureText(text).width + 13);
        const fits = (border, tier) => { const room = border.room[tier]; return !room || (room.d * zoom - room.outer * u) * 2 >= widths[tier] + 5; };
        for (let b = 0; b < map.borders.length; b++) {
            const border = map.borders[b];
            if (close ? !map.fans && b % 2 === 1 : !texts.every((_, tier) => tier === 0 || fits(border, tier))) continue;
            for (let tier = 0; tier < TIERS; tier++) {
                const [x, y] = cam.toScreen(border.cos * ringRadius(tier), border.sin * ringRadius(tier), w, h);
                if (x < -40 || y < -20 || x > w + 40 || y > h + 20 || !fits(border, tier)) continue;
                levels.push({ x, y, half: widths[tier] / 2, tall: height / 2, text: texts[tier], crown: tier === TIERS - 1, size });
            }
        }
    }
    const placed = []; // where names lie
    const drawLevels = () => {
        for (const l of levels) {
            if (placed.some(b => Math.abs(b.x - l.x) < b.half + l.half && Math.abs(b.y - l.y) < b.tall + l.tall)) continue;
            g.font = `800 ${l.size}px ${FONT}`;
            roundRect(g, l.x - l.half, l.y - l.tall, l.half * 2, l.tall * 2, l.tall);
            g.fillStyle = 'rgba(8,10,28,0.9)';
            g.fill();
            g.lineWidth = 1;
            g.strokeStyle = l.crown ? 'rgba(233,184,92,0.65)' : 'rgba(150,170,255,0.35)';
            g.stroke();
            g.fillStyle = l.crown ? GOLD_BRIGHT : '#c6d0f5';
            g.fillText(l.text, l.x, l.y + 0.5);
        }
    };

    // the trees' names past their crowns, readable at every zoom
    const size = titleSize(zoom);
    const crown = NODE_RADIUS.capstone * map.crownScale * u * CROWN_RAYS;
    const titles = map.hubs.map(hub => {
        const box = titleBox(g, hub, size);
        const [ox, oy] = cam.toScreen(hub.cos * OUTER_RING, hub.sin * OUTER_RING, w, h);
        const [dx, dy] = titleOffset(map, hub, box, crown);
        let x = ox + dx, y = oy + dy;
        if (box.hw * 2 < w && zoom < LABEL_MIN_ZOOM && !map.fans) {
            // in the overview kept inside the view (a phone shows the side trees' names whole), and off the crown
            x = Math.min(w - box.hw - 4, Math.max(box.hw + 4, x));
            if (Math.abs(x - ox) < box.hw + crown * 0.7 && Math.abs(y - oy) < box.hh + crown * 0.7) y = oy + (hub.sin > 0.3 ? 1 : -1) * (crown * 0.8 + box.hh + 4);
        }
        return { hub, box, x, y };
    });
    // Nine trees on a narrow view: the names by the top (or the bottom) meet once they are kept inside it. The side
    // trees keep their places; a name nearer the pole steps outwards, past the ones it would lie on.
    const settled = [];
    for (const title of titles.slice().sort((a, b) => Math.abs(a.hub.sin) - Math.abs(b.hub.sin))) {
        for (let pass = 0; pass < 4; pass++) {
            const hit = settled.find(o => Math.abs(o.x - title.x) < o.box.hw + title.box.hw - 2 && Math.abs(o.y - title.y) < o.box.hh + title.box.hh);
            if (!hit) break;
            title.y = hit.y + (title.hub.sin < 0 ? -1 : 1) * (hit.box.hh + title.box.hh + 2);
        }
        settled.push(title);
    }
    for (const { hub, box, x, y } of titles) {
        drawPlate(g, x, y - size * 0.45, box.nameWidth + size * 1.6, size * 1.5);
        g.font = `800 ${size}px ${TITLE_FONT}`;
        shadowText(g, hub.name.toUpperCase(), x, y - size * 0.42, '#fbe7b8');
        g.font = `800 ${Math.max(12, size * 0.62)}px ${FONT}`;
        shadowText(g, `${hub.skill} ${hub.level}`, x, y + size * 0.86, hub.pal.light);
        placed.push({ x, y, half: box.hw, tall: box.hh });
    }
    if (map.fans) {
        // where names may lie beside their skills, they keep off the medallion and the trees' emblems by it
        const [mx, my] = cam.toScreen(0, 0, w, h);
        placed.push({ x: mx, y: my, half: MEDALLION_RADIUS * 1.15 * zoom, tall: MEDALLION_RADIUS * 1.15 * zoom });
        for (const hub of map.hubs) {
            const [hx, hy] = cam.toScreen(hub.x, hub.y, w, h);
            placed.push({ x: hx, y: hy, half: 40 * zoom, tall: 40 * zoom });
        }
    }

    // loadout slots, and the ranks learned of a passive: small badges at the node's shoulder
    const names = zoom >= (map.fans ? FAN_LABEL_MIN_ZOOM : LABEL_MIN_ZOOM);
    const nameSize = map.fans ? Math.max(13, Math.min(17, 34 * zoom)) : Math.max(12, Math.min(16, 15 * zoom));
    for (const n of map.nodes) {
        const [x, y] = cam.toScreen(n.x, n.y, w, h);
        n.sx = x; n.sy = y;
        n.seen = x > -90 && y > -50 && x < w + 90 && y < h + 50;
        if (!n.seen) continue;
        const r = n.r * u;
        if (n.slot && r >= 8) {
            badge(g, String(n.slot), x + r * 0.95, y - r * 0.95, Math.max(11, Math.min(15, 13 * u)), '#e8a33d', '#10131a');
        } else if (names && n.kind === 'passive' && n.max > 1 && n.rank > 0) {
            const full = n.rank >= n.max;
            badge(g, `${n.rank}/${n.max}`, x + r * 0.95, y - r * 0.95, Math.max(11, Math.min(14, 12 * u)), full ? GOLD_BRIGHT : '#141a3c', full ? '#1a1206' : GOLD_BRIGHT);
        }
    }
    if (!names) { drawLevels(); return; }

    // the skills' names, the ones that matter most first (map.named): a name that would lie over another name or
    // over another skill is tried above its node, and left out if that is taken too. Closer in, all of them fit.
    for (const n of map.named) {
        if (!n.seen) continue;
        const capstone = n.capstone, picked = selected === n.id || hover === n.id;
        const fontSize = capstone ? nameSize + 2 : nameSize;
        g.font = `${picked || capstone ? 800 : 700} ${fontSize}px ${FONT}`;
        // Among a few fans the skills stand close and their names are long ("Enchanter's Patience"): a long name is
        // set on two lines there, so it fits under or over its own skill and is not pushed off to a corner.
        let lines = [n.name];
        if (map.fans && g.measureText(n.name).width > 84) {
            let cut = -1;
            for (let i = n.name.indexOf(' '); i >= 0; i = n.name.indexOf(' ', i + 1)) {
                if (cut < 0 || Math.abs(i - n.name.length / 2) < Math.abs(cut - n.name.length / 2)) cut = i;
            }
            if (cut > 0) lines = [n.name.slice(0, cut), n.name.slice(cut + 1)];
        }
        const lineHeight = fontSize * 1.12;
        const half = Math.max(...lines.map(line => g.measureText(line).width)) / 2 + 3, tall = fontSize * 0.62 + (lines.length - 1) * lineHeight / 2;
        const grown = n.outer * u * (n.id === hover ? 1.1 : 1);
        const reach = grown + 5 + tall;
        // under the node, else over it; among a few fans (their skills stand one above the other where the fans lie
        // on their sides) also beside it
        const sides = capstone ? (map.fans ? [[0, 1], [1, 0], [-1, 0]] : [[0, 1]])
            : map.fans ? [[0, 1], [0, -1], [1, 0], [-1, 0], [0.75, 1], [-0.75, 1], [0.75, -1], [-0.75, -1]] : [[0, 1], [0, -1]];
        let at = null;
        for (const [dx, dy] of sides) {
            // (beside it, a name is kept inside the view: a phone's is narrow)
            // (the last four: a corner of the node, the name's near end by its middle)
            const corner = dx !== 0 && dy !== 0;
            const cx = dx ? Math.min(w - half - 5, Math.max(half + 5, n.sx + dx * (corner ? grown * 0.5 + half : grown + 7 + half))) : n.sx, cy = n.sy + dy * reach;
            if (dx && !corner && Math.abs(cx - n.sx) < grown + half) continue;
            let free = true;
            // (among the fans a little air between two names, so two of them never read as one)
            const air = map.fans ? 4 : 0;
            for (const b of placed) { if (Math.abs(b.x - cx) < b.half + half + air && Math.abs(b.y - cy) < b.tall + tall + air) { free = false; break; } }
            if (free) {
                for (const m of map.nodes) {
                    if (m === n || !m.seen) continue;
                    const mr = m.outer * u * (map.fans ? 0.78 : 0.9); // (the fans' skills are big: their corners leave room)
                    // (a capstone wears a crown above it, which a name must not lie on either)
                    const up = map.fans && m.capstone ? mr * 0.6 : 0;
                    if (Math.abs(m.sx - cx) < mr + half && Math.abs(m.sy - up / 2 - cy) < mr + up / 2 + tall) { free = false; break; }
                }
            }
            if (free || picked) { at = [cx, cy]; break; }
        }
        if (at === null) continue;
        placed.push({ x: at[0], y: at[1], half, tall });
        const color = n.state === 'locked' ? (capstone ? 'rgba(214,196,150,0.6)' : 'rgba(226,224,236,0.55)') : capstone ? GOLD_BRIGHT : n.state === 'open' ? 'rgba(255,244,222,0.82)' : '#fff6e2';
        for (let i = 0; i < lines.length; i++) shadowText(g, lines[i], at[0], at[1] + (i - (lines.length - 1) / 2) * lineHeight, color);
    }
    drawLevels();
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
        g.lineJoin = 'round';
        g.lineWidth = 3.5;
        g.strokeStyle = 'rgba(6,8,18,0.92)';
        g.strokeText(text, x, y);
    }
    g.fillStyle = color;
    g.fillText(text, x, y);
}

/** What a skill's state means, in a few words (under the pointer). */
function stateText(n, hub) {
    switch (n.state) {
        case 'locked': return [n.needs ? `Needs ${n.needs}` : `Needs ${hub.skill} ${TIER_LEVELS[n.tier]}`, '#ff9a7a'];
        case 'available': return ['Ready to learn', '#a5ecb4'];
        case 'learned': return [n.max > 1 ? `Rank ${n.rank} of ${n.max}` : 'Learned', GOLD_BRIGHT];
        case 'maxed': return [n.max > 1 ? 'Max rank' : 'Learned', GOLD_BRIGHT];
        default: return ['Needs a path or points', 'rgba(236,228,214,0.8)'];
    }
}

/** The name of the skill under the pointer and where it stands, on a small panel above it. */
function drawTip(g, view) {
    const { w, h, cam, map, hover, selected } = view;
    const n = hover && hover !== selected ? map.byId.get(hover) : null;
    if (!n) return;
    const u = cam.zoom * boostOf(cam.zoom);
    const [x, y] = cam.toScreen(n.x, n.y, w, h);
    const [text, color] = stateText(n, map.hubs[n.tree]);
    g.textAlign = 'center';
    g.textBaseline = 'middle';
    g.font = `800 15px ${FONT}`;
    const nameWidth = g.measureText(n.name).width;
    g.font = `700 13px ${FONT}`;
    const width = Math.max(nameWidth, g.measureText(text).width) + 24, height = 50;
    const reach = (n.capstone ? n.r * 1.5 : n.outer) * u * 1.1;
    const below = cam.zoom >= (map.fans ? FAN_LABEL_MIN_ZOOM : LABEL_MIN_ZOOM) ? 26 : 8; // past the name under the node
    const px = Math.max(6, Math.min(w - width - 6, x - width / 2));
    let py = y - reach - 10 - height;
    if (py < 6) py = y + reach + below;
    panel(g, px, py, width, height);
    g.font = `800 15px ${FONT}`;
    g.fillStyle = n.capstone ? GOLD_BRIGHT : '#fbe7b8';
    g.fillText(n.name, px + width / 2, py + 17);
    g.font = `700 13px ${FONT}`;
    g.fillStyle = color;
    g.fillText(text, px + width / 2, py + 36);
}

const LEGEND_PAL = paletteOf('#8fa2ff');
const LEGEND_KINDS = [['Active', { kind: 'active', r: NODE_RADIUS.active }], ['Passive', { kind: 'passive', r: NODE_RADIUS.passive, max: 5 }],
    ['Special', { kind: 'special', r: NODE_RADIUS.special }], ['Capstone', { kind: 'special', capstone: true, r: NODE_RADIUS.capstone, state: 'learned', still: true }]];
const LEGEND_STATES = [['Locked', 'locked', 0], ['Not yet', 'open', 0], ['Ready', 'available', 0], ['Learned', 'learned', 2]];

/** The map's key in its bottom left corner, drawn with the map's own nodes so it always matches. Wide screens only. */
function drawLegend(g, view) {
    const { w, h, time } = view;
    // (not beside a few fans: their view has a wide bar along the bottom, and no actives to explain)
    if (!LEGEND || view.map.fans || w < 1100 || h < 620) return;
    const row = 27, pad = 12, left = 114, width = pad * 2 + left + 100, height = pad * 2 + row * 4 - 3;
    const x0 = 14, y0 = h - 14 - height;
    panel(g, x0, y0, width, height);
    g.textAlign = 'left';
    g.textBaseline = 'middle';
    for (let i = 0; i < 4; i++) {
        const y = y0 + pad + 12 + i * row;
        const [kindName, kind] = LEGEND_KINDS[i];
        const a = { state: 'open', rank: 0, max: 1, ...kind };
        a.outer = outerOf(a);
        drawNode(g, a, x0 + pad + 12, y + (a.capstone ? 2 : 0), a.capstone ? 7.5 : a.kind === 'active' ? 9.5 : 9, LEGEND_PAL, a.still ? 0 : time, null);
        const [stateName, state, rank] = LEGEND_STATES[i];
        const b = { kind: 'passive', r: NODE_RADIUS.passive, max: 3, rank, state };
        b.outer = outerOf(b);
        drawNode(g, b, x0 + pad + left + 12, y, 8, LEGEND_PAL, time, null);
        g.font = `700 13px ${FONT}`;
        g.fillStyle = '#f3e6cc';
        g.fillText(kindName, x0 + pad + 31, y + 0.5);
        g.fillText(stateName, x0 + pad + left + 30, y + 0.5);
    }
}

// ---- the sky with WebGL (three.js) ---------------------------------------------------------------------------------

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
        this.nebula = this.makeNebula();
        this.scene.add(this.nebula);
        this.stars = this.makeStars();
        this.scene.add(this.stars);
        this.constellations = this.makeConstellations();
        this.scene.add(this.constellations);
        this.motes = this.makeMotes();
        this.scene.add(this.motes);

        this.wedgeMaterial = new THREE.ShaderMaterial({
            vertexShader: 'attribute vec4 tint; varying vec4 vTint; void main() { vTint = tint; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
            fragmentShader: 'varying vec4 vTint; void main() { gl_FragColor = vTint; }',
            transparent: true, depthWrite: false, depthTest: false, side: THREE.DoubleSide,
        });
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
        this.nebulaMaterial = this.glowMaterial();
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

    glowMaterial() {
        return new THREE.ShaderMaterial({
            uniforms: { zoom: { value: 1 }, pixelRatio: { value: window.devicePixelRatio || 1 } },
            vertexShader: GLOW_VERT, fragmentShader: GLOW_FRAG,
            transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
        });
    }

    /** Each tree's wedge of light (wedgeBands) as triangles, made again when the trees' levels change. */
    build(map) {
        const key = map.wedge + ' ' + map.hubs.map(hub => hub.color + hub.frontier + '@' + hub.angle).join(' ');
        if (key === this.wedgeKey) return;
        this.wedgeKey = key;
        if (this.wedges) { this.scene.remove(this.wedges); this.wedges.geometry.dispose(); }
        const steps = 32, pos = [], tint = [];
        for (const hub of map.hubs) {
            const [r, g, b] = hexToRgb(hub.color).map(v => v / 255);
            const a0 = hub.angle - map.wedge / 2 + WEDGE_GAP, span = map.wedge - WEDGE_GAP * 2;
            for (const [from, to, alphaFrom, alphaTo] of wedgeBands(hub)) {
                for (let i = 0; i < steps; i++) {
                    const a = a0 + span * i / steps, n = a0 + span * (i + 1) / steps;
                    const corners = [[a, from, alphaFrom], [a, to, alphaTo], [n, to, alphaTo], [a, from, alphaFrom], [n, to, alphaTo], [n, from, alphaFrom]];
                    for (const [angle, radius, alpha] of corners) {
                        pos.push(Math.cos(angle) * radius, -Math.sin(angle) * radius, -0.9);
                        tint.push(r, g, b, alpha);
                    }
                }
            }
        }
        const geom = new THREE.BufferGeometry();
        geom.setAttribute('position', new THREE.BufferAttribute(new Float32Array(pos), 3));
        geom.setAttribute('tint', new THREE.BufferAttribute(new Float32Array(tint), 4));
        this.wedges = new THREE.Mesh(geom, this.wedgeMaterial);
        this.wedges.frustumCulled = false;
        this.wedges.renderOrder = 5; // over the stars and the motes
        this.scene.add(this.wedges);
    }

    render(view) {
        const { w, h, cam, time } = view;
        const ratio = window.devicePixelRatio || 1;
        if (this.w !== w || this.h !== h || this.ratio !== ratio) {
            this.renderer.setPixelRatio(ratio);
            this.renderer.setSize(w, h, false);
            this.w = w; this.h = h; this.ratio = ratio;
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
        this.renderer.render(this.scene, this.camera);
    }

    dispose() {
        this.wedges?.geometry.dispose();
        this.renderer.dispose();
        this.renderer.forceContextLoss?.();
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
        this.hover = null;
        this.pointers = new Map();
        this.dirty = true;
        this.start = performance.now();
        this.lastDraw = 0;

        // people who asked their system for less motion get a still map: no pulses, turning rings or gliding camera
        this.motion = window.matchMedia ? window.matchMedia('(prefers-reduced-motion: reduce)') : null;
        this.reduced = !!(this.motion && this.motion.matches);
        this.onMotion = () => { this.reduced = this.motion.matches; this.dirty = true; };
        this.motion?.addEventListener?.('change', this.onMotion);

        element.classList.add('rf-skilltree');
        element.style.touchAction = 'none';
        this.useGl = this.options.mode !== '2d' && webglAvailable();
        if (this.useGl) {
            this.glCanvas = document.createElement('canvas');
            this.glCanvas.className = 'rf-skilltree__gl';
            element.appendChild(this.glCanvas);
            try { this.gl = new GlScene(this.glCanvas); }
            catch { this.useGl = false; this.gl = null; this.glCanvas.remove(); }
        }
        if (!this.useGl) {
            this.skyCanvas = document.createElement('canvas');
            this.skyCanvas.className = 'rf-skilltree__sky';
            element.appendChild(this.skyCanvas);
            this.sky = this.skyCanvas.getContext('2d');
        }
        this.canvas = document.createElement('canvas');
        this.canvas.className = 'rf-skilltree__ui';
        element.appendChild(this.canvas);
        this.g = this.canvas.getContext('2d');
        element.dataset.mode = this.useGl ? 'webgl' : '2d';

        this.icons = new Map();
        this.landscape = true;
        this.loadIcons(data);
        this.root = this.options.root || element;
        this.bindInput();
        // a new size keeps the whole map in view until the reader moves or zooms it themselves
        this.resizeObserver = new ResizeObserver(() => { if (this.fitted) this.needsFit = true; this.dirty = true; });
        this.resizeObserver.observe(element);
        // nothing is drawn while the map is scrolled out of sight
        this.inSight = true;
        if (window.IntersectionObserver) {
            this.sightObserver = new IntersectionObserver(entries => { this.inSight = entries[entries.length - 1].isIntersecting; this.dirty = true; });
            this.sightObserver.observe(element);
        }
        this.needsFit = true; // the element may not have its size yet: fit on the first frame that has one
        this.update(data, false);
        this.loop = this.loop.bind(this);
        this.frame = requestAnimationFrame(this.loop);
    }

    get mode() { return this.useGl ? 'webgl' : '2d'; }

    /** Icons by node id (imgs/skills/<id>.webp) and the trees' emblems (tree-<name>.webp); missing ones are left out. */
    loadIcons(data) {
        const base = this.options.iconBase || '/imgs/skills/';
        // capstones and passives without a picture of their own (the page says which have one: icon) wear their tree's emblem
        const want = data.nodes.filter(n => n.icon || (n.kind !== 'passive' && !n.capstone)).map(n => [n.id, n.id]);
        data.trees.forEach(t => want.push(['tree:' + t.name, 'tree-' + t.name.toLowerCase()]));
        want.push(['medallion', 'medallion']);
        this.wanted = this.wanted || new Set();
        for (const [key, file] of want) {
            if (this.wanted.has(key)) continue;
            this.wanted.add(key);
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

    /** The room kept free round the map changed (another bar lies along the bottom): the next fit and the panel use it. */
    setInsets(insets) { Object.assign(this.options, insets || {}); this.dirty = true; }

    placePopover(w, h) {
        const el = this.popover;
        if (!el) return;
        const n = this.selected ? this.map.byId.get(this.selected) : null;
        if (!n) { el.style.visibility = 'hidden'; return; }
        const pw = el.offsetWidth, ph = el.offsetHeight;
        const [x, y] = this.cam.toScreen(n.x, n.y, w, h);
        const r = (n.capstone ? n.r * 1.45 : n.outer + 9) * this.cam.zoom * boostOf(this.cam.zoom) + 14;
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
        this.map = layout(data, this.landscape, this.pairWedge);
        for (const n of this.map.nodes) {
            const hub = this.map.hubs[n.tree];
            n.pal = hub ? hub.pal : LEGEND_PAL;
            n.picture = !n.icon && (n.kind === 'passive' || n.capstone) ? 'tree:' + (hub ? hub.name : '') : n.id;
        }
        if (this.hover && !this.map.byId.has(this.hover)) this.hover = null;
        if (this.selected && !this.map.byId.has(this.selected)) this.selected = null;
        if (this.highlighted && !this.map.byId.has(this.highlighted)) this.highlighted = null;
        this.gl?.build(this.map);
        if (fit) this.fit();
        this.dirty = true;
    }

    /** What the map needs round its middle at a zoom, in screen pixels: the outer ring, the crowns' rays, the names. */
    fitNeeds(zoom, narrow) {
        const crown = NODE_RADIUS.capstone * this.map.crownScale * zoom * boostOf(zoom) * CROWN_RAYS;
        const size = titleSize(zoom);
        if (this.map.fans) {
            // fans: only what their arcs, their crowns and their names reach, so two trees side by side may use the
            // whole width. The crown's rays are in the fan's middle; its corners are the ring and a level's label.
            const ring = OUTER_RING * zoom + 18, reach = OUTER_RING * zoom + crown;
            let w = MEDALLION_RADIUS * 1.4 * zoom, h = w;
            for (const hub of this.map.hubs) {
                for (let k = 0; k <= 8; k++) {
                    const a = hub.angle - this.map.wedge / 2 + this.map.wedge * k / 8;
                    w = Math.max(w, Math.abs(Math.cos(a)) * ring + 30);
                    h = Math.max(h, Math.abs(Math.sin(a)) * ring + 30);
                }
                w = Math.max(w, Math.abs(hub.cos) * reach + 34);
                h = Math.max(h, Math.abs(hub.sin) * reach + 30);
                const box = titleBox(this.g, hub, size);
                const [dx, dy] = titleOffset(this.map, hub, box, crown);
                w = Math.max(w, Math.abs(hub.cos * OUTER_RING * zoom + dx) + box.hw + 8);
                h = Math.max(h, Math.abs(hub.sin * OUTER_RING * zoom + dy) + box.hh + 10);
            }
            return { w, h };
        }
        let hw = 0, hh = 0;
        for (const hub of this.map.hubs) { const box = titleBox(this.g, hub, size); hw = Math.max(hw, box.hw); hh = Math.max(hh, box.hh); }
        const reach = OUTER_RING * zoom + crown;
        // on a narrow screen the side trees' names move in over the map instead of taking its width
        return { w: reach + (narrow ? 6 : 12 + hw * 2), h: reach + 18 + hh * 2 };
    }

    /** The whole map in view. Glides there, except when asked not to (the first frame, a new size). */
    fit(glide = true) {
        const r = this.element.getBoundingClientRect();
        // room kept free for what lies over the map (the loadout along the bottom)
        const narrow = r.width < 620;
        const top = (narrow ? this.options.fitTopNarrow : this.options.fitTop) || 0;
        const bottom = (narrow ? this.options.fitBottomNarrow : this.options.fitBottom) || 0;
        const height = Math.max(200, (r.height || r.width) - top - bottom), width = Math.max(200, r.width);
        const zoomThatFits = () => {
            let lo = MIN_ZOOM, hi = 1.5;
            for (let i = 0; i < 18; i++) {
                const mid = (lo + hi) / 2;
                const needs = this.fitNeeds(mid, narrow);
                if (needs.w * 2 <= width && needs.h * 2 <= height) lo = mid; else hi = mid;
            }
            return lo;
        };
        // two trees: side by side where the view is wide, one above the other where it is tall, their fans as wide as
        // costs the map no size (a narrower one only when it shows the map clearly bigger)
        const landscape = width >= height * 1.1;
        if (this.data && this.data.trees.length === 2) {
            let best = null;
            for (const wedge of landscape ? PAIR_WEDGES_WIDE : PAIR_WEDGES_TALL) {
                this.map = layout(this.data, landscape, wedge);
                const zoom = zoomThatFits();
                if (!best || zoom > best.zoom * 1.04) best = { wedge, zoom };
            }
            this.landscape = landscape;
            this.pairWedge = best.wedge;
            this.update(this.data, false);
        } else {
            this.landscape = landscape;
        }
        const lo = zoomThatFits();
        this.fitted = true;
        this.glideTo(0, (bottom - top) / 2 / lo, lo, glide ? 320 : 0);
    }

    /** Moves the camera there over a moment (at once for those who asked for less motion). */
    glideTo(x, y, zoom, ms) {
        zoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoom));
        if (!ms || this.reduced) {
            this.glide = null;
            this.cam.x = x; this.cam.y = y; this.cam.zoom = zoom;
        } else {
            this.glide = { x0: this.cam.x, y0: this.cam.y, z0: this.cam.zoom, x, y, zoom, from: performance.now(), ms };
        }
        this.dirty = true;
    }

    stepGlide(now) {
        const a = this.glide;
        if (!a) return;
        const k = Math.min(1, (now - a.from) / a.ms), e = 1 - Math.pow(1 - k, 3);
        this.cam.zoom = a.z0 * Math.pow(a.zoom / a.z0, e);
        // the middle moves with the zoom, so the point under the view's middle glides straight
        const m = a.zoom === a.z0 ? e : (1 / a.z0 - 1 / this.cam.zoom) / (1 / a.z0 - 1 / a.zoom);
        this.cam.x = a.x0 + (a.x - a.x0) * m;
        this.cam.y = a.y0 + (a.y - a.y0) * m;
        if (k >= 1) this.glide = null;
        this.dirty = true;
    }

    zoomBy(factor) {
        this.fitted = false;
        const to = this.glide || this.cam;
        this.glideTo(to.x, to.y, to.zoom * factor, 220);
    }

    select(id, notify) {
        this.selected = id;
        this.dirty = true;
        if (notify && this.dotnet) this.dotnet.invokeMethodAsync('OnNodeSelected', id || '');
    }

    /** A pulsing gold ring on a skill, so the page can point at it (the locked loadout slot's unlock); null clears it. */
    highlight(id) {
        this.highlighted = id && this.map.byId.has(id) ? id : null;
        this.dirty = true;
    }

    /** Centres the camera on a node (from the page's list or search). */
    focus(id) {
        const n = this.map.byId.get(id);
        if (!n) return;
        this.fitted = false;
        this.glideTo(n.x, n.y, Math.max(this.cam.zoom, 1.1), 360);
        this.select(id, false);
    }

    hit(sx, sy) {
        const r = this.element.getBoundingClientRect();
        const [wx, wy] = this.cam.toWorld(sx, sy, r.width, r.height);
        const boost = boostOf(this.cam.zoom);
        let best = null, bestD = Infinity;
        for (const n of this.map.nodes) {
            const d = Math.hypot(n.x - wx, n.y - wy);
            const reach = n.r * boost * 1.35 + 14 / this.cam.zoom; // fingers are bigger than mice
            if (d < reach && d < bestD) { best = n; bestD = d; }
        }
        return best;
    }

    setHover(id) {
        if (id === this.hover) return;
        this.hover = id;
        this.element.classList.toggle('is-hover', !!id);
        this.dirty = true;
    }

    bindInput() {
        const el = this.element;
        const local = e => { const r = el.getBoundingClientRect(); return [e.clientX - r.left, e.clientY - r.top]; };
        this.onDown = e => {
            el.setPointerCapture?.(e.pointerId);
            this.pointers.set(e.pointerId, local(e));
            this.moved = 0;
            this.pinch = null;
            this.glide = null;
        };
        this.onMove = e => {
            if (!this.pointers.has(e.pointerId)) {
                // a mouse moving over the map: the skill under it lifts and says what it is
                if (e.pointerType === 'mouse' && this.pointers.size === 0) { const [x, y] = local(e); const n = this.hit(x, y); this.setHover(n ? n.id : null); }
                return;
            }
            const prev = this.pointers.get(e.pointerId);
            const now = local(e);
            this.pointers.set(e.pointerId, now);
            if (this.pointers.size === 1) {
                this.cam.x -= (now[0] - prev[0]) / this.cam.zoom;
                this.cam.y -= (now[1] - prev[1]) / this.cam.zoom;
                this.moved += Math.abs(now[0] - prev[0]) + Math.abs(now[1] - prev[1]);
                if (this.moved >= 8) { this.fitted = false; el.classList.add('is-dragging'); this.setHover(null); }
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
            if (this.pointers.size === 0) el.classList.remove('is-dragging');
            if (p && this.pointers.size === 0 && this.moved < 8) {
                const n = this.hit(p[0], p[1]);
                this.select(n ? n.id : null, true);
            }
        };
        this.onLeave = () => { if (this.pointers.size === 0) this.setHover(null); };
        this.onWheel = e => {
            e.preventDefault();
            this.fitted = false;
            this.glide = null;
            const [x, y] = local(e);
            const r = el.getBoundingClientRect();
            this.cam.zoomAt(Math.exp(-e.deltaY * 0.0015), x, y, r.width, r.height);
            this.dirty = true;
        };
        this.onDouble = e => {
            this.fitted = false;
            this.glide = null;
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
        el.addEventListener('pointerleave', this.onLeave);
        el.addEventListener('wheel', this.onWheel, { passive: false });
        el.addEventListener('dblclick', this.onDouble);
    }

    loop(now) {
        this.frame = requestAnimationFrame(this.loop);
        if (document.hidden || !this.inSight) return;
        const r = this.element.getBoundingClientRect();
        if (r.width < 2 || r.height < 2) return;
        if (this.needsFit) {
            this.needsFit = false;
            this.fit(false);
            if (!this.opened) {
                // the first look: the map settles in from a little further away
                this.opened = true;
                const { x, y, zoom } = this.cam;
                this.cam.zoom = zoom * 0.88;
                this.glideTo(x, y, zoom, 700);
            }
        }
        this.stepGlide(now);
        this.placePopover(r.width, r.height);

        // how often to draw: at once after a change; while something pulses or turns, every frame (the plain canvas
        // every other); otherwise only the WebGL sky's slow drift, ten times a second; and never for a still map
        const lively = this.map.lively || !!this.selected || !!this.highlighted;
        const wait = this.dirty ? 0 : this.reduced ? Infinity : lively ? (this.useGl ? 0 : 30) : (this.useGl ? 100 : Infinity);
        if (now - this.lastDraw < wait) return;
        this.lastDraw = now;

        const time = this.reduced ? 0 : (now - this.start) / 1000;
        const w = Math.max(1, Math.round(r.width)), h = Math.max(1, Math.round(r.height));
        const ratio = window.devicePixelRatio || 1;
        const sized = (canvas) => {
            if (canvas.width === Math.round(w * ratio) && canvas.height === Math.round(h * ratio)) return false;
            canvas.width = Math.round(w * ratio);
            canvas.height = Math.round(h * ratio);
            return true;
        };
        sized(this.canvas);
        const view = { w, h, cam: this.cam, map: this.map, selected: this.selected, hover: this.hover, highlight: this.highlighted, time };
        if (this.useGl) {
            this.gl.render(view);
        } else {
            // the still sky: only when the map moved or changed
            const key = `${w} ${h} ${ratio} ${this.cam.x} ${this.cam.y} ${this.cam.zoom}`;
            if (sized(this.skyCanvas) || key !== this.skyKey || this.skyMap !== this.map) {
                this.skyKey = key;
                this.skyMap = this.map;
                this.sky.setTransform(ratio, 0, 0, ratio, 0, 0);
                drawSky2d(this.sky, view);
            }
        }
        const g = this.g;
        g.setTransform(ratio, 0, 0, ratio, 0, 0);
        g.clearRect(0, 0, w, h);
        drawMap(g, view, this.icons);
        drawLabels(g, view);
        drawLegend(g, view);
        drawTip(g, view);
        this.dirty = false;
    }

    dispose() {
        cancelAnimationFrame(this.frame);
        this.resizeObserver.disconnect();
        this.sightObserver?.disconnect();
        this.motion?.removeEventListener?.('change', this.onMotion);
        const el = this.element;
        el.removeEventListener('pointerdown', this.onDown);
        el.removeEventListener('pointermove', this.onMove);
        el.removeEventListener('pointerup', this.onUp);
        el.removeEventListener('pointercancel', this.onUp);
        el.removeEventListener('pointerleave', this.onLeave);
        el.removeEventListener('wheel', this.onWheel);
        el.removeEventListener('dblclick', this.onDouble);
        document.removeEventListener('pointerdown', this.onOutside, true);
        document.removeEventListener('keydown', this.onKey);
        el.classList.remove('is-hover', 'is-dragging');
        this.gl?.dispose();
        el.innerHTML = '';
    }
}

/**
 * Opens the map in an element. data: { trees: [{ name, skill, level, color }], nodes: [{ id, name, tree (its index in
 * trees), tier, kind ('active' | 'passive' | 'special'), capstone, rank, max, state ('learned' | 'maxed' | 'available' |
 * 'open' | 'locked'), slot (loadout slot from 1, or 0), requires, icon (it has a picture of its own), needs (what a
 * locked one waits for, when it is not "<the tree's skill> <the tier's level>") }] }. options.mode '2d' forces the plain
 * canvas sky. dotnet gets OnNodeSelected(id).
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
