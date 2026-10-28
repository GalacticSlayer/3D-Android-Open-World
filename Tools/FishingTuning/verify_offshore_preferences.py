"""Validate offshore profiles, compiled odds, spawn gates and unchanged scope.

Usage: python3 Tools/FishingTuning/verify_offshore_preferences.py [baseline-ref]
"""
import csv
import io
import json
import subprocess
import sys
from pathlib import Path
from apply_offshore_preferences import ROOT, TUNING, PROFILES, compile_rows


def parse(text):
    return list(csv.DictReader(x for x in text.splitlines() if not x.startswith('#')))


def read(name):
    return parse((TUNING / name).read_text())


profiles = json.loads(PROFILES.read_text())
expected = {'bluewater-cay': [0,9,3,13,25,24,8,5,26,15,19],
            'deep-ocean': [0,9,10,3,13,8,5,15,19]}
keys = ['worms','shrimp','squid'] + ['lure:'+str(i) for i in range(5)]
for biome, ids in expected.items():
    fish = [p for p in profiles if p['biomeId'] == biome]
    assert [p['speciesId'] for p in fish] == ids
    assert [p['rank'] for p in fish] == list(range(1,len(ids)+1))
    worms = [p['percent']['worms'] for p in fish]
    assert worms == sorted(worms, reverse=True) and worms[-1] == 0
    for p in fish:
        v = p['percent']; preferred = p['preferred']
        assert set(v) == set(keys) and sum(v.values()) == 100
        assert v[preferred] == max(v.values())
        if preferred.startswith('lure:'):
            assert v[preferred] == 30
            assert len(set(p['secondary'])) == 2 and preferred not in p['secondary']
            assert all(v[k] == 15 for k in p['secondary'])
            assert all(v[k] <= 15 for k in keys[3:] if k != preferred)
            assert 60 <= sum(sorted([v[k] for k in keys[3:]], reverse=True)[:3]) <= 65
            assert 0 <= sum(v[k] for k in keys[:3]) <= 15
            assert v['worms'] < min(v['shrimp'],v['squid'])
        else:
            assert 0 <= sum(v[k] for k in keys[3:]) <= 20
            order = {'worms':['worms','shrimp','squid'],
                     'shrimp':['shrimp','squid','worms'],
                     'squid':['squid','shrimp','worms']}[preferred]
            assert v[order[0]] > v[order[1]] > v[order[2]]

compiled = compile_rows(profiles)
rows = read('BiomeBaitSpeciesChance.csv')
weights = {(r['biomeId'],int(r['speciesId'])):r for r in read('BiomeFishWeights.csv')}
stats = {int(r['speciesId']):r for r in read('FishStats.csv')}
assert len(rows) == 45 and len({(r['biomeId'],r['baitKey']) for r in rows}) == 45
for row in rows:
    biome,bait = row['biomeId'],row['baitKey']
    odds = {int(k.split('_')[0]):int(v) for k,v in row.items() if k not in ('biomeId','baitKey')}
    assert sum(odds.values()) == 100 and all(v >= 0 for v in odds.values())
    for sid,v in odds.items():
        w = weights[biome,sid]
        if v: assert float(w['maxKg']) > float(w['minKg']) > 0
    if biome in expected:
        assert odds == {sid:compiled[biome,bait].get(sid,0) for sid in odds}
        assert {sid for sid,v in odds.items() if v} <= set(expected[biome])
for (biome,sid),w in weights.items():
    lo,hi = float(w['minKg']),float(w['maxKg'])
    assert (lo == hi == 0) or (float(stats[sid]['minWeightKg']) <= lo < hi <= float(stats[sid]['maxWeightKg']))
    if biome in expected: assert (hi > 0) == (sid in expected[biome])
for biome in expected:
    worm = next(r for r in rows if r['biomeId']==biome and r['baitKey']=='worms')
    legacy = next(r for r in rows if r['biomeId']==biome and r['baitKey']=='worms-legacy')
    assert {k:v for k,v in worm.items() if k!='baitKey'} == {k:v for k,v in legacy.items() if k!='baitKey'}
    for p in [p for p in profiles if p['biomeId']==biome]:
        odds = {bait:compiled[biome,bait][p['speciesId']] for bait in keys}
        assert odds[p['preferred']] == max(odds.values()), (p,odds)

if len(sys.argv)>1:
    baseline=sys.argv[1]
    def old(name):
        return subprocess.check_output(['git','show',baseline+':Assets/Resources/FishingTuning/'+name],cwd=ROOT,text=True)
    assert old('FishStats.csv') == (TUNING/'FishStats.csv').read_text()
    original = parse(old('BiomeBaitSpeciesChance.csv'))
    assert [r for r in rows if r['biomeId'] not in expected] == [r for r in original if r['biomeId'] not in expected]
    original_weights={(r['biomeId'],int(r['speciesId'])):r for r in parse(old('BiomeFishWeights.csv'))}
    allowed={('bluewater-cay',3),('bluewater-cay',15),('bluewater-cay',20),('deep-ocean',14),('deep-ocean',20)}
    assert {key for key in weights if weights[key]!=original_weights[key]} == allowed
    changed=subprocess.check_output(['git','diff',baseline,'--name-only'],cwd=ROOT,text=True).splitlines()
    assert not any(x.endswith('.cs') for x in changed), 'Runtime C# changed'
print('PASS: 20 preference profiles; 45 runtime rows; exact spawn lists; preferred tackle remains best; worm taper; legacy alias; stat bounds and scope.')
