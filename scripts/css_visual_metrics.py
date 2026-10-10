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



def align_raster_canvases(chrome, factspdf):
    """Pad a <=1px paper-rounding discrepancy; never translate or rescale ink.

    Chrome quantizes the A4 media-box width to PDF units (e.g. 594.96pt)
    while FactsPDF uses 595.28pt. At 120 DPI this can produce a single
    column difference. Padding at the original top-left origin makes all
    ink coordinates comparable without any geometric registration.
    """
    dimensions = {
        "chrome_original_pixels": list(chrome.size),
        "factspdf_original_pixels": list(factspdf.size),
    }
    if abs(chrome.width - factspdf.width) > 1 or abs(chrome.height - factspdf.height) > 1:
        raise ValueError("Page geometry differs by >1 pixel; cannot mask a real size mismatch.")
    width, height = max(chrome.width, factspdf.width), max(chrome.height, factspdf.height)
    def canvas(image):
        rendered = Image.new("RGB", (width, height), "white")
        rendered.paste(_rgb(image), (0, 0))
        return rendered
    dimensions["common_canvas_pixels"] = [width, height]
    dimensions["operation"] = "white origin-padding only; no rescale or registration"
    return canvas(chrome), canvas(factspdf), dimensions


def compare(chrome, factspdf):
    """Unregistered, exact-page-coordinate metrics; NEVER resize for scoring."""
    if chrome.size != factspdf.size:
        raise ValueError("Page raster dimensions differ; rescaling would disguise geometry errors.")
    left, right = _rgb(chrome), _rgb(factspdf)
    max_channel, delta = _max_channel_difference(left, right)
    count = left.width * left.height
    changed = max_channel.point(lambda value: 255 if value > 16 else 0).histogram()[255]
    bounds_left, bounds_right = _ink_bbox(left), _ink_bbox(right)
    if bounds_left is None and bounds_right is None:
        ink_union = None
    elif bounds_left is None or bounds_right is None:
        ink_union = bounds_left or bounds_right
    else:
        ink_union = [
            min(bounds_left[0], bounds_right[0]), min(bounds_left[1], bounds_right[1]),
            max(bounds_left[2], bounds_right[2]), max(bounds_left[3], bounds_right[3])
        ]
    if ink_union is None:
        ink_fraction, ink_mean = None, None
    else:
        box = tuple(ink_union)
        area = (box[2] - box[0]) * (box[3] - box[1])
        ink_mask = max_channel.crop(box).point(lambda value: 255 if value > 16 else 0)
        ink_fraction = round(ink_mask.histogram()[255] / area, 7)
        ink_mean = round(sum(ImageStat.Stat(delta.crop(box)).mean) / 3, 5)
    return {
        "width_px": left.width,
        "height_px": left.height,
        "pixel_count": count,
        "pixels_changed_over_16": changed,
        "change_fraction_over_16": round(changed / count, 7),
        "rgb_mean_abs_error": round(sum(ImageStat.Stat(delta).mean) / 3, 5),
        "max_channel_error": max_channel.getextrema()[1],
        "chrome_ink_bbox": bounds_left,
        "factspdf_ink_bbox": bounds_right,
        "ink_union_bbox": ink_union,
        "ink_union_change_fraction_over_16": ink_fraction,
        "ink_union_rgb_mean_abs_error": ink_mean,
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
    leftbox, rightbox = _ink_bbox(left), _ink_bbox(right)
    available = [bbox for bbox in (leftbox, rightbox) if bbox is not None]
    if available:
        box = (max(0, min(q[0] for q in available) - 18),
               max(0, min(q[1] for q in available) - 18),
               min(left.width, max(q[2] for q in available) + 18),
               min(left.height, max(q[3] for q in available) + 18))
    else:
        box = (0, 0, left.width, left.height)
    details = (left.crop(box), right.crop(box), heatmap.crop(box))
    cw, ch = details[0].size
    detail = Image.new("RGB", (cw * 3 + gap * 4, ch + top + 2 * gap), "white")
    detail_draw = ImageDraw.Draw(detail)
    for i, (caption, crop) in enumerate(zip(
        ("CHROME DETAIL", "FACTSPDF DETAIL", "DIFFERENCE (INK REGION)"), details
    )):
        x = gap + i * (cw + gap)
        detail_draw.text((x, 7), caption, fill="#202020")
        detail.paste(crop, (x, top))
    images = {
        "chrome": left,
        "factspdf": right,
        "overlay": overlay,
        "difference": heatmap,
        "comparison": sheet,
        "detail": detail,
    }
    files = {}
    for key, image in images.items():
        path = directory / (prefix + "-" + key + ".png")
        image.save(path, optimize=True)
        files[key] = str(path)
    return files
