"""Render transparent UI icons from the same prepared Level 8 meshes and UVs."""
import bpy,json,math,sys
from mathutils import Vector,Matrix
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]

def bounds(points):
 return (Vector(tuple(min(p[i] for p in points) for i in range(3))),
         Vector(tuple(max(p[i] for p in points) for i in range(3))))

def reel_card_transform(positions):
 # Prepared reel coordinates are relative to the Unity ReelMount at the origin.
 # Match ShopPreview: align the mount/spool/handle, yaw to the opposite face,
 # then apply the +60 degree counterclockwise rotation in the card plane.
 def center(name):
  lo,hi=bounds(positions[name])
  return (lo+hi)/2
 spool=center('Spool')
 up=(-spool).normalized()
 right=center('Handle')-spool
 right=(right-up*right.dot(up)).normalized()
 forward=right.cross(up)
 source_basis=Matrix((right,up,forward))
 yaw=Matrix.Rotation(math.radians(180-22),3,'Y')
 roll=Matrix.Rotation(math.radians(60),3,'Z')
 # Unity screen right/up/depth -> Blender X/Z/Y. The axis swap reverses
 # handedness, so the caller reverses triangle winding for the reel.
 to_blender=Matrix(((1,0,0),(0,0,1),(0,1,0)))
 return to_blender@roll@yaw@source_basis

kinds=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
if not kinds:kinds=['Rod','Reel']
assert all(kind in ('Rod','Reel') for kind in kinds),'Choose Rod and/or Reel after --'
for kind in kinds:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 scene=bpy.context.scene
 scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=24
 if kind=='Reel':
  scene.cycles.use_denoising=False;scene.cycles.samples=64
 scene.render.resolution_x=512;scene.render.resolution_y=512;scene.render.resolution_percentage=100
 scene.render.film_transparent=True
 scene.view_settings.view_transform='Standard'
 mat=bpy.data.materials.new('Original UV texture');mat.use_nodes=True
 nodes=mat.node_tree.nodes;bsdf=nodes.get('Principled BSDF')
 tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ROOT/'Assets/_Game/Fishing/RodReel/Level8/Source/Level8EquipmentTexture.png'))
 mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
 bsdf.inputs['Roughness'].default_value=.42;bsdf.inputs['Metallic'].default_value=.15
 points=[]
 parts=json.loads((ROOT/f'Tools/Level8Gear/Prepared/Level8Fishing{kind}.json').read_text())['parts']
 positions={part['name']:[Vector(part['vertices'][i:i+3])+Vector(part['position'])
                         for i in range(0,len(part['vertices']),3)] for part in parts}
 transform=(Matrix.Rotation(-math.pi/4,3,'Z') if kind=='Rod'
            else reel_card_transform(positions))
 for part in parts:
  vs=[transform@p for p in positions[part['name']]]
  triangles=[part['triangles'][i:i+3] for i in range(0,len(part['triangles']),3)]
  if kind=='Reel':triangles=[tri[::-1] for tri in triangles]
  mesh=bpy.data.meshes.new(part['name']);mesh.from_pydata(vs,[],triangles);mesh.update()
  uv=mesh.uv_layers.new()
  for l in mesh.loops:uv.data[l.index].uv=part['uv'][l.vertex_index*2:l.vertex_index*2+2]
  if kind=='Reel':
   for face in mesh.polygons:face.use_smooth=True
   normals=part['normals']
   mesh.normals_split_custom_set_from_vertices([transform@Vector(normals[i:i+3]) for i in range(0,len(normals),3)])
  obj=bpy.data.objects.new(part['name'],mesh);scene.collection.objects.link(obj);mesh.materials.append(mat);points.extend(vs)
 lo,hi=bounds(points);center=(lo+hi)/2
 camdata=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',camdata);scene.collection.objects.link(cam);scene.camera=cam
 direction=Vector((0,0,1)) if kind=='Rod' else Vector((0,-1,0))
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
