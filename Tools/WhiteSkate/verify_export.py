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
error=max(abs(x-y) for action in old for a,b in zip(old[action],new[action]) for bone in a for x,y in zip(a[bone],b[bone]))
assert error<.0001,error
print('PASS: both loops and all 29 animated bone world matrices match Leopard; max error',error)
