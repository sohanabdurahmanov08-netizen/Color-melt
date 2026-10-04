"""Builds the packed surface texture for the ColorMelt/ChannelFillURP shader.

One tileable RGBA texture, so the shader needs only two cheap fetches:
  R, G  ripple normal (tangent-space xy, 0.5 = flat)
  B     foam: round bubbles of random size and height over a softer froth.
        The shader thresholds it, so low bubbles pop first as foam settles.
  A     smooth low-frequency noise for streaks and marbling.

The data is linear (the importer must have sRGB off). Rerun after edits:
    python Tools/Liquid/make_liquid_surface.py
"""
import os

import numpy as np
from PIL import Image

SIZE = 256
SEED = 7
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Shaders", "LiquidSurface.png")

rng = np.random.default_rng(SEED)


def spectral_noise(size, k_low, k_high, power):
    """Tileable noise: white noise band-passed in the frequency domain."""
    white = rng.standard_normal((size, size))
    kx = np.fft.fftfreq(size) * size
    k = np.sqrt(kx[None, :] ** 2 + kx[:, None] ** 2)
    band = np.exp(-((np.maximum(k_low - k, 0)) ** 2) / 2.0) * np.exp(-((np.maximum(k - k_high, 0)) ** 2) / 8.0)
    band /= np.maximum(k, 1.0) ** power
    band[0, 0] = 0.0
    field = np.real(np.fft.ifft2(np.fft.fft2(white) * band))
    field -= field.min()
    return field / field.max()


def ripple_normal(height, strength):
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5 * SIZE
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5 * SIZE
    nx, ny = -dx * strength, -dy * strength
    length = np.sqrt(nx * nx + ny * ny + 1.0)
    return nx / length, ny / length


def bubbles(size, count):
    """Domes of random radius and peak, wrapped around the tile edges."""
    field = np.zeros((size, size))
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float64)
    radii = np.concatenate([
        rng.uniform(0.010, 0.022, int(count * 0.7)),
        rng.uniform(0.022, 0.040, count - int(count * 0.7)),
    ]) * size
    for radius in radii:
        cx, cy = rng.uniform(0, size, 2)
        peak = rng.uniform(0.45, 1.0)
        dx = np.abs(xs - cx)
        dy = np.abs(ys - cy)
        dx = np.minimum(dx, size - dx)
        dy = np.minimum(dy, size - dy)
        u = np.sqrt(dx * dx + dy * dy) / radius
        dome = np.sqrt(np.clip(1.0 - u * u, 0.0, 1.0)) * peak
        field = np.maximum(field, dome)
    return field


def main():
    height = spectral_noise(SIZE, 2.0, 5.0, 0.8) * 0.8 + spectral_noise(SIZE, 7.0, 14.0, 0.4) * 0.2
    nx, ny = ripple_normal(height, 0.06)
    scale = 0.5 / max(np.abs(nx).max(), np.abs(ny).max())
    r = 0.5 + nx * scale
    g = 0.5 + ny * scale

    froth = spectral_noise(SIZE, 6.0, 12.0, 0.2)
    b = np.maximum(bubbles(SIZE, 900), froth * 0.6)
    a = spectral_noise(SIZE, 1.0, 4.0, 1.0)

    rgba = np.stack([r, g, b, a], axis=-1)
    image = Image.fromarray(np.clip(rgba * 255.0 + 0.5, 0, 255).astype(np.uint8), "RGBA")
    image.save(os.path.normpath(OUT))
    print("wrote", os.path.normpath(OUT), "normal scale", round(scale, 3))


if __name__ == "__main__":
    main()
