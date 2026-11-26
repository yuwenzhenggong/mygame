"""Extract one approved walk cycle; requires Pillow, NumPy and imageio_ffmpeg."""
import hashlib
import json
import shutil
import subprocess
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
ART = PROJECT / 'Assets/Resources/Art'
VIDEO = ROOT / 'source.mp4'
PYTHON_TOOLS = Path.home() / '.codex/tmp/video-tools'
sys.path.insert(0, str(PYTHON_TOOLS))
import imageio_ffmpeg


def cutout(rgb):
    color = rgb.astype(np.int16)
    red, green, blue = color.transpose(2, 0, 1)
    # Grass and its shadow remain green; the character has neutral/blue blacks.
    vegetation = (green - np.maximum(red, blue) > 4) & (green > red * 1.05)
    candidate = ~vegetation
    candidate[:65] = False
    candidate[645:] = False
    candidate[:, :160] = False
    candidate[:, 560:] = False
    # Keep only the connected character, removing isolated compression noise.
    keep = np.zeros(candidate.shape, dtype=bool)
    queue = deque([(320, 360)])
    keep[320, 360] = True
    while queue:
        y, x = queue.popleft()
        for yy, xx in ((y-1, x), (y+1, x), (y, x-1), (y, x+1)):
            if 0 <= yy < 720 and 0 <= xx < 720 and candidate[yy, xx] and not keep[yy, xx]:
                keep[yy, xx] = True
                queue.append((yy, xx))
    # Trim one source pixel (about 0.12 final pixels) of mixed grass edge.
    keep = np.array(Image.fromarray(keep.astype(np.uint8) * 255)
                    .filter(ImageFilter.MinFilter(3))) > 0
    result = np.dstack((rgb.copy(), keep.astype(np.uint8) * 255))
    result[~keep] = 0
    return Image.fromarray(result)


def main():
    if not VIDEO.exists():
        shutil.copy2(Path.home() / 'Downloads/生成指定条件的视频.mp4', VIDEO)
    for name in ('backup-runtime', 'source-frames', 'cutouts', 'frames', 'previews'):
        (ROOT / name).mkdir(exist_ok=True)
    for source in ART.glob('hero_run_down_*.png*'):
        backup = ROOT / 'backup-runtime' / source.name
        if not backup.exists():
            shutil.copy2(source, backup)

    indices = [88 + int(i * 20 / 12 + 0.5) for i in range(12)]
    raw = subprocess.run([
        imageio_ffmpeg.get_ffmpeg_exe(), '-v', 'error', '-i', str(VIDEO),
        '-vf', "select='between(n,88,108)'", '-fps_mode', 'passthrough',
        '-f', 'rawvideo', '-pix_fmt', 'rgb24', 'pipe:1',
    ], stdout=subprocess.PIPE, check=True).stdout
    decoded = np.frombuffer(raw, np.uint8).reshape(-1, 720, 720, 3)
    sprites = []
    for i, source_index in enumerate(indices):
        rgb = decoded[source_index - 88]
        Image.fromarray(rgb).save(ROOT / 'source-frames' / f'{source_index:03}.png')
        sprite = cutout(rgb)
        sprite.save(ROOT / 'cutouts' / f'{i:02}.png')
        sprites.append(sprite)

    bounds = [s.getbbox() for s in sprites]
    top = min(b[1] for b in bounds)
    bottom = max(b[3] for b in bounds)
    height = bottom - top
    # One fixed crop/scale for the whole cycle: keep body bounce and foot motion.
    width = round(height * 48 / 64)
    crop = (360 - width // 2, top, 360 - width // 2 + width, bottom)
    finals = []
    for i, sprite in enumerate(sprites):
        final = sprite.crop(crop).resize((48, 64), Image.Resampling.LANCZOS)
        # Quantize transparency for crisp point-filtered sprites; premultiplied
        # alpha during Pillow's resize prevents green/dark matte fringes.
        pixels = np.array(final)
        pixels[pixels[:, :, 3] < 128] = 0
        pixels[pixels[:, :, 3] >= 128, 3] = 255
        final = Image.fromarray(pixels)
        final.save(ROOT / 'frames' / f'hero_run_down_{i}.png')
        finals.append(final)

    sheet = Image.new('RGB', (6 * 176, 2 * 224), '#33343e')
    draw = ImageDraw.Draw(sheet)
    for i, sprite in enumerate(finals):
        x, y = (i % 6) * 176 + 16, (i // 6) * 224 + 22
        sheet.paste(sprite.resize((144, 192), Image.Resampling.NEAREST), (x, y),
                    sprite.resize((144, 192), Image.Resampling.NEAREST))
        draw.text((x, y-16), f'{i+1:02} / video {indices[i]}', fill='white')
    sheet.save(ROOT / 'previews/contact-sheet.png')
    for bg_name, color in [('dark', '#292c39'), ('light', '#eee8dc')]:
        preview = []
        for sprite in finals:
            canvas = Image.new('RGB', (240, 288), color)
            big = sprite.resize((192, 256), Image.Resampling.NEAREST)
            canvas.paste(big, (24, 16), big)
            preview.append(canvas)
        preview[0].save(ROOT / f'previews/walk-{bg_name}.gif', save_all=True,
                        append_images=preview[1:], loop=0, duration=70, disposal=2)

    manifest = {
        'source_sha256': hashlib.sha256(VIDEO.read_bytes()).hexdigest(),
        'source_fps': 24, 'cycle_start_frame': 88, 'cycle_end_exclusive': 108,
        'selected_source_frames': indices, 'duration_per_frame': 0.07,
        'cycle_seconds': 0.84, 'canvas': [48, 64], 'common_source_crop': crop,
        'source_bounds': bounds, 'pixels_per_unit': 32,
        'left_uses_original': True, 'right_uses_flipX': True,
    }
    (ROOT / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    main()
