"""Blender 4.3+: preserve the supplied FBX's geometry/materials and rotor motion.
The source exported the rotor take onto every object; freeze non-rotor parts.
Run: blender -b --python Tools/WindgroveShoal/prepare_turbine.py -- source.fbx output.fbx
"""
import bpy, sys, json
from mathutils import Vector
src,dst=sys.argv[sys.argv.index('--')+1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
s=bpy.context.scene
rotor=bpy.data.objects['Rotor blade 2']
a=next(a for a in bpy.data.actions if a.name.startswith('Rotor blade 2|') and a.frame_range[1]>2)
s.frame_set(1)
for o in list(s.objects):
 matrix=o.matrix_world.copy()
 o.animation_data_clear()
 o.matrix_world=matrix
rotor.animation_data_create();rotor.animation_data.action=a
s.frame_start=1;s.frame_end=101;s.render.fps=24
s.frame_set(1)
initial={o.name:o.matrix_world.copy() for o in s.objects}
for frame in [26,51,76,101]:
 s.frame_set(frame)
 for o in s.objects:
  if o!=rotor: assert all(abs(x-y)<1e-5 for r,t in zip(o.matrix_world,initial[o.name]) for x,y in zip(r,t)),o.name
 if frame!=101: assert any(abs(x-y)>0.1 for r,t in zip(rotor.matrix_world,initial[rotor.name]) for x,y in zip(r,t))
assert all(abs(x-y)<1e-4 for r,t in zip(rotor.matrix_world,initial[rotor.name]) for x,y in zip(r,t))
s.frame_set(1)
points=[o.matrix_world@Vector(p) for o in s.objects for p in o.bound_box]
print('Verified: stationary parts, moving rotor, closed loop. Height:',max(p.z for p in points)-min(p.z for p in points))
bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0.0)
