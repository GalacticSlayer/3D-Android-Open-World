"""Validate generated lure payloads, decoded maps and optional gameplay regression."""
import json,math,re,subprocess,sys,io
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
catalog=(ROOT/'Assets/_Game/Scripts/ShopWorld/ShopCatalog.cs').read_text()
for name,count in [('HeavyJig',2),('LightJig',2),('Popper',3)]:
 folder=ROOT/'Assets/_Game/Fishing'/name
 data=json.loads((folder/'AuthoredLure.json').read_text())
 assert data['duration']==.375 and data['fps']==192 and len(data['parts'])==count
 assert 'Fishing/'+name in catalog
 for filename in ['Albedo.png','MetallicRoughness.png']:
  split = name=='Popper' and filename=='Albedo.png'
  source = io.BytesIO(b''.join((folder/f'Albedo.part{i}.bytes').read_bytes() for i in range(2))) if split else folder/filename
  with Image.open(source) as image:
   image.load();assert image.format=='PNG' and image.size==(4096,4096)
  if not split:
   meta=(folder/(filename+'.meta')).read_text()
   assert 'sRGBTexture: '+str(int(filename=='Albedo.png')) in meta
 for part in data['parts']:
  assert len(part['vertices'])==len(part['normals'])==len(part['uv'])
  assert len(part['triangles'])%3==0 and max(part['triangles'])<len(part['vertices'])
  assert len(part['frames'])==73
  for key in ['vertices','normals','uv']:
   assert all(math.isfinite(v) for point in part[key] for v in point.values())
  for axis in 'xy':assert max(p[axis] for p in part['uv'])>min(p[axis] for p in part['uv'])
  for key,axes in [('position','xyz'),('rotation','xyzw')]:
   assert max(abs(part['frames'][0][key][a]-part['frames'][-1][key][a]) for a in axes)<1e-5
  for frame in part['frames']:assert abs(sum(v*v for v in frame['rotation'].values())-1)<1e-5
 print(name,'valid geometry, UVs, animated loop and colour/data textures')
assert re.findall(r'"([^"]+)"',catalog.split('LureNames =')[1].split('};')[0])==['Heavy Jig','Light Jig','Fire Shad Crankbait','Metal Spoon','Popper']
if len(sys.argv)>1:
 baseline=sys.argv[1]
 def before(p):return subprocess.check_output(['git','show',baseline+':'+str(p)],cwd=ROOT,text=True)
 def numeric_array(source,name):return re.search(r'\b'+name+r'\s*=\s*\{([^}]+)\}',source).group(1)
 old=before(Path('Assets/_Game/Scripts/ShopWorld/ShopCatalog.cs'))
 for name in ['LurePrices','BaitPrices','RodPrices','ReelPrices','LinePrices','LineBonus']:
  assert numeric_array(old,name)==numeric_array(catalog,name),name
 paths=subprocess.check_output(['git','ls-tree','-r','--name-only',baseline],cwd=ROOT,text=True).splitlines()
 for p in paths:
  if p.endswith('.cs') and any(k in p for k in ['FishingSystem.cs','FishingTuning','ShopLedger.cs','ShopProgress.cs','FishCatalog.cs']):
   local=ROOT/p
   if local.exists():assert before(Path(p))==local.read_text(),p
 print('Baseline regression: prices, fishing rules and save/ownership implementation unchanged')
print('All replacement checks passed')
