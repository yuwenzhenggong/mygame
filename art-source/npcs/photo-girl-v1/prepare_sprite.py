"""Prepare the supplied NPC art at the player's pixel scale and preview placement."""
import hashlib
import json
import shutil
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
ART = PROJECT / 'Assets/Resources/Art'
SOURCE = ROOT / 'source.png'
POSITION = (672, 310)


def main():
    if not SOURCE.exists():
        shutil.copy2(Path('D:/1游戏素材/拍照女站立帧.png'), SOURCE)
    original = np.array(Image.open(SOURCE).convert('RGBA'))
    candidate = original[:, :, 3] >= 128
    # Start in the face and retain the connected character, including diagonals.
    keep = np.zeros(candidate.shape, dtype=bool)
    seed = (550, 627)
    assert candidate[seed]
    keep[seed] = True
    queue = deque([seed])
    while queue:
        y, x = queue.popleft()
        for dy, dx in [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]:
            yy, xx = y+dy, x+dx
            if 0 <= yy < candidate.shape[0] and 0 <= xx < candidate.shape[1] and candidate[yy, xx] and not keep[yy, xx]:
                keep[yy, xx] = True
                queue.append((yy, xx))
    cleaned = original.copy()
    cleaned[~keep] = 0
    cleaned[keep, 3] = 255
    highres = Image.fromarray(cleaned)
    bounds = highres.getbbox()
    height = bounds[3]-bounds[1]
    width = round(height*48/64)
    center = (bounds[0]+bounds[2])/2
    left = round(center-width/2)
    crop = (left, bounds[1], left+width, bounds[3])
    sprite = highres.crop(crop).resize((48, 64), Image.Resampling.LANCZOS)
    pixels = np.array(sprite)
    pixels[pixels[:, :, 3] < 128] = 0
    pixels[pixels[:, :, 3] >= 128, 3] = 255
    sprite = Image.fromarray(pixels)
    sprite.save(ROOT / 'npc_photo_girl.png')
    assert sprite.getbbox()[3] == 64
    # These checks catch a matte accidentally filling the space between legs.
    assert any(sprite.getpixel((x, 60))[3] == 0 for x in range(20, 29))
    assert not pixels[pixels[:, :, 3] == 0].any()

    lines = (PROJECT / 'Assets/Resources/Data/scene_mask.txt').read_text(encoding='utf-8-sig').splitlines()
    mask = [line for line in lines if line and not line.startswith('//')]
    for dx in [-10, 0, 10]:
        for dy in [-5, 0, 5]:
            assert mask[int((POSITION[1]+dy)//16)][int((POSITION[0]+dx)//16)] != '#'

    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 21)
    hero = Image.open(ART / 'hero_front.png').convert('RGBA')
    comparison = Image.new('RGB', (600, 340), '#e8e2d7')
    draw = ImageDraw.Draw(comparison)
    for index, (image, label) in enumerate([(hero, '当前主角'), (sprite, '拍照女')]):
        big = image.resize((192, 256), Image.Resampling.NEAREST)
        comparison.paste(big, (index*300+54, 60), big)
        draw.text((index*300+108, 15), label, font=font, fill='#3e414d')
    comparison.save(ROOT / 'character-comparison.png')

    # Camera-sized crop of the actual city background; positions use map pixels.
    city = Image.open(ART / 'city_street.png').convert('RGB')
    crop_box = (420, 100, 1060, 460)
    view = city.crop(crop_box).resize((1280, 720), Image.Resampling.NEAREST)
    preview = Image.new('RGB', (1280, 780), '#262a36')
    preview.paste(view, (0, 60))
    for image, (x, y) in [(sprite, POSITION)]:
        big = image.resize((image.width*3, image.height*3), Image.Resampling.NEAREST)
        preview.paste(big, (round((x-crop_box[0])*2-big.width/2), round((y-crop_box[1])*2+60-big.height)), big)
    ImageDraw.Draw(preview).text((20, 16), '拍照女 · 售货机左侧位置预览（素材合成，非 Unity 录像）', font=font, fill='#eee9e2')
    preview.save(ROOT / 'city-placement-preview.png')
    report = {
        'source_sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
        'sprite_sha256': hashlib.sha256((ROOT / 'npc_photo_girl.png').read_bytes()).hexdigest(),
        'source_character_bounds': bounds, 'source_crop': crop, 'canvas': [48, 64],
        'removed_detached_pixels': int((candidate & ~keep).sum()),
        'name': '拍照女', 'scene': 'overworld', 'feet_image_position': POSITION,
        'pixels_per_unit': 32, 'character_scale': 1.5,
        'feet_area_walkable': True, 'preview_type': 'Offline composition',
    }
    (ROOT / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
