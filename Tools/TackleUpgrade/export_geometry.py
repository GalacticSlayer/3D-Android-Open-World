"""Run with Blender 5.x: blender -b --python Tools/TackleUpgrade/export_geometry.py.
Preserves the supplied corner UVs/normals and the existing animated reel-part contract.
"""
import bpy,json
from pathlib import Path
from mathutils import Matrix,Vector
ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'Tools/TackleUpgrade/Source'
OUTPUT=ROOT/'Tools/TackleUpgrade/Prepared'
MAP={'Plane':'ReelFootAndBody','Cylinder':'Rotor','Cylinder.001':'ReelHousing','Cylinder.002':'Spool','Cylinder.003':'Handle'}

def export(label,rod=False):
 bpy.ops.wm.open_mainfile(filepath=str(SOURCE/(label+'.blend')))
 bpy.context.scene.frame_set(1)
 deps=bpy.context.evaluated_depsgraph_get()
 parts=[]
 objects={o.name:o for o in bpy.context.scene.objects if o.type=='MESH'}
 names={next(iter(objects)):'RodBlank'} if rod else MAP
 assert set(objects)==set(names),(label,list(objects))
 for name,target in names.items():
  obj=objects[name];evaluated=obj.evaluated_get(deps)
  mesh=evaluated.to_mesh(preserve_all_data_layers=True,depsgraph=deps)
  try:
   mesh.calc_loop_triangles();assert mesh.uv_layers.active is not None
   world=obj.matrix_world.copy()
   if rod:
    points=[world@v.co for v in mesh.vertices]
    lo=Vector(tuple(min(p[i] for p in points) for i in range(3)))
    hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
    axis=max(range(3),key=lambda i:hi[i]-lo[i]);a,b=[i for i in range(3) if i!=axis]
    sign=1 if abs(lo[axis])<=abs(hi[axis]) else -1
    base=lo[axis] if sign==1 else hi[axis]
    # Match the established RodTip height; preserve aspect ratio and original UVs.
    scale=1.80232/(hi[axis]-lo[axis])
    rows=[[0.0]*3 for _ in range(3)];rows[0][a]=scale;rows[1][axis]=scale*sign;rows[2][b]=scale
    basis=Matrix(rows)
    linear=basis@world.to_3x3()
    position=basis@(world.translation-Vector(tuple(base if i==axis else (lo[i]+hi[i])*.5 for i in range(3))))
   else:
    basis=Matrix(((1,0,0),(0,-1,0),(0,0,-1)))*.04
    linear=basis@obj.matrix_local.to_3x3();position=basis@obj.matrix_local.translation
   normal_matrix=linear.inverted().transposed();flip=linear.determinant()<0
   part={'name':target,'position':list(position),'vertices':[],'normals':[],'tangents':[],'uv':[],'triangles':[]}
   for tri in mesh.loop_triangles:
    indices=[]
    ps=[linear@mesh.vertices[mesh.loops[li].vertex_index].co for li in tri.loops]
    us=[mesh.uv_layers.active.data[li].uv for li in tri.loops]
    e1,e2=ps[1]-ps[0],ps[2]-ps[0];d1,d2=us[1]-us[0],us[2]-us[0]
    det=d1.x*d2.y-d1.y*d2.x
    tangent=(e1*d2.y-e2*d1.y)/det if abs(det)>1e-12 else Vector((1,0,0))
    bitangent=(e2*d1.x-e1*d2.x)/det if abs(det)>1e-12 else Vector((0,0,1))
    for li in tri.loops:
     loop=mesh.loops[li];indices.append(len(part['vertices'])//3)
     p=linear@mesh.vertices[loop.vertex_index].co
     n=(normal_matrix@mesh.corner_normals[li].vector).normalized()
     t=(tangent-n*n.dot(tangent)).normalized()
     part['vertices'].extend(p);part['normals'].extend(n)
     part['tangents'].extend((*t,(-1 if n.cross(t).dot(bitangent)<0 else 1)))
     part['uv'].extend(mesh.uv_layers.active.data[li].uv)
    part['triangles'].extend(indices[::-1] if flip else indices)
   parts.append(part)
  finally:evaluated.to_mesh_clear()
 total=sum(len(p['triangles'])//3 for p in parts)
 assert total==(1340 if rod else 1772),(label,total)
 OUTPUT.mkdir(parents=True,exist_ok=True)
 path=OUTPUT/(label.replace(' ','')+'.json')
 path.write_text(json.dumps({'parts':parts},separators=(',',':')))
 print('EXPORTED',label,total,'triangles; original corner UVs retained')
export('Level 5 Fishing Reel')
export('Level 6 Fishing Reel')
export('Level 6 Fishing Rod',True)
