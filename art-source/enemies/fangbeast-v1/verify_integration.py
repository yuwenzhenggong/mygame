"""Check that the jungle roster, quest targets and animation resources agree."""
import json
import hashlib
from collections import Counter
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
RESOURCES = PROJECT / 'Assets/Resources'


def main():
    table = json.loads((RESOURCES / 'Data/enemies.json').read_text(encoding='utf-8-sig'))
    spawns = [spawn for spawn in table['spawns'] if spawn['scene'] == 'jungle']
    counts = Counter(spawn['enemy'] for spawn in spawns)
    assert counts == {'beast': 3, 'elite': 1}, f'Expected three beasts and one elite, got {dict(counts)}'
    enemies = {enemy['id']: enemy for enemy in table['enemies']}
    quest = json.loads((RESOURCES / 'Data/quests.json').read_text(encoding='utf-8-sig'))['quests'][0]
    assert quest['targetEnemy'] == 'beast'
    assert counts[quest['targetEnemy']] >= quest['targetCount'], 'Quest cannot be completed with the jungle roster'
    for enemy_id, size in [('beast', (96, 64)), ('elite', (132, 88))]:
        prefix = enemies[enemy_id]['sprite']
        base = RESOURCES / 'Art' / f'{prefix}.png'
        assert Image.open(base).size == size
        source_base = ROOT / 'png' / ('normal.png' if enemy_id == 'beast' else 'elite.png')
        assert base.read_bytes() == source_base.read_bytes()
        for action, expected in [('idle', 12), ('walk', 24), ('attack', 12)]:
            files = sorted((RESOURCES / 'Art').glob(f'{prefix}_{action}_*.png'),
                           key=lambda p: int(p.stem.rsplit('_', 1)[1]))
            assert len(files) == expected
            assert [file.name for file in files] == [f'{prefix}_{action}_{i}.png' for i in range(expected)]
            for index, file in enumerate(files):
                image = Image.open(file)
                assert image.size == size and image.mode == 'RGBA'
                source = ROOT / 'png' / ('' if enemy_id == 'beast' else 'elite') / action / f'{index:02}.png'
                assert hashlib.sha256(file.read_bytes()).digest() == hashlib.sha256(source.read_bytes()).digest()
                meta = file.with_suffix('.png.meta').read_text()
                for setting in ['spritePixelsToUnits: 32', 'filterMode: 0', 'enableMipMap: 0', 'textureCompression: 0']:
                    assert setting in meta
    print('PASS: 3 normal + 1 elite; reachable quest; all 96 animation resources load consecutively.')


if __name__ == '__main__':
    main()
