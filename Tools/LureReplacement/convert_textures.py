"""Convert extracted packed images to PNG without changing their decoded pixels."""
from pathlib import Path
from PIL import Image, ImageChops
ROOT=Path(__file__).resolve().parents[2]
for name in ['Popper','LightJig','HeavyJig']:
 for filename in ['Albedo.png','MetallicRoughness.png']:
  packed=ROOT/'Tools/LureReplacement/Source'/f'{name}_{filename}.packed'
  out=ROOT/'Assets/_Game/Fishing'/name/filename
  with Image.open(packed) as original:
   original.load()
   original.save(out,format='PNG')
   with Image.open(out) as decoded:
    decoded.load()
    assert decoded.size==original.size==(4096,4096)
    assert ImageChops.difference(original.convert('RGBA'),decoded.convert('RGBA')).getbbox() is None
   print(name,filename,'verified exact decoded pixels from packed',original.format)

# Preserve the exact canonical Popper PNG in upload-sized parts.
png=ROOT/'Assets/_Game/Fishing/Popper/Albedo.png'
data=png.read_bytes()
for i,part in enumerate([data[:7000000],data[7000000:]]):
 png.with_name(f'Albedo.part{i}.bytes').write_bytes(part)
png.unlink()
