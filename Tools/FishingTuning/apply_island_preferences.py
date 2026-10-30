"""Compile the three island preference profiles without changing fishing mechanics.

Only chance rows are generated. Weight availability and tournament metadata are
maintained separately so running this command cannot silently change catch sizes.
"""
import csv
import json
from pathlib import Path
from apply_offshore_preferences import TUNING, compile_rows


def main():
    profiles = json.loads(Path(__file__).with_name('IslandPreferenceProfiles.json').read_text())
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
