"""Decode packed Blender textures without changing pixels (requires Pillow)."""
from pathlib import Path
from PIL import Image,ImageChops
R=Path(__file__).resolve().parents[2]
for packed,name in [('Albedo','WhiteSkateTexture'),('MetallicRoughness','WhiteSkateMetallicRoughness')]:
 with Image.open(R/f'Tools/WhiteSkate/Source/{packed}.packed') as original:
  original.load();out=R/f'Assets/_Game/Reef/Source/{name}.png';original.save(out,format='PNG')
  with Image.open(out) as result:
   result.load();assert ImageChops.difference(original.convert('RGBA'),result.convert('RGBA')).getbbox() is None
  print(name,'decoded pixels verified')
