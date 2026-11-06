"""
Marca YUNO: gera o símbolo (Y-atleta), o app icon, a logo horizontal e o .ico.
Saída: Yuno/brand/  e  src/Yuno/UI/Assets/
Uso: python gerar_marca.py   (precisa de Pillow)
"""
import os, math
from PIL import Image, ImageDraw, ImageFont, ImageFilter

AQUI = os.path.dirname(os.path.abspath(__file__))
BRAND = os.path.normpath(os.path.join(AQUI, "..", "brand"))
ASSETS = os.path.normpath(os.path.join(AQUI, "..", "src", "Yuno", "UI", "Assets"))
os.makedirs(BRAND, exist_ok=True); os.makedirs(ASSETS, exist_ok=True)

# Paleta (inspirada no template: verde neon -> verde floresta, fundo grafite)
VERDE_CLARO = (124, 243, 166)   # #7CF3A6
VERDE       = (46, 204, 113)    # #2ECC71  (cor principal)
VERDE_FUNDO = (14, 110, 74)     # #0E6E4A
VERDE_NOITE = (8, 52, 40)       # #083428
GRAFITE     = (30, 31, 33)      # #1E1F21
BRANCO      = (255, 255, 255)

def grad_tile(S):
    """Quadrado arredondado com degradê diagonal + brilho no canto (como o template)."""
    base = Image.new("RGBA", (S, S))
    px = base.load()
    for y in range(S):
        for x in range(S):
            t = (x * 0.55 + y * 0.45) / S                    # diagonal
            if t < 0.45:
                a, b, k = VERDE_CLARO, VERDE, t / 0.45
            elif t < 0.8:
                a, b, k = VERDE, VERDE_FUNDO, (t - 0.45) / 0.35
            else:
                a, b, k = VERDE_FUNDO, VERDE_NOITE, (t - 0.8) / 0.2
            px[x, y] = tuple(int(a[i] + (b[i] - a[i]) * k) for i in range(3)) + (255,)
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((-S * 0.25, -S * 0.3, S * 0.6, S * 0.45), fill=(190, 255, 210, 110))
    base = Image.alpha_composite(base, glow.filter(ImageFilter.GaussianBlur(S * 0.12)))
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, S - 1, S - 1), radius=int(S * 0.24), fill=255)
    out = Image.new("RGBA", (S, S), (0, 0, 0, 0)); out.paste(base, (0, 0), mask)
    return out

def stroke(d, pts, w, cor):
    # traço suave: carimba círculos bem próximos ao longo do caminho (sem "dentes" nas curvas)
    r = w / 2
    dens = []
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        n = max(1, int(math.hypot(x1 - x0, y1 - y0) / max(1.0, r * 0.15)))
        dens += [(x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n) for i in range(n)]
    dens.append(pts[-1])
    for x, y in dens:
        d.ellipse((x - r, y - r, x + r, y + r), fill=cor)

def quad(p0, p1, p2, n=40):
    return [((1-t)**2*p0[0] + 2*(1-t)*t*p1[0] + t*t*p2[0], (1-t)**2*p0[1] + 2*(1-t)*t*p1[1] + t*t*p2[1])
            for t in (i / n for i in range(n + 1))]

def simbolo(d, S, ox, oy, esc, cor):
    """Y-atleta: dois braços erguidos (gola em V) + tronco + cabeça. Coordenadas numa caixa 100x100."""
    P = lambda x, y: (ox + x * esc, oy + y * esc)
    w = 12.5 * esc
    stroke(d, quad(P(22, 30), P(30, 50), P(50, 58)), w, cor)   # braço esquerdo
    stroke(d, quad(P(78, 30), P(70, 50), P(50, 58)), w, cor)   # braço direito
    stroke(d, [P(50, 58), P(50, 84)], w, cor)                  # tronco
    r = 8.5 * esc
    cx, cy = P(50, 30)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=cor)      # cabeça

def app_icon(S):
    im = grad_tile(S)
    simbolo(ImageDraw.Draw(im), S, 0, 0, S / 100, BRANCO)
    return im

def simbolo_solto(S, cor):
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    simbolo(ImageDraw.Draw(im), S, 0, 0, S / 100, cor)
    return im

def fonte(px):
    for nome in ("bahnschrift.ttf", "seguibl.ttf", "segoeuib.ttf", "arialbd.ttf"):
        try:
            f = ImageFont.truetype(os.path.join(os.environ.get("WINDIR", "C:/Windows"), "Fonts", nome), px)
            try: f.set_variation_by_name("Bold")
            except Exception: pass
            return f
        except Exception:
            continue
    return ImageFont.load_default()

def logo_horizontal(cor_texto, H=240):
    ic = app_icon(H)
    f = fonte(int(H * 0.62))
    larg = int(ImageDraw.Draw(Image.new("RGBA", (1, 1))).textlength("YUNO", font=f))
    W = H + int(H * 0.28) + larg + int(H * 0.1)
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    im.alpha_composite(ic, (0, 0))
    d = ImageDraw.Draw(im)
    x = H + int(H * 0.28)
    d.text((x, H * 0.5), "YUNO", font=f, fill=cor_texto, anchor="lm")
    return im

# --- saídas ---------------------------------------------------------------
grande = app_icon(1024)
grande.save(os.path.join(BRAND, "yuno-icone-1024.png"))
for s in (512, 256, 128, 64, 32):
    grande.resize((s, s), Image.LANCZOS).save(os.path.join(BRAND, f"yuno-icone-{s}.png"))
grande.save(os.path.join(BRAND, "yuno.ico"), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

simbolo_solto(1024, VERDE).save(os.path.join(BRAND, "yuno-simbolo-verde.png"))
simbolo_solto(1024, BRANCO).save(os.path.join(BRAND, "yuno-simbolo-branco.png"))
logo_horizontal(BRANCO).save(os.path.join(BRAND, "yuno-logo-fundo-escuro.png"))
logo_horizontal(GRAFITE).save(os.path.join(BRAND, "yuno-logo-fundo-claro.png"))

# para dentro do sistema (WPF) e do instalador
grande.resize((128, 128), Image.LANCZOS).save(os.path.join(ASSETS, "yuno-icone.png"))
grande.save(os.path.join(ASSETS, "yuno.ico"), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

# SVG do símbolo (vetor, para gráfica/site)
svg = f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="512" height="512">
  <defs>
    <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#7CF3A6"/><stop offset="0.45" stop-color="#2ECC71"/>
      <stop offset="0.8" stop-color="#0E6E4A"/><stop offset="1" stop-color="#083428"/>
    </linearGradient>
    <radialGradient id="b" cx="0.15" cy="0.05" r="0.6"><stop offset="0" stop-color="#BEFFD2" stop-opacity="0.45"/><stop offset="1" stop-color="#BEFFD2" stop-opacity="0"/></radialGradient>
  </defs>
  <rect width="100" height="100" rx="24" fill="url(#g)"/>
  <rect width="100" height="100" rx="24" fill="url(#b)"/>
  <g fill="none" stroke="#fff" stroke-width="12.5" stroke-linecap="round" stroke-linejoin="round">
    <path d="M22 30 Q30 50 50 58 Q70 50 78 30"/>
    <path d="M50 58 V84"/>
  </g>
  <circle cx="50" cy="30" r="8.5" fill="#fff"/>
</svg>
'''
with open(os.path.join(BRAND, "yuno-icone.svg"), "w", encoding="utf-8") as f:
    f.write(svg)
print("ok", BRAND)
