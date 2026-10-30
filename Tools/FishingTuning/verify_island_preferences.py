"""Validate island preference shares, spawn gates and scope against the prior tuning."""
import csv,json,subprocess
from pathlib import Path
from apply_offshore_preferences import ROOT,TUNING,compile_rows
BASE='54c6c782630a3bcd0570254598017cccdd1a367c'
EXPECTED={'suncrest-reef':[(6,'worms'),(7,'worms'),(41,'squid'),(33,'squid'),(31,'shrimp'),(32,'shrimp'),(42,'squid'),(35,'lure:1'),(37,'lure:0'),(36,'lure:3'),(39,'lure:4')], 'snapper-island':[(16,'worms'),(30,'shrimp'),(1,'shrimp'),(17,'squid'),(21,'squid'),(38,'lure:1'),(22,'lure:4'),(23,'lure:4'),(18,'lure:0')], 'brinebreak-isle':[(27,'squid'),(10,'shrimp'),(12,'shrimp'),(2,'lure:2'),(11,'lure:2'),(14,'lure:4'),(40,'lure:1'),(29,'lure:0'),(28,'lure:0')]}
KEYS=['worms','shrimp','squid']+[f'lure:{i}' for i in range(5)]
def parse(s):return list(csv.DictReader(x for x in s.splitlines() if x and not x.startswith('#')))
def current(n):return parse((TUNING/n).read_text())
def old(p):return subprocess.check_output(['git','show',BASE+':'+p],cwd=ROOT,text=True)
p=json.loads(Path(__file__).with_name('IslandPreferenceProfiles.json').read_text());assert len(p)==29
for b,e in EXPECTED.items():
 f=[x for x in p if x['biomeId']==b];assert [(x['speciesId'],x['preferred']) for x in f]==e
 assert [x['rank'] for x in f]==list(range(1,len(f)+1))
 worms=[x['percent']['worms'] for x in f];assert worms==sorted(worms,reverse=True) and worms[-1]==0
 for x in f:
  v=x['percent'];pref=x['preferred'];assert list(v)==KEYS and sum(v.values())==100
  assert all(type(n)==int and n>=0 for n in v.values()) and v[pref]==max(v.values())
  if pref.startswith('lure:'):
   assert v[pref]==30 and len(set(x['secondary']))==2 and pref not in x['secondary']
   assert all(v[k]==15 for k in x['secondary']) and all(v[k]<=15 for k in KEYS[3:] if k!=pref)
   assert sum(sorted((v[k] for k in KEYS[3:]),reverse=True)[:3])==60
   assert sum(v[k] for k in KEYS[:3])<=15 and v['worms']<min(v['shrimp'],v['squid'])
  else:
   order={'worms':['worms','shrimp','squid'],'shrimp':['shrimp','squid','worms'],'squid':['squid','shrimp','worms']}[pref]
   assert v[order[0]]>v[order[1]]>v[order[2]] and 0<=sum(v[k] for k in KEYS[3:])<=20
c=compile_rows(p);rows=current('BiomeBaitSpeciesChance.csv');w={(x['biomeId'],int(x['speciesId'])):x for x in current('BiomeFishWeights.csv')};stats={int(x['speciesId']):x for x in current('FishStats.csv')}
assert len(rows)==45 and len({(x['biomeId'],x['baitKey']) for x in rows})==45
for x in rows:
 b,k=x['biomeId'],x['baitKey'];v={int(a.split('_')[0]):int(n) for a,n in x.items() if a not in ('biomeId','baitKey')}
 assert sum(v.values())==100 and all(n>=0 for n in v.values())
 for sid,n in v.items():
  if n:assert float(w[b,sid]['maxKg'])>float(w[b,sid]['minKg'])>0
 if b in EXPECTED:assert v=={sid:c[b,k].get(sid,0) for sid in v}
for b,e in EXPECTED.items():
 allowed={sid for sid,_ in e}|({34} if b=='suncrest-reef' else set())
 assert {sid for (biome,sid),x in w.items() if biome==b and float(x['maxKg'])>0}==allowed
 assert c[b,'worms']==c[b,'worms-legacy']
 for sid,pref in e:assert c[b,pref][sid]==max(c[b,k][sid] for k in KEYS),(b,sid,pref)
for (b,sid),x in w.items():
 lo,hi=float(x['minKg']),float(x['maxKg']);s=stats[sid]
 assert lo==hi==0 or float(s['minWeightKg'])<=lo<hi<=float(s['maxWeightKg'])
prefix='Assets/Resources/FishingTuning/'
assert old(prefix+'FishStats.csv')==(TUNING/'FishStats.csv').read_text()
assert [x for x in rows if x['biomeId'] not in EXPECTED]==[x for x in parse(old(prefix+'BiomeBaitSpeciesChance.csv')) if x['biomeId'] not in EXPECTED]
previous={(x['biomeId'],int(x['speciesId'])):x for x in parse(old(prefix+'BiomeFishWeights.csv'))}
for key,x in w.items():
 b,sid=key
 if b=='brinebreak-isle' and sid in (27,28,29):
  y=previous['suncrest-reef',sid];assert (x['minKg'],x['maxKg'])==(y['minKg'],y['maxKg'])
 elif float(x['maxKg'])>0 or b not in EXPECTED:assert x==previous[key]
server='Server/runtime/tournament-species.mjs';text=(ROOT/server).read_text();oldtext=old(server)
def islands(s):return json.loads(s.split('export const tournamentIslands = ',1)[1].strip().removesuffix(';'))
before=islands(oldtext)
for island in islands(text):
 if island['id'] in (2,3):assert island==before[island['id']];continue
 b={0:'suncrest-reef',1:'brinebreak-isle',4:'snapper-island'}[island['id']]
 assert {f['id'] for f in island['fish']}=={sid for (biome,sid),x in w.items() if biome==b and float(x['maxKg'])>0}
 for f in island['fish']:assert f['maxKg']==float(w[b,f['id']]['maxKg'])*1.5
assert text.split('export const tournamentIslands = ',1)[0]==oldtext.split('export const tournamentIslands = ',1)[0]
changed=subprocess.check_output(['git','diff',BASE,'--name-only'],cwd=ROOT,text=True).splitlines();assert not any(x.endswith('.cs') for x in changed)
print('PASS: 29 profiles, 45 runtime tables, preferred tackle, worm taper, exact spawn gates, retained weights, tournament metadata, unchanged offshore data and difficulty.')
