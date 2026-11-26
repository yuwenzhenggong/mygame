"""Editable layered SVG creature studies and a normal-beast motion sample."""
import io
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path.home() / '.codex/tmp/svg-tools'))
import resvg_py

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
ART = PROJECT / 'Assets/Resources/Art'
INK = '#191922'


def path(d, fill, stroke=INK, width=4, extra=''):
    return f'<path d="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{width}" stroke-linejoin="round" stroke-linecap="round" {extra}/>'


def leg(hip, foot, rear=False, far=False, elite=False):
    hx, hy = hip
    ax, ay = foot
    dx, dy = ax-hx, ay-hy
    distance = math.hypot(dx, dy)
    a = (59**2-54**2+distance**2)/(2*distance)
    h = math.sqrt(max(0, 59**2-a*a))
    sign = 1 if rear else -1
    kx, ky = hx+a*dx/distance-sign*h*dy/distance, hy+a*dy/distance+sign*h*dx/distance
    thick = 21 if elite else 16
    base = '#34353f' if far else '#4a4b58'
    result = path(f'M {hx-thick},{hy-9} Q {hx},{hy-22} {hx+thick},{hy-7} '
                  f'L {kx+13},{ky-5} Q {kx+15},{ky+9} {kx+8},{ky+15} '
                  f'L {ax+10},{ay+3} L {ax-12},{ay+3} L {kx-11},{ky+8} Z', base)
    if not far:
        result += path(f'M {hx-9},{hy-5} L {hx+5},{hy-9} L {kx+6},{ky+1} '
                       f'L {ax+4},{ay-3} L {ax-3},{ay-4} L {kx-5},{ky+6} Z', '#676775', 'none')
        result += path(f'M {kx-10},{ky+6} L {kx+4},{ky+13} L {ax-5},{ay-1}', 'none', '#2d2c37', 5)
    result += path(f'M {ax-25},{ay+1} Q {ax-23},{ay-12} {ax-10},{ay-13} '
                   f'L {ax+12},{ay-9} Q {ax+23},{ay-6} {ax+24},{ay+9} '
                   f'L {ax-22},{ay+11} Q {ax-31},{ay+9} {ax-25},{ay+1} Z', '#30303b' if far else '#50505d')
    for offset in [-22, -9, 4]:
        result += path(f'M {ax+offset},{ay+3} l -7,7 l 11,-1 Z', '#a4a4b1' if far else '#e1d8c5', width=2)
    if not far:
        result += path(f'M {ax-16},{ay-6} l 9,-2 M {ax+2},{ay-5} l 10,2', 'none', '#747480', 3)
        if elite:
            result += path(f'M {kx-12},{ky-7} L {kx+7},{ky-15} L {kx+18},{ky+6} '
                           f'L {kx-4},{ky+13} Z', '#767482')
    return result


def creature(elite=False, action='idle', phase=0):
    angle = phase*math.tau
    bob = (2-math.cos(angle*2)*2) if action == 'walk' else math.sin(angle)*1.8
    breathe = math.sin(angle)*.012 if action == 'idle' else 0
    crouch = lunge = mouth = lift = 0
    if action == 'attack':
        if phase < .30:
            crouch = math.sin(phase/.30*math.pi/2)*9
            mouth = phase/.30*.3
        elif phase < .60:
            power = math.sin((phase-.30)/.30*math.pi)
            lunge, lift, mouth = power*18, power*33, .65+power*.35
        else:
            recover = (1-phase)/.4
            mouth = max(0, recover)*.65
    body_y = bob+crouch
    root = f'<g id="pose" transform="translate({-lunge:.3f},0)">'
    parts = [root]
    hips = [(201, 218), (310, 217), (172, 211), (341, 216)]
    bases = [(192, 289), (308, 290), (154, 306), (359, 306)]
    limbs = []
    for index, (hip, foot) in enumerate(zip(hips, bases)):
        hx, hy = hip
        fx, fy = foot
        if action == 'walk':
            gait = angle + (math.pi if index in (0, 3) else 0)
            fx += math.cos(gait)*26
            fy -= max(0, math.sin(gait))*24
        if action == 'attack' and index in (0, 2):
            fx -= lunge*.65
            fy -= lift
        limbs.append(leg((hx, hy+body_y), (fx, fy), index in (1, 3), index < 2, elite))
    parts.append('<g id="far-legs">'+''.join(limbs[:2])+'</g>')
    tail_wave = math.sin(angle+1)*10
    parts.append('<g id="tail">'+path(
        f'M 347,{196+body_y} C 384,{181+body_y} 426,{191+tail_wave} 426,{151+tail_wave} '
        f'Q 430,{128+tail_wave} 445,{119+tail_wave} L 442,{147+tail_wave} '
        f'L 452,{139+tail_wave} Q 452,{173+tail_wave} 431,{193+tail_wave} '
        f'Q 394,{223+body_y} 353,{217+body_y} Z', '#393a46')+
        path(f'M 429,{151+tail_wave} L 444,{123+tail_wave} L 441,{146+tail_wave} L 447,{143+tail_wave} '
             f'L 439,{163+tail_wave} Z', '#777481', 'none')+'</g>')
    parts.append(f'<g id="torso" transform="translate(0,{body_y:.3f}) translate(265,213) scale(1,{1+breathe:.4f}) translate(-265,-213)">')
    parts.append(path('M 155,178 Q 165,146 206,141 Q 231,129 271,144 '
                      'Q 313,137 343,159 L 359,157 L 354,174 Q 375,196 355,223 '
                      'L 362,236 L 340,231 Q 318,249 295,236 L 278,246 L 270,235 '
                      'Q 241,248 211,231 L 199,240 L 194,222 Q 161,214 155,178 Z', '#484955'))
    parts.append(path('M 177,165 Q 217,139 253,151 Q 306,140 338,164 '
                      'L 325,177 Q 280,159 247,176 L 221,170 L 198,185 Z', '#646573', 'none'))
    parts.append(path('M 219,204 Q 246,216 274,207 L 300,217 L 322,205 '
                      'L 338,218 L 325,231 Q 308,241 288,228 L 276,237 L 267,224 '
                      'L 245,232 L 238,221 L 219,225 Z', '#2f303b', 'none'))
    parts.append(path('M 224,187 l 17,-8 l -5,11 l 16,-2 M 292,174 l 18,6 l -11,6 M 315,204 l 15,-8',
                      'none', '#747380', 3))
    for x, y, size in [(225, 142, 15), (255, 140, 19), (287, 144, 13)]:
        parts.append(path(f'M {x-10},{y+9} L {x+3},{y-size} L {x+16},{y+6} Z', '#95909b', width=3))
        parts.append(path(f'M {x+3},{y-size+5} L {x+6},{y+6} L {x-5},{y+7} Z', '#bbb2ae', 'none'))
    if elite:
        parts.append('<g id="shoulder-armor">'+path(
            'M 173,153 L 201,130 L 226,140 L 241,164 L 228,194 L 199,209 L 171,191 Z', '#66616f')+
            path('M 174,153 L 202,135 L 225,143 L 218,160 L 192,173 Z', '#8f8593', 'none')+
            path('M 198,174 L 211,164 L 205,184 L 218,177 L 206,201', 'none', '#bd86e7', 5)+
            path('M 195,137 L 201,113 L 217,133 M 222,145 L 239,126 L 235,159', '#a9a1ab', width=3)+'</g>')
        parts.append(path('M 270,159 l 11,19 l -12,12 l 17,7 l -5,18 M 318,180 l -7,15 l 13,13', 'none', '#a271cf', 4))
    parts.append('</g>')
    parts.append('<g id="near-legs">'+''.join(limbs[2:])+'</g>')
    parts.append(f'<g id="head" transform="translate({-lunge*.25:.3f},{body_y-lift*.11:.3f}) translate(125,180) scale(1.13) translate(-125,-180)">')
    # A jagged ruff overlaps the shoulders and legs, concealing the joints.
    parts.append(path('M 140,139 L 170,119 L 177,139 L 193,133 L 186,158 '
                      'L 203,156 L 190,178 L 200,193 L 180,194 L 177,218 '
                      'L 156,210 L 147,227 L 131,209 L 109,214 L 116,191 Z', '#373743'))
    parts.append(path('M 151,156 L 174,147 L 166,165 L 179,172 L 161,183 '
                      'L 166,201 L 149,197 L 143,212 L 132,193 Z', '#85818c', 'none'))
    parts.append(path('M 76,116 Q 69,90 77,65 Q 99,68 116,101 '
                      'Q 136,92 150,103 Q 155,73 174,67 Q 188,89 176,126 '
                      'L 187,142 L 175,147 L 186,163 L 172,173 '
                      'Q 166,192 147,200 L 128,195 L 105,204 L 86,192 '
                      'L 57,194 Q 39,186 45,168 L 66,154 Q 62,134 76,116 Z', '#50505e'))
    parts.append(path('M 80,78 Q 91,83 102,105 L 85,117 Z M 165,83 L 176,78 L 169,117 L 155,116 Z', '#91818e', width=2))
    parts.append(path('M 93,113 Q 126,96 151,114 L 171,138 L 150,142 '
                      'L 143,130 L 127,146 L 115,130 L 91,140 L 77,132 Z', '#73717f', 'none'))
    parts.append(path('M 91,143 L 111,154 L 96,166 L 76,159 Z', '#282631', width=3))
    parts.append(path('M 80,151 L 95,153 L 99,160 L 83,160 Z', '#e9b35b', 'none'))
    parts.append(path('M 87,152 l 2,9 l 3,-1 l -1,-9 Z', '#211e28', 'none'))
    parts.append(path('M 129,143 L 155,148 L 151,166 L 128,163 Z', '#282631', width=3))
    parts.append(path('M 132,152 L 152,153 L 148,163 L 132,160 Z', '#f1bf64', 'none'))
    parts.append(path('M 140,153 l 1,9 l 4,-1 l -1,-8 Z', '#211e28', 'none'))
    parts.append(path('M 77,142 L 99,145 M 128,136 L 154,142', 'none', '#302b36', 6))
    if mouth > .05:
        parts.append(path('M 51,185 Q 81,178 117,192 L 103,217 Q 70,219 53,197 Z', '#211722', width=3))
        parts.append(path('M 66,205 Q 82,197 101,205 L 98,214 L 72,214 Z', '#9b677e', 'none'))
    parts.append(f'<g id="lower-jaw" transform="rotate({-mouth*25:.3f},117,190)">'+
                 path('M 55,183 Q 78,187 110,183 L 125,194 Q 104,214 75,207 L 54,198 Z', '#8e8a94')+
                 path('M 59,194 Q 81,201 107,193', 'none', '#37313e', 3)+'</g>')
    parts.append(path('M 53,165 L 75,158 L 94,166 L 113,177 L 106,189 '
                      'Q 79,194 52,186 L 44,177 Z', '#a5a0a6'))
    parts.append(path('M 49,169 L 62,166 L 68,176 L 58,184 L 46,179 Z', '#25222d', width=3))
    parts.append(path('M 48,170 l 10,-1 l -3,4 Z', '#b1a4b0', 'none'))
    parts.append(path('M 66,187 Q 86,195 109,184', 'none', '#302934', 3))
    parts.append(path('M 78,189 L 84,202 L 90,191 Z M 101,188 L 106,198 L 110,184 Z', '#eee5d0', width=2))
    parts.append(path('M 145,175 L 165,166 L 159,184 L 144,194 L 131,184 Z', '#65616f', 'none'))
    parts.append(path('M 110,119 l 8,-8 M 159,178 l -9,7 M 96,122 l -7,6', 'none', '#908795', 3))
    if elite:
        parts.append('<g id="horns">'+path('M 98,110 Q 101,72 77,37 Q 116,44 130,87 L 121,113 Z', '#9f96a5')+
                     path('M 106,99 Q 111,69 89,46 Q 118,61 124,89 L 117,109 Z', '#d1c4c1', 'none')+
                     path('M 150,117 Q 178,80 169,44 Q 199,69 185,104 L 171,128 Z', '#8a8294')+
                     path('M 174,98 Q 185,76 175,56 L 184,82 L 180,107 Z', '#c0b2bf', 'none')+'</g>')
        parts.append(path('M 121,118 l 8,17 l -8,9 l 7,8 M 168,151 l -6,15 l 8,11', 'none', '#c695ec', 4))
    parts.extend(['</g>', '</g>'])
    return '<svg xmlns="http://www.w3.org/2000/svg" width="480" height="320" viewBox="0 0 480 320"><title>'+('精英獠兽' if elite else '普通獠兽')+'</title>'+''.join(parts)+'</svg>'


def render(svg, size):
    image = Image.open(io.BytesIO(resvg_py.svg_to_bytes(svg_string=svg, width=size[0]*4, height=size[1]*4,
                                                       skip_system_fonts=True))).convert('RGBA')
    image = image.resize(size, Image.Resampling.LANCZOS)
    pixels = np.array(image)
    pixels[pixels[:, :, 3] < 128] = 0
    pixels[pixels[:, :, 3] >= 128, 3] = 255
    return Image.fromarray(pixels)


def main():
    for name in ['svg', 'png', 'previews']:
        (ROOT / name).mkdir(exist_ok=True)
    for elite, name, size in [(False, 'normal', (96, 64)), (True, 'elite', (132, 88))]:
        svg = creature(elite)
        (ROOT / f'svg/{name}.svg').write_text(svg, encoding='utf-8')
        render(svg, size).save(ROOT / f'png/{name}.png')
    actions = {'idle': (12, .10), 'walk': (24, .035), 'attack': (12, .05)}
    for elite, prefix, size in [(False, '', (96, 64)), (True, 'elite/', (132, 88))]:
        for action, (count, duration) in actions.items():
            (ROOT / 'svg' / prefix / action).mkdir(parents=True, exist_ok=True)
            (ROOT / 'png' / prefix / action).mkdir(parents=True, exist_ok=True)
            for i in range(count):
                svg = creature(elite, action, i/count)
                (ROOT / f'svg/{prefix}{action}/{i:02}.svg').write_text(svg, encoding='utf-8')
                render(svg, size).save(ROOT / f'png/{prefix}{action}/{i:02}.png')
    (ROOT / 'manifest.json').write_text(json.dumps({
        'status': 'SVG source package; runtime integration is checked separately',
        'normal_canvas': [96, 64], 'elite_canvas': [132, 88],
        'actions': {name: {'frames': count, 'frame_seconds': duration} for name, (count, duration) in actions.items()},
        'source_format': 'Layered SVG with named body parts', 'runtime_files_modified': False,
    }, indent=2), encoding='utf-8')
    print('Rendered two creatures and 96 animation frames.')


if __name__ == '__main__':
    main()
