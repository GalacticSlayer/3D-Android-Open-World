# Popper, Light Jig and Heavy Jig cosmetic replacements

Stable lure slots: Heavy Jig = 0 (Neon Breach), Light Jig = 1 (Reef Minnow),
Popper = 4 (Bloody Bait). Ownership, saved selection, prices, catch probabilities,
retrieval speed, depth and fish-size rules are unchanged. Fire Shad and Metal Spoon
retain their existing models and animation.

Unity automatically builds the three prefabs after compilation, before Play and
before a player build. No Blender installation or manual asset placement is needed.
A forced rebuild is available at Tools > Open World > Rebuild Authored Lures.

The supplied FBX and Blend originals are preserved in Source. Geometry, split
normals, UV0 and evaluated body/hook motion are exported from the Blend files so
parent transforms and Child Of constraints are baked accurately. Each matching
nine-frame action is cycled independently, including the hooks' one-frame phase
offset. Animation curves retain the shared retrieve/pause behavior.

Packed colour and metallic/roughness textures are decoded to lossless PNG;
conversion verifies exact decoded pixel equality. UVs are unmodified. Colour is
sRGB, metallic/roughness is linear (B = metallic, G = roughness). Hooks retain their
own Blender material, without disconnected legacy image nodes. Separate shader
keeps existing equipment unchanged. LineAttach follows the body's nose;
RetrieveFrame/PreviewFrame provide upright world and side-profile UI alignment.

Reproduce using Blender 5.2 and Python/Pillow:

```
blender -b --python Tools/LureReplacement/export_lures.py
python Tools/LureReplacement/convert_textures.py
python Tools/LureReplacement/check_replacement.py
```

Export checks cover all 73 sampled poses per mesh, original UV corners, non-static
body/hook motion, and matching loop endpoints. Unity compilation, shader rendering
and device Play Mode must additionally be checked in the Unity editor.

Popper's full-resolution PNG is stored as two byte parts to fit upload limits.
The importer verifies their combined SHA-256 and reconstructs the identical PNG
under Generated before configuring its sRGB import. No downsampling or lossy
compression is used; the source parts are outside Resources and are not bundled
into the player.
