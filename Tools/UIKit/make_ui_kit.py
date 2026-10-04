"""Generates the Color Melt UI kit: 9-slice buttons, pills, window card,
ribbons, icons, coin and stars. Flat shapes, one ink outline colour, matte
faces with a pressed-lip underneath - no glow, no gloss blobs, no drips.

Run from the repository root:  python Tools/UIKit/make_ui_kit.py
It overwrites the PNGs in Assets/Art/UI/Kit (plus coin/star_on/star_off in
Assets/Art/UI). Unity keeps the import settings and 9-slice borders in the
.meta files; for a NEW sprite set its border to the values printed at the end
(left, bottom, right, top). Needs numpy and Pillow."""
import os
import numpy as np
from sdf import Canvas, hexc, mix, U, I, S

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
ART = os.path.join(ROOT, 'Assets', 'Art', 'UI')
OUT = os.path.join(ART, 'Kit')
os.makedirs(os.path.join(OUT, 'Icons'), exist_ok=True)

INK = hexc('#2B2350')
WHITE = np.array([1.0, 1.0, 1.0])

# face top, face bottom, lip
PALETTE = {
    'blue':   ('#63A6FF', '#3D7CF2', '#2853BF'),
    'green':  ('#66DD86', '#2FBB5B', '#1E8A42'),
    'pink':   ('#FF7C98', '#FF4870', '#C7284E'),
    'yellow': ('#FFD95A', '#FFB81E', '#D4850A'),
    'purple': ('#AD88FF', '#8758F2', '#5D36C0'),
    'orange': ('#FFAA55', '#FF8226', '#CC570E'),
    'gray':   ('#CFCBDF', '#ADA8C6', '#807A9E'),
    'white':  ('#FFFFFF', '#F0EBF8', '#C9BFDF'),
}

borders = {}


def button(name, top, bottom, lip, W=200, H=200, O=6, R=46, L=14):
    cv = Canvas(W, H)
    top, bottom, lip = hexc(top), hexc(bottom), hexc(lip)
    outer = cv.rrect(1, 1, W - 1, H - 1, R)
    cv.fill(outer, INK)
    body = cv.rrect(1 + O, 1 + O, W - 1 - O, H - 1 - O, R - O)
    cv.fill(body, lip)
    face_y1 = H - 1 - O - L
    face = cv.rrect(1 + O, 1 + O, W - 1 - O, face_y1, R - O)
    # thin crease between face and lip
    cv.fill(I(face - 2.0, body), mix(lip, INK, 0.25), 0.6)
    cv.fill(face, cv.vgrad(top, bottom, 1 + O, face_y1))
    # soft light along the top edge of the face
    shifted = cv.rrect(1 + O, 1 + O + 8, W - 1 - O, face_y1 + 8, R - O)
    cv.fill(S(face, shifted), WHITE, 0.30)
    # a touch of shade along the bottom of the face
    up = cv.rrect(1 + O, 1 + O - 6, W - 1 - O, face_y1 - 6, R - O)
    cv.fill(S(face, up), lip, 0.25)
    cv.save(os.path.join(OUT, name + '.png'))
    # Unity spriteBorder order: left, bottom, right, top
    borders[name] = (R + 4, O + L + R + 2, R + 4, R + 4)


for key, (t, b, l) in PALETTE.items():
    button('btn_' + key, t, b, l)


def pill(name, top, bottom, lip, W=240, H=120, O=6, L=8):
    cv = Canvas(W, H)
    R = H / 2
    top, bottom, lip = hexc(top), hexc(bottom), hexc(lip)
    cv.fill(cv.rrect(1, 1, W - 1, H - 1, R), INK)
    body = cv.rrect(1 + O, 1 + O, W - 1 - O, H - 1 - O, R)
    cv.fill(body, lip)
    face_y1 = H - 1 - O - L
    face = cv.rrect(1 + O, 1 + O, W - 1 - O, face_y1, R)
    cv.fill(face, cv.vgrad(top, bottom, 1 + O, face_y1))
    cv.fill(S(face, cv.rrect(1 + O, 1 + O + 6, W - 1 - O, face_y1 + 6, R)), WHITE, 0.5)
    cv.save(os.path.join(OUT, name + '.png'))
    borders[name] = (H // 2 + 2, H // 2, H // 2 + 2, H // 2)


pill('pill_white', '#FFFFFF', '#F2EEF9', '#CFC6E3')


def card(name, W=320, H=320, O=7, R=70, L=18):
    cv = Canvas(W, H)
    cv.fill(cv.rrect(1, 1, W - 1, H - 1, R), INK)
    body = cv.rrect(1 + O, 1 + O, W - 1 - O, H - 1 - O, R - O)
    cv.fill(body, hexc('#DCC3A4'))
    face_y1 = H - 1 - O - L
    face = cv.rrect(1 + O, 1 + O, W - 1 - O, face_y1, R - O)
    cv.fill(face, cv.vgrad(hexc('#FFFCF6'), hexc('#FBF0DF'), 1 + O, face_y1))
    cv.fill(S(face, cv.rrect(1 + O, 1 + O + 9, W - 1 - O, face_y1 + 9, R - O)), WHITE, 0.8)
    cv.save(os.path.join(OUT, name + '.png'))
    borders[name] = (R + 6, O + L + R + 4, R + 6, R + 6)


card('window_card')


def well(name, W=160, H=160, R=34):
    """Inset area on a card: a little darker, with a shadow under its top edge."""
    cv = Canvas(W, H)
    d = cv.rrect(1, 1, W - 1, H - 1, R)
    cv.fill(d, hexc('#F1E3CF'))
    cv.fill(S(d, cv.rrect(1, 7, W - 1, H + 5, R)), hexc('#D9C2A2'), 0.9)
    cv.save(os.path.join(OUT, name + '.png'))
    borders[name] = (R + 4, R + 4, R + 4, R + 10)


well('window_well')


def ribbon(name, top, bottom, lip, W=640, H=190):
    cv = Canvas(W, H)
    top, bottom, lip = hexc(top), hexc(bottom), hexc(lip)
    dark = mix(lip, INK, 0.45)
    O = 6
    band_y0, band_y1 = 14, 138
    tail_y0, tail_y1 = 52, 178
    # tails behind the band, with a V notch on the outer end
    lt = cv.polygon([(8, tail_y0), (118, tail_y0), (118, tail_y1), (8, tail_y1), (46, (tail_y0 + tail_y1) / 2)])
    rt = cv.polygon([(W - 8, tail_y0), (W - 118, tail_y0), (W - 118, tail_y1), (W - 8, tail_y1),
                     (W - 46, (tail_y0 + tail_y1) / 2)])
    tails = U(lt, rt) - 2
    cv.fill(tails - O, INK)
    cv.fill(tails, cv.vgrad(mix(lip, top, 0.35), lip, tail_y0, tail_y1))
    # folds where the band wraps behind
    lf = cv.polygon([(70, band_y1 - 10), (118, band_y1 - 10), (118, tail_y1 - 6)])
    rf = cv.polygon([(W - 70, band_y1 - 10), (W - 118, band_y1 - 10), (W - 118, tail_y1 - 6)])
    folds = U(lf, rf)
    cv.fill(folds - O, INK)
    cv.fill(folds, dark)
    band = cv.rrect(64, band_y0, W - 64, band_y1, 22)
    cv.fill(band - O, INK)
    cv.fill(band, cv.vgrad(top, bottom, band_y0, band_y1))
    cv.fill(S(band, cv.rrect(64, band_y0 + 9, W - 64, band_y1 + 9, 22)), WHITE, 0.32)
    cv.fill(S(band, cv.rrect(64, band_y0 - 7, W - 64, band_y1 - 7, 22)), lip, 0.35)
    cv.save(os.path.join(OUT, name + '.png'))
    borders[name] = (150, 0, 150, 0)


ribbon('ribbon_blue', *PALETTE['blue'])
ribbon('ribbon_purple', *PALETTE['purple'])
ribbon('ribbon_green', *PALETTE['green'])
ribbon('ribbon_orange', *PALETTE['orange'])
ribbon('ribbon_slate', '#A3A8CC', '#7E83AD', '#585C86')


# ---------------------------------------------------------------- icons
def icon(name, shape, N=128, shadow=5.0):
    """White glyph with a short ink shadow under it."""
    cv = Canvas(N, N)
    oy = cv.y
    cv.y = oy - shadow
    d_sh = shape(cv)
    cv.y = oy
    d = shape(cv)
    cv.fill(d_sh, INK, 0.28)
    cv.fill(d, WHITE)
    cv.save(os.path.join(OUT, 'Icons', name + '.png'))


def rotated(cv, ang, fn, cx=64, cy=64):
    ox, oy = cv.x, cv.y
    c, s = np.cos(ang), np.sin(ang)
    dx, dy = ox - cx, oy - cy
    cv.x = cx + c * dx + s * dy
    cv.y = cy - s * dx + c * dy
    d = fn(cv)
    cv.x, cv.y = ox, oy
    return d


def gear(cv):
    d = cv.circle(64, 64, 34)
    for k in range(8):
        d = U(d, rotated(cv, k * np.pi / 4, lambda c: c.rrect(53, 14, 75, 40, 5)))
    return S(d, cv.circle(64, 64, 14))


def home(cv):
    roof = cv.polygon([(64, 22), (108, 62), (20, 62)]) - 5
    body = cv.rrect(32, 52, 96, 108, 9)
    door = cv.rrect(54, 76, 74, 116, 7)
    chimney = cv.rrect(84, 26, 98, 52, 3)
    return S(U(roof, body, chimney), door)


def shop(cv):
    bag = cv.rrect(24, 44, 104, 112, 13)
    ring = np.abs(cv.circle(64, 46, 22)) - 6
    handle = I(ring, cv.y - 46)
    holes = U(cv.circle(44, 60, 5.5), cv.circle(84, 60, 5.5))
    return U(S(bag, holes), handle)


def levels(cv):
    pts = [(14, 34), (46, 22), (82, 34), (114, 22), (114, 94), (82, 106), (46, 94), (14, 106)]
    m = cv.polygon(pts) - 3
    folds = U(cv.segment(46, 22, 46, 94, 3.2), cv.segment(82, 34, 82, 106, 3.2))
    return S(m, folds)


def trophy(cv):
    cup = U(cv.rrect(34, 18, 94, 46, 4), I(cv.circle(64, 44, 30), 44 - cv.y))
    handles = U(I(np.abs(cv.circle(34, 42, 14)) - 5, cv.x - 36), I(np.abs(cv.circle(94, 42, 14)) - 5, 92 - cv.x))
    stem = cv.rrect(57, 66, 71, 92, 3)
    base = cv.rrect(38, 90, 90, 108, 6)
    return U(cup, handles, stem, base)


def close(cv):
    return U(cv.segment(40, 40, 88, 88, 11), cv.segment(88, 40, 40, 88, 11))


def restart(cv):
    cx, cy, r = 62, 68, 32
    ring = np.abs(cv.circle(cx, cy, r)) - 9
    a = np.radians(-28)
    gap = cv.polygon([(cx, cy), (cx, -60), (cx + 160, -60), (cx + 160 * np.cos(a), cy + 160 * np.sin(a))])
    arc = S(ring, gap)
    head = cv.polygon([(cx - 2, cy - r - 24), (cx + 30, cy - r), (cx - 2, cy - r + 24)]) - 3
    return U(arc, head)


def play(cv):
    return cv.polygon([(46, 32), (98, 64), (46, 96)]) - 8


def pause(cv):
    return U(cv.rrect(36, 28, 56, 100, 6), cv.rrect(72, 28, 92, 100, 6))


def hint(cv):
    bulb = U(cv.circle(64, 52, 32), cv.polygon([(46, 70), (82, 70), (76, 88), (52, 88)]) - 3)
    cap1 = cv.rrect(50, 92, 78, 100, 4)
    cap2 = cv.rrect(55, 104, 73, 112, 4)
    shine = I(np.abs(cv.circle(64, 52, 20)) - 3.5, cv.polygon([(64, 52), (20, 52), (20, 10), (64, 10)]))
    return U(S(bulb, shine), cap1, cap2)


def lock(cv):
    body = cv.rrect(30, 56, 98, 112, 12)
    shackle = U(I(np.abs(cv.circle(64, 50, 21)) - 7, cv.y - 50), cv.rrect(36, 48, 50, 62, 2), cv.rrect(78, 48, 92, 62, 2))
    key = U(cv.circle(64, 80, 8), cv.rrect(60, 80, 68, 98, 3))
    return S(U(body, shackle), key)


def sound(cv):
    box = cv.rrect(16, 48, 42, 80, 5)
    cone = cv.polygon([(36, 50), (66, 26), (66, 102), (36, 78)]) - 2
    w1 = I(np.abs(cv.circle(66, 64, 22)) - 5, 70 - cv.x)
    w2 = I(np.abs(cv.circle(66, 64, 40)) - 5, 70 - cv.x, np.abs(cv.y - 64) - 32)
    return U(box, cone, w1, w2)


def music(cv):
    n1 = cv.circle(42, 94, 15)
    n2 = cv.circle(92, 82, 15)
    s1 = cv.rrect(49, 30, 58, 94, 3)
    s2 = cv.rrect(99, 20, 108, 82, 3)
    beam = cv.polygon([(49, 30), (108, 18), (108, 38), (49, 50)]) - 1
    return U(n1, n2, s1, s2, beam)


def check(cv):
    return U(cv.segment(28, 66, 54, 92, 11), cv.segment(54, 92, 100, 38, 11))


def back(cv):
    return U(cv.segment(34, 64, 100, 64, 10), cv.polygon([(22, 64), (62, 28), (62, 100)]) - 4)


def plus(cv):
    return U(cv.rrect(54, 22, 74, 106, 7), cv.rrect(22, 54, 106, 74, 7))


def ad(cv):
    """Small film/play badge for 'watch ad' buttons."""
    box = cv.rrect(18, 30, 110, 98, 16)
    tri = cv.polygon([(54, 46), (84, 64), (54, 82)]) - 3
    return S(box, tri)


for nm, fn in [('gear', gear), ('home', home), ('shop', shop), ('levels', levels), ('trophy', trophy),
               ('close', close), ('restart', restart), ('play', play), ('pause', pause), ('hint', hint),
               ('lock', lock), ('sound', sound), ('music', music), ('check', check), ('back', back),
               ('plus', plus), ('ad', ad)]:
    icon(nm, fn)


# ---------------------------------------------------------------- coin & stars
def smin(a, b, k):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)


def coin(path, N=128):
    cv = Canvas(N, N)
    c = N / 2
    cv.fill(cv.circle(c, c, N / 2 - 2), INK)
    cv.fill(cv.circle(c, c + 3, N / 2 - 8), hexc('#D27F0C'))
    cv.fill(cv.circle(c, c - 1, N / 2 - 9), hexc('#F2A516'))
    face = cv.circle(c, c - 1, N / 2 - 17)
    cv.fill(face, cv.vgrad(hexc('#FFE27A'), hexc('#FFC02B'), 18, N - 18))
    drop = smin(cv.circle(c, c + 9, 15), cv.polygon([(c, c - 26), (c + 12, c + 2), (c - 12, c + 2)]), 6)
    cv.fill(drop - 1, hexc('#E69410'))
    cv.fill(cv.circle(c - 6, c + 6, 4), hexc('#FFE27A'), 0.9)
    cv.fill(S(cv.circle(c, c - 1, N / 2 - 9), cv.circle(c + 5, c + 4, N / 2 - 9)), WHITE, 0.55)
    cv.save(path)


def star_shape(cv, cx, cy, R, r):
    pts = []
    for k in range(10):
        ang = -np.pi / 2 + k * np.pi / 5
        rad = R if k % 2 == 0 else r
        pts.append((cx + rad * np.cos(ang), cy + rad * np.sin(ang)))
    return cv.polygon(pts)


def star(path, on, N=256):
    cv = Canvas(N, N)
    c = N / 2
    d = star_shape(cv, c, c + 8, 108, 52) - 12
    if on:
        cv.fill(d, INK)
        inner = d + 12
        cv.fill(inner + 2, hexc('#D4850A'))
        top = star_shape(cv, c, c + 4, 104, 50) - 1
        face = I(inner + 1, top)
        cv.fill(face, cv.vgrad(hexc('#FFE26E'), hexc('#FFB81E'), 30, 220))
        cv.fill(S(face, star_shape(cv, c, c + 14, 104, 50) - 1), WHITE, 0.45)
    else:
        cv.fill(d, hexc('#D3BE9F'))
        cv.fill(d + 9, hexc('#E7D8C2'))
        cv.fill(S(d + 9, star_shape(cv, c, c + 2, 108, 52) - 3), hexc('#CDB693'), 0.8)
    cv.save(path)


coin(os.path.join(ART, 'coin.png'))
star(os.path.join(ART, 'star_on.png'), True)
star(os.path.join(ART, 'star_off.png'), False)


# ---------------------------------------------------------------- achievement icons
def star_glyph(cv):
    return star_shape(cv, 64, 70, 54, 24) - 6


def flag(cv):
    pole = cv.rrect(28, 14, 40, 108, 5)
    cloth = cv.polygon([(40, 20), (104, 24), (88, 44), (104, 64), (40, 62)]) - 3
    base = cv.rrect(18, 102, 52, 114, 5)
    return U(pole, cloth, base)


def crown(cv):
    body = cv.polygon([(20, 42), (44, 66), (64, 32), (84, 66), (108, 42), (98, 94), (30, 94)]) - 4
    tips = U(cv.circle(20, 40, 9), cv.circle(64, 28, 10), cv.circle(108, 40, 9))
    band = cv.rrect(28, 100, 100, 112, 5)
    return U(body, tips, band)


def drop(cv):
    # Circle plus the two tangents from the tip: a clean teardrop.
    d = U(cv.circle(64, 78, 32), cv.polygon([(64, 12), (92, 62), (64, 78), (36, 62)]) - 1)
    shine = I(np.abs(cv.circle(64, 80, 20)) - 4.5, cv.x - 58, 82 - cv.y)
    return S(d, shine)


def brush(cv):
    handle = cv.segment(100, 16, 70, 56, 8)
    ferrule = cv.segment(68, 58, 56, 72, 13)
    # Bristles: a teardrop pointing down-left (circle plus tangents to the tip).
    tip = U(cv.circle(50, 80, 16), cv.polygon([(24, 114), (34.6, 75.8), (50, 80), (58.1, 93.8)]) - 1)
    return U(handle, ferrule, tip)


def bolt(cv):
    return cv.polygon([(76, 10), (28, 70), (60, 70), (48, 118), (100, 52), (68, 52), (84, 10)]) - 3


def hourglass(cv):
    caps = U(cv.rrect(28, 12, 100, 26, 6), cv.rrect(28, 102, 100, 116, 6))
    glass = np.abs(cv.polygon([(38, 26), (90, 26), (68, 64), (90, 102), (38, 102), (60, 64)]) - 2) - 5
    sand = U(cv.polygon([(46, 98), (82, 98), (64, 80)]) - 2, cv.polygon([(52, 36), (76, 36), (64, 50)]) - 2)
    return U(caps, glass, sand)


def coins(cv):
    disc = cv.circle(64, 64, 46)
    ring = np.abs(cv.circle(64, 64, 33)) - 3.5
    mark = smin(cv.circle(64, 74, 13), cv.polygon([(64, 40), (76, 66), (52, 66)]), 5)
    return S(disc, U(ring, mark))


for nm, fn in [('star', star_glyph), ('flag', flag), ('crown', crown), ('drop', drop), ('brush', brush),
               ('bolt', bolt), ('hourglass', hourglass), ('coins', coins)]:
    icon(nm, fn)

print('9-slice borders (left, bottom, right, top):')
for k, v in borders.items():
    print('  %-14s %d %d %d %d' % ((k,) + tuple(int(round(x)) for x in v)))
