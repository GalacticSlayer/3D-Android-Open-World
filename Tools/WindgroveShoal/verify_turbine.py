import bpy
from pathlib import Path
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(Path(__file__).resolve().parents[2]/'Assets/Resources/Environment/Windgrove/WindTurbine.fbx'))
s=bpy.context.scene
start,end=bpy.data.objects['Rotor blade 2'].animation_data.action.frame_range
s.frame_set(int(start))
initial={o.name:o.matrix_world.copy() for o in s.objects}
changed=set()
for f in [int(start+25),int(start+50),int(start+75),int(end)]:
 s.frame_set(f)
 for o in s.objects:
  diff=max(abs(x-y) for r,t in zip(o.matrix_world,initial[o.name]) for x,y in zip(r,t))
  if diff>1e-4:changed.add(o.name)
  if f==int(end):assert diff<1e-4,(o.name,diff)
assert changed=={'Rotor blade 2'},changed
assert len([o for o in s.objects if o.type=='MESH'])==13
print('PASS: reimported FBX has 13 meshes; only rotor moves; loop closes; materials:',[m.name for m in bpy.data.materials])
