"""Remove the cyan matte and weak edge speckles from the installed idle sprite."""
import argparse
import hashlib
import json
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
ART = ROOT.parents[2] / 'Assets/Resources/Art'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    source = ROOT / 'hero_front.before.png'
    assert source.exists(), 'Create the original sprite and metadata backups first.'
    original = np.array(Image.open(source).convert('RGBA'))
    color = original.astype(int)
    r, g, b, alpha = color.transpose(2, 0, 1)
    # Cyan/green matte has green above red, unlike the warm gray character.
    cyan = (g-r > 4) & (g > b*.70) & (alpha > 0)
    keep = (alpha >= 128) & ~cyan
    # Preserve diagonal pixel-art details; discard only detached fragments.
    connected = np.zeros(keep.shape, dtype=bool)
    queue = deque([(25, 24)])
    connected[25, 24] = True
    while queue:
        y, x = queue.popleft()
        for dy, dx in [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]:
            yy, xx = y+dy, x+dx
            if 0 <= yy < 64 and 0 <= xx < 48 and keep[yy, xx] and not connected[yy, xx]:
                connected[yy, xx] = True
                queue.append((yy, xx))
    cleaned = original.copy()
    cleaned[~connected] = 0
    cleaned[connected, 3] = 255
    # The old cyan floor line occupied the last row. Align the real soles with
    # the canvas bottom after removing it; do not resize or repaint the body.
    bottom = np.nonzero(connected)[0].max()
    shift_y = 63-int(bottom)
    result = np.zeros_like(cleaned)
    result[shift_y:] = cleaned[:64-shift_y]
    output = Image.fromarray(result)
    output.save(ROOT / 'hero_front.cleaned.png')
    assert output.size == (48, 64) and output.getbbox()[3] == 64
    assert not ((result[:, :, 1].astype(int)-result[:, :, 0].astype(int) > 4)
                & (result[:, :, 3] > 0)).any()
    # Retained visible RGB pixels are byte-identical, apart from the translation.
    assert np.array_equal(cleaned[connected, :3], original[connected, :3])

    preview = Image.new('RGB', (600, 680))
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 19)
    for row, (background, foreground) in enumerate([('#ece6db', '#353642'), ('#303441', '#e5e7f0')]):
        for column, (sprite, label) in enumerate([(Image.fromarray(original), '清理前'), (output, '清理后')]):
            panel = Image.new('RGB', (300, 340), background)
            draw = ImageDraw.Draw(panel)
            draw.text((113, 12), label, font=font, fill=foreground)
            big = sprite.resize((192, 256), Image.Resampling.NEAREST)
            panel.paste(big, (54, 70), big)
            preview.paste(panel, (column*300, row*340))
    preview.save(ROOT / 'before-after.png')

    other_names = [f'hero_run_down_{i}.png' for i in range(24)] + [f'hero_combo_{i}.png' for i in range(5)]
    unaffected = {name: digest(ART / name) for name in other_names}
    meta_hash = digest(ART / 'hero_front.png.meta')
    if args.apply:
        output.save(ART / 'hero_front.png')
        assert digest(ART / 'hero_front.png') == digest(ROOT / 'hero_front.cleaned.png')
        assert digest(ART / 'hero_front.png.meta') == meta_hash
        assert all(digest(ART / name) == value for name, value in unaffected.items())
    report = {
        'applied': args.apply, 'original_sha256': digest(source),
        'cleaned_sha256': digest(ROOT / 'hero_front.cleaned.png'),
        'removed_cyan_pixels': int(cyan.sum()),
        'removed_weak_alpha_pixels': int(((alpha > 0) & (alpha < 128)).sum()),
        'removed_detached_pixels': int((keep & ~connected).sum()),
        'vertical_alignment_pixels': shift_y, 'canvas': [48, 64],
        'remaining_cyan_pixels': 0, 'retained_character_rgb_unchanged': True,
        'sprite_metadata_unchanged': True, 'walk_and_attack_hashes': unaffected,
    }
    (ROOT / 'report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({key: value for key, value in report.items() if key != 'walk_and_attack_hashes'}, indent=2))


if __name__ == '__main__':
    main()
