"""One-time additive scene patch. Never run as an in-game scatterer."""
from pathlib import Path
import hashlib,math,random,re
ROOT=Path(__file__).resolve().parents[2]
def guid(path):return hashlib.sha256(('windgrove-v1:'+path).encode()).hexdigest()[:32]
def metadata(path,kind='MonoImporter'):
 p=ROOT/path
 suffix='  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n' if kind=='MonoImporter' else ''
 (ROOT/(path+'.meta')).write_text(f'fileFormatVersion: 2\nguid: {guid(path)}\n{kind}:\n  externalObjects: {{}}\n'+suffix+'  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
script='Assets/_Game/Scripts/Environment/WindgroveTurbine.cs'
for p in [script,'Assets/_Game/Editor/WindgroveTurbineImporter.cs','Assets/_Game/Editor/WindgroveTurbineSceneSetup.cs']:metadata(p)
metadata('Assets/Resources/Environment/Windgrove/WindTurbine.fbx','ModelImporter')
rng=random.Random(7762026)
points=[]
for attempt in range(100000):
 r=math.sqrt(rng.uniform(85**2,330**2));a=rng.uniform(0,math.tau)
 x,z=r*math.cos(a),r*math.sin(a)
 if all(math.hypot(x-p[0],z-p[1])>=82 for p in points):points.append((x,z,rng.uniform(-30,30),rng.random()))
 if len(points)==30:break
assert len(points)==30
scene=ROOT/'Assets/Scenes/PrototypeWorld.unity'
s=scene.read_text()
assert 'The Windgrove Shoal - Wind Turbines' not in s
base=9200000000
assert not re.search(r'&9200000\d+',s)
header='  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
def obj(n,name,parent,pos,rot=(0,0,0,1),children=(),component=False):
 go,tf,mb=n,n+1,n+2
 out=f'--- !u!1 &{go}\nGameObject:\n'+header+'  serializedVersion: 6\n  m_Component:\n'+f'  - component: {{fileID: {tf}}}\n'
 if component:out+=f'  - component: {{fileID: {mb}}}\n'
 out+=f'  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n'
 out+=f'--- !u!4 &{tf}\nTransform:\n'+header+f'  m_GameObject: {{fileID: {go}}}\n  serializedVersion: 2\n'
 out+='  m_LocalRotation: {x: %s, y: %s, z: %s, w: %s}\n'%rot
 out+='  m_LocalPosition: {x: %.6f, y: %.6f, z: %.6f}\n'%pos
 out+='  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 1\n'
 out+='  m_Children:\n'+''.join(f'  - {{fileID: {c}}}\n' for c in children) if children else '  m_Children: []\n'
 out+=f'  m_Father: {{fileID: {parent}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n'
 return out
out=obj(base,'The Windgrove Shoal - Wind Turbines',0,(459.11584,-277.5,-3230.79875),children=[base+10+i*10+1 for i in range(30)])
for i,(x,z,angle,phase) in enumerate(points):
 n=base+10+i*10
 out+=obj(n,f'Wind Turbine {i+1:02}',base+1,(x,0,z),(0,math.sin(math.radians(angle)/2),0,math.cos(math.radians(angle)/2)),component=True)
 out+=f'--- !u!114 &{n+2}\nMonoBehaviour:\n'+header+f'  m_GameObject: {{fileID: {n}}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {guid(script)}, type: 3}}\n  m_Name:\n  m_EditorClassIdentifier: Assembly-CSharp::WindgroveTurbine\n  phase: {phase:.8f}\n  animatedModel: {{fileID: 0}}\n  rotationClip: {{fileID: 0}}\n'
marker='--- !u!1660057539 &9223372036854775807'
assert marker in s
s=s.replace(marker,out+marker)
s=s.rstrip()+f'\n  - {{fileID: {base+1}}}\n'
scene.write_text(s)
print('Saved 30 placements, radius <= 330 m, minimum spacing 82 m.')
