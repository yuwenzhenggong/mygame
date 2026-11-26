"""Resample the approved 20-source-frame walk cycle into 24 motion-compensated frames."""
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
PREVIOUS = ROOT.parent / 'video-walk-v1'
PROJECT = ROOT.parents[2]
ART = PROJECT / 'Assets/Resources/Art'
sys.path.insert(0, str(PREVIOUS))
from extract_walk import cutout
import imageio_ffmpeg


def main():
    for folder in ['backup-runtime', 'frames', 'cutouts', 'previews']:
        (ROOT / folder).mkdir(exist_ok=True)
    # Snapshot only once, so regeneration cannot overwrite the previous version.
    if not (ROOT / 'baseline.json').exists():
        for path in ART.glob('hero_run_down_*.png*'):
            shutil.copy2(path, ROOT / 'backup-runtime' / path.name)
        shutil.copy2(PROJECT / 'Assets/Scripts/World/PlayerWalker.cs', ROOT / 'backup-runtime/PlayerWalker.cs')
        names = ['hero_front.png'] + [f'hero_combo_{i}.png' for i in range(5)]
        (ROOT / 'baseline.json').write_text(json.dumps({
            'reference_hashes': {name: hashlib.sha256((ART / name).read_bytes()).hexdigest() for name in names}
        }, indent=2), encoding='utf-8')

    ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
    video = PREVIOUS / 'source.mp4'
    raw = subprocess.run([
        ffmpeg, '-v', 'error', '-i', str(video),
        '-vf', "select='between(n,88,107)'", '-fps_mode', 'passthrough',
        '-f', 'rawvideo', '-pix_fmt', 'rgb24', 'pipe:1',
    ], stdout=subprocess.PIPE, check=True).stdout
    source = np.frombuffer(raw, np.uint8).reshape(-1, 720, 720, 3)
    assert len(source) == 20
    source_hashes = [hashlib.sha256(frame.tobytes()).hexdigest() for frame in source]
    assert len(set(source_hashes)) == 20
    print('Decoded all 20 unique source frames; interpolating cyclic motion...', flush=True)
    # Repeat the cycle to give the motion estimator real neighbours at BOTH
    # sides of the seam. Retain only the middle cycle, never duplicate/hold frames.
    interpolated = subprocess.run([
        ffmpeg, '-v', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24',
        '-s', '720x720', '-r', '24', '-i', 'pipe:0', '-vf',
        'minterpolate=fps=144/5:mi_mode=mci:mc_mode=aobmc:me_mode=bidir:vsbmc=1:scd=none',
        '-f', 'rawvideo', '-pix_fmt', 'rgb24', 'pipe:1',
    ], input=np.tile(source, (3, 1, 1, 1)).tobytes(), stdout=subprocess.PIPE, check=True).stdout
    frames = np.frombuffer(interpolated, np.uint8).reshape(-1, 720, 720, 3)[24:48]
    assert len(frames) == 24
    # Keep precisely the same registration and scale as the installed v1.
    crop = json.loads((PREVIOUS / 'manifest.json').read_text())['common_source_crop']
    sprites = []
    for i, rgb in enumerate(frames):
        cut = cutout(rgb)
        cut.save(ROOT / 'cutouts' / f'{i:02}.png')
        pixels = np.array(cut.crop(crop).resize((48, 64), Image.Resampling.LANCZOS))
        pixels[pixels[:, :, 3] < 128] = 0
        pixels[pixels[:, :, 3] >= 128, 3] = 255
        sprite = Image.fromarray(pixels)
        sprite.save(ROOT / 'frames' / f'hero_run_down_{i}.png')
        sprites.append(sprite)

    sheet = Image.new('RGB', (8*144, 3*210), '#33343e')
    draw = ImageDraw.Draw(sheet)
    for i, sprite in enumerate(sprites):
        x, y = (i % 8)*144, (i // 8)*210+18
        big = sprite.resize((144, 192), Image.Resampling.NEAREST)
        sheet.paste(big, (x, y), big)
        draw.text((x+6, y-16), f'{i+1:02} / 24', fill='white')
    sheet.save(ROOT / 'previews/contact-sheet.png')
    manifest = {
        'source': '../video-walk-v1/source.mp4',
        'source_sha256': hashlib.sha256(video.read_bytes()).hexdigest(),
        'source_fps': 24, 'source_cycle': [88, 108],
        'unique_source_frames': 20, 'output_frames': 24,
        'interpolation': 'FFmpeg motion compensated AOBMC, bidirectional, cyclic context',
        'resampled_source_positions': [88+i*20/24 for i in range(24)],
        'duration_per_frame': 0.035, 'cycle_seconds': 0.84,
        'canvas': [48, 64], 'common_source_crop': crop, 'pixels_per_unit': 32,
    }
    (ROOT / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print('Generated 24 frames, 35 ms each, fixed 48x64 registration.', flush=True)


if __name__ == '__main__':
    main()
