"""Blender background round-trip check against the installed Leopard animation."""
import bpy,json
from pathlib import Path
R=Path(__file__).resolve().parents[2]/'Assets/_Game/Reef/Source'
def capture(name):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(R/(name+'.fbx')))
 arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
 assert len(arm.data.bones)==29 and mesh.data.uv_layers.active
 assert all(b.parent for b in arm.data.bones if b.name.startswith('Fin_'))
 output={}
 for action in bpy.data.actions:
  key=action.name.split('|')[-1]
  if key not in ('Swim','HeldStruggle'):continue
  arm.animation_data.action=action
  if len(action.slots):arm.animation_data.action_slot=action.slots[0]
  poses=[]
  for frame in range(1,31):
   bpy.context.scene.frame_set(frame)
   poses.append({b.name:[v for row in arm.matrix_world@b.matrix for v in row] for b in arm.pose.bones})
  assert max(abs(x-y) for n in poses[0] for x,y in zip(poses[0][n],poses[14][n]))>.01
  assert max(abs(x-y) for n in poses[0] for x,y in zip(poses[0][n],poses[-1][n]))<.02
  output[key]=poses
 assert set(output)=={'Swim','HeldStruggle'}
 return output

old=capture('LeopardStingray');new=capture('WhiteSkate')
swim_error=max(abs(x-y) for a,b in zip(old['Swim'],new['Swim']) for bone in a for x,y in zip(a[bone],b[bone]))
assert swim_error<.0001,swim_error
# Compare held motion to the original Blend, with only the established root roll.
import math
from mathutils import Matrix
bpy.ops.wm.open_mainfile(filepath=str(R.parents[3]/'Tools/WhiteSkate/Source/WhiteSkate.blend'))
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
action=bpy.data.actions['Stingray Struggling'];arm.animation_data.action=action
if len(action.slots):arm.animation_data.action_slot=action.slots[0]
rename={f'Bone.{10+i:03d}':('Bone' if i==0 else f'Bone.{i:03d}') for i in range(9)}
error=0
for frame in range(1,31):
 bpy.context.scene.frame_set(frame)
 for bone in arm.pose.bones:
  expected=Matrix.Rotation(-math.pi/2,4,'Y')@arm.matrix_world@bone.matrix
  actual=new['HeldStruggle'][frame-1][rename.get(bone.name,bone.name)]
  error=max(error,max(abs(x-y) for x,y in zip([v for row in expected for v in row],actual)))
assert error<.0001,error
print('PASS: unchanged Leopard swim and original White Skate struggle; errors',swim_error,error)
