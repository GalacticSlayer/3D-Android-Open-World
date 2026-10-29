"""Inspect supplied binary FBX rig/skin/UV/animation without modifying it."""
from pathlib import Path
import struct, zlib, json
p=Path('Assets/_Game/Reef/Source/PantherGrouper.fbx');b=p.read_bytes()
assert b.startswith(b'Kaydara FBX Binary  \x00\x1a\x00')
version=struct.unpack_from('<I',b,23)[0];wide=version>=7500
header='<QQQB' if wide else '<IIIB';hs=25 if wide else 13

def prop(pos):
 t=chr(b[pos]);pos+=1
 formats={'Y':'h','C':'?','I':'i','F':'f','D':'d','L':'q'}
 if t in formats:
  fmt='<'+formats[t];return struct.unpack_from(fmt,b,pos)[0],pos+struct.calcsize(fmt)
 if t in 'SR':
  n=struct.unpack_from('<I',b,pos)[0];pos+=4;v=b[pos:pos+n]
  return (v.decode('utf-8','replace') if t=='S' else v),pos+n
 if t in 'fdlibc':
  n,enc,size=struct.unpack_from('<III',b,pos);pos+=12;raw=b[pos:pos+size];raw=zlib.decompress(raw) if enc else raw
  fmt={'f':'f','d':'d','l':'q','i':'i','b':'b','c':'b'}[t];assert len(raw)==n*struct.calcsize(fmt)
  return list(struct.unpack('<'+str(n)+fmt,raw)),pos+size
 raise ValueError(t)

def node(pos):
 end,count,size,n=struct.unpack_from(header,b,pos)
 if end==0:return None,pos+hs
 pos+=hs;name=b[pos:pos+n].decode();pos+=n;props=[]
 for _ in range(count):v,pos=prop(pos);props.append(v)
 children=[]
 while pos<end-hs:
  child,pos=node(pos)
  if child is None:break
  children.append(child)
 return dict(name=name,props=props,children=children),end
roots=[];pos=27
while pos<len(b):
 n,pos=node(pos)
 if n is None:break
 roots.append(n)
def walk(nodes):
 for n in nodes:yield n;yield from walk(n['children'])
nodes=list(walk(roots));objects=next(x for x in roots if x['name']=='Objects')['children']
models=[n['props'][1].split('\x00')[0] for n in objects if n['name']=='Model']
for name in ['Bone','Bone.001','Bone.002','Bone.003','Bone.004']:assert name in models,name
stacks=[n['props'][1].split('\x00')[0] for n in objects if n['name']=='AnimationStack'];assert len(stacks)==1,stacks
arrays=lambda name:[n['props'][0] for n in nodes if n['name']==name]
assert arrays('Vertices') and sum(len(x)//3 for x in arrays('Vertices'))>100
assert arrays('UV') and arrays('Weights')
curves=arrays('KeyValueFloat');assert any(max(x)-min(x)>1e-5 for x in curves if x),'Animation must change over time'
assert any(n['name']=='Deformer' and 'Skin' in n['props'] for n in nodes)
print(json.dumps({'version':version,'models':models,'animation_stacks':stacks,'vertices':sum(len(x)//3 for x in arrays('Vertices')),'uv_pairs':sum(len(x)//2 for x in arrays('UV')),'animated_curves':len(curves)},indent=2))
