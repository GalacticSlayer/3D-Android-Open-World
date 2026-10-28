"""Compile per-fish preference shares into existing per-tackle catch tables.

Run from any directory. Only Bluewater Cay and Deep Ocean rows are replaced.
No runtime normalization, bait timing, fight rules, or species stats are changed.
"""
import csv
import json
from fractions import Fraction
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TUNING = ROOT / 'Assets/Resources/FishingTuning'
PROFILES = Path(__file__).with_name('OffshorePreferenceProfiles.json')


def compile_rows(profiles):
    result = {}
    for biome in sorted({p['biomeId'] for p in profiles}):
        fish = sorted((p for p in profiles if p['biomeId'] == biome), key=lambda p: p['rank'])
        assert len({p['speciesId'] for p in fish}) == len(fish)
        for p in fish:
            assert sum(p['percent'].values()) == 100
            assert all(isinstance(v, int) and v >= 0 for v in p['percent'].values())
        for bait in fish[0]['percent']:
            total = sum(p['percent'][bait] for p in fish)
            assert total > 0
            exact = [Fraction(100 * p['percent'][bait], total) for p in fish]
            rounded = [int(x) for x in exact]
            # Largest remainders; ties follow the user's fish-list order.
            order = sorted(range(len(fish)), key=lambda i: (-(exact[i] - rounded[i]), i))
            for i in order[:100 - sum(rounded)]:
                rounded[i] += 1
            result[biome, bait] = {p['speciesId']: v for p, v in zip(fish, rounded)}
        result[biome, 'worms-legacy'] = result[biome, 'worms'].copy()
    return result


def main():
    profiles = json.loads(PROFILES.read_text())
    compiled = compile_rows(profiles)
    path = TUNING / 'BiomeBaitSpeciesChance.csv'
    lines = path.read_text().splitlines()
    header = next(csv.reader([next(x for x in lines if not x.startswith('#'))]))
    ids = [int(x.split('_')[0]) for x in header[2:]]
    for i, line in enumerate(lines):
        cells = line.split(',')
        if tuple(cells[:2]) in compiled:
            values = compiled[tuple(cells[:2])]
            lines[i] = ','.join(cells[:2] + [str(values.get(sid, 0)) for sid in ids])
    path.write_text('\n'.join(lines) + '\n')


if __name__ == '__main__':
    main()
