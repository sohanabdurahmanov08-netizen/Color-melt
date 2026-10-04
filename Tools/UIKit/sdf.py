"""Tiny signed-distance-field painter for flat UI sprites (numpy + PIL)."""
import numpy as np
from PIL import Image


def hexc(h):
    h = h.lstrip('#')
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


def mix(a, b, t):
    return a * (1 - t) + b * t


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        y, x = np.mgrid[0:h, 0:w].astype(np.float64)
        self.x = x + 0.5
        self.y = y + 0.5
        self.rgb = np.zeros((h, w, 3))
        self.a = np.zeros((h, w))

    # --- primitives (negative inside) ---
    def rrect(self, x0, y0, x1, y1, r, dx=0.0, dy=0.0):
        cx, cy = (x0 + x1) / 2 + dx, (y0 + y1) / 2 + dy
        hw, hh = (x1 - x0) / 2, (y1 - y0) / 2
        r = min(r, hw, hh)
        qx = np.abs(self.x - cx) - (hw - r)
        qy = np.abs(self.y - cy) - (hh - r)
        out = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0))
        return out + np.minimum(np.maximum(qx, qy), 0) - r

    def circle(self, cx, cy, r):
        return np.hypot(self.x - cx, self.y - cy) - r

    def segment(self, ax, ay, bx, by, r):
        px, py = self.x - ax, self.y - ay
        vx, vy = bx - ax, by - ay
        t = np.clip((px * vx + py * vy) / (vx * vx + vy * vy), 0, 1)
        return np.hypot(px - vx * t, py - vy * t) - r

    def polygon(self, pts):
        pts = np.asarray(pts, dtype=np.float64)
        n = len(pts)
        d = (self.x - pts[0, 0]) ** 2 + (self.y - pts[0, 1]) ** 2
        s = np.ones_like(d)
        j = n - 1
        for i in range(n):
            ex, ey = pts[j] - pts[i]
            wx, wy = self.x - pts[i, 0], self.y - pts[i, 1]
            t = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey), 0, 1)
            bx, by = wx - ex * t, wy - ey * t
            d = np.minimum(d, bx * bx + by * by)
            c1 = self.y >= pts[i, 1]
            c2 = self.y < pts[j, 1]
            c3 = ex * wy > ey * wx
            flip = (c1 & c2 & c3) | (~c1 & ~c2 & ~c3)
            s = np.where(flip, -s, s)
            j = i
        return s * np.sqrt(d)

    # --- painting ---
    def fill(self, d, color, alpha=1.0, soft=0.0):
        """Composite a shape over the canvas. color: rgb or (h,w,3) array."""
        if soft > 0:
            a = np.clip(0.5 - d / soft, 0, 1)
            a = a * a * (3 - 2 * a)
        else:
            a = np.clip(0.5 - d, 0, 1)
        a = a * alpha
        col = np.asarray(color, dtype=np.float64)
        if col.ndim == 1:
            col = np.broadcast_to(col, self.rgb.shape)
        out_a = a + self.a * (1 - a)
        safe = np.where(out_a > 1e-6, out_a, 1)
        self.rgb = (col * a[..., None] + self.rgb * (self.a * (1 - a))[..., None]) / safe[..., None]
        self.a = out_a

    def vgrad(self, top, bottom, y0, y1):
        t = np.clip((self.y - y0) / max(1e-6, (y1 - y0)), 0, 1)[..., None]
        return mix(np.asarray(top), np.asarray(bottom), t)

    def image(self):
        arr = np.dstack([self.rgb, self.a])
        return Image.fromarray(np.clip(arr * 255 + 0.5, 0, 255).astype(np.uint8), 'RGBA')

    def save(self, path):
        self.image().save(path)


def U(*ds):
    return np.minimum.reduce(ds)


def I(*ds):
    return np.maximum.reduce(ds)


def S(a, b):
    return np.maximum(a, -b)

