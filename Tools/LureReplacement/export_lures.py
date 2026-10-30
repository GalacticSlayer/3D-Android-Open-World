"""Blender 5.2, background: --python Tools/LureReplacement/export_lures.py.
Preserve original UV corners, evaluated parent/constraint motion and packed maps.
Canonical frame: the body's -X nose becomes Unity +Z; local +Z stays up.
"""
import bpy, json, math
from pathlib import Path
from mathutils import Matrix, Vector
ROOT=Path(__file__).resolve().parents[2]
SCALE=.032892715
for source,name in [('Popper Lure','Popper'),('Light Jig','LightJig'),('Heavy Jig','HeavyJig')]:
 bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Tools/LureReplacement/Source'/f'{source}.blend'))
 scene=bpy.context.scene
 # All supplied actions have matching endpoint keys and a nine-frame period.
 # Cycle each channel independently, preserving the hook's one-frame offset.
 for a in bpy.data.actions:
  for layer in a.layers:
   for strip in layer.strips:
    for bag in strip.channelbags:
     for f in bag.fcurves:
      assert abs(f.keyframe_points[0].co.y-f.keyframe_points[-1].co.y)<1e-5
      if not any(m.type=='CYCLES' for m in f.modifiers):f.modifiers.new('CYCLES')
  a.update_tag()
 for o in bpy.data.objects:o.update_tag(refresh={'OBJECT'})
 scene.frame_set(0);scene.frame_set(1)
 body=next(o for o in scene.objects if o.type=='MESH' and o.name.startswith('geometry'))
 objects=[body]+sorted([o for o in scene.objects if o.type=='MESH' and o!=body],key=lambda o:o.name)
 C=Matrix(((0,1,0),(0,0,1),(-1,0,0)))@body.matrix_world.to_3x3().normalized().inverted()*SCALE
 origin=body.matrix_world.translation.copy()
 out=ROOT/'Assets/_Game/Fishing'/name;out.mkdir(parents=True,exist_ok=True)
 mat=body.data.materials[0];bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
 albedo=bsdf.inputs['Base Color'].links[0].from_node.image
 separate=bsdf.inputs['Metallic'].links[0].from_node
 mr=separate.inputs[0].links[0].from_node.image
 assert bsdf.inputs['Metallic'].links[0].from_socket.name=='Blue'
 assert bsdf.inputs['Roughness'].links[0].from_socket.name=='Green'
 for im,filename in [(albedo,'Albedo.png'),(mr,'MetallicRoughness.png')]:
  assert im.packed_file and im.size[0]>0
  # Preserve embedded bytes; Pillow converts the packed WebP to lossless PNG.
  (ROOT/'Tools/LureReplacement/Source'/f'{name}_{filename}.packed').write_bytes(im.packed_file.data)
 # Average the small front cap to attach the line at the centre of the nose.
 xmin=min(v.co.x for v in body.data.vertices);xmax=max(v.co.x for v in body.data.vertices)
 nose=[v.co for v in body.data.vertices if v.co.x<xmin+.015*(xmax-xmin)]
 local=sum(nose,Vector())/len(nose);local.x=xmin
 line=C@(body.matrix_world@local-origin)
 parts=[];max_error=0
 for index,obj in enumerate(objects):
  scene.frame_set(1);mesh=obj.data;mesh.calc_loop_triangles();rest=obj.matrix_world.copy();pivot=rest.translation.copy()
  assert not obj.modifiers and mesh.uv_layers.active and len(mesh.materials)==1
  normal_matrix=(C@rest.to_3x3()).inverted().transposed()
  m=obj.data.materials[0];b=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
  part=dict(name='Body' if index==0 else 'Hook'+str(index),vertices=[],normals=[],uv=[],triangles=[],frames=[],metallic=b.inputs['Metallic'].default_value,smoothness=1-b.inputs['Roughness'].default_value,color=dict(zip('rgba',(1,1,1,1) if index==0 else b.inputs['Base Color'].default_value)))
  corners=[]
  for tri in mesh.loop_triangles:
   start=len(part['vertices'])
   for li in tri.loops:
    corners.append(li)
    co=C@(rest@mesh.vertices[mesh.loops[li].vertex_index].co-pivot)
    normal=(normal_matrix@mesh.corner_normals[li].vector).normalized();uv=mesh.uv_layers.active.data[li].uv
    part['vertices'].append(dict(zip('xyz',co)));part['normals'].append(dict(zip('xyz',normal)));part['uv'].append(dict(zip('xy',uv)))
   part['triangles'].extend([start,start+2,start+1])
  previous=None
  for i in range(73):
   frame=1+9*i/72;scene.frame_set(int(frame),subframe=frame-int(frame))
   current=obj.matrix_world.copy();delta=C@current.to_3x3()@rest.to_3x3().inverted()@C.inverted()
   quat=delta.to_quaternion().normalized()
   if previous and quat.dot(previous)<0:quat.negate()
   previous=quat.copy();pos=C@(current.translation-origin)
   part['frames'].append(dict(time=i/(8*scene.render.fps),position=dict(zip('xyz',pos)),rotation=dict(zip('xyzw',(quat.x,quat.y,quat.z,quat.w)))))
   for li,v in zip(corners,part['vertices']):
    expected=C@(current@mesh.vertices[mesh.loops[li].vertex_index].co-origin)
    actual=quat@Vector([v[k] for k in 'xyz'])+pos;max_error=max(max_error,(expected-actual).length)
  for key,axes in [('position','xyz'),('rotation','xyzw')]:
   assert max(abs(part['frames'][0][key][a]-part['frames'][-1][key][a]) for a in axes)<1e-5,(name,part['name'],key,part['frames'][0],part['frames'][-1])
  assert max(sum((f['rotation'][a]-part['frames'][0]['rotation'][a])**2 for a in 'xyzw') for f in part['frames'])>.01
  parts.append(part)
 assert max_error<1e-6,max_error
 data=dict(version=1,duration=9/scene.render.fps,fps=8*scene.render.fps,lineAttach=dict(zip('xyz',line)),parts=parts)
 (out/'AuthoredLure.json').write_text(json.dumps(data,separators=(',',':')))
 print('VERIFIED',name,'parts',len(parts),'triangles',sum(len(p['triangles'])//3 for p in parts),'max animated vertex error',max_error,'line',list(line),'packed maps',albedo.name,mr.name)
