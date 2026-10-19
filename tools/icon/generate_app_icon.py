"""Gera o ícone do app MyRemoteControll (monitor com o símbolo de energia e ondas de sinal).

Saída em app/assets/icon/:
  app_icon.png             ícone completo (iOS e Android antigo), 1024 × 1024, sem transparência
  app_icon_foreground.png  camada da frente do ícone adaptativo do Android (desenho no centro, fundo transparente)
  app_icon_background.png  camada de fundo do ícone adaptativo (degradê)
  app_icon_monochrome.png  ícone temático do Android 13+ (silhueta branca)

Uso (na raiz do repositório): python tools/icon/generate_app_icon.py
Depois: cd app && dart run flutter_launcher_icons
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

SIZE = 1024
SCALE = 4  # desenha em 4096 px e reduz: bordas suaves
CANVAS = SIZE * SCALE

NAVY_TOP = (11, 18, 34)
NAVY_BOTTOM = (26, 43, 82)
BLUE = (61, 123, 255)  # azul elétrico do app (#3D7BFF)
BLUE_LIGHT = (124, 168, 255)
WHITE = (236, 242, 255)

OUT = Path(__file__).resolve().parents[2] / "app" / "assets" / "icon"


def s(value: float) -> int:
    return round(value * SCALE)


def background() -> Image.Image:
    """Degradê diagonal azul-marinho com um brilho azul suave atrás do desenho."""
    image = Image.new("RGB", (CANVAS, CANVAS))
    pixels = image.load()
    for y in range(0, CANVAS, SCALE):
        for x in range(0, CANVAS, SCALE):
            t = (x + y) / (2 * CANVAS)
            color = tuple(round(a + (b - a) * t) for a, b in zip(NAVY_TOP, NAVY_BOTTOM))
            for dy in range(SCALE):
                for dx in range(SCALE):
                    pixels[x + dx, y + dy] = color

    glow = Image.new("L", (CANVAS, CANVAS), 0)
    ImageDraw.Draw(glow).ellipse([s(232), s(212), s(792), s(772)], fill=110)
    glow = glow.filter(ImageFilter.GaussianBlur(s(110)))
    blue = Image.new("RGB", (CANVAS, CANVAS), BLUE)
    return Image.composite(blue, image, glow)


def glyph(scale: float, mono: bool = False) -> Image.Image:
    """Monitor + base, símbolo de energia na tela e duas ondas de sinal acima, centrado em 1024 × 1024."""
    layer = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    c = SIZE / 2

    def p(value: float) -> int:
        """Coordenada relativa ao centro, já na escala do desenho."""
        return s(c + (value - c) * scale)

    frame = WHITE if mono else BLUE
    accent = WHITE if mono else WHITE
    wave = WHITE if mono else BLUE_LIGHT
    stroke = s(40 * scale)

    # Tela: retângulo arredondado com borda grossa.
    draw.rounded_rectangle([p(262), p(330), p(762), p(660)], radius=s(54 * scale), outline=frame, width=stroke)
    # Pescoço e base.
    draw.rounded_rectangle([p(486), p(660), p(538), p(724)], radius=s(10 * scale), fill=frame)
    draw.rounded_rectangle([p(392), p(718), p(632), p(756)], radius=s(19 * scale), fill=frame)

    # Símbolo de energia: arco aberto em cima + traço vertical.
    radius = 92 * scale
    center_y = 505
    box = [s(c - radius), s(c + (center_y - c) * scale - radius), s(c + radius), s(c + (center_y - c) * scale + radius)]
    draw.arc(box, start=-58, end=238, fill=accent, width=s(30 * scale))
    draw.rounded_rectangle([p(497), p(398), p(527), p(505)], radius=s(15 * scale), fill=accent)

    # Ondas de sinal acima da tela (controle à distância).
    for index, r in enumerate((70, 122)):
        arc_center_y = c + (300 - c) * scale
        bounds = [s(c - r * scale), s(arc_center_y - r * scale), s(c + r * scale), s(arc_center_y + r * scale)]
        draw.arc(bounds, start=215, end=325, fill=wave, width=s((30 - index * 4) * scale))

    return layer


def downscale(image: Image.Image) -> Image.Image:
    return image.resize((SIZE, SIZE), Image.Resampling.LANCZOS)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    back = background()

    # Ícone completo: o desenho ocupa ~78% (o iOS aplica a própria máscara de cantos).
    full = back.convert("RGBA")
    full.alpha_composite(glyph(1.0))
    downscale(full).convert("RGB").save(OUT / "app_icon.png")

    # Adaptativo: o desenho fica dentro da zona segura (~61% central) da camada de 108 dp.
    downscale(glyph(0.72)).save(OUT / "app_icon_foreground.png")
    downscale(back).save(OUT / "app_icon_background.png")
    downscale(glyph(0.72, mono=True)).save(OUT / "app_icon_monochrome.png")
    print(f"ícones gerados em {OUT}")


if __name__ == "__main__":
    main()
