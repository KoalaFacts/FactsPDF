"""Deterministic, repository-owned PNG fixtures; no renderer code is imported."""
import argparse
import json
from pathlib import Path
import struct
import zlib


def chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))


def png(width, height, color_type, depth, rows, *, palette=None, transparency=None):
    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[color_type]
    stride = max(1, (channels * depth + 7) // 8)
    filtered = bytearray()
    previous = bytes(len(rows[0]))
    for y, row in enumerate(rows):
        method = y % 5
        filtered.append(method)
        for x, value in enumerate(row):
            left = row[x - stride] if x >= stride else 0
            above = previous[x]
            upper_left = previous[x - stride] if x >= stride else 0
            if method == 0:
                predictor = 0
            elif method == 1:
                predictor = left
            elif method == 2:
                predictor = above
            elif method == 3:
                predictor = (left + above) // 2
            else:
                p = left + above - upper_left
                distances = [abs(p - left), abs(p - above), abs(p - upper_left)]
                predictor = [left, above, upper_left][distances.index(min(distances))]
            filtered.append((value - predictor) & 255)
        previous = row
    encoded = zlib.compress(filtered, 9)
    result = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, depth, color_type, 0, 0, 0))
    if palette is not None:
        result += chunk(b'PLTE', palette)
    if transparency is not None:
        result += chunk(b'tRNS', transparency)
    # Exercise legal split boundaries, including a split zlib header/trailer.
    for part in (encoded[:1], encoded[1:-2], encoded[-2:]):
        result += chunk(b'IDAT', part)
    return result + chunk(b'IEND', b'')


def pack(values, depth):
    result = bytearray((len(values) * depth + 7) // 8)
    for i, value in enumerate(values):
        result[i * depth // 8] |= value << (8 - depth - i * depth % 8)
    return bytes(result)


def create(directory):
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    width, height = 33, 17
    quadrant = lambda x, y: (0 if y < 8 else 2) + (0 if x < 16 else 1)
    definitions = [
        ('rgba', 6, 8, [(240, 20, 40, 255), (20, 220, 60, 128), (30, 50, 230, 0), (40, 70, 200, 255)], None, None),
        ('rgb', 2, 8, [(240, 20, 40), (20, 220, 60), (30, 50, 230), (40, 70, 200)], None, None),
        ('gray', 0, 8, [(0,), (85,), (170,), (255,)], None, None),
        ('gray-alpha', 4, 8, [(0, 255), (85, 128), (170, 0), (255, 255)], None, None),
        ('indexed', 3, 2, [(0,), (1,), (2,), (3,)], bytes([240, 20, 40, 20, 220, 60, 30, 50, 230, 40, 70, 200]), bytes([255, 128, 0])),
        ('gray-packed', 0, 2, [(0,), (1,), (2,), (3,)], None, None),
        ('gray-key', 0, 2, [(0,), (1,), (2,), (3,)], None, struct.pack('>H', 1)),
        ('rgb-key', 2, 8, [(240, 20, 40), (20, 220, 60), (30, 50, 230), (40, 70, 200)], None, struct.pack('>HHH', 20, 220, 60)),
    ]
    cases = []
    for name, kind, depth, colors, palette, transparency in definitions:
        rows = []
        rgba = bytearray()
        for y in range(height):
            values = []
            for x in range(width):
                sample = colors[quadrant(x, y)]
                values.extend(sample)
                if kind == 3:
                    index = sample[0]
                    rgb = palette[index * 3:index * 3 + 3]
                    alpha = transparency[index] if index < len(transparency) else 255
                elif kind in (0, 4):
                    value = sample[0] * 255 // ((1 << depth) - 1)
                    rgb = bytes([value] * 3)
                    alpha = sample[1] if kind == 4 else (0 if transparency and sample[0] == 1 else 255)
                else:
                    rgb = bytes(sample[:3])
                    alpha = sample[3] if kind == 6 else (0 if transparency and sample == colors[1] else 255)
                rgba.extend(rgb)
                rgba.append(alpha)
            rows.append(bytes(values) if depth == 8 else pack(values, depth))
        data = png(width, height, kind, depth, rows, palette=palette, transparency=transparency)
        filename = name + '.png'
        (directory / filename).write_bytes(data)
        cases.append({'name': name, 'file': filename, 'width': width, 'height': height,
                      'rgba': list(rgba), 'gray': kind in (0, 4),
                      'alpha': kind in (4, 6) or transparency is not None})

    # Small synthetic mark, deliberately unrelated to any third-party branding.
    logo_width, logo_height = 144, 48
    rows = []
    for y in range(logo_height):
        row = bytearray()
        for x in range(logo_width):
            if 8 <= x < 40 and 8 <= y < 40:
                pixel = (15, 118, 110, 255)
            elif 48 <= x < 80 and 16 <= y < 40:
                pixel = (14, 165, 166, 255)
            elif 88 <= x < 120 and 24 <= y < 40:
                pixel = (14, 165, 166, 128)
            else:
                pixel = (255, 255, 255, 0)
            row.extend(pixel)
        rows.append(bytes(row))
    (directory / 'brand-mark.png').write_bytes(png(logo_width, logo_height, 6, 8, rows))
    manifest = {'source': 'authored synthetic samples', 'cases': cases}
    (directory / 'manifest.json').write_text(json.dumps(manifest, separators=(',', ':')) + '\n')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    manifest = create(args.output)
    print(f'Created {len(manifest["cases"])} PNG cases and brand-mark.png in {args.output}')
