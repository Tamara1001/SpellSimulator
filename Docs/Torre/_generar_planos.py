#!/usr/bin/env python3
"""Genera los 4 planos de jugador de la torre-tutorial (Docs/Torre/0N_*.svg).
Convencion: 0 grados = este, 90 = norte (arriba). Escala 31 px/m. Centro de la torre en (CX, CY).
Cada plano: muro, rampa norte, camino verde, 1 obstaculo obligatorio resaltado, estaciones, carteles numerados.
Uso: python _generar_planos.py
"""
import math, os

W, H = 1000, 820
CX, CY = 400, 420
S = 31.0
BG, FLOOR, WALL = "#1e2530", "#39424f", "#8d7a6b"
PATH, ACC, STAT, OPT, WARN = "#58d68d", "#f5a623", "#6fb8ff", "#8fd3e8", "#e5534b"
TXT = "#eef1f5"


def xy(r, a):
    t = math.radians(a)
    return CX + r * S * math.cos(t), CY - r * S * math.sin(t)


def arc(r, a0, a1):
    """Camino de a0 a a1 (grados) pasando por el lado corto en sentido de decrecimiento si a1<a0."""
    if a1 > a0:
        a1 -= 360  # siempre en sentido horario (angulo decreciente)
    x0, y0 = xy(r, a0)
    x1, y1 = xy(r, a1)
    large = 1 if abs(a1 - a0) > 180 else 0
    sweep = 1  # horario en pantalla
    return f"M{x0:.1f},{y0:.1f} A{r*S:.1f},{r*S:.1f} 0 {large} {sweep} {x1:.1f},{y1:.1f}"


class Svg:
    def __init__(self, title, subtitle):
        self.o = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" font-family="Segoe UI, Arial, sans-serif">',
                  '<defs><marker id="ar" viewBox="0 0 10 10" refX="7" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M0 0L10 5L0 10z" fill="context-stroke"/></marker></defs>',
                  f'<rect width="{W}" height="{H}" fill="{BG}"/>']
        self.o.append(f'<text x="30" y="46" font-size="26" font-weight="800" fill="{TXT}">{title}</text>')
        self.o.append(f'<text x="30" y="72" font-size="15" fill="#aab3c0">{subtitle}</text>')

    def add(self, s):
        self.o.append(s)

    def base(self, outer=6.2, extra=None):
        self.add(f'<circle cx="{CX}" cy="{CY}" r="{outer*S+14:.0f}" fill="none" stroke="{WALL}" stroke-width="12"/>')
        self.add(f'<circle cx="{CX}" cy="{CY}" r="{outer*S:.0f}" fill="{FLOOR}"/>')
        if extra:
            self.add(extra)

    def ramp(self, label_up="llegás", label_down="bajás"):
        # media corona norte: 0..180 grados, r 4.8..6.15
        r0, r1 = 4.8, 6.15
        x0, y0 = xy(r1, 0); x1, y1 = xy(r1, 180); x2, y2 = xy(r0, 180); x3, y3 = xy(r0, 0)
        self.add(f'<path d="M{x0:.1f},{y0:.1f} A{r1*S:.1f},{r1*S:.1f} 0 0 0 {x1:.1f},{y1:.1f} L{x2:.1f},{y2:.1f} A{r0*S:.1f},{r0*S:.1f} 0 0 1 {x3:.1f},{y3:.1f} Z" fill="#555e6e" stroke="#7b8596" stroke-width="2"/>')
        tx, ty = xy(5.5, 90)
        self.add(f'<text x="{tx:.0f}" y="{ty:.0f}" font-size="14" fill="#c4ccd8" text-anchor="middle">rampa</text>')

    def path(self, pts, color=PATH, w=6, dash=None):
        d = pts if isinstance(pts, str) else "M" + " L".join(f"{x:.1f},{y:.1f}" for x, y in pts)
        da = f' stroke-dasharray="{dash}"' if dash else ""
        self.add(f'<path d="{d}" fill="none" stroke="{color}" stroke-width="{w}" stroke-linecap="round"{da} marker-end="url(#ar)"/>')

    def label(self, r, a, text, dx=0, dy=0, anchor="middle", size=15, color=TXT, weight=700):
        x, y = xy(r, a)
        self.add(f'<text x="{x+dx:.0f}" y="{y+dy:.0f}" font-size="{size}" font-weight="{weight}" fill="{color}" text-anchor="{anchor}">{text}</text>')

    def dot(self, r, a, color, rad=11):
        x, y = xy(r, a)
        self.add(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{rad}" fill="{color}" stroke="#111" stroke-width="2"/>')

    def sign(self, n, r, a):
        x, y = xy(r, a)
        self.add(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="13" fill="#f4d03f" stroke="#111" stroke-width="2"/>')
        self.add(f'<text x="{x:.1f}" y="{y+5:.1f}" font-size="14" font-weight="800" fill="#111" text-anchor="middle">{n}</text>')

    def bar(self, r0, r1, a, color, w=12):
        x0, y0 = xy(r0, a); x1, y1 = xy(r1, a)
        self.add(f'<line x1="{x0:.1f}" y1="{y0:.1f}" x2="{x1:.1f}" y2="{y1:.1f}" stroke="{color}" stroke-width="{w}" stroke-linecap="round"/>')

    def legend(self, items):
        x, y = 760, 120
        self.add(f'<rect x="{x-14}" y="{y-26}" width="236" height="{34+len(items)*28}" rx="8" fill="#161c26" stroke="#3a4352"/>')
        for i, (kind, text) in enumerate(items):
            yy = y + i * 28
            if kind == "path":
                self.add(f'<line x1="{x}" y1="{yy}" x2="{x+30}" y2="{yy}" stroke="{PATH}" stroke-width="6" stroke-linecap="round"/>')
            elif kind == "block":
                self.add(f'<rect x="{x}" y="{yy-8}" width="30" height="16" rx="3" fill="{WARN}"/>')
            elif kind == "sign":
                self.add(f'<circle cx="{x+15}" cy="{yy}" r="11" fill="#f4d03f" stroke="#111" stroke-width="2"/>')
            elif kind == "opt":
                self.add(f'<rect x="{x}" y="{yy-8}" width="30" height="16" rx="3" fill="none" stroke="{OPT}" stroke-width="3" stroke-dasharray="5 3"/>')
            elif kind == "stat":
                self.add(f'<rect x="{x}" y="{yy-8}" width="30" height="16" rx="3" fill="{STAT}"/>')
            elif kind == "aim":
                self.add(f'<line x1="{x}" y1="{yy}" x2="{x+30}" y2="{yy}" stroke="#7fd4ff" stroke-width="4" stroke-dasharray="6 4"/>')
            self.add(f'<text x="{x+42}" y="{yy+5}" font-size="14" fill="{TXT}">{text}</text>')

    def compass(self):
        self.add(f'<text x="{W-60}" y="{H-70}" font-size="18" font-weight="800" fill="{TXT}" text-anchor="middle">N</text>')
        self.add(f'<line x1="{W-60}" y1="{H-62}" x2="{W-60}" y2="{H-30}" stroke="{TXT}" stroke-width="3" marker-end="url(#ar)" transform="rotate(180 {W-60} {H-46})"/>')
        self.add(f'<line x1="30" y1="{H-30}" x2="{30+5*S:.0f}" y2="{H-30}" stroke="{TXT}" stroke-width="3"/>')
        self.add(f'<text x="30" y="{H-38}" font-size="13" fill="#aab3c0">5 m</text>')

    def save(self, name):
        self.compass()
        self.o.append("</svg>")
        with open(os.path.join(os.path.dirname(__file__), name), "w", encoding="utf-8") as f:
            f.write("\n".join(self.o))


# ----------------------------------------------------------------- Terraza
def terraza():
    global S
    S = 31.0
    s = Svg("Terraza · movilidad", "Caminá, agachate bajo la viga, saltá el muro y la reja se abre.")
    s.base(outer=8.2)
    s.add(f'<circle cx="{CX}" cy="{CY}" r="{4.8*S:.0f}" fill="#2c343f" stroke="#6b7586" stroke-width="3"/>')  # disco central
    s.ramp()
    # valla alta que cierra el oeste (lado NO obligatorio)
    s.bar(4.8, 8.2, 115, WARN, 8)
    s.label(8.5, 123, "valla (no se pasa)", dx=-6, dy=-4, size=13, color="#e8a09b", weight=600, anchor="end")
    # camino horario: spawn 90 -> 25 -> 325 -> 195 -> 180
    s.path(arc(6.6, 88, 204), w=6)
    # obstaculos de borde a borde
    s.bar(4.8, 8.2, 25, ACC, 12)
    s.label(8.6, 25, "viga baja · C", dx=40, dy=-2, size=15, color=ACC)
    s.bar(4.8, 8.2, 325, "#b8bfca", 12)
    s.label(8.6, 325, "muro · Espacio", dx=44, dy=8, size=15, color="#d5dae1")
    s.bar(4.8, 6.2, 195, "#c678ff", 10)
    s.label(7.4, 198, "reja mágica", dx=-6, dy=26, size=15, color="#d9a8ff", anchor="end")
    # inicio / salteo
    s.dot(6.8, 90, PATH, 12)
    s.label(6.8, 90, "INICIO", dx=22, dy=-14, size=16, anchor="start")
    s.dot(7.4, 102, STAT, 9)
    s.label(7.9, 106, "palanca: saltear", dx=-8, dy=-16, size=13, color=STAT, weight=600, anchor="end")
    # salida
    s.label(5.5, 180, "BAJÁS", dx=-4, dy=-52, size=16, color=ACC)
    s.path([xy(5.5, 188), xy(5.5, 172)], ACC, 6)
    # carteles
    for n, r, a in [(1, 7.4, 98), (2, 6.8, 72), (3, 6.6, 42), (4, 6.6, 344), (5, 5.5, 212)]:
        s.sign(n, r, a)
    s.legend([("path", "camino"), ("block", "obstáculo"), ("sign", "cartel (ver carteles.md)"), ("stat", "extra / salteo")])
    s.save("01_terraza.svg")


# ----------------------------------------------------------------- Piso 2
def piso2():
    global S
    S = 40.0
    s = Svg("Piso 2 · mover objetos", "Despejá la pila de cajas para seguir. Lo celeste es práctica opcional.")
    s.base()
    s.ramp()
    # zona opcional central
    s.add(f'<circle cx="{CX}" cy="{CY}" r="{3.9*S:.0f}" fill="none" stroke="{OPT}" stroke-width="3" stroke-dasharray="8 6"/>')
    s.label(0, 0, "PRÁCTICA OPCIONAL", dy=6, size=14, color=OPT)
    s.dot(2.5, 20, STAT, 9); s.label(2.5, 20, "mesa + lienzo", dx=14, dy=-12, size=13, anchor="start", color=STAT, weight=600)
    s.dot(3.1, 60, OPT, 9); s.label(3.1, 60, "1 flotar", dx=12, dy=-8, size=13, anchor="start", color=OPT, weight=600)
    s.dot(3.1, 110, OPT, 9); s.label(3.1, 110, "2 de canto", dx=-12, dy=-8, size=13, anchor="end", color=OPT, weight=600)
    s.dot(3.5, 315, OPT, 9); s.label(3.5, 315, "3 diana (Q)", dx=14, dy=16, size=13, anchor="start", color=OPT, weight=600)
    s.bar(5.7, 6.15, 315, OPT, 8)
    # camino horario 0 -> 270 -> 180
    s.path(arc(5.5, 4, 190), w=6)
    s.label(5.5, 0, "LLEGÁS", dx=36, dy=-6, size=16, anchor="start", color=PATH)
    # pila obligatoria
    s.bar(4.8, 6.15, 198, WARN, 18)
    s.label(6.6, 198, "PILA DE CAJAS", dx=-10, dy=24, size=16, color="#f0a8a3", anchor="end")
    s.label(5.5, 180, "BAJÁS", dx=-4, dy=-52, size=16, color=ACC)
    for n, r, a in [(6, 4.4, 8), (10, 5.35, 216)]:
        s.sign(n, r, a)
    s.legend([("path", "camino"), ("block", "obstáculo"), ("opt", "opcional"), ("sign", "cartel")])
    s.save("02_piso2.svg")


# ----------------------------------------------------------------- Piso 1
def piso1():
    global S
    S = 40.0
    s = Svg("Piso 1 · objetos mágicos", "Tomá la pluma, leé el Tomo I, dibujá viento y apuntalo a la cortina.")
    s.base()
    s.ramp()
    s.path(arc(5.3, 4, 206), w=6)
    s.label(5.3, 0, "LLEGÁS", dx=36, dy=-6, size=16, anchor="start", color=PATH)
    # estaciones en orden, sobre el interior del arco
    s.dot(3.7, 320, STAT, 12); s.label(2.7, 320, "1 pluma + tinta", dy=6, size=14, color=STAT)
    s.dot(3.7, 283, STAT, 12); s.label(2.7, 283, "2 Tomo I: viento", dy=6, size=14, color=STAT)
    s.dot(3.7, 237, STAT, 12); s.label(2.7, 237, "3 lienzo con plantilla", dy=6, size=14, color=STAT)
    s.path([xy(3.7, 232), xy(5.35, 198)], "#7fd4ff", 4, "7 5")
    s.label(4.9, 214, "apuntá", dx=-6, dy=-10, size=13, color="#7fd4ff", weight=600, anchor="end")
    s.bar(4.8, 6.15, 195, WARN, 18)
    s.label(6.6, 195, "CORTINA PESADA", dx=-10, dy=22, size=16, color="#f0a8a3", anchor="end")
    s.label(5.5, 180, "BAJÁS", dx=-4, dy=-52, size=16, color=ACC)
    for n, r, a in [(11, 5.3, 338), (12, 5.3, 300), (13, 5.3, 252)]:
        s.sign(n, r, a)
    s.legend([("path", "camino"), ("block", "obstáculo"), ("stat", "estación"), ("aim", "flecha de apunte")])
    s.save("03_piso1.svg")


# ----------------------------------------------------------------- Planta baja
def planta_baja():
    global S
    S = 40.0
    s = Svg("Planta baja · la puerta", "Dibujá viento en el lienzo y apuntalo al pestillo para abrir.")
    s.base()
    s.ramp()
    # camino 0 -> 320 -> 285 -> 262 -> puerta 270
    s.path(arc(5.2, 4, 282), w=6)
    s.label(5.2, 0, "LLEGÁS", dx=36, dy=-6, size=16, anchor="start", color=PATH)
    s.dot(2.6, 320, STAT, 12); s.label(2.6, 320, "1 libro: elementos", dx=-16, dy=4, size=14, anchor="end", color=STAT)
    s.dot(3.0, 262, STAT, 12); s.label(3.0, 262, "2 lienzo con plantilla", dx=-16, dy=-6, size=14, anchor="end", color=STAT)
    s.dot(5.85, 256, WARN, 11); s.label(5.85, 256, "3 pestillo", dx=-16, dy=14, size=14, anchor="end", color="#f0a8a3")
    s.path([xy(3.0, 258), xy(5.7, 256)], "#7fd4ff", 4, "7 5")
    # puerta sur
    x, y = xy(6.2, 270)
    s.add(f'<rect x="{x-40:.0f}" y="{y-12:.0f}" width="80" height="24" rx="4" fill="{WARN}" stroke="#111" stroke-width="2"/>')
    s.label(6.2, 270, "PUERTA", dy=6, size=14, color="#111", weight=800)
    s.label(6.2, 270, "SALÍS AL NEXO ↓", dy=52, size=16, color=ACC)
    for n, r, a in [(14, 2.9, 285)]:
        s.sign(n, r, a)
    s.legend([("path", "camino"), ("block", "puerta trabada"), ("stat", "estación"), ("sign", "cartel")])
    s.save("04_planta_baja.svg")


if __name__ == "__main__":
    terraza(); piso2(); piso1(); planta_baja()
    print("ok")
