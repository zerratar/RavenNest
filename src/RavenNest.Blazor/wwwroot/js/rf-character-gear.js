// What a character WEARS, on the 3D character of the customizer (rf-character-designer.js): armor, capes, and the
// weapons, shields, hats and necklaces that are models of their own. For the Equipment tab of a character page.
//
// The data is what the game's tools/characters/export-gear-web.cs wrote to /characters/gear: one small file per armor
// part and per prop, and gear.json with the part lists by the game's own names (an item's model ids are indices into
// them), every material by its ItemMaterial name, the parts that show the wearer's skin and the hair that is swapped
// under hats. An item arrives here as the server has it (its type, material, model ids, HeadMask and prefab), and
// dress() below does with it what the game's SyntyPlayerAppearance.EquipArmor does in its prefab. Nothing here names
// an item or a gear line.
//
// Only what is worn is fetched. Any other piece is fetched the first time it is looked at or put on, and kept (here
// for the visit, and by the browser for good: every file is asked for with the export's version in its address).

const BASE = '/characters/gear/';
let manifestPromise = null;

function manifest() {
    manifestPromise ??= fetch(BASE + 'gear.json', { cache: 'no-cache' }).then(r => {
        if (!r.ok) throw new Error('no gear export');
        return r.json();
    }).then(g => {
        g.skin = new Set(g.skinParts || []);
        return g;
    });
    return manifestPromise;
}

// ---- The game's numbers (RavenNest.Models) ---------------------------------------------------------------------------

const Type = {
    TwoHandedSword: 1, OneHandedSword: 2, TwoHandedAxe: 3, OneHandedAxe: 4, TwoHandedStaff: 5, TwoHandedBow: 6,
    TwoHandedSpear: 7, Helmet: 8, Chest: 9, Gloves: 10, Boots: 11, Leggings: 12, Shield: 13, LeftShoulder: 14,
    RightShoulder: 15, Amulet: 17, Hat: 30, Mask: 31, HeadCovering: 32,
};
// HeadMask: what a head piece hides of the head under it, and the attachments a body piece has none of
const Hides = { Hair: 1, Brows: 2, Beard: 4, Ears: 8, Head: 16, All: 31 };
const None = { ShoulderPads: 32, ElbowPads: 64, Forearms: 128, KneePads: 256 };

// the bone an item that is a model of its own hangs on (SyntyPlayerAppearance.UpdateBoneTransforms)
const BONE = {
    [Type.Amulet]: 'Neck',
    [Type.Helmet]: 'Head_Attachment', [Type.Mask]: 'Head_Attachment', [Type.Hat]: 'Head_Attachment', [Type.HeadCovering]: 'Head_Attachment',
    [Type.TwoHandedSword]: 'Hand_R', [Type.TwoHandedStaff]: 'Hand_R', [Type.TwoHandedAxe]: 'Hand_R', [Type.OneHandedAxe]: 'Hand_R',
    [Type.TwoHandedSpear]: 'Hand_R', [Type.OneHandedSword]: 'Hand_R',
    [Type.TwoHandedBow]: 'Hand_L', [Type.Shield]: 'Hand_L',
    [Type.LeftShoulder]: 'Shoulder_L', [Type.RightShoulder]: 'Shoulder_R',
};
// how a character stands with each in hand (the game's animator has a Weapon number for each: ItemTypeExtensions.GetAnimation)
const STANCE = {
    [Type.OneHandedSword]: 'armed', [Type.OneHandedAxe]: 'armed', [Type.TwoHandedSword]: 'sword2', [Type.TwoHandedAxe]: 'axe2',
    [Type.TwoHandedSpear]: 'spear', [Type.TwoHandedBow]: 'bow', [Type.TwoHandedStaff]: 'staff', [Type.Shield]: 'shield',
};
const MELEE = new Set([Type.TwoHandedSword, Type.OneHandedSword, Type.TwoHandedAxe, Type.OneHandedAxe, Type.TwoHandedSpear]);
const ONE_HANDED = new Set([Type.OneHandedSword, Type.OneHandedAxe]);
const HEAD_TYPES = new Set([Type.Helmet, Type.Mask, Type.Hat, Type.HeadCovering]);

// the body's own parts, by what they are: a piece of armor takes the place of the ones of its kind
const BODY_KIND = /^Chr_(Torso|ArmUpperRight|ArmUpperLeft|ArmLowerRight|ArmLowerLeft|HandRight|HandLeft|Hips|LegLeft|LegRight)_/;

function modelIds(text) {
    // "27,20,20,0": the main part first (the game's ItemManager.ParseModelIds). A token of -1000 or less names a part
    // games before 0.9.7 do not have: -(1000 * (position + 1) + index) takes that position's place (the Dark Fortress
    // armor sets on the class gear's tiers 7 and 8)
    if (!text) return [];
    const raw = String(text).split(',').map(x => parseInt(x, 10));
    if (Number.isNaN(raw[0])) return [];
    const ids = [];
    const newer = [];
    for (const x of raw) {
        if (Number.isNaN(x)) ids.push(-1);
        else if (x <= -1000) newer.push([Math.floor(-x / 1000) - 1, -x % 1000]);
        else ids.push(x);
    }
    for (const [position, index] of newer) {
        while (ids.length <= position) ids.push(-1);
        ids[position] = index;
    }
    return ids;
}

/**
 * What shows on a character: the game's SyntyPlayerAppearance.EquipArmor, UpdateHelmetVisibility and ShowHead, on the
 * export's part lists. Answers the armor parts to draw (file, material, whether it takes the wearer's skin), the
 * body parts they take the place of, what of the head is hidden, and the props with their bones.
 */
function dress(g, m, a, outfit) {
    const male = a.gender !== 'female';
    const G = male ? 'male' : 'female';
    const list = name => g.lists[name] || [];
    const at = (name, i) => { const l = list(name); return i >= 0 && i < l.length ? l[i] : null; };
    const isBody = file => !!file && file.endsWith('_00');

    // a group is a set of parts of which one shows (the game switches a part's siblings off when it puts one on)
    const groups = new Map();
    const put = (group, file, mat) => { if (file) groups.set(group, { file, mat }); };
    const drop = group => groups.delete(group);
    const pair = (group, left, right, mat) => { put(group + 'Left', left, mat); put(group + 'Right', right, mat); };
    // a pair of attachments, or none on a side whose list has no such part
    const attach = (group, leftList, rightList, i, mat) => {
        const l = at(leftList, i), r = at(rightList, i);
        if (l) put(group + 'Left', l, mat); else drop(group + 'Left');
        if (r) put(group + 'Right', r, mat); else drop(group + 'Right');
    };

    const extra = [];   // attachments that are worn several at a time: on the head, the belt
    const props = [];
    let head = null;    // { hides, list, index }
    const cape = outfit.cape >= 0 && !outfit.capeHidden ? at('capes', outfit.cape) : null;
    const hasMelee = outfit.pieces.some(p => MELEE.has(p.slotType));
    const oneHanded = outfit.pieces.some(p => ONE_HANDED.has(p.slotType));
    const style = outfit.style || 'melee';

    for (const piece of outfit.pieces) {
        const path = piece.prefab || (male ? piece.prefabMale : piece.prefabFemale);
        if (!(piece.armor && !piece.prefab)) {
            // a model of its own, on a bone; only one of the three weapons is in the hand, the one for what is trained
            if (!path || !BONE[piece.slotType] || !g.props[path]) continue;
            const t = piece.slotType;
            if (MELEE.has(t) && style !== 'melee') continue;
            if (t === Type.Shield && (style !== 'melee' || (hasMelee && !oneHanded))) continue;
            if (t === Type.TwoHandedBow && style !== 'ranged') continue;
            if (t === Type.TwoHandedStaff && style !== 'magic' && style !== 'healing') continue;
            // a head piece of armor that is a model of its own (the Dark Fortress helmets): it takes the head piece's
            // place and hides what its mask says (the game's SyntyPlayerAppearance.EquipModelHeadPiece); the helmet
            // switch hides it and what it hid comes back
            if (piece.armor && HEAD_TYPES.has(t)) {
                if (outfit.helmet === false) continue;
                head = { file: null, mat: null, hides: (piece.mask | 0) & Hides.All, hat: -1, attachments: [], flatHair: true };
            }
            props.push({ path, bone: BONE[t], type: t, slot: piece.slot });
            continue;
        }

        const ids = modelIds(male ? piece.male : piece.female);
        if (!ids.length || ids[0] < 0) continue;
        const index = ids[0], more = ids.slice(1), mat = piece.mat || null, mask = piece.mask | 0;
        switch (piece.type) {
            case Type.Boots:
                pair('leg', at(G + 'LegsLeft', index), at(G + 'LegsRight', index), mat);
                break;

            case Type.Chest:
                put('torso', at(G + 'Torso', index), mat);
                if (index > 0) {
                    // pauldrons, or none (a robe, a jerkin)
                    if (mask & None.ShoulderPads) { drop('shoulderLeft'); drop('shoulderRight'); }
                    else attach('shoulder', 'shoulderPadsLeft', 'shoulderPadsRight', more.length > 0 ? more[0] : (male ? 20 : 14), mat);
                    const upper = more.length > 1 ? more[1] : (male ? 13 : 12);
                    pair('armUpper', at(G + 'ArmUpperLeft', upper), at(G + 'ArmUpperRight', upper), mat);
                    if (mask & None.ElbowPads) { drop('elbowLeft'); drop('elbowRight'); }
                    else attach('elbow', 'elbowsLeft', 'elbowsRight', more.length > 2 ? more[2] : 0, mat);
                    // what it wears on the back, unless a cape hangs there
                    drop('back');
                    if (!cape && more.length > 3) put('back', at('backAttachments', more[3]), mat);
                }
                break;

            case Type.Gloves:
                pair('hand', at(G + 'HandsLeft', index), at(G + 'HandsRight', index), mat);
                // gloves that end at the wrist leave the body's own forearms
                if (index > 0 && !(mask & None.Forearms)) {
                    const lower = more.length > 0 ? more[0] : (male ? 5 : 15);
                    pair('armLower', at(G + 'ArmLowerLeft', lower), at(G + 'ArmLowerRight', lower), mat);
                }
                break;

            case Type.LeftShoulder:
            case Type.RightShoulder:
                pair('shoulder', at('shoulderPadsLeft', index), at('shoulderPadsRight', index), mat);
                break;

            case Type.Hat:
            case Type.Mask:
            case Type.Helmet:
            case Type.HeadCovering: {
                const from = piece.type === Type.Helmet ? G + 'Helmets' : piece.type === Type.Mask ? 'masks' : piece.type === Type.Hat ? 'hats' : 'headCoverings';
                const file = at(from, index);
                if (!file) break;
                // a helmet is a whole head of its own and hides everything unless it says what; a hat sits on the head
                let hides = mask & Hides.All;
                if (piece.type === Type.Helmet && hides === 0) hides = Hides.All;
                head = { file, mat, hides, hat: from === 'hats' ? index : -1, attachments: more.map(i => at('headAttachments', i)).filter(Boolean) };
                break;
            }

            case Type.Leggings:
                put('hips', at(G + 'Hips', index), mat);
                if (more.length > 0 && !(mask & None.KneePads)) attach('knee', 'kneeAttachmentsLeft', 'kneeAttachmentsRight', more[0], mat);
                else { drop('kneeLeft'); drop('kneeRight'); }
                for (const i of more.slice(1)) { const f = at('beltAttachments', i); if (f) extra.push({ file: f, mat }); }
                break;
        }
    }

    const hide = { head: false, brows: false, beard: false, hair: false, hairSwap: -1 };
    // the helmet toggle (!toggle helmet): a head piece that is switched off shows nothing and hides nothing
    if (head && outfit.helmet !== false) {
        if (head.file) groups.set('head', { file: head.file, mat: head.mat });
        for (const f of head.attachments) extra.push({ file: f, mat: head.mat });
        hide.head = !!(head.hides & Hides.Head);
        hide.brows = !!(head.hides & Hides.Brows);
        hide.beard = !!(head.hides & Hides.Beard);
        hide.hair = !!(head.hides & Hides.Hair);
        // a hat hides nothing, and many hair styles come through it: those are swapped for a flat one while it shows
        const through = g.hair && g.hair.through ? g.hair.through[String(head.hat)] : null;
        // under a head piece that is a model (a crown, the Lich Crown) every style is laid flat (the game's HairUnder)
        if (!hide.hair && head.flatHair && m.parts.hairs[a.hair]) {
            const flat = g.hair.long.includes(a.hair) ? g.hair.flatLong : g.hair.flatShort;
            if (flat !== a.hair && m.parts.hairs[flat]) hide.hairSwap = flat;
        } else if (!hide.hair && through && through.includes(a.hair) && m.parts.hairs[a.hair]) {
            const flat = g.hair.long.includes(a.hair) ? g.hair.flatLong : g.hair.flatShort;
            if (m.parts.hairs[flat]) hide.hairSwap = flat;
        }
    }

    const worn = [];
    const replaced = new Set();
    for (const [group, part] of groups) {
        const kind = BODY_KIND.exec(part.file);
        if (isBody(part.file)) continue; // the body's own part of that kind stays (bare arms under a sleeveless top)
        if (kind) replaced.add(kind[1]);
        worn.push({ file: part.file, mat: part.mat, skin: g.skin.has(part.file), group });
    }
    for (const part of extra) if (!isBody(part.file)) worn.push({ file: part.file, mat: part.mat, skin: g.skin.has(part.file), group: 'extra' });
    // parts with a picture of their own (the Dark Fortress armor): drawn with it whatever the item's tier, and their
    // bare skin, a file of its own, with the body's material in the wearer's skin (the game's SetPartMaterials)
    const own = g.ownParts || {};
    for (const part of worn.slice()) {
        const info = own[part.file];
        if (!info) continue;
        part.own = info;
        if (info.skin) worn.push({ file: info.skin, mat: null, skin: false, body: true, group: part.group });
    }
    return { worn, replaced, hide, props, cape };
}

// ---- Files ---------------------------------------------------------------------------------------------------------

const partCache = new Map();
const propCache = new Map();
const textureCache = new Map();
const stats = { requests: 0, bytes: 0 };

function align4(n) { return (n + 3) & ~3; }

/** One armor part: a header, then the body export's layout (positions int16 over the bounds, normals int8, uvs uint16, 4 bones and weights). */
function loadPart(THREE, g, remap, file) {
    if (partCache.has(file)) return partCache.get(file);
    const promise = fetch(BASE + 'parts/' + file + '.bin?v=' + g.version).then(r => {
        if (!r.ok) throw new Error('no part ' + file);
        return r.arrayBuffer();
    }).then(buf => {
        stats.requests++; stats.bytes += buf.byteLength;
        const head = new DataView(buf, 0, 52);
        if (head.getUint32(0, true) !== 0x31474652) throw new Error('not a gear part: ' + file);
        const n = head.getUint32(4, true), count = head.getUint32(8, true);
        const f = i => head.getFloat32(12 + i * 4, true);
        const lo = [f(0), f(1), f(2)], hi = [f(3), f(4), f(5)], uvLo = [f(6), f(7)], uvHi = [f(8), f(9)];
        let at = 52;
        const q = new Int16Array(buf, at, n * 3); at += align4(n * 6);
        const nq = new Int8Array(buf, at, n * 4); at += n * 4;
        const uq = new Uint16Array(buf, at, n * 2); at += n * 4;
        const skin = new Uint8Array(buf, at, n * 8); at += n * 8;
        const index = new Uint16Array(buf.slice(at, at + count * 2));
        const pos = new Float32Array(n * 3), nrm = new Float32Array(n * 3), uv = new Float32Array(n * 2);
        const idx = new Uint16Array(n * 4), wt = new Float32Array(n * 4);
        for (let i = 0; i < n; i++) {
            for (let k = 0; k < 3; k++) {
                pos[i * 3 + k] = lo[k] + (q[i * 3 + k] + 32768) / 65535 * (hi[k] - lo[k]);
                nrm[i * 3 + k] = nq[i * 4 + k] / 127;
            }
            uv[i * 2] = uvLo[0] + uq[i * 2] / 65535 * (uvHi[0] - uvLo[0]);
            uv[i * 2 + 1] = uvLo[1] + uq[i * 2 + 1] / 65535 * (uvHi[1] - uvLo[1]);
            for (let k = 0; k < 4; k++) {
                idx[i * 4 + k] = remap[skin[i * 8 + k]];
                wt[i * 4 + k] = skin[i * 8 + 4 + k] / 255;
            }
        }
        const geo = new THREE.BufferGeometry();
        geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
        geo.setAttribute('normal', new THREE.BufferAttribute(nrm, 3));
        geo.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
        geo.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(idx, 4));
        geo.setAttribute('skinWeight', new THREE.Float32BufferAttribute(wt, 4));
        geo.setIndex(new THREE.BufferAttribute(index, 1));
        geo.computeBoundingBox();
        geo.computeBoundingSphere();
        return geo;
    });
    partCache.set(file, promise);
    promise.catch(() => partCache.delete(file));
    return promise;
}

/** One prop: its meshes as one, a group of triangles for each of its materials. */
function loadProp(THREE, g, path) {
    if (propCache.has(path)) return propCache.get(path);
    const info = g.props[path];
    const promise = fetch(BASE + 'props/' + info.f + '.bin?v=' + g.version).then(r => {
        if (!r.ok) throw new Error('no prop ' + path);
        return r.arrayBuffer();
    }).then(buf => {
        stats.requests++; stats.bytes += buf.byteLength;
        const head = new DataView(buf);
        if (head.getUint32(0, true) !== 0x31504652) throw new Error('not a gear prop: ' + path);
        const n = head.getUint32(4, true), count = head.getUint32(8, true), groups = head.getUint32(12, true);
        const f = i => head.getFloat32(16 + i * 4, true);
        const lo = [f(0), f(1), f(2)], hi = [f(3), f(4), f(5)], uvLo = [f(6), f(7)], uvHi = [f(8), f(9)];
        let at = 56;
        const geo = new THREE.BufferGeometry();
        for (let k = 0; k < groups; k++) { geo.addGroup(head.getUint32(at, true), head.getUint32(at + 4, true), k); at += 8; }
        const q = new Int16Array(buf.slice(at, at + n * 6)); at += align4(n * 6);
        const nq = new Int8Array(buf, at, n * 4); at += n * 4;
        const uq = new Uint16Array(buf.slice(at, at + n * 4)); at += n * 4;
        const index = new Uint16Array(buf.slice(at, at + count * 2));
        const pos = new Float32Array(n * 3), nrm = new Float32Array(n * 3), uv = new Float32Array(n * 2);
        for (let i = 0; i < n; i++) {
            for (let k = 0; k < 3; k++) {
                pos[i * 3 + k] = lo[k] + (q[i * 3 + k] + 32768) / 65535 * (hi[k] - lo[k]);
                nrm[i * 3 + k] = nq[i * 4 + k] / 127;
            }
            uv[i * 2] = uvLo[0] + uq[i * 2] / 65535 * (uvHi[0] - uvLo[0]);
            uv[i * 2 + 1] = uvLo[1] + uq[i * 2 + 1] / 65535 * (uvHi[1] - uvLo[1]);
        }
        geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
        geo.setAttribute('normal', new THREE.BufferAttribute(nrm, 3));
        geo.setAttribute('uv', new THREE.BufferAttribute(uv, 2));
        geo.setIndex(new THREE.BufferAttribute(index, 1));
        geo.computeBoundingSphere();
        return geo;
    });
    propCache.set(path, promise);
    promise.catch(() => propCache.delete(path));
    return promise;
}

function texture(THREE, url, color) {
    const key = url + (color ? '|c' : '');
    if (textureCache.has(key)) return textureCache.get(key);
    const t = new THREE.TextureLoader().load(url);
    t.colorSpace = color ? THREE.SRGBColorSpace : THREE.NoColorSpace;
    t.anisotropy = 4;
    textureCache.set(key, t);
    return t;
}

// ---- Materials -----------------------------------------------------------------------------------------------------

// the order of a material's colors in gear.json
const C = { primary: 0, secondary: 1, leather1: 2, leather2: 3, metal1: 4, metal2: 5, metalDark: 6, skin: 7, hair: 8, stubble: 9, scar: 10, bodyArt: 11, eyes: 12 };

/**
 * The game's gear shader ("Glowing Synty Gear", the same graph as the body's): the atlas picture, then one color after
 * another through its mask: cloth, trim, the two leathers, plate, metal trim, mail, hair, skin, stubble, scar, body art.
 * The body's material has its colors baked into its picture; a tier's material sets them all, so here they are uniforms.
 */
function gearMaterial(THREE, g, bodyManifest, info) {
    const color = i => new THREE.Color(info.c[i] || '#000000');
    const version = '?v=' + g.version;
    const mat = new THREE.MeshStandardMaterial({
        map: texture(THREE, BASE + g.textures.albedo + version, true),
        roughness: 1 - (info.smoothness ?? 0.2) * 0.8,
        metalness: info.metallic ?? 0,
    });
    const uniforms = {
        masksA: { value: texture(THREE, '/characters/' + bodyManifest.material.masksA + '?v=' + bodyManifest.version, false) },
        masksB: { value: texture(THREE, '/characters/' + bodyManifest.material.masksB + '?v=' + bodyManifest.version, false) },
        masksC: { value: texture(THREE, BASE + g.textures.masksC + version, false) },
        masksD: { value: texture(THREE, BASE + g.textures.masksD + version, false) },
        masksE: { value: texture(THREE, BASE + g.textures.masksE + version, false) },
        gPrimary: { value: color(C.primary) }, gSecondary: { value: color(C.secondary) },
        gLeather1: { value: color(C.leather1) }, gLeather2: { value: color(C.leather2) },
        gMetal1: { value: color(C.metal1) }, gMetal2: { value: color(C.metal2) }, gMetalDark: { value: color(C.metalDark) },
        gSkin: { value: color(C.skin) }, gHair: { value: color(C.hair) }, gStubble: { value: color(C.stubble) },
        gScar: { value: color(C.scar) }, gBodyArt: { value: color(C.bodyArt) },
        // the graph paints the eyes a fixed red (its eye color is not wired in); a helmet's own head is all that has any
        gEyes: { value: new THREE.Color('#000000') },
        gBodyArtAmount: { value: info.bodyArt ?? 0 },
        gGlow: { value: (info.glow ?? 0) },
        gTime: { value: 0 },
    };
    // the glowing tiers (the graph's Glow_Factor): the plate shines in its own color, and a band of color (red at the
    // feet through purple to blue) drifts up and down the figure over it
    const glows = (info.glow ?? 0) > 0;
    if (glows) mat.onBeforeRender = () => { uniforms.gTime.value = performance.now() / 1000; };
    mat.onBeforeCompile = shader => {
        Object.assign(shader.uniforms, uniforms);
        if (glows) {
            shader.vertexShader = shader.vertexShader
                .replace('#include <common>', '#include <common>\nvarying vec3 vRfPos;')
                .replace('#include <project_vertex>', 'vRfPos = transformed;\n#include <project_vertex>');
        }
        shader.fragmentShader = (glows ? '#define RF_GLOW\n' : '') + shader.fragmentShader
            .replace('#include <map_pars_fragment>', `#include <map_pars_fragment>
uniform sampler2D masksA; uniform sampler2D masksB; uniform sampler2D masksC; uniform sampler2D masksD; uniform sampler2D masksE;
uniform vec3 gPrimary; uniform vec3 gSecondary; uniform vec3 gLeather1; uniform vec3 gLeather2;
uniform vec3 gMetal1; uniform vec3 gMetal2; uniform vec3 gMetalDark;
uniform vec3 gSkin; uniform vec3 gHair; uniform vec3 gStubble; uniform vec3 gScar; uniform vec3 gBodyArt; uniform vec3 gEyes;
uniform float gBodyArtAmount; uniform float gGlow;
vec3 rfGlow = vec3(0.0);
#ifdef RF_GLOW
varying vec3 vRfPos; uniform float gTime;
float rfSmooth(float a, float b, float x) { float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }
vec3 rfBand(float t) {
    vec3 c = vec3(1.0, 0.0, 0.0192);
    c = mix(c, vec3(1.0, 0.6833, 0.0), clamp(t / 0.1971, 0.0, 1.0));
    c = mix(c, vec3(0.6, 0.0, 0.1173), clamp((t - 0.1971) / 0.1706, 0.0, 1.0));
    c = mix(c, vec3(0.2257, 0.0, 0.3529), clamp((t - 0.3677) / 0.2529, 0.0, 1.0));
    c = mix(c, vec3(0.1329, 0.0423, 0.0497), clamp((t - 0.6206) / 0.2412, 0.0, 1.0));
    c = mix(c, vec3(0.0, 0.1829, 1.0), clamp((t - 0.8618) / 0.1382, 0.0, 1.0));
    return pow(c, vec3(2.2));
}
#endif`)
            .replace('#include <map_fragment>', `
vec3 rfC = texture2D(map, vMapUv).rgb;
vec3 rfA = texture2D(masksA, vMapUv).rgb;
vec3 rfB = texture2D(masksB, vMapUv).rgb;
vec3 rfMc = texture2D(masksC, vMapUv).rgb;
vec3 rfMd = texture2D(masksD, vMapUv).rgb;
vec3 rfMe = texture2D(masksE, vMapUv).rgb;
rfC = mix(rfC, gPrimary, rfMc.r);
rfC = mix(rfC, gSecondary, rfMc.g);
rfC = mix(rfC, gLeather1, rfMd.r);
rfC = mix(rfC, gLeather2, rfMd.g);
rfC = mix(rfC, gMetal1, rfMe.r);
rfC = mix(rfC, gMetal2, rfMe.g);
vec3 rfLit = rfC;
rfC = mix(rfC, gMetalDark, rfMc.b);
rfC = mix(rfC, gHair, rfA.b);
rfC = mix(rfC, gSkin, rfA.r);
rfC = mix(rfC, gStubble, rfA.g);
rfC = mix(rfC, gScar, rfB.g);
rfC = mix(rfC, gBodyArt, rfB.r * gBodyArtAmount);
rfC = mix(rfC, gEyes, rfB.b);
#ifdef RF_GLOW
float rfAcross = rfSmooth(1.0, -1.92, vRfPos.x);
float rfUp = rfSmooth(-0.14, 0.53, vRfPos.y * (sin(gTime) + 1.0) * 0.5);
float rfRim = pow(1.0 - clamp(dot(normalize(vNormal), normalize(vViewPosition)), 0.0, 1.0), 2.53);
vec3 rfSweep = 0.88 * rfBand(rfUp) * (rfAcross + rfRim + rfUp);
rfGlow = gGlow * (rfLit * 0.5 + 0.1 * (rfSweep + rfRim * vec3(0.0, 0.6628, 1.0)));
rfC = mix(rfC, rfSweep, gGlow * rfAcross);
#endif
diffuseColor.rgb *= rfC;`)
            .replace('#include <emissivemap_fragment>', `#include <emissivemap_fragment>
totalEmissiveRadiance += rfGlow;`);
    };
    mat.customProgramCacheKey = () => (glows ? 'rf-gear-glow' : 'rf-gear');
    mat.userData.uniforms = uniforms;
    return mat;
}

/**
 * The bows' and staffs' own shader (the game's Weapon.shader): no picture, four colors laid out over the uv in bands
 * (the grip, the limbs, their inner edge, the trim), and a glow that is a picture times a color.
 */
function weaponMaterial(THREE, g, w) {
    const white = texture(THREE, BASE + 'textures/' + w.glowMap + '?v=' + g.version, true);
    const mat = new THREE.MeshStandardMaterial({ map: white, roughness: 0.6, metalness: 0 });
    const glow = new THREE.Color(w.glow || '#000000');
    if (w.glowMap && (glow.r + glow.g + glow.b) > 0.001) {
        mat.emissive = glow;
        mat.emissiveMap = white;
    }
    const uniforms = {
        wHandle: { value: new THREE.Color(w.handle) }, wEdge: { value: new THREE.Color(w.edge) },
        wBody: { value: new THREE.Color(w.body) }, wInner: { value: new THREE.Color(w.inner) },
    };
    mat.onBeforeCompile = shader => {
        Object.assign(shader.uniforms, uniforms);
        shader.fragmentShader = shader.fragmentShader
            .replace('#include <map_pars_fragment>', `#include <map_pars_fragment>
uniform vec3 wHandle; uniform vec3 wEdge; uniform vec3 wBody; uniform vec3 wInner;
float rfBand(vec2 uv, vec2 reach) { vec2 d = abs(uv * 2.0 - 1.0) - reach; return (d.x < 0.0 && d.y < 0.0) ? 1.0 : 0.0; }`)
            .replace('#include <map_fragment>', `
float rfGrip = rfBand(vMapUv, vec2(1.0, 0.5));
float rfNear = rfBand(vec2(vMapUv.x + 0.5), vec2(0.67, 1.0));
float rfLimb = rfBand(vec2(vMapUv.x * 1.49 - 0.2), vec2(0.5));
float rfFar = rfBand(vec2(vMapUv.x - 0.38), vec2(0.5));
float rfTrim = rfBand(vMapUv + 0.03, vec2(0.5, 0.23));
vec3 rfW = wHandle * rfNear + (rfLimb * wBody + rfFar * wInner) * (1.0 - rfNear);
rfW = wHandle * rfGrip + rfW * (1.0 - rfGrip) * (1.0 - rfTrim) + rfTrim * wEdge;
diffuseColor.rgb *= rfW;`);
    };
    mat.customProgramCacheKey = () => 'rf-weapon';
    return mat;
}

function propMaterial(THREE, g, bodyManifest, info) {
    if (!info) return new THREE.MeshStandardMaterial({ color: 0x888888 });
    if (info.gear) return gearMaterial(THREE, g, bodyManifest, info.gear);
    if (info.weapon) return weaponMaterial(THREE, g, info.weapon);
    // drawn without light in the game (the katanas): the picture as it is
    if (info.shader === 'Unlit/Texture' && info.map)
        return new THREE.MeshBasicMaterial({ map: texture(THREE, BASE + 'textures/' + info.map + '?v=' + g.version, true) });
    const mat = new THREE.MeshStandardMaterial({
        color: new THREE.Color(info.color || '#ffffff'),
        roughness: 1 - (info.smoothness ?? 0.2) * 0.8,
        metalness: info.metallic ?? 0,
    });
    if (info.map) mat.map = texture(THREE, BASE + 'textures/' + info.map + '?v=' + g.version, true);
    if (info.emission) {
        mat.emissive = new THREE.Color(info.emission);
        if (info.map) mat.emissiveMap = mat.map;
    }
    return mat;
}

// Unity's Quaternion.Euler (degrees; z, then x, then y), mirrored into this space like every rotation of the export
function unityEuler(THREE, x, y, z) {
    const d = Math.PI / 180;
    const qx = new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(1, 0, 0), x * d);
    const qy = new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), y * d);
    const qz = new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 0, 1), z * d);
    const q = qy.multiply(qx).multiply(qz);
    return new THREE.Quaternion(-q.x, -q.y, q.z, q.w);
}

// ---- The wardrobe of one stage -------------------------------------------------------------------------------------

/**
 * Puts what a character wears on a stage of the customizer (the 3D one; the flat one shows the character as it is).
 * outfit: { pieces: [{ slot, armor, type, slotType, mat, male, female, mask, prefab, prefabMale, prefabFemale }],
 *           style: 'melee' | 'ranged' | 'magic' | 'healing', helmet: bool, cape: index or -1, capeHidden: bool,
 *           capeImage: address or null, relaxed: bool (stands at ease with nothing in the hands) }
 * Answers { set(outfit), preview(piece or null), stats(), dispose() }, or null where there is no gear to show.
 */
export async function attach(designer, stage, onBusy) {
    if (!stage || stage.mode !== '3d') return null;
    const THREE = designer.THREE;
    let g;
    try { g = await manifest(); } catch { return null; }
    const m = stage.m;

    // the export's bones, in the body's
    const byName = new Map(m.skeleton.map((b, i) => [b.name, i]));
    const remap = g.bones.map(name => byName.get(name) ?? 0);
    const bone = name => stage.bones[byName.get(name)];
    // the armor's own bind pose for the bones the body's meshes do not use (the cape's, the attachments'): the same
    // bones, skinned from where the armor was bound
    let skeleton = stage.skeleton;
    if (g.inverses && Object.keys(g.inverses).length > 0) {
        const inverses = stage.skeleton.boneInverses.map(x => x.clone());
        for (const [index, values] of Object.entries(g.inverses)) inverses[remap[+index]] = new THREE.Matrix4().fromArray(values);
        skeleton = new THREE.Skeleton(stage.bones, inverses);
    }
    const rootBone = m.skeleton.find(b => b.parent < 0);
    const itemScale = 1 / ((rootBone && rootBone.s && rootBone.s[0]) || 1);

    const materials = new Map();
    const materialOf = (name, skin) => {
        const info = name ? g.materials[name] : null;
        // no material of its own (a look without a tier): the body's
        if (!info) return stage.material;
        const key = name + (skin ? '|skin' : '');
        if (!materials.has(key)) {
            const mat = gearMaterial(THREE, g, m, info);
            mat.userData.skin = skin;
            materials.set(key, mat);
        }
        return materials.get(key);
    };

    // a part's own picture (and glow), shared by every part that has it
    const ownMaterials = new Map();
    const ownMaterialOf = info => {
        const key = info.tex + '|' + (info.glow || '');
        if (!ownMaterials.has(key)) {
            const load = file => texture(THREE, BASE + file + '?v=' + g.version, true);
            const mat = new THREE.MeshStandardMaterial({ map: load(info.tex), roughness: 0.85, metalness: 0 });
            if (info.glow) { mat.emissiveMap = load(info.glow); mat.emissive = new THREE.Color(1, 1, 1); }
            ownMaterials.set(key, mat);
        }
        return ownMaterials.get(key);
    };

    let capeMaterial = null;
    const capeOf = image => {
        capeMaterial ??= new THREE.MeshStandardMaterial({ roughness: 0.85, metalness: 0, side: THREE.DoubleSide });
        const url = image || (g.cape && g.cape.map ? BASE + 'textures/' + g.cape.map + '?v=' + g.version : null);
        if (url && capeMaterial.userData.url !== url) {
            const t = new THREE.TextureLoader().load(url);
            t.colorSpace = THREE.SRGBColorSpace;
            t.wrapS = t.wrapT = THREE.RepeatWrapping;
            const scale = g.cape && g.cape.scale ? g.cape.scale : 1;
            t.repeat.set(scale, scale);
            t.offset.set(g.cape ? g.cape.offset || 0 : 0, g.cape ? g.cape.offset || 0 : 0);
            capeMaterial.map = t;
            capeMaterial.userData.url = url;
            capeMaterial.needsUpdate = true;
        }
        return capeMaterial;
    };

    let pending = 0;
    const busy = on => {
        pending += on ? 1 : -1;
        if (onBusy) onBusy(pending > 0);
    };

    let worn = { pieces: [], style: 'melee', helmet: true, cape: -1, capeHidden: false, capeImage: null, relaxed: false };
    let look = null;       // a piece being looked at, in place of the one worn in its slot
    const shownProps = new Map(); // path|bone -> Object3D
    let propToken = 0;
    let wearing = { parts: [], props: [] }; // what the last dressing put on, for the numbers and for tests

    const current = () => {
        if (!look) return worn;
        const pieces = worn.pieces.filter(p => p.slot !== look.slot);
        pieces.push(look);
        // the piece looked at is the one in the hand
        let style = worn.style;
        if (MELEE.has(look.slotType) || look.slotType === Type.Shield) style = 'melee';
        else if (look.slotType === Type.TwoHandedBow) style = 'ranged';
        else if (look.slotType === Type.TwoHandedStaff && style !== 'healing') style = 'magic';
        // a head piece looked at shows whatever the helmet switch says (it hides the worn one only: the one tried on
        // is what the player wants to see), and a weapon looked at is held, also when the figure stands at ease
        const head = HEAD_TYPES.has(look.type);
        const held = !!STANCE[look.slotType];
        return { ...worn, pieces, style, helmet: head ? true : worn.helmet, relaxed: held ? false : worn.relaxed };
    };

    const showProps = async props => {
        const token = ++propToken;
        const wanted = new Map(props.map(p => [p.path + '|' + p.bone, p]));
        const loaded = await Promise.all([...wanted].filter(([key]) => !shownProps.has(key)).map(async ([key, p]) => {
            busy(true);
            try { return [key, p, await loadProp(THREE, g, p.path)]; }
            catch { return null; }
            finally { busy(false); }
        }));
        if (token !== propToken || stage.disposed) return;
        for (const [key, object] of [...shownProps]) {
            if (wanted.has(key)) continue;
            object.removeFromParent();
            shownProps.delete(key);
        }
        for (const entry of loaded) {
            if (!entry) continue;
            const [key, p, geo] = entry;
            const parent = bone(p.bone);
            if (!parent || shownProps.has(key)) continue;
            const info = g.props[p.path];
            const mesh = new THREE.Mesh(geo, info.m.map(i => propMaterial(THREE, g, m, g.propMaterials[i])));
            mesh.frustumCulled = false;
            const holder = new THREE.Group();
            if (p.type === Type.Amulet) {
                // the game puts every necklace at one place on the neck, whatever its prefab says
                holder.position.set(1.1495, -0.095946, -0);
                holder.quaternion.copy(unityEuler(THREE, 83.663, 314.554, 44.606));
                holder.scale.set(0.7563273, 0.8765048, 0.8765048);
            } else {
                holder.position.fromArray(info.p);
                holder.quaternion.fromArray(info.q);
                holder.scale.fromArray(info.s);
            }
            holder.add(mesh);
            // the game hangs an item on its bone at the bone's own place, unturned, and at its true size: the bones
            // are in centimeters under a root a hundredth the size, so the item is a hundred times the bone's measure
            const item = new THREE.Group();
            item.scale.setScalar(itemScale);
            item.add(holder);
            parent.add(item);
            shownProps.set(key, item);
        }
        stand(props, token);
    };

    // with a weapon in hand the character stands as the game's does (the animation pack's idle for that weapon)
    const stances = new Map();
    let roomy = null;
    const stand = async (props, token) => {
        if (typeof stage.stand !== 'function') return;
        let name = null;
        for (const p of props) {
            const s = STANCE[p.type];
            if (s && (s !== 'shield' || !name)) name = s;
        }
        const stance = name && g.stances ? g.stances[name] : null;
        const file = stance ? stance.file : null;
        // a stance stands wide: the camera steps back for it
        const far = on => {
            if (!stage.wanted || typeof stage.wanted.distance !== 'number') return;
            roomy ??= stage.wanted.distance;
            stage.wanted.distance = roomy * (on ? 1.2 : 1);
            // the weapon is held out to one side: the view moves over a little, so it is in the picture
            if (stage.wanted.target && stage.view !== 'face') stage.wanted.target.x = on ? -0.14 : 0;
        };
        if (!file) { far(false); stage.stand(null); return; }
        if (!stances.has(name)) {
            stances.set(name, fetch(BASE + file + '?v=' + g.version).then(r => {
                if (!r.ok) throw new Error('no stance ' + name);
                return r.text();
            }).then(text => { stats.requests++; stats.bytes += text.length; return JSON.parse(text); }));
            stances.get(name).catch(() => stances.delete(name));
        }
        let animation;
        try { animation = await stances.get(name); } catch { return; }
        if (token !== propToken || stage.disposed) return;
        far(true);
        stage.stand(name, animation, stance.turn);
    };

    // the customizer asks this each time it dresses the character: the body parts armor takes the place of go, and
    // the armor's own meshes come back
    stage.outfit = async (a, parts) => {
        const outfit = current();
        let d;
        try { d = dress(g, m, a, outfit); }
        catch (e) {
            // whatever is wrong with a piece, the character itself still shows
            console.warn('The gear could not be worked out', e);
            return { meshes: [] };
        }
        parts.body = parts.body.filter(p => { const kind = BODY_KIND.exec(p.name); return !(kind && d.replaced.has(kind[1])); });
        if (d.hide.head) parts.head = null;
        if (d.hide.brows) parts.eyebrows = null;
        if (d.hide.beard) parts.beard = null;
        if (d.hide.hair) parts.hair = null;
        else if (d.hide.hairSwap >= 0) parts.hair = m.parts.hairs[d.hide.hairSwap];

        const wanted = d.worn.map(p => p.own
            ? { key: 'g:' + p.file + '|own', file: p.file, material: ownMaterialOf(p.own) }
            : p.body
                ? { key: 'g:' + p.file + '|body', file: p.file, material: stage.material }
                : { key: 'g:' + p.file + '|' + (p.mat || '') + (p.skin ? '|skin' : ''), file: p.file, material: materialOf(p.mat, p.skin) });
        if (d.cape) wanted.push({ key: 'g:' + d.cape + '|cape', file: d.cape, material: capeOf(outfit.capeImage) });
        // the wearer's own skin on the armor that shows skin
        for (const mat of materials.values()) if (mat.userData.skin) mat.userData.uniforms.gSkin.value.set(a.skin);

        busy(true);
        let meshes;
        try {
            meshes = (await Promise.all(wanted.map(async w => {
                try { return { key: w.key, geometry: await loadPart(THREE, g, remap, w.file), material: w.material, skeleton }; }
                catch { return null; }
            }))).filter(Boolean);
        } finally { busy(false); }
        // at ease the hands are empty: no weapon and no shield, so no fighting stance either
        const props = outfit.relaxed ? d.props.filter(p => !STANCE[p.type]) : d.props;
        showProps(props);
        wearing = { parts: wanted.map(w => w.key.slice(2)), props: props.map(p => p.path + ' on ' + p.bone) };
        return { meshes };
    };

    // the character again as it was last shown (its look as the page gave it), now with what it wears
    const redress = () => (stage.look ? stage.show(stage.look) : Promise.resolve());

    return {
        set(outfit) {
            worn = { ...worn, ...outfit, pieces: outfit.pieces || [] };
            look = null;
            return redress();
        },
        preview(piece) {
            if ((piece ? piece.id : null) === (look ? look.id : null)) return Promise.resolve();
            look = piece || null;
            return redress();
        },
        stats() { return { ...stats, parts: partCache.size, props: propCache.size, wearing }; },
        dispose() {
            stage.outfit = null;
            for (const object of shownProps.values()) object.removeFromParent();
            shownProps.clear();
            for (const mat of materials.values()) mat.dispose();
            if (capeMaterial) capeMaterial.dispose();
            if (skeleton !== stage.skeleton) skeleton.dispose();
        },
    };
}
