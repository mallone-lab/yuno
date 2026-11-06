"""
Gera a barra de ferramentas "YUNO" do CorelDRAW:
  - ícones (desenhados aqui) -> YunoRes.res -> YunoRes.dll (DLL só de recursos)
  - config.xml, AppUI.xslt e UserUI.xslt em src/Yuno/Addon

Uso: python gerar_barra.py   (precisa de Pillow; usa o csc.exe do .NET Framework)
"""
import os, struct, subprocess, uuid, io
from PIL import Image, ImageDraw

# Ícones da barra: Tabler Icons (MIT, tools/icons/LICENSE-tabler.txt), recoloridos no verde YUNO.
# Cada botão usa o ícone que diz o que ele faz.
SVG_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "icons")
SVG_DO_BOTAO = {
    "definir": "ruler-measure",   # Definir tamanhos -> régua de medida
    "aplicar": "stack-2",         # Aplicar matrizes -> camadas (a matriz vai para todos os tamanhos)
    "montar": "shirt-sport",      # Montar pedido -> camisa de jogo com número
    "ferramentas": "tools",       # Ferramentas -> régua e chave
    "encaixe": "puzzle",          # Encaixe sobre outro -> peça de quebra-cabeça
    "exportar": "file-export",    # Exportar -> arquivo saindo
    "config": "settings",         # Configurações -> engrenagem
}
COR_ICONE = "#22B06A"             # verde YUNO com contraste em tema claro e escuro

def icone_svg(chave, t):
    import resvg_py
    with open(os.path.join(SVG_DIR, SVG_DO_BOTAO[chave] + ".svg"), encoding="utf-8") as f:
        svg = f.read().replace("currentColor", COR_ICONE)
    # traço um pouco mais grosso nos tamanhos pequenos para ficar nítido
    if t <= 20: svg = svg.replace('stroke-width="2"', 'stroke-width="2.2"')
    png = resvg_py.svg_to_bytes(svg_string=svg, width=t, height=t)
    return Image.open(io.BytesIO(bytes(png))).convert("RGBA")

def icone_tam(chave, t):
    if chave in SVG_DO_BOTAO:
        return icone_svg(chave, t)
    return icone(chave).resize((t, t), Image.LANCZOS)

AQUI = os.path.dirname(os.path.abspath(__file__))
ADDON = os.path.normpath(os.path.join(AQUI, "..", "src", "Yuno", "Addon"))
OBJ = os.path.join(AQUI, "obj")
os.makedirs(OBJ, exist_ok=True)

# GUIDs fixos (não mudar: o Corel guarda a posição da barra por eles)
NS = uuid.UUID("5b0f6a51-9a3e-4c1e-9d0a-3a1f0c0ffee1")
def gid(nome): return str(uuid.uuid5(NS, nome))

BARRA = gid("toolbar")
RES_NAME = gid("resources")

# (chave, legenda/tooltip, classe do painel)
BOTOES = [
    ("inicio",      "YUNO",                 "Yuno.UI.MainDocker"),
    ("definir",     "Definir tamanhos",     "Yuno.UI.PainelDefinir"),
    ("aplicar",     "Aplicar matrizes",     "Yuno.UI.PainelAplicar"),
    ("montar",      "Montar pedido",        "Yuno.UI.PainelPedido"),
    ("ferramentas", "Ferramentas",          "Yuno.UI.PainelFerramentas"),
    ("encaixe",     "Encaixe sobre outro",  "Yuno.UI.PainelEncaixe"),
    ("exportar",    "Exportar",             "Yuno.UI.PainelExportar"),
    ("config",      "Configurações",        "Yuno.UI.PainelConfig"),
]

AZUL = (46, 204, 113, 255)      # verde YUNO (cor principal)
VERDE = (19, 134, 90, 255)      # verde floresta
LARANJA = (124, 243, 166, 255)  # verde neon
ROXO = (14, 110, 74, 255)       # verde noite
CINZA = (154, 160, 168, 255)
BRANCO = (255, 255, 255, 255)

S = 256  # desenha grande e reduz

def camisa(d, ox, oy, esc, cor, contorno=None, larg=0):
    pts = [(-0.30,-0.42),(-0.12,-0.48),(-0.06,-0.40),(0.06,-0.40),(0.12,-0.48),(0.30,-0.42),
           (0.46,-0.20),(0.32,-0.08),(0.26,-0.16),(0.26,0.46),(-0.26,0.46),(-0.26,-0.16),(-0.32,-0.08),(-0.46,-0.20)]
    p = [(ox + x*esc, oy + y*esc) for x, y in pts]
    d.polygon(p, fill=cor)
    if contorno: d.line(p + [p[0]], fill=contorno, width=larg, joint="curve")

def icone(chave):
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c = S / 2
    if chave == "inicio":
        marca = os.path.normpath(os.path.join(AQUI, "..", "brand", "yuno-icone-1024.png"))
        return Image.open(marca).convert("RGBA").resize((S, S), Image.LANCZOS)
    if chave == "definir":
        camisa(d, c - 10, c + 6, 200, AZUL)
        d.ellipse((c + 34, c - 118, c + 122, c - 30), fill=LARANJA)
        d.text((c + 62, c - 100), "1", fill=(8, 52, 40, 255), font_size=64)
    elif chave == "aplicar":
        camisa(d, c - 34, c - 18, 170, CINZA)
        camisa(d, c + 26, c + 22, 170, AZUL, BRANCO, 8)
    elif chave == "montar":
        camisa(d, c, c + 4, 230, VERDE)
        d.text((c - 30, c - 34), "10", fill=BRANCO, font_size=78)
    elif chave == "ferramentas":
        d.line((c - 80, c + 80, c + 40, c - 40), fill=CINZA, width=40)
        d.ellipse((c + 6, c - 116, c + 116, c - 6), fill=CINZA)
        d.ellipse((c + 40, c - 82, c + 82, c - 40), fill=(0, 0, 0, 0))
        d.polygon([(c + 60, c - 120), (c + 120, c - 60), (c + 90, c - 40), (c + 40, c - 90)], fill=(0, 0, 0, 0))
        d.rounded_rectangle((c - 110, c - 40, c - 40, c + 30), radius=12, fill=AZUL)
    elif chave == "encaixe":
        r = 18
        d.rounded_rectangle((28, 28, 120, 120), r, fill=AZUL)
        d.rounded_rectangle((136, 28, 228, 120), r, fill=VERDE)
        d.rounded_rectangle((28, 136, 120, 228), r, fill=LARANJA)
        d.rounded_rectangle((136, 136, 228, 228), r, fill=ROXO)
    elif chave == "exportar":
        d.rounded_rectangle((40, 70, 190, 220), 22, outline=CINZA, width=22)
        d.line((110, 150, 210, 50), fill=AZUL, width=30)
        d.polygon([(150, 36), (226, 36), (226, 112)], fill=AZUL)
    elif chave == "config":
        import math
        for k in range(8):
            a = k * math.pi / 4
            x, y = c + math.cos(a) * 92, c + math.sin(a) * 92
            d.ellipse((x - 26, y - 26, x + 26, y + 26), fill=CINZA)
        d.ellipse((c - 86, c - 86, c + 86, c + 86), fill=CINZA)
        d.ellipse((c - 38, c - 38, c + 38, c + 38), fill=(0, 0, 0, 0))
    return im

# ---------------------------------------------------------------- .ico (BMP 32bpp) e .res

def icon_bmp(img):
    w, h = img.size
    px = img.load()
    hdr = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, 0, 0, 0, 0, 0)
    xor = bytearray()
    for y in range(h - 1, -1, -1):
        for x in range(w):
            r, g, b, a = px[x, y]
            xor += bytes((b, g, r, a))
    row = ((w + 31) // 32) * 4
    andm = bytes(row * h)
    return hdr + bytes(xor) + andm

def res_entry(rtype, rname, data, lang=0x0416):
    def ident(v): return struct.pack("<HH", 0xFFFF, v)
    head = ident(rtype) + ident(rname)
    hdr_size = 8 + len(head) + 16
    body = struct.pack("<II", len(data), hdr_size) + head + struct.pack("<IHHII", 0, 0x1030, lang, 0, 0)
    out = body + data
    while len(out) % 4: out += b"\0"
    return out

def string_table(strings):  # {id: text}
    blocks = {}
    for i, s in strings.items():
        blocks.setdefault(i // 16 + 1, {})[i % 16] = s
    res = []
    for b, m in blocks.items():
        data = b""
        for k in range(16):
            s = m.get(k, "")
            data += struct.pack("<H", len(s)) + s.encode("utf-16-le")
        res.append((b, data))
    return res

TAMANHOS = [16, 20, 24, 32, 40, 48]
partes = [struct.pack("<IIHHHH", 0, 32, 0xFFFF, 0, 0xFFFF, 0) + bytes(16)]  # cabeçalho vazio obrigatório do .res
icon_id = 1
strings = {}
for n, (chave, legenda, _) in enumerate(BOTOES):
    grande = icone_tam(chave, 64)
    grp = struct.pack("<HHH", 0, 1, len(TAMANHOS))
    for t in TAMANHOS:
        img = icone_tam(chave, t)
        bmp = icon_bmp(img)
        partes.append(res_entry(3, icon_id, bmp))
        grp += struct.pack("<BBBBHHIH", t, t, 0, 0, 1, 32, len(bmp), icon_id)
        icon_id += 1
    partes.append(res_entry(14, 101 + n, grp))
    strings[30000 + n] = legenda
    grande.save(os.path.join(OBJ, f"{chave}.png"))
for b, data in string_table(strings):
    partes.append(res_entry(6, b, data))

res_path = os.path.join(OBJ, "YunoRes.res")
with open(res_path, "wb") as f:
    f.write(b"".join(partes))

# DLL de recursos (assembly vazio + recursos Win32), igual a outros add-ons do Corel
cs = os.path.join(OBJ, "Vazio.cs")
with open(cs, "w") as f:
    f.write("namespace YunoRes { internal static class R { } }\n")
csc = r"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
dll = os.path.join(ADDON, "YunoRes.dll")
subprocess.check_call([csc, "/nologo", "/target:library", f"/out:{dll}", f"/win32res:{res_path}", cs])

# ---------------------------------------------------------------- config.xml
cfg = ['<?xml version="1.0"?>', "<config>", "<resources>",
       f'<resource name="{RES_NAME}" path="YunoRes.dll">', "<resourceMap>"]
for n, (chave, _, _) in enumerate(BOTOES):
    cfg.append(f'<resEntry id="{gid("btn-" + chave)}" string="{30000 + n}" icon="{101 + n}"/>')
cfg += ["</resourceMap>", "</resource>", "</resources>", "</config>", ""]
with open(os.path.join(ADDON, "config.xml"), "w", encoding="utf-8") as f:
    f.write("\n".join(cfg))

# ---------------------------------------------------------------- AppUI.xslt
MENU_FLYOUT = "4cb58535-9fa4-4688-9236-4b7887e33059"
MENU_BAR = "09ba2112-3fa1-4178-96e7-d230c33692f3"
MENU_ITEM = "6c07c4c6-543b-4724-9e1a-91202afb8596"
MAIN_HOST = "fbe1a850-6bbc-47e4-a3fe-2bdffbc6f723"
MAIN_DOCKER = "74ca6974-78e4-49ab-af1f-4804be8afc9f"
DOCKHOST = ("uiConfig/containers/container[@guid='bee85f91-3ad9-dc8d-48b5-d2a87c8b2109']"
            "/container[@guid='Framework_MainFrame-layout']/dockHost[@guid='894bf987-2ec1-8f83-41d8-68f6797d0db4']"
            "/toolbar[@guidRef='c2b44f69-6dec-444e-a37e-5dbf7ff43dae']")
DOCKHOST_STATE = ("uiConfig/states/state[1]/container[@guidRef='bee85f91-3ad9-dc8d-48b5-d2a87c8b2109']"
                  "/layout/dockHost[@guid='894bf987-2ec1-8f83-41d8-68f6797d0db4']"
                  "/toolbar[@guidRef='c2b44f69-6dec-444e-a37e-5dbf7ff43dae']")

itens, dockers, barra = [], [], []
for chave, legenda, classe in BOTOES:
    b, h, dk = gid("btn-" + chave), gid("host-" + chave), gid("docker-" + chave)
    itens.append(f'''      <itemData guid="{b}" type="checkButton" check="*Docker('{dk}')"
                userCaption="{legenda}" icon="guid://{b}" enable="true" />
      <itemData guid="{h}" type="wpfhost" hostedType="Addons\\Yuno\\Yuno.dll,{classe}" width="330" enable="true" />''')
    dockers.append(f'''      <dockerData guid="{dk}" userCaption="{legenda}" wantReturn="true" focusStyle="noThrow">
        <container><item dock="fill" margin="0,0,0,0" guidRef="{h}" /></container>
      </dockerData>''')
    barra.append(f'          <item guidRef="{b}" dock="top"/>')

app_ui = f'''<?xml version="1.0"?>
<!-- GERADO por tools/gerar_barra.py. Barra "YUNO" + menu Janela > YUNO -->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:frmwrk="Corel Framework Data">
  <xsl:output method="xml" encoding="UTF-8" indent="yes"/>
  <frmwrk:uiconfig>
    <frmwrk:applicationInfo userConfiguration="true" />
  </frmwrk:uiconfig>

  <xsl:template match="node()|@*">
    <xsl:copy><xsl:apply-templates select="node()|@*"/></xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/items">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <itemData guid="{MENU_FLYOUT}" noBmpOnMenu="true" type="flyout" dynamicCategory="2cc24a3e-fe24-4708-9a74-9c75406eebcd"
                userCaption="YUNO" enable="true" flyoutBarRef="{MENU_BAR}" />
      <itemData guid="{MENU_ITEM}" noBmpOnMenu="true" type="checkButton" check="*Docker('{MAIN_DOCKER}')"
                dynamicCategory="2cc24a3e-fe24-4708-9a74-9c75406eebcd" userCaption="YUNO (painel completo)" enable="true" />
      <itemData guid="{MAIN_HOST}" type="wpfhost" hostedType="Addons\\Yuno\\Yuno.dll,Yuno.UI.MainDocker" width="340" enable="true" />
{chr(10).join(itens)}
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/commandBars">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <commandBarData guid="{MENU_BAR}" type="menu" nonLocalizableName="YUNO" flyout="true"><menu /></commandBarData>
      <commandBarData guid="{BARRA}" nonLocalizableName="YUNO" userCaption="YUNO" type="toolbar">
        <toolbar>
{chr(10).join(barra)}
        </toolbar>
      </commandBarData>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/commandBars/commandBarData[@guid='{MENU_BAR}']/menu">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <xsl:if test="not(./item[@guidRef='{MENU_ITEM}'])"><item guidRef="{MENU_ITEM}"/></xsl:if>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/dockers">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <dockerData guid="{MAIN_DOCKER}" userCaption="YUNO" wantReturn="true" focusStyle="noThrow">
        <container><item dock="fill" margin="0,0,0,0" guidRef="{MAIN_HOST}" /></container>
      </dockerData>
{chr(10).join(dockers)}
    </xsl:copy>
  </xsl:template>

  <!-- coloca a barra no topo, ao lado da barra de propriedades -->
  <xsl:template match="{DOCKHOST}">
    <xsl:copy-of select="."/>
    <toolbar guidRef="{BARRA}" />
  </xsl:template>
</xsl:stylesheet>
'''
with open(os.path.join(ADDON, "AppUI.xslt"), "w", encoding="utf-8") as f:
    f.write(app_ui)

user_ui = f'''<?xml version="1.0"?>
<!-- GERADO por tools/gerar_barra.py. Insere o menu e a barra na área de trabalho do usuário. -->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:frmwrk="Corel Framework Data" exclude-result-prefixes="frmwrk">
  <xsl:output method="xml" encoding="UTF-8" indent="yes"/>
  <frmwrk:uiconfig>
    <frmwrk:compositeNode xPath="/uiConfig/commandBars/commandBarData[@guid='cdb9ea9c-223e-4865-97a6-31c007c69674']"/>
    <frmwrk:compositeNode xPath="/uiConfig/commandBars/commandBarData[@guid='{MENU_BAR}']"/>
    <frmwrk:compositeNode xPath="/uiConfig/commandBars/commandBarData[@guid='{BARRA}']"/>
    <frmwrk:compositeNode xPath="/uiConfig/frame"/>
    <frmwrk:compositeNode xPath="/uiConfig/states/state[1]"/>
  </frmwrk:uiconfig>

  <xsl:template match="node()|@*">
    <xsl:copy><xsl:apply-templates select="node()|@*"/></xsl:copy>
  </xsl:template>

  <!-- faz o Corel reaplicar este arquivo a cada inicialização -->
  <xsl:template match="Addons/new_element[contains(translate(@installFile,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'\\addons\\yuno\\userui.xslt')]"/>

  <xsl:template match="node()|@*" mode="insert-item">
    <xsl:param name="after"></xsl:param>
    <xsl:param name="content"></xsl:param>
    <xsl:copy>
      <xsl:apply-templates select="@*"/>
      <xsl:for-each select="node()">
        <xsl:copy><xsl:apply-templates select="node()|@*"/></xsl:copy>
        <xsl:if test="name()='item' and @guidRef=$after"><xsl:copy-of select="$content"/></xsl:if>
      </xsl:for-each>
      <xsl:if test="not(./item[@guidRef=$after])"><xsl:copy-of select="$content"/></xsl:if>
    </xsl:copy>
  </xsl:template>

  <!-- menu Janela -->
  <xsl:template match="commandBarData[@guid='cdb9ea9c-223e-4865-97a6-31c007c69674']/menu">
    <xsl:apply-templates mode="insert-item" select=".">
      <xsl:with-param name="content">
        <xsl:if test="not(./item[@guidRef='{MENU_FLYOUT}'])"><item guidRef="{MENU_FLYOUT}"/></xsl:if>
      </xsl:with-param>
      <xsl:with-param name="after" select="'47cc9a2d-0b7a-4df1-a686-ea3aa21b4631'"/>
    </xsl:apply-templates>
  </xsl:template>

  <xsl:template match="commandBarData[@guid='{MENU_BAR}']/menu">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <xsl:if test="not(./item[@guidRef='{MENU_ITEM}'])"><item guidRef="{MENU_ITEM}"/></xsl:if>
    </xsl:copy>
  </xsl:template>

  <!-- barra no topo (layout atual e estado salvo) -->
  <xsl:template match="{DOCKHOST}">
    <xsl:copy><xsl:apply-templates select="node()|@*"/></xsl:copy>
    <xsl:if test="not(../toolbar[@guidRef='{BARRA}'])"><toolbar guidRef="{BARRA}"/></xsl:if>
  </xsl:template>
  <xsl:template match="{DOCKHOST_STATE}">
    <xsl:copy><xsl:apply-templates select="node()|@*"/></xsl:copy>
    <xsl:if test="not(../toolbar[@guidRef='{BARRA}'])"><toolbar guidRef="{BARRA}"/></xsl:if>
  </xsl:template>
</xsl:stylesheet>
'''
with open(os.path.join(ADDON, "UserUI.xslt"), "w", encoding="utf-8") as f:
    f.write(user_ui)

print("barra:", BARRA)
print("ok:", dll)
