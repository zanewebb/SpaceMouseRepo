"""
SpaceMouseRepo Thunderstore icon generator — refined pass.
256x256 PNG — Orbital Precision philosophy.
SpaceMouse puck at center, crisp 6DOF axis arrows, glowing cyan accent ring,
rotation arcs, on near-black with hex grid texture.
"""

import math
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

FONTS_DIR = "/Users/shahzodaakhmedova/.claude/plugins/cache/anthropic-agent-skills/example-skills/b0cbd3df1533/skills/canvas-design/canvas-fonts"
OUT_PATH = "/Users/shahzodaakhmedova/Documents/GitHub/SpaceMouseRepo/thunderstore/icon.png"

W = H = 256
CX = CY = 128

# Palette
BG      = (7, 9, 18)
CYAN    = (0, 218, 232)
PURPLE  = (148, 72, 230)
GREEN   = (72, 228, 112)
WHITE   = (245, 248, 255)

def lerp_color(a, b, t):
    return tuple(int(a[i] + (b[i]-a[i])*t) for i in range(3))

def alpha_over(base, layer):
    return Image.alpha_composite(base, layer)

# ── base canvas ───────────────────────────────────────────────────────────────
img = Image.new("RGBA", (W, H), (*BG, 255))

# ── hex grid background ───────────────────────────────────────────────────────
HEX_R = 13
hex_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
hdraw = ImageDraw.Draw(hex_layer)

def hex_pts(cx, cy, r):
    return [(cx + r * math.cos(math.radians(60*i - 30)),
             cy + r * math.sin(math.radians(60*i - 30))) for i in range(6)]

rows = int(H / (HEX_R * 1.5)) + 3
cols = int(W / (HEX_R * 1.732)) + 3
for row in range(-1, rows):
    for col in range(-1, cols):
        hx = col * HEX_R * 1.732 + (HEX_R * 0.866 if row % 2 else 0) - 6
        hy = row * HEX_R * 1.5 - 6
        dist = math.sqrt((hx - CX)**2 + (hy - CY)**2)
        fade = max(0.0, min(1.0, (dist - 20) / 100))
        alpha = int(22 + 18 * fade)
        pts = hex_pts(hx, hy, HEX_R - 1)
        hdraw.line(pts + [pts[0]], fill=(28, 40, 80, alpha), width=1)

img = alpha_over(img, hex_layer)

# ── outer sensor rings ────────────────────────────────────────────────────────
ring_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
rdraw = ImageDraw.Draw(ring_layer)
for r, alpha in [(112, 22), (95, 35), (78, 25)]:
    rdraw.ellipse([(CX-r, CY-r), (CX+r, CY+r)],
                  outline=(40, 55, 95, alpha), width=1)
img = alpha_over(img, ring_layer)

# ── 6DOF translation arrows ───────────────────────────────────────────────────
# X (cyan), Y (purple), Z diagonal (green)
ARROW_INNER = 55
ARROW_OUTER = 104

def draw_axis_arrow(draw, cx, cy, angle_deg, color, inner, outer, width=2):
    rad = math.radians(angle_deg)
    x0, y0 = cx + inner * math.cos(rad), cy + inner * math.sin(rad)
    x1, y1 = cx + outer * math.cos(rad), cy + outer * math.sin(rad)
    draw.line([(x0, y0), (x1, y1)], fill=(*color, 200), width=width)
    # arrowhead
    hl = 9
    hw = 4
    bx = x1 + hl * math.cos(math.radians(angle_deg + 180))
    by = y1 + hl * math.sin(math.radians(angle_deg + 180))
    px = math.cos(math.radians(angle_deg + 90))
    py = math.sin(math.radians(angle_deg + 90))
    draw.polygon([
        (x1, y1),
        (bx + hw*px, by + hw*py),
        (bx - hw*px, by - hw*py),
    ], fill=(*color, 220))

arrow_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
adraw = ImageDraw.Draw(arrow_layer)

draw_axis_arrow(adraw, CX, CY,   0, CYAN,   ARROW_INNER, ARROW_OUTER, 2)
draw_axis_arrow(adraw, CX, CY, 180, CYAN,   ARROW_INNER, ARROW_OUTER, 2)
draw_axis_arrow(adraw, CX, CY,  90, PURPLE, ARROW_INNER, ARROW_OUTER, 2)
draw_axis_arrow(adraw, CX, CY, 270, PURPLE, ARROW_INNER, ARROW_OUTER, 2)
draw_axis_arrow(adraw, CX, CY,  45, GREEN,  ARROW_INNER+8, ARROW_OUTER-8, 2)
draw_axis_arrow(adraw, CX, CY, 225, GREEN,  ARROW_INNER+8, ARROW_OUTER-8, 2)

img = alpha_over(img, arrow_layer)

# ── rotation arcs (Rx, Ry, Rz) ────────────────────────────────────────────────
arc_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
acdraw = ImageDraw.Draw(arc_layer)

ARC_SPECS = [
    (64, CYAN,   10,   80),
    (69, PURPLE, 130, 200),
    (74, GREEN,  250, 320),
]
for arc_r, col, start_deg, end_deg in ARC_SPECS:
    prev = None
    for deg in range(start_deg, end_deg + 1, 2):
        rad = math.radians(deg)
        x = CX + arc_r * math.cos(rad)
        y = CY + arc_r * math.sin(rad)
        if prev:
            acdraw.line([prev, (x, y)], fill=(*col, 170), width=2)
        prev = (x, y)
    # arrowhead at arc tip
    tip_rad  = math.radians(end_deg)
    prev_rad = math.radians(end_deg - 6)
    tx = CX + arc_r * math.cos(tip_rad)
    ty = CY + arc_r * math.sin(tip_rad)
    px2 = CX + arc_r * math.cos(prev_rad)
    py2 = CY + arc_r * math.sin(prev_rad)
    dx, dy = tx - px2, ty - py2
    ln = math.sqrt(dx*dx + dy*dy) or 1
    dx /= ln; dy /= ln
    nx, ny = -dy, dx
    acdraw.polygon([
        (tx, ty),
        (tx - dx*7 + nx*3, ty - dy*7 + ny*3),
        (tx - dx*7 - nx*3, ty - dy*7 - ny*3),
    ], fill=(*col, 200))

img = alpha_over(img, arc_layer)

# ── puck body ─────────────────────────────────────────────────────────────────
PUCK_R = 46
PUCK_BODY = (18, 24, 40)

puck_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
pdraw = ImageDraw.Draw(puck_layer)

# dark body fill
pdraw.ellipse([(CX-PUCK_R, CY-PUCK_R), (CX+PUCK_R, CY+PUCK_R)],
              fill=(*PUCK_BODY, 255))

# multi-layer glow ring (cyan accent)
for r_off, alpha in [(0, 220), (1, 100), (2, 50), (3, 20)]:
    r2 = PUCK_R - r_off
    pdraw.ellipse([(CX-r2, CY-r2), (CX+r2, CY+r2)],
                  outline=(*CYAN, alpha), width=2)

img = alpha_over(img, puck_layer)

# ── dome cap ───────────────────────────────────────────────────────────────────
DOME_R = 28
dome_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
ddraw = ImageDraw.Draw(dome_layer)

# gradient fill via concentric discs
for step in range(DOME_R, 0, -1):
    t = 1 - (step / DOME_R)
    col = lerp_color((32, 42, 68), (14, 18, 34), t)
    ddraw.ellipse([(CX-step, CY-step), (CX+step, CY+step)],
                  fill=(*col, 255))

# glowing edge on dome
for off, alpha in [(0, 200), (1, 90), (2, 35)]:
    r3 = DOME_R - off
    ddraw.ellipse([(CX-r3, CY-r3), (CX+r3, CY+r3)],
                  outline=(*CYAN, alpha), width=1)

# specular highlight (top-left)
ddraw.ellipse([(CX-11, CY-16), (CX+2, CY-6)],
              fill=(255, 255, 255, 38))

img = alpha_over(img, dome_layer)

# ── puck rim tick marks ────────────────────────────────────────────────────────
tick_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
tkdraw = ImageDraw.Draw(tick_layer)
for deg in range(0, 360, 45):
    rad = math.radians(deg)
    r_in  = PUCK_R - 5
    r_out = PUCK_R - 1
    xi, yi = CX + r_in  * math.cos(rad), CY + r_in  * math.sin(rad)
    xo, yo = CX + r_out * math.cos(rad), CY + r_out * math.sin(rad)
    tkdraw.line([(xi, yi), (xo, yo)], fill=(*CYAN, 150), width=1)
img = alpha_over(img, tick_layer)

# ── corner accent dots ─────────────────────────────────────────────────────────
CORNER_DATA = [(14, 14, CYAN), (242, 14, GREEN), (14, 242, PURPLE), (242, 242, CYAN)]
corner_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
cdraw = ImageDraw.Draw(corner_layer)
for cx2, cy2, col in CORNER_DATA:
    for r_off, alpha in [(3, 180), (5, 50)]:
        cdraw.ellipse([(cx2-r_off, cy2-r_off), (cx2+r_off, cy2+r_off)],
                      fill=(*col, alpha))
img = alpha_over(img, corner_layer)

# ── thin cross-hair lines (engineering precision feel) ─────────────────────────
cross_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
xdraw = ImageDraw.Draw(cross_layer)
# horizontal hair
xdraw.line([(8, CY), (CX - PUCK_R - 6, CY)], fill=(*CYAN, 40), width=1)
xdraw.line([(CX + PUCK_R + 6, CY), (W-8, CY)], fill=(*CYAN, 40), width=1)
# vertical hair
xdraw.line([(CX, 8), (CX, CY - PUCK_R - 6)], fill=(*PURPLE, 40), width=1)
xdraw.line([(CX, CY + PUCK_R + 6), (CX, H-8)], fill=(*PURPLE, 40), width=1)
img = alpha_over(img, cross_layer)

# ── text labels ────────────────────────────────────────────────────────────────
try:
    font_label = ImageFont.truetype(os.path.join(FONTS_DIR, "GeistMono-Bold.ttf"), 11)
    font_sub   = ImageFont.truetype(os.path.join(FONTS_DIR, "GeistMono-Regular.ttf"), 8)
except Exception:
    font_label = ImageFont.load_default()
    font_sub   = font_label

txt_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
tdraw = ImageDraw.Draw(txt_layer)

label = "6DOF"
bbox  = tdraw.textbbox((0, 0), label, font=font_label)
lw    = bbox[2] - bbox[0]
tdraw.text(((W - lw) // 2, 9), label, font=font_label, fill=(*CYAN, 210))

sub  = "SPACEMOUSE"
bbox2 = tdraw.textbbox((0, 0), sub, font=font_sub)
sw    = bbox2[2] - bbox2[0]
tdraw.text(((W - sw) // 2, H - 19), sub, font=font_sub, fill=(*WHITE, 110))

img = alpha_over(img, txt_layer)

# ── edge vignette ──────────────────────────────────────────────────────────────
vig_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
for x in range(W):
    for y in range(H):
        dist = math.sqrt((x - CX)**2 + (y - CY)**2)
        if dist > 100:
            t = min(1.0, (dist - 100) / 40)
            alpha = int(t * t * 160)
            vig_layer.putpixel((x, y), (0, 0, 0, alpha))
img = alpha_over(img, vig_layer)

# ── flatten to RGB and save ────────────────────────────────────────────────────
final = Image.new("RGB", (W, H), BG)
final.paste(img, mask=img.split()[3])
final.save(OUT_PATH, "PNG", optimize=True)
print(f"Saved: {OUT_PATH}  size={os.path.getsize(OUT_PATH)//1024}KB  dims={final.size}")
