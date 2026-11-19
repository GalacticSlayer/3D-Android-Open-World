"""Build native Unity meshes/materials/prefabs from prepared authored geometry.
No Blender dependency in Unity. Existing transform and animation documents are copied unchanged.
Usage: python Tools/Level8Gear/build_unity_assets.py [uploaded-texture-folder] [--level 8|9]
"""
from pathlib import Path
import argparse,hashlib,json,re,shutil
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
BASE='Assets/_Game/Fishing/RodReel'
parser=argparse.ArgumentParser()
parser.add_argument('source',nargs='?',type=Path,help='Optional folder with the original texture')
parser.add_argument('--level',type=int,choices=(8,9),default=8)
args=parser.parse_args()
level=args.level
PREP=ROOT/f'Tools/Level{level}Gear/Prepared'
GEAR=f'{BASE}/Level{level}'
def guid(path):return hashlib.sha256((f'level{level}-gear-v1:'+path).encode()).hexdigest()[:32]
def write(path,text):
 p=ROOT/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text if level==8 else '\n'.join(line.rstrip() for line in text.split('\n')))
def native_meta(path,fileid):
 write(path+'.meta',f'fileFormatVersion: 2\nguid: {guid(path)}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: {fileid}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
def docs(path):
 s=(ROOT/path).read_text();return re.split(r'(?=^--- !u!)',s,flags=re.M)
def components(ds):
 names={};transforms={}
 for d in ds:
  if d.startswith('--- !u!1 '):names[re.search(r'&(-?\d+)',d)[1]]=re.search(r'^  m_Name: (.*)$',d,re.M)[1]
 for d in ds:
  if d.startswith('--- !u!4 '):
   name=names[re.search(r'm_GameObject: \{fileID: (-?\d+)\}',d)[1]]
   match=re.search(r'm_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)',d)
   transforms[name]=[float(x) for x in match.groups()]
 return names,transforms
TEMPLATE=(ROOT/(BASE+'/Level5Rod/Generated/Level5FishingRod.asset')).read_text()
def vec(v):return '{x: %.9g, y: %.9g, z: %.9g}'%tuple(v)
def mesh(path,part,target):
 vertices=np.array(part['vertices'],dtype=float).reshape(-1,3)+np.array(part['position'])-np.array(target)
 normals=np.array(part['normals']).reshape(-1,3);tangents=np.array(part['tangents']).reshape(-1,4);uv=np.array(part['uv']).reshape(-1,2)
 count=len(vertices);assert count<65536
 packed=np.concatenate([vertices,normals,tangents,uv],axis=1).astype('<f4').tobytes()
 indices=np.array(part['triangles'],dtype='<u2').tobytes()
 center=(vertices.min(axis=0)+vertices.max(axis=0))/2;extent=(vertices.max(axis=0)-vertices.min(axis=0))/2
 s=TEMPLATE
 for pattern,value in [(r'(?m)^  m_Name: .*','  m_Name: '+Path(path).stem),(r'(?m)^(    indexCount: )\d+',r'\g<1>'+str(len(part['triangles']))),(r'(?m)^(    vertexCount: )\d+',r'\g<1>'+str(count)),(r'(?m)^(    m_VertexCount: )\d+',r'\g<1>'+str(count)),(r'(?m)^(    m_DataSize: )\d+',r'\g<1>'+str(len(packed))),(r'(?m)^(  m_IndexBuffer: ).*',r'\g<1>'+indices.hex()),(r'(?m)^(    _typelessdata: ).*',r'\g<1>'+packed.hex()),(r'm_Center: \{[^}]+\}','m_Center: '+vec(center)),(r'm_Extent: \{[^}]+\}','m_Extent: '+vec(extent))]:s=re.sub(pattern,value,s)
 write(path,s);native_meta(path,4300000)
 return len(part['triangles'])//3

def material(path,texture):
 s=(ROOT/'Assets/_Game/ContentUpdate/Generated/Level4Reel/Material.mat').read_text()
 s=s.replace('  m_Name: Material','  m_Name: '+Path(path).stem)
 s=re.sub(r'm_Texture: \{fileID: 2800000, guid: \w+, type: 2\}',f'm_Texture: {{fileID: 2800000, guid: {guid(texture)}, type: 3}}',s)
 write(path,s);native_meta(path,2100000)

def texture(path,source=None):
 if source: (ROOT/path).parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,ROOT/path)
 assert (ROOT/path).exists()
 s=(ROOT/(BASE+'/Level5Rod/Source/Level5FishingRodTexture.jpg.meta')).read_text()
 s=re.sub(r'^guid: .*','guid: '+guid(path),s,flags=re.M).replace('  nPOTScale: 1','  nPOTScale: 0')
 write(path+'.meta',s)

texture_path=f'{GEAR}/Source/Level{level}EquipmentTexture.png'
original_name='neon_mirror_seamless-1.png' if level==8 else 'Level 9 Rod and Reel Texture.png'
texture(texture_path,args.source/original_name if args.source else None)
for kind in ('Reel','Rod'):
 label=f'Level{level}Fishing{kind}';isrod=kind=='Rod'
 prepared=json.loads((PREP/(label+'.json')).read_text())
 template='Assets/Resources/Fishing/FishingRodReelLevel6Rod.prefab' if isrod else 'Assets/Resources/Fishing/FishingRodReelLevel6.prefab'
 ds=docs(template);names,positions=components(ds)
 gen=f'{GEAR}/Generated'
 mat=f'{gen}/{label}.mat';material(mat,texture_path)
 parts={p['name']:p for p in prepared['parts']};meshpaths={}
 for name,part in parts.items():
  assert isrod or np.linalg.norm(np.array(part['position'])-np.array(positions[name]))<.002, ('Reel pivot mismatch',name)
  path=f'{gen}/{name}.asset';meshpaths[name]=path;mesh(path,part,positions[name])
 out=[]
 for d in ds:
  match=re.search(r'm_GameObject: \{fileID: (-?\d+)\}',d)
  name=names.get(match[1]) if match else None
  if name in parts:
   if d.startswith('--- !u!33 '):d=re.sub(r'm_Mesh: \{[^}]+\}',f'm_Mesh: {{fileID: 4300000, guid: {guid(meshpaths[name])}, type: 2}}',d)
   if d.startswith('--- !u!23 '):d=re.sub(r'(m_Materials:\n  - )\{[^}]+\}',r'\g<1>'+f'{{fileID: 2100000, guid: {guid(mat)}, type: 2}}',d)
  out.append(d)
 stem=f'FishingRodReelLevel{level}'+('Rod' if isrod else '')
 path='Assets/Resources/Fishing/'+stem+'.prefab'
 result=''.join(out).replace('  m_Name: '+Path(template).stem+'\n','  m_Name: '+stem+'\n')
 write(path,result)
 write(path+'.meta',f'fileFormatVersion: 2\nguid: {guid(path)}\nPrefabImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
 print('BUILT',path)
# Stable folder GUIDs, without changing any existing folder metadata.
for parent in [ROOT/GEAR,*(ROOT/GEAR).rglob('*')]:
 if parent.is_dir() and not parent.with_suffix('.meta').exists():
  path=parent.relative_to(ROOT).as_posix()
  write(path+'.meta',f'fileFormatVersion: 2\nguid: {guid(path)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')

if level==9:
 # Source FBXs are retained for editing. Game prefabs use the native meshes only.
 for kind in ('Rod','Reel'):
  source=f'{GEAR}/Source/Level{level}Fishing{kind}.fbx'
  assert (ROOT/source).is_file()
  meta=(ROOT/f'{BASE}/Level8/Source/Level8Fishing{kind}.fbx.meta').read_text()
  write(source+'.meta',re.sub(r'^guid: .*','guid: '+guid(source),meta,flags=re.M))
  icon=f'Assets/Resources/Fishing/ItemIcons/{kind}{level-1}.png'
  meta=(ROOT/f'Assets/Resources/Fishing/ItemIcons/{kind}7.png.meta').read_text()
  write(icon+'.meta',re.sub(r'^guid: .*','guid: '+guid(icon),meta,flags=re.M))
