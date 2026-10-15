"""Generates the Color Melt app icon in the UI kit's flat candy style: one ink
outline, matte faces with a darker lip. Paint drips from the top edge onto a
purple block that melts into a puddle.

Run from the repository root:  python Tools/Icon/make_icon.py
It writes Assets/Art/Icon/app_icon.png (1024 px, full bleed: the launcher
masks the corners). Needs numpy and Pillow."""
import os
import sys
import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'UIKit'))
from sdf import Canvas, hexc, mix, U, I, S  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'Art', 'Icon')
os.makedirs(OUT, exist_ok=True)

N = 1024
INK = hexc('#2B2350')
WHITE = np.array([1.0, 1.0, 1.0])
LINE = 16

cv = Canvas(N, N)

# Background: soft sky-to-lilac gradient.
cv.fill(cv.rrect(-10, -10, N + 10, N + 10, 0), cv.vgrad(hexc('#BFE3FF'), hexc('#E9D6FF'), 0, N))


def inked(shape, color, line=LINE):
    cv.fill(shape - line, INK)
    cv.fill(shape, color)


# Paint dripping from the top edge: x, drip length, drop radius, colour.
drips = [
    (95, 250, 50, '#FF4870'),
    (285, 300, 58, '#FFB81E'),
    (512, 210, 52, '#2FBB5B'),
    (735, 290, 58, '#3D7CF2'),
    (930, 230, 50, '#8758F2'),
]
band = cv.rrect(-40, -200, N + 40, 120, 60)
for index, (x, length, radius, color) in enumerate(drips):
    left = drips[index - 1][0] if index > 0 else -60
    right = drips[index + 1][0] if index + 1 < len(drips) else N + 60
    # Each colour owns a slice of the top band, so the stripes meet edge to edge.
    slice_ = I(band, cv.rrect((left + x) / 2, -300, (x + right) / 2, 400, 0))
    stem = cv.segment(x, 0, x, length, radius * 0.72)
    drop = cv.circle(x, length, radius)
    shape = U(slice_, stem, drop)
    cv.fill(shape - LINE, INK)
for index, (x, length, radius, color) in enumerate(drips):
    left = drips[index - 1][0] if index > 0 else -60
    right = drips[index + 1][0] if index + 1 < len(drips) else N + 60
    slice_ = I(band, cv.rrect((left + x) / 2, -300, (x + right) / 2, 400, 0))
    shape = U(slice_, cv.segment(x, 0, x, length, radius * 0.72), cv.circle(x, length, radius))
    cv.fill(shape, hexc(color))
    # Matte highlight on each drop.
    cv.fill(cv.circle(x - radius * 0.35, length - radius * 0.3, radius * 0.28), WHITE, 0.55)

# The melting block: a rounded cube face with a darker lip, running down
# into one puddle (a single outline around block, drips and puddle).
x0, y0, x1, y1 = 262, 410, 762, 810
body = cv.rrect(x0, y0, x1, y1, 90)
# The base spreads into a puddle, joined to the block by a smooth union.
puddle = cv.rrect(150, 760, 874, 880, 60)
blend = 70.0
h = np.clip(0.5 + 0.5 * (puddle - body) / blend, 0, 1)
melt = puddle * (1 - h) + body * h - blend * h * (1 - h)
cv.fill(melt - LINE, INK)
cv.fill(melt, hexc('#5D36C0'))
cv.fill(cv.rrect(200, 838, 470, 860, 11), WHITE, 0.3)
inked(cv.circle(905, 925, 34), hexc('#5D36C0'), 14)
inked(cv.circle(110, 935, 22), hexc('#5D36C0'), 12)
face = cv.rrect(x0, y0, x1, y1 - 46, 90)
cv.fill(I(face - 3.0, body), mix(hexc('#5D36C0'), INK, 0.25), 0.6)
cv.fill(face, cv.vgrad(hexc('#AD88FF'), hexc('#8758F2'), y0, y1 - 46))
# Soft light along the top of the face, and a glint.
cv.fill(S(face, cv.rrect(x0, y0 + 26, x1, y1 - 20, 90)), WHITE, 0.35)
cv.fill(cv.rrect(x0 + 60, y0 + 70, x0 + 190, y0 + 110, 20), WHITE, 0.6)
cv.fill(cv.circle(x0 + 230, y0 + 90, 20), WHITE, 0.6)

# Cute face: eyes and a smile, in ink.
cv.fill(cv.circle(440, 600, 34), INK)
cv.fill(cv.circle(584, 600, 34), INK)
cv.fill(cv.circle(452, 588, 11), WHITE)
cv.fill(cv.circle(596, 588, 11), WHITE)
cv.fill(I(cv.circle(512, 648, 58), cv.rrect(430, 660, 594, 720, 0)), INK)

cv.image().convert('RGB').save(os.path.join(OUT, 'app_icon.png'))
print('wrote', os.path.join(OUT, 'app_icon.png'))
