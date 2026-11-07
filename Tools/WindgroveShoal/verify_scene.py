"""Run from repo root: python Tools/WindgroveShoal/verify_scene.py [original-scene]"""
from pathlib import Path
import math,re,sys
root=Path(__file__).resolve().parents[2]
s=(root/'Assets/Scenes/PrototypeWorld.unity').read_text()
docs=re.split(r'(?=^--- !u!)',s,flags=re.M)
new=[d for d in docs if re.match(r'--- !u!\d+ &9200000\d+',d)]
assert len(new)==92,len(new) # root GO/transform + 30 GO/transform/script
ids=re.findall(r'^--- !u!\d+ &(-?\d+)',s,re.M)
assert len(ids)==len(set(ids))
assert len(re.findall(r'^  m_Name: Wind Turbine \d\d$',s,re.M))==30
points=[]
for d in new:
 if not d.startswith('--- !u!4 ') or 'm_Father: {fileID: 9200000001}' not in d:continue
 x,y,z=map(float,re.search(r'm_LocalPosition: \{x: (.*?), y: (.*?), z: (.*?)\}',d).groups())
 assert math.hypot(x,z)<=330.001
 assert y==0
 points.append((x,z))
assert len(points)==30
assert min(math.dist(a,b) for i,a in enumerate(points) for b in points[i+1:])>=81.999
if len(sys.argv)>1:
 original=Path(sys.argv[1]).read_text()
 restored=''.join(d for d in docs if d not in new).replace('  - {fileID: 9200000001}\n','')
 assert restored.rstrip()==original.rstrip(),'Existing scene data changed'
zone=(root/'Assets/_Game/Scripts/Islands/OffshoreOilRigZone.cs').read_text()
assert 'Name = "The Windgrove Shoal"' in zone
assert 'Id = "offshore-oil-rig"' in zone and 'BiomeId = 5' in zone
runtime=(root/'Assets/_Game/Scripts/Environment/WindgroveTurbine.cs').read_text()
assert 'Time.unscaledTime' in runtime and 'SampleAnimation(animatedModel, time)' in runtime
assert not re.search(r'(?<!\.)transform\.(position|rotation|localScale)\s*=',runtime)
print('PASS: exactly 30 saved, separated placements inside the shoal; unique IDs; original scene preserved; save IDs retained.')
