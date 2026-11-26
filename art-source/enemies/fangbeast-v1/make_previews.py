"""Make review boards and validate art without changing the live game."""
import hashlib
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
ART = PROJECT / 'Assets/Resources/Art'
PREVIEWS = ROOT / 'previews'
FONT = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 25)
SMALL = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 18)
NORMAL_SCALE = 0.7
ELITE_SCALE = 0.7


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def paste_sprite(canvas, sprite, center, baseline, scale):
    big = sprite.resize((round(sprite.width*scale), round(sprite.height*scale)), Image.Resampling.NEAREST)
    canvas.paste(big, (round(center-big.width/2), round(baseline-big.height)), big)


def save_gif(frames, durations, filename):
    atlas = Image.new('RGB', (frames[0].width, frames[0].height*len(frames)))
    for i, frame in enumerate(frames):
        atlas.paste(frame, (0, i*frame.height))
    palette = atlas.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    encoded = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    encoded[0].save(PREVIEWS / filename, save_all=True, append_images=encoded[1:],
                    duration=durations, loop=0, disposal=2, optimize=False)


def main():
    baseline_path = ROOT / 'game-baseline.json'
    tracked = [ART / 'hero_front.png', ART / 'enemy_attacker.png', ART / 'enemy_beast.png', ART / 'enemy_elite.png',
               PROJECT / 'Assets/Resources/Data/enemies.json', PROJECT / 'Assets/Scripts/World/Enemy.cs']
    baseline = {str(path.relative_to(PROJECT)): digest(path) for path in tracked}
    integrated = (ROOT / 'integration.json').exists()
    expected = json.loads((ROOT / 'integration.json').read_text())['installed_sha256'] if integrated else None
    if baseline_path.exists():
        assert baseline == (expected if integrated else json.loads(baseline_path.read_text()))
    else:
        baseline_path.write_text(json.dumps(baseline, indent=2), encoding='utf-8')
    hero = Image.open(ART / 'hero_front.png').convert('RGBA')
    normal = Image.open(ROOT / 'png/normal.png').convert('RGBA')
    elite = Image.open(ROOT / 'png/elite.png').convert('RGBA')

    board = Image.new('RGB', (1200, 470), '#e8e2d7')
    draw = ImageDraw.Draw(board)
    draw.text((30, 20), '丛林獠兽 · SVG 造型初稿', font=FONT, fill='#303441')
    draw.text((30, 61), '同一比例展示：主角 / 普通异兽 / 精英异兽', font=SMALL, fill='#686573')
    draw.line((35, 402, 1165, 402), fill='#c3bcb3', width=2)
    for sprite, center, label in [(hero, 150, '当前主角'), (normal, 470, '普通獠兽'), (elite, 930, '精英獠兽')]:
        paste_sprite(board, sprite, center, 400, 3*(NORMAL_SCALE if sprite is normal else ELITE_SCALE if sprite is elite else 1))
        draw.text((center-54, 424), label, font=SMALL, fill='#454653')
    board.save(PREVIEWS / 'lineup.png')

    scene = Image.open(ART / 'jungle_arena.png').convert('RGB').resize((1280, 720), Image.Resampling.NEAREST)
    map_board = Image.new('RGB', (1280, 780), '#222632')
    map_board.paste(scene, (0, 60))
    draw = ImageDraw.Draw(map_board)
    draw.text((20, 16), '丛林配置预览：3 只普通异兽 + 1 只精英异兽（素材合成，非实机录像）', font=FONT, fill='#ece7e2')
    scale = 1280/960
    for sprite, x, y in [(normal, 296, 200), (normal, 648, 216), (normal, 300, 380),
                         (elite, 680, 328), (hero, 488, 280)]:
        paste_sprite(map_board, sprite, x*scale, y*scale+60, 1.5*scale*(NORMAL_SCALE if sprite is normal else ELITE_SCALE if sprite is elite else 1))
    map_board.save(PREVIEWS / 'jungle-layout.png')

    actions = {}
    for name in ['idle', 'walk', 'attack']:
        actions[name] = [Image.open(path).convert('RGBA') for path in sorted((ROOT / 'png' / name).glob('*.png'))]
    labels = {'idle': '待机 · 呼吸与摆尾', 'walk': '行走 · 四足交替', 'attack': '攻击 · 蓄力与扑咬'}
    # A 2.4 second shared timeline shows the three actions independently.
    movie = []
    for i in range(144):
        time = i/60
        frame = Image.new('RGB', (1080, 310), '#e8e2d7')
        draw = ImageDraw.Draw(frame)
        for column, (name, duration) in enumerate([('idle', .1), ('walk', .035), ('attack', .05)]):
            sprite = actions[name][int(time/duration) % len(actions[name])]
            paste_sprite(frame, sprite, column*360+180, 285, 3*NORMAL_SCALE)
            draw.text((column*360+45, 17), labels[name], font=SMALL, fill='#414450')
        movie.append(frame)
    # GIF works best at 30 fps, with a shared palette and rounded total timing.
    save_gif(movie[::2], [30, 30, 40]*24, 'normal-actions.gif')

    walk_frames = []
    for sprite in actions['walk']:
        frame = Image.new('RGB', (384, 288), '#303441')
        paste_sprite(frame, sprite, 192, 264, 3.5*NORMAL_SCALE)
        walk_frames.append(frame)
    save_gif(walk_frames, [30, 40]*12, 'normal-walk.gif')

    elite_walk = [Image.open(path).convert('RGBA') for path in sorted((ROOT / 'png/elite/walk').glob('*.png'))]
    paired = []
    for i in range(72):
        time = i*.035
        frame = Image.new('RGB', (1000, 450), '#e8e2d7')
        draw = ImageDraw.Draw(frame)
        draw.text((160, 18), '普通獠兽 · 行走', font=FONT, fill='#414450')
        draw.text((630, 18), '精英獠兽 · 行走', font=FONT, fill='#414450')
        paste_sprite(frame, actions['walk'][i % 24], 250, 420, 4*NORMAL_SCALE)
        paste_sprite(frame, elite_walk[int(time/.0525) % 24], 730, 420, 4*ELITE_SCALE)
        paired.append(frame)
    save_gif(paired, [30, 40]*36, 'beasts-walk.gif')

    # Inspect all source vectors and exported alpha, including seam continuity.
    pngs = list((ROOT / 'png').rglob('*.png'))
    bounds = []
    for path in pngs:
        image = Image.open(path)
        assert image.mode == 'RGBA'
        pixels = np.array(image)
        assert set(np.unique(pixels[:, :, 3])) == {0, 255}
        assert not pixels[pixels[:, :, 3] == 0].any()
        box = image.getbbox()
        assert box[0] > 0 and box[2] < image.width and box[1] > 0
        bounds.append({'name': str(path.relative_to(ROOT)), 'size': image.size, 'bounds': box})
    for path in (ROOT / 'svg').rglob('*.svg'):
        xml = ET.parse(path)
        ids = {node.attrib.get('id') for node in xml.iter()}
        assert {'far-legs', 'near-legs', 'torso', 'head', 'tail', 'lower-jaw'} <= ids
    walk = [np.array(image).astype(float) for image in actions['walk']]
    differences = [float(np.abs(walk[(i+1) % 24]-image).mean()) for i, image in enumerate(walk)]
    assert len({image.tobytes() for image in walk}) == 24
    assert differences[-1] < max(differences[:-1])*1.6
    current = {str(path.relative_to(PROJECT)): digest(path) for path in tracked}
    assert current == baseline
    report = {'passed': True, 'png_files': len(pngs), 'svg_files': len(list((ROOT / 'svg').rglob('*.svg'))),
              'normal_walk_unique_frames': 24, 'walk_cycle_seconds': .84,
              'walk_internal_max_difference': max(differences[:-1]), 'walk_seam_difference': differences[-1],
              'runtime_snapshot_matches_expected': True, 'integrated_in_game': integrated, 'bounds': bounds}
    (ROOT / 'validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({key: value for key, value in report.items() if key != 'bounds'}, indent=2))


if __name__ == '__main__':
    main()
