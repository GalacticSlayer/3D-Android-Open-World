"""Check native Unity mesh payloads, preserved UVs, prefab transforms and material links."""
from pathlib import Path
import json,re,struct,math,hashlib
root=Path(__file__).resolve().parents[2]
base=root/'Assets/_Game/Fishing/RodReel'
def guid(p):return re.search(r'^guid: (\w+)',Path(str(p)+'.meta').read_text(),re.M)[1]
def docs(p):return re.split(r'(?=^--- !u!)',p.read_text(),flags=re.M)
count=0
for level,kind in [(8,'Reel'),(8,'Rod')]:
    prepared=json.loads((root/f'Tools/Level8Gear/Prepared/Level{level}Fishing{kind}.json').read_text())
    folder=base/'Level8/Generated'
    material=folder/f'Level{level}Fishing{kind}.mat'
    texture=base/'Level8/Source/Level8EquipmentTexture.png'
    assert guid(texture) in material.read_text()
    assert '_BaseColor: {r: 1, g: 1, b: 1, a: 1}' in material.read_text()
    suffix='Rod' if kind=='Rod' else ''
    prefab=root/f'Assets/Resources/Fishing/FishingRodReelLevel{level}{suffix}.prefab'
    template=root/('Assets/Resources/Fishing/FishingRodReelLevel6Rod.prefab' if kind=='Rod' else 'Assets/Resources/Fishing/FishingRodReelLevel6.prefab')
    # All transforms and animation components remain byte-for-byte unchanged.
    keep=lambda d:d.startswith('--- !u!4 ') or d.startswith('--- !u!114 ') or d.startswith('--- !u!95 ')
    assert list(filter(keep,docs(prefab)))==list(filter(keep,docs(template)))
    for part in prepared['parts']:
        mesh=folder/(part['name']+'.asset');s=mesh.read_text()
        assert guid(mesh) in prefab.read_text()
        assert guid(material) in prefab.read_text()
        data=bytes.fromhex(re.search(r'_typelessdata: (\w+)',s)[1])
        vertex_count=int(re.search(r'm_VertexCount: (\d+)',s)[1])
        assert len(data)==vertex_count*48
        assert vertex_count==len(part['uv'])//2
        for i in range(vertex_count):
            vertex=struct.unpack_from('<12f',data,i*48)
            assert all(math.isfinite(v) for v in vertex)
            assert all(abs(vertex[10+j]-part['uv'][i*2+j])<1e-6 for j in range(2)), 'UV changed'
            assert abs(sum(x*x for x in vertex[3:6])-1)<1e-4
        indices=bytes.fromhex(re.search(r'm_IndexBuffer: (\w+)',s)[1])
        assert list(struct.unpack('<'+'H'*(len(indices)//2),indices))==part['triangles']
        assert max(part['triangles'])<vertex_count
        count+=1
source=(root/'Assets/_Game/Scripts/Fishing/FishingSystem.cs').read_text()
callback=source.split('private void ApplyShopGear()',1)[1].split('private CaughtFishRecord',1)[0]
assert 'SetPropertyBlock' not in callback and 'SetColor' not in callback
print(f'PASS: {count} meshes, original UVs, finite normals, valid indices, preserved transforms/animations, shared textures, white materials, tint-free save callback.')

# Purple assigns a fixed speed, so the reduction must be after it and before retrieval.
fight=source[source.index('private void UpdateFightBobber('):]
assert fight.index('if(ActiveTemperament==FishTemperament.Fragile)') < fight.index('outwardSpeed *= ShopCatalog.ReelEscapeMultiplier(') < fight.index('Vector3 velocity =') < fight.index('if (reeling)')
assert 'inwardGain * reelPower * ProgressReel' in fight
reel=(root/'Assets/_Game/Scripts/Fishing/Level2FishingReelRuntime.cs').read_text()
assert 'if(tier>=7)return 2.40f;' in reel
for kind in ['Rod','Reel']:
    from PIL import Image
    icon=root/f'Assets/Resources/Fishing/ItemIcons/{kind}7.png'
    im=Image.open(icon)
    assert im.mode=='RGBA' and im.getchannel('A').getextrema()==(0,255)
    assert Path(str(icon)+'.meta').exists()
    if kind=='Reel':
        alpha=im.getchannel('A')
        width,height=im.size
        left,top,right,bottom=alpha.point(lambda a: 255 if a>=128 else 0).getbbox()
        assert alpha.histogram()[255]>=width*height*.08, 'reel silhouette is blank or too small'
        assert abs(left+right-width)<=width*.04 and abs(top+bottom-height)<=height*.04, 'reel silhouette is off-center'
        assert .75<=max((right-left)/width,(bottom-top)/height)<=.90, 'reel does not fit inside its card'
print('PASS: slowdown after purple override, independent retrieval, 2.4x reel bonus, transparent icons.')
