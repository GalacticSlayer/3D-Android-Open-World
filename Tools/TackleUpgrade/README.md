# Level 5/6 equipment

Unity-ready native meshes and prefabs are committed under Assets. Blender is not required to import or play the game. Original Blender and FBX files are retained in Source; Prepared contains evaluated frame-1 geometry with the authored UVs. Blender geometry is authoritative because the supplied reel FBXs have different animated-part rest transforms.

To regenerate: run Blender 5 with `-b --python Tools/TackleUpgrade/export_geometry.py`, then `python Tools/TackleUpgrade/build_unity_assets.py` (requires NumPy). Both Level 6 materials share one texture. Existing prefab transforms and animation components are preserved.

Run `python Tools/TackleUpgrade/validate_assets.py` for mesh/UV/reference/transform checks. In Unity, run the Fishing Burst Damage validation menu to verify every rod's deterministic damage range. Play-mode validation should cover hook, escape, catch, re-equip, save/reload, and the new shop/inventory tiers. Unity is not installed in the implementation environment.
