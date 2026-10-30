"""Use the supplied mesh/UV/weights with the installed Leopard swim and supplied White Skate struggle."""
import bpy,math,json
from pathlib import Path
from mathutils import Matrix
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Assets/_Game/Reef/Source'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(OUT/'LeopardStingray.fbx'))
reference=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
rest={b.name:b.matrix_local.copy() for b in reference.data.bones}
parents={b.name:b.parent.name if b.parent else None for b in reference.data.bones}
samples={}
for action in bpy.data.actions:
 name=action.name.split('|')[-1]
 if name not in ('Swim','HeldStruggle'):continue
 reference.animation_data.action=action
 if len(action.slots):reference.animation_data.action_slot=action.slots[0]
 samples[name]={}
 for frame in range(1,31):
  bpy.context.scene.frame_set(frame)
  samples[name][frame]={b.name:b.matrix.copy() for b in reference.pose.bones}
assert set(samples)=={'Swim','HeldStruggle'}
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Tools/WhiteSkate/Source/WhiteSkate.blend'))
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');body=next(o for o in bpy.context.scene.objects if o.type=='MESH')
assert len(arm.data.bones)==29 and body.data.uv_layers.active
# Capture the authored struggle before changing hierarchy or bone names.
struggle=bpy.data.actions['Stingray Struggling']
arm.animation_data.action=struggle
if len(struggle.slots):arm.animation_data.action_slot=struggle.slots[0]
rename={f'Bone.{10+i:03d}':('Bone' if i==0 else f'Bone.{i:03d}') for i in range(9)}
samples['HeldStruggle']={}
for frame in range(1,31):
 bpy.context.scene.frame_set(frame)
 samples['HeldStruggle'][frame]={rename.get(b.name,b.name):b.matrix.copy() for b in arm.pose.bones}

mat=body.data.materials[0];bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
colour=bsdf.inputs['Base Color'].links[0].from_node.image
mr=bsdf.inputs['Metallic'].links[0].from_node.inputs[0].links[0].from_node.image
for image,name in [(colour,'Albedo'),(mr,'MetallicRoughness')]:
 assert image.packed_file
 (ROOT/'Tools/WhiteSkate/Source'/f'{name}.packed').write_bytes(image.packed_file.data)
# Keep supplied geometry, corner UVs and weights. Match the installed rig hierarchy.
arm.animation_data.action=None;arm.data.pose_position='REST'
for i in range(9):arm.data.bones[f'Bone.{10+i:03d}'].name='Bone' if i==0 else f'Bone.{i:03d}'
bpy.context.view_layer.objects.active=arm;arm.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for bone in arm.data.edit_bones:
 bone.parent=arm.data.edit_bones.get(parents[bone.name]) if parents[bone.name] else None
 bone.use_connect=False
bpy.ops.object.mode_set(mode='OBJECT')
rest_error=max(abs(b.matrix_local[i][j]-rest[b.name][i][j]) for b in arm.data.bones for i in range(4) for j in range(4))
assert rest_error<1e-4,rest_error
arm.data.pose_position='POSE'
for a in list(bpy.data.actions):bpy.data.actions.remove(a)
def depth(b):return 0 if b.parent is None else 1+depth(b.parent)
bones=sorted(arm.pose.bones,key=depth);error=0
for name,frames in samples.items():
 action=bpy.data.actions.new(name);arm.animation_data.action=action;action.use_fake_user=True
 for frame,poses in frames.items():
  bpy.context.scene.frame_set(frame)
  for b in bones:
   b.rotation_mode='QUATERNION';b.matrix=poses[b.name];bpy.context.view_layer.update()
   for channel in ('location','rotation_quaternion','scale'):b.keyframe_insert(channel,frame=frame)
  error=max(error,max(abs(b.matrix[i][j]-poses[b.name][i][j]) for b in bones for i in range(4) for j in range(4)))
assert error<1e-4,error
# Same roll as the installed Leopard; new atlas UVs must not decide orientation.
arm.matrix_world=Matrix.Rotation(-math.pi/2,4,'Y')@arm.matrix_world
arm.animation_data.action=bpy.data.actions['Swim'];bpy.context.scene.frame_set(1)
for o in bpy.context.scene.objects:o.select_set(False)
for o in [arm,body]:o.hide_set(False);o.hide_viewport=False;o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'WhiteSkate.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_simplify_factor=0,path_mode='AUTO')
print('VERIFIED',json.dumps(dict(bones=len(bones),vertices=len(body.data.vertices),rest_error=rest_error,pose_error=error,actions=list(samples))))
