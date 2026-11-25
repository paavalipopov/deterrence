"""Render settings and meter equations with a common canvas and baseline."""

import os
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


OUTPUT = Path(__file__).resolve().parents[1] / "Textures/UI/Deterrence/Equations"
FONTS = Path(os.environ["WINDIR"]) / "Fonts"
SCALE = 8
OUTPUT_SCALE = 4  # Neutral coverage master; the game integrates it at the final pixel size.
HEIGHT = 32
BASELINE = 22


def font(size, italic=False):
    return ImageFont.truetype(FONTS / ("segoeuii.ttf" if italic else "segoeui.ttf"), size * SCALE)


NORMAL = font(15)
VARIABLE = font(15, italic=True)
FRACTION = font(12, italic=True)
POWER = font(10)
POWER_VARIABLE = font(10, italic=True)
PARENTHESIS = font(27)


class Equation:
    def __init__(self):
        self.mask = Image.new("L", (700 * SCALE, HEIGHT * SCALE))
        self.draw = ImageDraw.Draw(self.mask)
        self.x = 0

    def text(self, value, style=NORMAL, baseline=BASELINE):
        self.draw.text((self.x, baseline * SCALE), value, fill=255, font=style, anchor="ls")
        self.x += self.draw.textlength(value, font=style)

    def fraction(self, numerator, denominator):
        width = max(self.draw.textlength(part, font=FRACTION) for part in (numerator, denominator)) + 4 * SCALE
        center = self.x + width / 2
        for part, baseline in ((numerator, 13), (denominator, 28)):
            self.draw.text((center, baseline * SCALE), part, fill=255, font=FRACTION, anchor="ms")
        self.draw.line((self.x, 16 * SCALE, self.x + width, 16 * SCALE), fill=255, width=SCALE)
        self.x += width

    def radical(self, draw_content):
        left = self.x
        self.x += 14 * SCALE
        draw_content()
        right = self.x + 2 * SCALE
        points = [(left + x * SCALE, y * SCALE) for x, y in ((0, 19), (3, 18), (7, 29), (12, 2))]
        points.append((right, 2 * SCALE))
        self.draw.line(points, fill=255, width=SCALE, joint="curve")
        self.x = right + 2 * SCALE

    def save(self, name):
        _, _, right, _ = self.mask.getbbox()
        if right == self.mask.width:
            raise ValueError(f"{name} exceeds the equation canvas")
        stride = SCALE // OUTPUT_SCALE
        width = (right + stride - 1) // stride + 1
        alpha = self.mask.crop((0, 0, width * stride, HEIGHT * SCALE)).resize(
            (width, HEIGHT * OUTPUT_SCALE), Image.Resampling.BOX)
        image = Image.new("RGBA", alpha.size, (255, 255, 255, 0))
        image.putalpha(alpha)
        image.save(OUTPUT / f"{name}.png")


def growth(square_root):
    equation = Equation()
    equation.text("G(E)", VARIABLE)
    equation.text(" = ")
    equation.text("K", VARIABLE)
    equation.text(" × ")
    if not square_root:
        equation.text("ln")
    equation.text("(", PARENTHESIS, baseline=24)

    def inside_root():
        equation.text("1 + ")
        equation.fraction("2E" if square_root else "E", "K")

    if square_root:
        equation.radical(inside_root)
        equation.text(" − 1")
    else:
        inside_root()
    equation.text(")", PARENTHESIS, baseline=24)
    equation.save("GrowthSquareRoot" if square_root else "GrowthLogarithmic")


def fade(linear):
    equation = Equation()
    equation.text("Balance = " if linear else "Remaining = ")
    equation.text("B" if linear else "B₀", VARIABLE)
    equation.text(" × (1 − ")
    if not linear:
        equation.text("3")
    equation.text("u", VARIABLE)
    if not linear:
        equation.text("2", POWER, baseline=15)
        equation.text(" + 2")
        equation.text("u", VARIABLE)
        equation.text("3", POWER, baseline=15)
    equation.text(")")
    equation.save("FadeLinear" if linear else "FadeCubic")


def duration(scaled):
    equation = Equation()
    equation.text("T", VARIABLE)
    equation.text(" = ")
    equation.text("D", VARIABLE)
    if scaled:
        equation.text(" × ")
        equation.text("(", PARENTHESIS, baseline=24)
        equation.fraction("B", "A")
        equation.text(")", PARENTHESIS, baseline=24)
        equation.text("p", POWER_VARIABLE, baseline=12)
    equation.save("DurationScaled" if scaled else "DurationFixed")


def curve_controls():
    equation = Equation()
    equation.text("Daily gain  ")
    equation.fraction("dH", "dt")
    equation.text(" = R ", VARIABLE)
    equation.text("(", PARENTHESIS, baseline=24)
    equation.text("1 + ")
    equation.fraction("t", "pT")
    equation.text(")", PARENTHESIS, baseline=24)
    equation.text("−p", POWER_VARIABLE, baseline=12)
    equation.save("SharedGrowthRate")

    equation = Equation()
    equation.text("T = ", VARIABLE)
    equation.fraction("K", "R")
    equation.save("ExposureTimescale")

    equation = Equation()
    equation.text("T = D ", VARIABLE)
    equation.radical(lambda: equation.fraction("R", "2,000"))
    equation.save("RateTimescale")

    equation = Equation()
    equation.text("Daily gain  ")
    equation.fraction("dH", "dt")
    equation.text(" = R", VARIABLE)
    equation.save("GrowthLinearRate")

    equation = Equation()
    equation.text("A = ", VARIABLE)
    equation.text("∫", PARENTHESIS, baseline=24)
    equation.text(" H(t) dt", VARIABLE)
    equation.save("HistoryArea")

    for name, amount, reference in (("FadeDuration", "B₀", "ref. balance"), ("HistoryDuration", "A", "ref. area")):
        equation = Equation()
        equation.text("F = L ", VARIABLE)
        equation.text("(", PARENTHESIS, baseline=24)
        equation.fraction(amount, reference)
        equation.text(")", PARENTHESIS, baseline=24)
        equation.text("q", POWER_VARIABLE, baseline=12)
        equation.save(name)


def factor(equation, name):
    equation.text("k", VARIABLE)
    equation.text(name, POWER, baseline=26)


def threat(deterrence, biome, island):
    equation = Equation()
    equation.text("Threat = ")
    equation.text("max(0, Base − Deterrence)" if deterrence else "Base")
    for enabled, name in ((biome, "biome"), (island, "island")):
        if enabled:
            equation.text(" × ")
            factor(equation, name)
    equation.save("Threat" + ("Deterrence" if deterrence else "") +
                  ("Biome" if biome else "") + ("Island" if island else ""))


def terrain_and_colony():
    for name in ("biome", "island"):
        equation = Equation()
        factor(equation, name)
        equation.text(" =")
        equation.save("Factor" + name.title())

    equation = Equation()
    factor(equation, "biome")
    equation.text(" = ")
    equation.text("w", VARIABLE)
    equation.text("s", POWER_VARIABLE, baseline=14)
    equation.save("TerrainBiome")

    equation = Equation()
    factor(equation, "island")
    equation.text(" = ")
    equation.text("{", PARENTHESIS, baseline=24)
    left = equation.x
    equation.text("m   on a qualifying island", font(12), baseline=13)
    equation.x = left
    equation.text("1    elsewhere", font(12), baseline=28)
    equation.save("TerrainIsland")

    equation = Equation()
    equation.text("Colony factor = ")
    equation.text("(", PARENTHESIS, baseline=24)
    equation.fraction("colonists", "reference size")
    equation.text(")", PARENTHESIS, baseline=24)
    equation.text("p", POWER_VARIABLE, baseline=12)
    equation.save("ColonyFactor")


if __name__ == "__main__":
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for option in (False, True):
        growth(option)
        fade(option)
        duration(option)
        for biome in (False, True):
            for island in (False, True):
                threat(option, biome, island)
    terrain_and_colony()
    curve_controls()
    equation = Equation()
    equation.text("u", VARIABLE)
    equation.text(" = ")
    equation.fraction("elapsed days", "F")
    equation.save("FadeProgress")
    for name, difference in (("HeatExposure", "outdoor °C − threshold °C"),
                             ("ColdExposure", "threshold °C − outdoor °C")):
        equation = Equation()
        equation.text("Exposure = max")
        equation.text("(", PARENTHESIS, baseline=24)
        equation.text("0, ")
        equation.fraction(difference, "10 °C")
        equation.text(")", PARENTHESIS, baseline=24)
        equation.save(name)
    for name, formula in {
        "Body": "Body = combat power × weight × colony factor",
        "Death": "Gain = combat power × weight × colony factor",
        "Gear": "Gain = (power × weight + gear × share) × colony factor",
        "Act": "Gain = amount × colony factor",
        "Daily": "Daily rate = rate × exposure × colony factor",
        "Stock": "Deterrence = stock value × multiplier",
        "Surface": "Surface contribution = counted value × multiplier",
    }.items():
        equation = Equation()
        equation.text(formula)
        equation.save(name)
