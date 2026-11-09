"""Generate tileable albedo and tangent-space normal PNGs with Python 3."""

import json
from argparse import ArgumentParser
from concurrent.futures import ProcessPoolExecutor
from functools import cache
from math import cos, floor, hypot, pi, sin, sqrt
from pathlib import Path
from struct import pack
from zlib import compress, crc32


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Art/FloorCollection/Textures"
SIZE = 512
TAU = 2 * pi
CHIPS = [(173, 113, 74), (194, 146, 82), (136, 144, 101), (151, 143, 132), (220, 176, 109)]


def rgb(code):
    return tuple(int(code[index:index + 2], 16) for index in (0, 2, 4))


def shade(color, value):
    return tuple(max(0, min(255, round(channel * value))) for channel in color)


def mix(first, second, alpha):
    return tuple(round(a + (b - a) * alpha) for a, b in zip(first, second))


def smooth(value):
    value = max(0, min(1, value))
    return value * value * (3 - 2 * value)


def rand(x, y, salt=0):
    value = (x * 0x27D4EB2D ^ y * 0x165667B1 ^ salt * 0x85EBCA6B) & 0xFFFFFFFF
    value = ((value ^ (value >> 15)) * 0x2C1B3C6D) & 0xFFFFFFFF
    return ((value ^ (value >> 12)) & 65535) / 65535


def rounded_edge(x, y, w, h, radius):
    dx = abs(x - w / 2) - w / 2 + radius
    dy = abs(y - h / 2) - h / 2 + radius
    return radius - (hypot(max(0, dx), max(0, dy)) + min(0, max(dx, dy)))


def grain(u, v, seed):
    warped = v + .044 * sin(TAU * (u * .87 + seed)) + .017 * sin(TAU * (u * 2.1 - v))
    return (.56 * sin(TAU * (warped * 9 + seed)) +
            .28 * sin(TAU * (warped * 19 + seed * 2)) +
            .16 * sin(TAU * (warped * 31 + u * .4)))


class Pattern:
    def __init__(self, style):
        self.number = style["number"]
        self.family = style["family"]
        self.base = rgb(style["color"])
        self.joint = rgb(style.get("joint", style["color"]))
        self.accent = rgb(style.get("accent", style["color"]))
        self.w, self.h = style["width"], style["depth"]
        self.columns, self.rows = style["columns"], style["rows"]
        self.repeat_x, self.repeat_y = self.w * self.columns, self.h * self.rows
        self.gap = style.get("gap", 0)
        self.variation = style["variation"]

    def sample(self, x, y):
        family = self.family
        if family == "stone":
            return self.stones(x, y)
        if family == "hex":
            return self.hexagons(x, y)
        if family == "terrazzo":
            return self.terrazzo(x, y)
        if family in ("diamond", "rubber"):
            return self.tread(x, y)
        if family == "herringbone":
            return self.herringbone(x, y)
        if family == "flat":
            return shade(self.base, 1 + self.cloud(x, y) * .015), 0
        return self.rectangles(x, y)

    def cloud(self, x, y):
        u, v = x / self.repeat_x, y / self.repeat_y
        return (.54 * sin(TAU * (3 * u + 2 * v)) + .30 * cos(TAU * (5 * u - 3 * v)) +
                .16 * sin(TAU * (9 * u - 7 * v)))

    def finish(self, tone, distance, lift, roundness=.001, grout=None):
        shoulder = smooth((distance - self.gap / 2 + .0004) / roundness)
        return mix(grout or self.joint, tone, shoulder), lift * shoulder

    def rectangles(self, x, y):
        family = self.family
        v = y / self.h
        j = floor(v)
        offset = 0
        if family in ("pavers", "cobble", "travertine"):
            offset = .5 * (j & 1)
        if family in ("wood", "deck"):
            offset = (j % 8) * .375
        u = x / self.w + offset
        i = floor(u)
        u -= i
        v -= j
        seed = rand(i % self.columns, j % self.rows, 71)
        jitter = self.variation * (seed - .5)
        lift, bevel, radius = .00045, .0012, .001
        grout = None
        color = self.accent if family == "checker" and (i + j) & 1 else self.base
        cloud = self.cloud(x, y)
        if family == "cobble":
            radius = .015 + seed * .007
            jitter += .059 * cloud
            lift, bevel = .0020, .004
            u += .026 * sin(TAU * (v + seed))
            color = mix(color, (138, 130, 112), seed * .42)
            grout = mix(self.joint, (95, 102, 69), smooth(.32 + cloud * .58))
        elif family == "pavers":
            radius = .004 if self.number == 28 else .007
            lift, bevel = .0015, .003
            jitter += .033 * cloud
        elif family in ("wood", "deck"):
            if family == "deck":
                warped = v + .05 * sin(TAU * (u + seed)) + .02 * sin(TAU * (3 * u - v))
                jitter += .075 * sin(TAU * (12 * warped + seed)) + .032 * sin(TAU * (25 * warped + u))
            else:
                jitter += grain(u, v, seed) * {11:.057, 12:.083, 13:.102}[self.number]
            jitter += .023 * sin(TAU * (v * 2.1 + seed))
            lift = .0014 if family == "deck" else .0006
            if family == "deck":
                u = .5  # Deck boards continue lengthwise without false joints.
        elif family == "endgrain":
            r = hypot((u - .38 - seed * .13) * .88, v - .46 + seed * .09)
            jitter += .087 * cos(TAU * (r * (12 + seed * 3) + .06 * sin(TAU * u)))
        elif family == "brushed":
            jitter += .029 * sin(TAU * (x * 58 - y * 68 + .32 * cloud))
            jitter += .016 * cos(TAU * (x * 23 - y * 31))
        elif family == "travertine":
            veins = v + .024 * cos(TAU * (u * .9 + seed)) + .018 * sin(TAU * (u * 2.3 + seed))
            jitter += .032 * sin(TAU * (veins * (3 + seed * 4) + seed))
            jitter += .017 * sin(TAU * (veins * 12 + seed)) + .026 * cloud
        elif family in ("cork", "speckled"):
            frequency = 95 if family == "cork" else 132
            nx, ny = floor(x / self.repeat_x * frequency), floor(y / self.repeat_y * frequency)
            speck = rand(nx % frequency, ny % frequency, 92)
            if family == "cork":
                jitter += (speck - .5) * .27 + .029 * cloud
            elif speck > .965 or speck < .025:
                jitter += .18 if speck > .965 else -.13
        else:
            jitter += .012 * cloud
        distance = rounded_edge(u * self.w, v * self.h, self.w, self.h, radius)
        return self.finish(shade(color, 1 + jitter), distance, lift, bevel, grout)

    def hexagons(self, x, y):
        step = .03
        pitch = step * sqrt(3) / 2
        nearest = None
        row = floor(y / pitch)
        for j in (row, row + 1):
            offset = (j & 1) / 2
            i = round(x / step - offset)
            dx, dy = abs(x - (i + offset) * step), abs(y - j * pitch)
            distance = min(step / 2 - dx, step / 2 - (.5 * dx + sqrt(3) / 2 * dy))
            if nearest is None or distance > nearest[0]:
                nearest = distance, i, j
        distance, i, j = nearest
        tone = shade(self.base, 1 + (rand(i % self.columns, j % self.rows, 16) - .5) * self.variation)
        return self.finish(tone, distance, .00023, .00055)

    @cache
    def stone_center(self, i, j):
        ix, iy = i % self.columns, j % self.rows
        return ((i + .5 + (rand(ix, iy, 83) - .5) * .86) * self.w,
                (j + .5 + (rand(ix, iy, 113) - .5) * .86) * self.h, rand(ix, iy, 13))

    def stones(self, x, y):
        i, j = floor(x / self.w), floor(y / self.h)
        candidates = []
        for v in range(j - 2, j + 3):
            for u in range(i - 2, i + 3):
                cx, cy, seed = self.stone_center(u, v)
                dx, dy = x - cx, y - cy
                candidates.append((dx * dx + dy * dy, dx, dy, seed))
        distance, dx, dy, seed = min(candidates)
        edge, next_edge = 1, 1
        for other, ox, oy, _ in candidates:
            if (ox, oy) == (dx, dy):
                continue
            bisector = (other - distance) / (2 * hypot(dx - ox, dy - oy))
            if bisector < edge:
                edge, next_edge = bisector, edge
            elif bisector < next_edge:
                next_edge = bisector
        fillet = .024 if self.number == 7 else .017
        edge -= max(0, fillet - (next_edge - edge)) ** 2 / (4 * fillet)
        tone = shade(self.base, 1 + (seed - .5) * self.variation + .037 * self.cloud(x, y))
        return self.finish(tone, edge, .0014, .0035)

    def terrazzo(self, x, y):
        u, v = x / self.w, y / self.h
        i, j = floor(u), floor(v)
        ix, iy = i % self.columns, j % self.rows
        tone = shade(self.base, 1 + .014 * self.cloud(x, y))
        for n in range(2):
            seed = rand(ix, iy, n + 70)
            if n == 1 and seed < .44:
                continue
            cx = .16 + n * .53 + .15 * rand(ix, iy, n + 11)
            cy = .19 + .61 * rand(ix, iy, n + 19)
            dx, dy = abs(u - i - cx), abs(v - j - cy) * (1 + .43 * seed)
            radius = (.15 if n == 0 else .075) * (.55 + seed * .65)
            edge = radius - max(dx, dy, (dx + dy) * .76)
            if edge > -.012:
                color = CHIPS[int(rand(ix, iy, n + 29) * 4.999)]
                tone = mix(tone, color, .83 * smooth((edge + .012) / .023))
        return tone, 0

    def herringbone(self, x, y):
        size = .08
        u, v = (x + y) / (sqrt(2) * size), (y - x) / (sqrt(2) * size)
        i, j = floor(u), floor(v)
        step = (i + j) % 10
        if step < 5:
            length, width = step + u - i, v - j
            ix, iy = i - step, j
        else:
            length, width = step - 5 + v - j, u - i
            ix, iy = i, j - (step - 5)
        seed = rand(ix % 10, iy % 10, 91)
        tint = 1 + self.variation * (seed - .5) + .076 * grain(length / 5, width, seed)
        edge = rounded_edge(length * size, width * size, .4, size, .001)
        return self.finish(shade(self.base, tint), edge, .00065)

    def tread(self, x, y):
        u, v = x / self.w, y / self.h
        i, j = floor(u), floor(v)
        dx, dy = u - i - .5, v - j - .5
        if self.family == "diamond":
            diagonal = -1 if (i + j) & 1 else 1
            along = abs((dx + diagonal * dy) * .7071)
            across = abs((dx - diagonal * dy) * .7071)
            edge = 1 - along / .42 - (across / .16) ** 1.3
            bump = smooth(edge * 2.6)
            tone = shade(self.base, 1 + .25 * bump + .015 * self.cloud(x, y))
            return tone, .0018 * bump
        edge = .33 - hypot(dx, dy)
        bump = smooth(edge * 18)
        return shade(self.base, 1 + .14 * bump + .015 * self.cloud(x, y)), .0021 * bump

    def safety(self, px, py):
        pane = 128
        column, row = px // pane, py // pane
        u, v = ((px % pane + .5 - 4) / 120, (py % pane + .5 - 4) / 120)
        x, y = (column % 4 + u) * self.w / 4, (row + v) * self.h / 4
        tone, height = self.rectangles(x, y)
        if column >= 4 and v > .58:
            phase = (u * 2 - (v - .58) * 2) % 1
            color = mix((225, 177, 63), (60, 62, 58), smooth((phase - .48) / .035))
            color = mix(color, (225, 177, 63), smooth((phase - .9825) / .0175))
            tone = mix(tone, color, smooth((v - .58) / .014) * .96)
        return tone, height


def png(path, width, height, rows):
    def chunk(kind, payload):
        return pack(">I", len(payload)) + kind + payload + pack(">I", crc32(kind + payload))

    data = b"".join(b"\x00" + bytes(row) for row in reversed(rows))
    header = pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", compress(data, 5)) + chunk(b"IEND", b""))


def render(style):
    pattern = Pattern(style)
    atlas = pattern.family == "safety"
    width, height = (1024, 512) if atlas else (SIZE, SIZE)
    pitch_x = pattern.repeat_x / width if not atlas else pattern.w / 480
    pitch_y = pattern.repeat_y / height if not atlas else pattern.h / 480
    albedo, relief = [], []
    for py in range(height):
        colors, elevations = bytearray(), []
        for px in range(width):
            if atlas:
                color, z = pattern.safety(px, py)
            else:
                color, z = pattern.sample((px + .5) * pitch_x, (py + .5) * pitch_y)
            colors.extend(color)
            elevations.append(z)
        albedo.append(colors)
        relief.append(elevations)

    normals = []
    for py in range(height):
        colors = bytearray()
        for px in range(width):
            nx = (relief[py][(px - 1) % width] - relief[py][(px + 1) % width]) / (2 * pitch_x)
            ny = (relief[(py - 1) % height][px] - relief[(py + 1) % height][px]) / (2 * pitch_y)
            scale = 127.5 / sqrt(nx * nx + ny * ny + 1)
            colors.extend(round(127.5 + component * scale) for component in (nx, ny, 1))
        normals.append(colors)
    name = f"Floor_{style['number']:02}_{style['key']}"
    png(OUT / f"{name}_Albedo.png", width, height, albedo)
    png(OUT / f"{name}_Normal.png", width, height, normals)
    Pattern.stone_center.cache_clear()
    return name


if __name__ == "__main__":
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("numbers", type=int, nargs="*", help="Regenerate only the given style numbers.")
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    styles = json.loads(Path(__file__).with_name("catalog.json").read_text(encoding="utf-8"))["styles"]
    if args.numbers:
        selected = set(args.numbers)
        if not selected <= {style["number"] for style in styles}:
            parser.error("Unknown floor style.")
        styles = [style for style in styles if style["number"] in selected]
    with ProcessPoolExecutor(max_workers=6) as executor:
        for name in executor.map(render, styles):
            print(name, flush=True)
