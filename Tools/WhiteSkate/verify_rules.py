from pathlib import Path
import csv,subprocess
from PIL import Image
R=Path(__file__).resolve().parents[2];D=R/'Assets/Resources/FishingTuning'
def rows(name):return list(csv.reader(l for l in (D/(name+'.csv')).read_text().splitlines() if l and not l.startswith('#')))
s=rows('FishStats');assert next(r for r in s[1:] if r[0]=='40')[2:]==next(r for r in s[1:] if r[0]=='29')[2:]
w=rows('BiomeFishWeights');home=next(r for r in w[1:] if r[:2]==['suncrest-reef','29'])[3:]
for r in [r for r in w[1:] if r[1]=='40']:assert r[3:]==(home if r[0]=='brinebreak-isle' else ['0','0'])
c=rows('BiomeBaitSpeciesChance');li=c[0].index('29_leopard_stingray');wi=c[0].index('40_white_skate');profile={r[1]:r[li] for r in c[1:] if r[0]=='suncrest-reef'}
base=subprocess.check_output(['git','show','564a03cb:Assets/Resources/FishingTuning/BiomeBaitSpeciesChance.csv'],cwd=R,text=True)
old=list(csv.reader(l for l in base.splitlines() if l and not l.startswith('#')))
assert len(c)==len(old)==46
for row,previous in zip(c[1:],old[1:]):
 assert sum(map(int,row[2:]))==100 and len(row)==len(c[0])
 assert row[wi]==(profile[row[1]] if row[0]=='brinebreak-isle' else '0')
 if row[0]!='brinebreak-isle':assert row[:-1]==previous
 else:
  for x,y in zip(previous[2:],row[2:-1]):
   if int(x)==0:assert int(y)==0
for n in ['WhiteSkateTexture','WhiteSkateMetallicRoughness']:
 with Image.open(R/f'Assets/_Game/Reef/Source/{n}.png') as im:im.load();assert im.size==(4096,4096)
print('PASS: exact Leopard stats, 45 normalized bait rows, Brinebreak-only spawn/weights, all other islands unchanged, PNG decode')
