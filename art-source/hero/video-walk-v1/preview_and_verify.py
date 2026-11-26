"""Replay the installed assets (not Unity footage) and check asset integrity."""
import hashlib
import json
import re
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
ART = PROJECT / 'Assets/Resources/Art'
PREVIEWS = ROOT / 'previews'
FONT = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 21)
SMALL = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 15)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save_gif(frames, durations, name):
    # Shared palette avoids flickering colors between GIF frames.
    atlas = Image.new('RGB', (frames[0].width, frames[0].height * len(frames)))
    for i, frame in enumerate(frames):
        atlas.paste(frame, (0, i * frame.height))
    palette = atlas.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    encoded = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in frames]
    encoded[0].save(PREVIEWS / name, save_all=True, append_images=encoded[1:],
                    duration=durations, loop=0, disposal=2, optimize=False)


def main():
    installed = sorted(ART.glob('hero_run_down_*.png'), key=lambda p: int(p.stem.rsplit('_', 1)[1]))
    assert [p.name for p in installed] == [f'hero_run_down_{i}.png' for i in range(12)]
    reference_hashes = json.loads((ROOT / 'unchanged-reference-hashes.json').read_text())
    assert all(digest(ART / name) == value for name, value in reference_hashes.items())
    checks = []
    sprites = []
    for path in installed:
        image = Image.open(path)
        assert image.mode == 'RGBA' and image.size == (48, 64)
        assert digest(path) == digest(ROOT / 'frames' / path.name)
        rgba = np.array(image).astype(int)
        alpha = rgba[:, :, 3]
        assert set(np.unique(alpha)) == {0, 255}
        assert not np.any(rgba[alpha == 0])
        green = rgba[:, :, 1] - np.maximum(rgba[:, :, 0], rgba[:, :, 2])
        # No clearly green grass pixels survived in the visible sprite.
        assert not np.any((green > 8) & (alpha > 0))
        meta = path.with_suffix('.png.meta').read_text()
        for setting in ('spritePixelsToUnits: 32', 'filterMode: 0', 'enableMipMap: 0',
                        'textureCompression: 0', 'alphaIsTransparency: 1'):
            assert setting in meta
        if int(path.stem.rsplit('_', 1)[1]) < 10:
            old_meta = (ROOT / 'backup-runtime' / (path.name + '.meta')).read_text()
            assert re.search(r'guid: (\w+)', meta)[1] == re.search(r'guid: (\w+)', old_meta)[1]
        checks.append({'name': path.name, 'sha256': digest(path), 'bounds': image.getbbox(),
                       'visible_pixels': int((alpha > 0).sum()),
                       'maximum_green_excess': int(green[alpha > 0].max())})
        sprites.append(image)
    assert len({item['sha256'] for item in checks}) == 12
    arrays = [np.array(s).astype(float) for s in sprites]
    distances = [float(np.abs(arrays[(i+1) % 12] - arrays[i]).mean()) for i in range(12)]
    # The closing transition should be comparable to normal adjacent steps.
    assert distances[-1] < max(distances[:-1]) * 1.6
    source = (PROJECT / 'Assets/Scripts/World/PlayerWalker.cs').read_text(encoding='utf-8-sig')
    assert 'WalkFrameDuration = 0.07f' in source
    assert '_body.flipX = useRun && !_faceLeft' in source
    loader = (PROJECT / 'Assets/Scripts/World/WorldRoot.cs').read_text(encoding='utf-8-sig')
    assert 'walker.SetWalkDown(runFrames.ToArray())' in loader

    idle = Image.open(ART / 'hero_front.png').convert('RGBA')
    attacks = [Image.open(ART / f'hero_combo_{i}.png').convert('RGBA') for i in range(5)]
    backdrop = Image.open(ART / 'safehouse.png').convert('RGB')
    mask = [s for s in (PROJECT / 'Assets/Resources/Data/safehouse_mask.txt').read_text(encoding='utf-8-sig').splitlines()
            if s and not s.startswith('//')]

    x = 400.0
    y = 300
    sequence = [('站立', idle, x, False, 420)]
    for mirrored, label, direction in [(False, '朝左行走', -1), (True, '朝右行走', 1)]:
        for sprite in sprites:
            x += direction * 5.5 * 32 * 0.07
            # Same foot collision extents as PlayerWalker, in map image pixels.
            for dx in [-0.28 * 32, 0.28 * 32]:
                for dy in [-0.16 * 32, 0.16 * 32]:
                    assert mask[int((y + dy) / 16)][int((x + dx) / 16)] != '#'
            sequence.append((label, sprite, x, mirrored, 70))
        sequence.append(('停下 · 回到站立', idle, x, mirrored, 280))
    sequence += [('攻击 · 第一段', sprite, x, True, 80) for sprite in attacks[:3]]
    sequence.append(('站立', idle, x, True, 120))
    sequence += [('攻击 · 第二段', sprite, x, True, 80) for sprite in attacks[3:]]
    sequence.append(('站立', idle, x, False, 630))

    demo, durations = [], []
    for label, sprite, x, flip, duration in sequence:
        # 1.5x character scale and 1.5x display of the 640x360 game background.
        frame = Image.new('RGB', (960, 600), '#20232e')
        frame.paste(backdrop.resize((960, 540), Image.Resampling.NEAREST), (0, 60))
        if flip:
            sprite = ImageOps.mirror(sprite)
        big = sprite.resize((round(sprite.width * 2.25), round(sprite.height * 2.25)), Image.Resampling.NEAREST)
        frame.paste(big, (round(x * 1.5 - big.width / 2), round(y * 1.5 + 60 - big.height)), big)
        draw = ImageDraw.Draw(frame)
        draw.text((18, 6), '已绑定素材演示：' + label, font=FONT, fill='white')
        draw.text((18, 35), '游戏地图 + 当前角色素材合成预览（非 Unity 实机录像）', font=SMALL, fill='#b8becf')
        demo.append(frame)
        durations.append(duration)
    save_gif(demo, durations, 'room-walk-demo.gif')
    demo[6].save(PREVIEWS / 'room-walk-still.png')

    comparison = []
    for i, sprite in enumerate(sprites):
        frame = Image.new('RGB', (720, 312), '#e8e3d9')
        draw = ImageDraw.Draw(frame)
        for column, (art, label) in enumerate([(idle, '当前站立'), (sprite, '视频提取 · 行走'), (attacks[i % 5], '当前攻击')]):
            big = art.resize((art.width*4, art.height*4), Image.Resampling.NEAREST)
            frame.paste(big, (column * 240 + (240-big.width)//2, 300-big.height), big)
            draw.text((column*240+15, 9), label, font=FONT, fill='#3a3b44')
        comparison.append(frame)
    save_gif(comparison, [70]*12, 'idle-walk-attack.gif')

    report = {'passed': True, 'frames': checks, 'adjacent_frame_mean_difference': distances,
              'idle_and_attack_unchanged': True, 'original_guids_preserved': True,
              'cycle_seconds': 0.84, 'demonstration': 'Offline asset composite, not engine footage',
              'unity_runtime_verified': False}
    (ROOT / 'validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
