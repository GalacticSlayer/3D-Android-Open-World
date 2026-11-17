"""Render transparent UI icons from the same prepared Level 8 meshes and UVs."""
import bpy,json,math
from mathutils import Vector,Matrix
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
for kind in ['Rod','Reel']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 scene=bpy.context.scene
 scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=24
 scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100
 scene.render.film_transparent=True
 scene.view_settings.view_transform='Standard'
 mat=bpy.data.materials.new('Original UV texture');mat.use_nodes=True
 nodes=mat.node_tree.nodes;bsdf=nodes.get('Principled BSDF')
 tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ROOT/'Assets/_Game/Fishing/RodReel/Level8/Source/Level8EquipmentTexture.png'))
 mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
 bsdf.inputs['Roughness'].default_value=.42;bsdf.inputs['Metallic'].default_value=.15
 points=[]
 data=json.loads((ROOT/f'Tools/Level8Gear/Prepared/Level8Fishing{kind}.json').read_text())
 for part in data['parts']:
  vs=[Vector(part['vertices'][i:i+3])+Vector(part['position']) for i in range(0,len(part['vertices']),3)]
  if kind=='Rod':vs=[Matrix.Rotation(-math.pi/4,3,'Z')@p for p in vs]
  triangles=[part['triangles'][i:i+3] for i in range(0,len(part['triangles']),3)]
  mesh=bpy.data.meshes.new(part['name']);mesh.from_pydata(vs,[],triangles);mesh.update()
  uv=mesh.uv_layers.new()
  for l in mesh.loops:uv.data[l.index].uv=part['uv'][l.vertex_index*2:l.vertex_index*2+2]
  obj=bpy.data.objects.new(part['name'],mesh);scene.collection.objects.link(obj);mesh.materials.append(mat);points.extend(vs)
 lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)));center=(lo+hi)/2
 camdata=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',camdata);scene.collection.objects.link(cam);scene.camera=cam
 direction=Vector((0,0,1)) if kind=='Rod' else Vector((2,-3,1.5)).normalized()
 cam.location=center+direction*max((hi-lo).length*3,1);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();camdata.type='ORTHO'
 inv=cam.rotation_euler.to_matrix().transposed();projected=[inv@(p-center) for p in points]
 camdata.ortho_scale=max(max(p[i] for p in projected)-min(p[i] for p in projected) for i in [0,1])*1.15
 world=bpy.data.worlds.new('Studio');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(1,1,1,1);world.node_tree.nodes['Background'].inputs[1].default_value=.7
 for offset,power,size in [((2,-1,4),150,3),((-2,2,3),100,2)]:
  lightdata=bpy.data.lights.new('Softbox','AREA');lightdata.energy=power;lightdata.shape='DISK';lightdata.size=size
  light=bpy.data.objects.new('Softbox',lightdata);scene.collection.objects.link(light);light.location=center+Vector(offset);light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
 scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
 scene.render.filepath=str(ROOT/f'Assets/Resources/Fishing/ItemIcons/{kind}7.png')
 bpy.ops.render.render(write_still=True)
