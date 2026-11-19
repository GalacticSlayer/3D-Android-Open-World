"""Check native Unity mesh payloads, preserved UVs, prefab transforms and material links."""
from pathlib import Path
import json,re,struct,math
from PIL import Image
root=Path(__file__).resolve().parents[2]
base=root/'Assets/_Game/Fishing/RodReel'
def guid(p):return re.search(r'^guid: (\w+)',Path(str(p)+'.meta').read_text(),re.M)[1]
def docs(p):return re.split(r'(?=^--- !u!)','\n'.join(line.rstrip() for line in p.read_text().split('\n')),flags=re.M)
count=0
for level,kind in [(level,kind) for level in (8,9) for kind in ('Reel','Rod')]:
    prepared=json.loads((root/f'Tools/Level{level}Gear/Prepared/Level{level}Fishing{kind}.json').read_text())
    folder=base/f'Level{level}/Generated'
    material=folder/f'Level{level}Fishing{kind}.mat'
    texture=base/f'Level{level}/Source/Level{level}EquipmentTexture.png'
    assert guid(texture) in material.read_text()
    assert 'sRGBTexture: 1' in Path(str(texture)+'.meta').read_text()
    assert all(f'wrap{axis}: 0' in Path(str(texture)+'.meta').read_text() for axis in ('U','V'))
    assert '_BaseColor: {r: 1, g: 1, b: 1, a: 1}' in material.read_text()
    suffix='Rod' if kind=='Rod' else ''
    prefab=root/f'Assets/Resources/Fishing/FishingRodReelLevel{level}{suffix}.prefab'
    template=root/('Assets/Resources/Fishing/FishingRodReelLevel6Rod.prefab' if kind=='Rod' else 'Assets/Resources/Fishing/FishingRodReelLevel6.prefab')
    # All transforms and animation components remain unchanged (ignoring trailing whitespace).
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
assert '=> ShopCatalog.ReelRetrievalMultiplier(tier);' in reel
catalog=(root/'Assets/_Game/Scripts/ShopWorld/ShopCatalog.cs').read_text()
assert 'if(tier>=8)return 2.60f;' in catalog
assert 'if(tier>=7)return 2.40f;' in catalog
assert 'tier>=8 ? 0.85f : tier>=7 ? 0.90f' in catalog
preview=(root/'Assets/_Game/Scripts/ShopWorld/ShopPreview.cs').read_text()
assert 'if(background.transform.GetSiblingIndex()<targetIndex)targetIndex--;' in preview
for level,kind in [(level,kind) for level in (8,9) for kind in ('Rod','Reel')]:
    key=f'{kind}{level-1}'
    assert f'resolvedGear=="{key}"' in preview
    icon=root/f'Assets/Resources/Fishing/ItemIcons/{key}.png'
    im=Image.open(icon)
    assert im.mode=='RGBA' and im.getchannel('A').getextrema()==(0,255)
    assert Path(str(icon)+'.meta').exists()
    alpha=im.getchannel('A')
    width,height=im.size
    left,top,right,bottom=alpha.point(lambda a: 255 if a>=128 else 0).getbbox()
    assert alpha.histogram()[255]>=width*height*(.08 if kind=='Reel' else .003), f'{key} silhouette is blank or too small'
    assert abs(left+right-width)<=width*.04 and abs(top+bottom-height)<=height*.04, f'{key} silhouette is off-center'
    assert .75<=max((right-left)/width,(bottom-top)/height)<=.90, f'{key} does not fit inside its card'
print('PASS: escape slowdown after purple override, independent retrieval, centered nonblank Level 8 and 9 icons.')
