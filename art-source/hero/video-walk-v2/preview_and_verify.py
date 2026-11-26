"""Validate installed sprites; render a 60 FPS offline replay and 12/24 frame comparison."""
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
ART = PROJECT / 'Assets/Resources/Art'
PREVIEWS = ROOT / 'previews'
sys.path.insert(0, str(Path.home() / '.codex/tmp/video-tools'))
import imageio_ffmpeg

FONT = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 21)
SMALL = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 15)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def movement_difference(sprites):
    frames = [np.array(image).astype(float) for image in sprites]
    return [float(np.abs(frames[(i+1) % len(frames)] - frame).mean()) for i, frame in enumerate(frames)]


def main():
    paths = sorted(ART.glob('hero_run_down_*.png'), key=lambda p: int(p.stem.rsplit('_', 1)[1]))
    assert [path.name for path in paths] == [f'hero_run_down_{i}.png' for i in range(24)]
    baseline = json.loads((ROOT / 'baseline.json').read_text())
    assert all(digest(ART / name) == value for name, value in baseline['reference_hashes'].items())
    sprites, checks, guids = [], [], []
    for i, path in enumerate(paths):
        image = Image.open(path)
        assert image.mode == 'RGBA' and image.size == (48, 64)
        assert digest(path) == digest(ROOT / 'frames' / path.name)
        pixels = np.array(image).astype(int)
        visible = pixels[:, :, 3] > 0
        assert set(np.unique(pixels[:, :, 3])) == {0, 255}
        assert not pixels[~visible].any()
        green = pixels[:, :, 1] - np.maximum(pixels[:, :, 0], pixels[:, :, 2])
        assert not ((green > 8) & visible).any()
        meta = path.with_suffix('.png.meta').read_text()
        for setting in ['spritePixelsToUnits: 32', 'filterMode: 0', 'enableMipMap: 0', 'textureCompression: 0']:
            assert setting in meta
        guid = re.search(r'guid: (\w+)', meta)[1]
        guids.append(guid)
        if i < 12:
            assert guid == re.search(r'guid: (\w+)', (ROOT / 'backup-runtime' / (path.name+'.meta')).read_text())[1]
        sprites.append(image)
        checks.append({'name': path.name, 'sha256': digest(path), 'bounds': image.getbbox()})
    assert len(set(guids)) == 24
    assert len({item['sha256'] for item in checks}) == 24
    old = [Image.open(ROOT / 'backup-runtime' / f'hero_run_down_{i}.png').convert('RGBA') for i in range(12)]
    before, after = movement_difference(old), movement_difference(sprites)
    assert after[-1] < max(after[:-1]) * 1.6
    assert np.mean(after) < np.mean(before)

    # GIF stores centiseconds: alternate 30/40 ms so 24 frames still last 840 ms.
    comparison = []
    for i in range(24):
        frame = Image.new('RGB', (480, 300), '#e8e3d9')
        draw = ImageDraw.Draw(frame)
        for column, (sprite, label) in enumerate([(old[i//2], '之前：12 帧'), (sprites[i], '现在：24 帧')]):
            big = sprite.resize((192, 256), Image.Resampling.NEAREST)
            frame.paste(big, (column*240+24, 42), big)
            draw.text((column*240+40, 8), label, font=FONT, fill='#343643')
        comparison.append(frame)
    atlas = Image.new('RGB', (480, 300*24))
    for i, frame in enumerate(comparison):
        atlas.paste(frame, (0, i*300))
    palette = atlas.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    encoded = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in comparison]
    encoded[0].save(PREVIEWS / '12-vs-24.gif', save_all=True, append_images=encoded[1:],
                    duration=[30, 40]*12, loop=0, disposal=2, optimize=False)

    idle = Image.open(ART / 'hero_front.png').convert('RGBA')
    attacks = [Image.open(ART / f'hero_combo_{i}.png').convert('RGBA') for i in range(5)]
    backdrop = Image.open(ART / 'safehouse.png').convert('RGB').resize((960, 540), Image.Resampling.NEAREST)
    # Root position advances every video frame, independently of sprite changes,
    # as PlayerWalker.Update does in the actual game.
    stages = [(.42, '站立', 'idle'), (.84, '朝左行走', 'left'), (.28, '停下', 'idle'),
              (.84, '朝右行走', 'right'), (.28, '停下', 'idle'), (.24, '攻击 · 第一段', 'attack1'),
              (.12, '站立', 'idle'), (.16, '攻击 · 第二段', 'attack2'), (.63, '站立', 'idle')]
    encoder_log = open(ROOT / 'preview-encode.log', 'w')
    encoder = subprocess.Popen([
        imageio_ffmpeg.get_ffmpeg_exe(), '-y', '-v', 'error',
        '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', '960x600', '-r', '60', '-i', 'pipe:0',
        '-an', '-c:v', 'libx264', '-crf', '18', '-preset', 'fast', '-pix_fmt', 'yuv420p',
        '-movflags', '+faststart', str(PREVIEWS / 'room-walk-24-60fps.mp4'),
    ], stdin=subprocess.PIPE, stderr=encoder_log)
    count = int(np.ceil(sum(stage[0] for stage in stages)*60))
    for index in range(count):
        elapsed = index/60
        for duration, label, kind in stages:
            if elapsed < duration:
                break
            elapsed -= duration
        sprite, flip, x = idle, False, 400.0
        if kind == 'left':
            x = 400 - elapsed*176
            sprite = sprites[int(elapsed/.035) % 24]
        elif kind == 'right':
            x = 400 - .84*176 + elapsed*176
            sprite = sprites[int(elapsed/.035) % 24]
            flip = True
        elif kind.startswith('attack'):
            sprite = attacks[min(int(elapsed/.08), 2)] if kind == 'attack1' else attacks[3+min(int(elapsed/.08), 1)]
            flip = True
        elif index/60 < .42+.84+.28 and index/60 >= .42+.84:
            x = 400-.84*176
        elif index/60 >= .42+.84+.28+.84:
            flip = True
        frame = Image.new('RGB', (960, 600), '#20232e')
        frame.paste(backdrop, (0, 60))
        if flip:
            sprite = ImageOps.mirror(sprite)
        big = sprite.resize((round(sprite.width*2.25), round(sprite.height*2.25)), Image.Resampling.NEAREST)
        frame.paste(big, (round(x*1.5-big.width/2), 510-big.height), big)
        draw = ImageDraw.Draw(frame)
        draw.text((18, 6), '24 帧步行 · '+label, font=FONT, fill='white')
        draw.text((18, 35), '0.84 秒/周期 · 60 FPS 合成预览（非 Unity 实机录像）', font=SMALL, fill='#b8becf')
        encoder.stdin.write(frame.tobytes())
        if index == 48:
            frame.save(PREVIEWS / 'room-walk-still.png')
    encoder.stdin.close()
    assert encoder.wait() == 0
    encoder_log.close()
    report = {
        'passed': True, 'frame_count': 24, 'unique_frame_count': 24, 'frames': checks,
        'duration_per_frame': .035, 'cycle_seconds': .84,
        'previous_mean_pixel_step': float(np.mean(before)), 'current_mean_pixel_step': float(np.mean(after)),
        'loop_seam_pixel_step': after[-1], 'maximum_internal_pixel_step': max(after[:-1]),
        'reference_assets_unchanged': True, 'original_12_guids_preserved': True,
        'preview_fps': 60, 'preview_frames': count, 'unity_runtime_verified': False,
    }
    (ROOT / 'validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({key: value for key, value in report.items() if key != 'frames'}, indent=2))


if __name__ == '__main__':
    main()
