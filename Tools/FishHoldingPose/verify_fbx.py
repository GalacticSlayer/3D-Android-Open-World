"""Validate pose FBX skeleton paths and animation takes against the live avatar."""
from pathlib import Path
import struct, zlib, json

def inspect(path):
    b=path.read_bytes()
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

    models={n['props'][0]: n['props'][1].split('\x00')[0] for n in objects if n['name']=='Model'}
    parents={n['props'][1]:n['props'][2] for n in nodes if n['name']=='C' and n['props'][0]=='OO' and n['props'][1] in models}
    def path_for(i):
        return (path_for(parents[i])+'/' if parents.get(i) in models else '')+models[i]
    paths={path_for(i) for i in models}
    takes=[n['props'][1].split('\x00')[0] for n in objects if n['name']=='AnimationStack']
    curves=[n['props'][0] for n in nodes if n['name']=='KeyTime']
    return paths,takes,curves

root=Path(__file__).resolve().parents[2]/'Assets/Manually Added Assets'
reference,_,_=inspect(root/'Female Holding the Fish.fbx')
model,_,_=inspect(root/'Female Character Model.fbx')
model_names={p.split('/')[-1] for p in model}
for size in ['Small']:
    name=f'Female Holding a {size} Fish Pose.fbx'
    paths,takes,curves=inspect(root/name)
    assert paths == reference, sorted(paths ^ reference)
    assert {p.split('/')[-1] for p in paths} <= model_names
    assert any(p.endswith('Hand.R') for p in paths)
    assert any(p.endswith('Hand.L') for p in paths)
    assert takes and curves
    print(json.dumps(dict(file=name,bones=len(paths),takes=takes,curves=len(curves),seconds=max(max(c) for c in curves)/46186158000)))
