"""Deterministic pixel metrics and public preview generation for PDF visual checks.

This module reports differences; it does not claim browser-equivalent layout
or independently establish an acceptable perceptual tolerance.
"""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageOps, ImageStat


def _rgb(image):
    return image.convert("RGB")


def _ink_bbox(image):
    white = Image.new("RGB", image.size, (255, 255, 255))
    ink = ImageChops.difference(image, white).convert("L")
    mask = ink.point(lambda value: 255 if value > 32 else 0)
    box = mask.getbbox()
    return list(box) if box is not None else None


def _max_channel_difference(first, second):
    delta = ImageChops.difference(first, second)
    channels = delta.split()
    return ImageChops.lighter(ImageChops.lighter(channels[0], channels[1]), channels[2]), delta


def compare(chrome, factspdf):
    """Unregistered, exact-page-coordinate metrics; NEVER resize for scoring."""
    if chrome.size != factspdf.size:
        raise ValueError("Page raster dimensions differ; rescaling would disguise geometry errors.")
    left, right = _rgb(chrome), _rgb(factspdf)
    max_channel, delta = _max_channel_difference(left, right)
    count = left.width * left.height
    changed = max_channel.point(lambda value: 255 if value > 16 else 0).histogram()[255]
    return {
        "width_px": left.width,
        "height_px": left.height,
        "pixel_count": count,
        "pixels_changed_over_16": changed,
        "change_fraction_over_16": round(changed / count, 7),
        "rgb_mean_abs_error": round(sum(ImageStat.Stat(delta).mean) / 3, 5),
        "max_channel_error": max_channel.getextrema()[1],
        "chrome_ink_bbox": _ink_bbox(left),
        "factspdf_ink_bbox": _ink_bbox(right),
    }


def save_visuals(chrome, factspdf, directory, prefix):
    """Save both originals and three independent diagnostic views.

    Images are public synthetic fixture outputs, never user documents/fonts.
    """
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    left, right = _rgb(chrome), _rgb(factspdf)
    if left.size != right.size:
        raise ValueError("Cannot overlay pages with different raster geometry.")
    max_channel, _ = _max_channel_difference(left, right)
    overlay = Image.blend(left, right, 0.5)
    heatmap = ImageOps.colorize(max_channel, black="white", white="#e10000")
    # A smaller side-by-side overview keeps a two-page fixture practical to inspect.
    preview_width = min(550, left.width)
    preview_height = max(1, round(left.height * preview_width / left.width))
    down = Image.Resampling.LANCZOS
    mini_left = left.resize((preview_width, preview_height), down)
    mini_right = right.resize((preview_width, preview_height), down)
    mini_diff = heatmap.resize((preview_width, preview_height), down)
    gap = 12
    top = 30
    sheet = Image.new("RGB", (preview_width * 3 + gap * 4, preview_height + top + 2 * gap), "white")
    painter = ImageDraw.Draw(sheet)
    for i, (caption, thumb) in enumerate((
        ("CHROME REFERENCE", mini_left),
        ("FACTSPDF NATIVE", mini_right),
        ("PIXEL DIFFERENCE", mini_diff),
    )):
        x = gap + i * (preview_width + gap)
        painter.text((x, 7), caption, fill="#202020")
        sheet.paste(thumb, (x, top))
    images = {
        "chrome": left,
        "factspdf": right,
        "overlay": overlay,
        "difference": heatmap,
        "comparison": sheet,
    }
    files = {}
    for key, image in images.items():
        path = directory / (prefix + "-" + key + ".png")
        image.save(path, optimize=True)
        files[key] = str(path)
    return files
