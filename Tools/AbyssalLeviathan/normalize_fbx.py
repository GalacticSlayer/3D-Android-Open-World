"""Rename only rig labels to the existing Mako presentation contract.
All mesh/UV/skin/animation property payloads remain byte-for-byte unchanged.
Usage: python normalize_fbx.py source.fbx destination.fbx
"""
import struct, sys
from pathlib import Path
source=Path(sys.argv[1]).read_bytes()
wide=struct.unpack_from('<I',source,23)[0]>=7500
fmt='<QQQB' if wide else '<IIIB';hs=25 if wide else 13
mapping={'Bone.001':'DorsalFin.001','Bone.002':'DorsalFin.002',
         **{'Bone.%03d.R'%i:'Bone.%03d'%i for i in range(1,5)}}
def node(at):
 end,count,size,n=struct.unpack_from(fmt,source,at)
 if not end:return None,at+hs
 name=source[at+hs:at+hs+n];pos=at+hs+n;props=[]
 for i in range(count):
  start=pos;t=chr(source[pos]);pos+=1
  if t in 'YCIFDL':pos+={'Y':2,'C':1,'I':4,'F':4,'D':8,'L':8}[t]
  elif t in 'SR':
   length=struct.unpack_from('<I',source,pos)[0];pos+=4+length
  elif t in 'fdlibc':
   length=struct.unpack_from('<I',source,pos+8)[0];pos+=12+length
  else:raise ValueError(t)
  raw=source[start:pos]
  if t=='S':
   value=raw[5:];parts=value.split(b'\0',1);old=parts[0].decode('utf8')
   if old in mapping:
    value=mapping[old].encode()+(b'\0'+parts[1] if len(parts)>1 else b'')
    raw=b'S'+struct.pack('<I',len(value))+value
  props.append(raw)
 children=[]
 while pos<end-hs:
  child,pos=node(pos)
  if child is None:break
  children.append(child)
 # Leaf nodes may have no null terminator.
 null=source[pos:end]
 return (name,props,children,null),end
roots=[];pos=27
while True:
 n,pos=node(pos)
 if n is None:break
 roots.append(n)
footer=source[pos:]
def encode(n,start):
 name,props,children,null=n;payload=b''.join(props);offset=start+hs+len(name)+len(payload);out=[]
 for c in children:
  data=encode(c,offset);out.append(data);offset+=len(data)
 return struct.pack(fmt,offset+len(null),len(props),len(payload),len(name))+name+payload+b''.join(out)+null
out=source[:27]
for n in roots:out+=encode(n,len(out))
out+=bytes(hs)+footer
Path(sys.argv[2]).write_bytes(out)
