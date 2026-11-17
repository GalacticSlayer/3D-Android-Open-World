# Level 8 fishing gear

Rod: 80–95 normal burst damage, 17% double critical chance, 13% tension control, 1,100,000 coins.
Reel: 2.4x retrieval speed (+140%), 0.9x fish escape speed in green/yellow/red/purple, 900,000 coins.
Both append at saved tier 7 and require the previous tier. Existing saved gear IDs are unchanged.

The uploaded Blender geometry and corner UVs are preserved. Six native Unity meshes use the Level 6 template transforms and existing reeling animation, with a white material and one shared original texture. No Blender installation or manual importer step is needed to play. Original FBX files are under Assets/_Game/Fishing/RodReel/Level8/Source; Blender originals are here in Source. Transparent Rod7/Reel7 icons are rendered from the prepared geometry and shared texture.

Regenerate with Blender 5: run export_geometry.py, then build_unity_assets.py (optionally supplying the uploaded texture directory), then render_icons.py. Run python3 Tools/Level8Gear/validate_assets.py and dotnet run --project Tools/Level8Gear/Checks.

To redraw just the shop/equipment reel icon from the prepared mesh, run `blender -b --python Tools/Level8Gear/render_icons.py -- Reel`. Its orientation follows the runtime reel cards' opposite-face view and 60° counterclockwise roll. The UI draws the water backdrop separately, behind the icon.

Server/runtime/sessions.mjs now accepts and advertises tier 7. Until that server update is deployed, outgoing room poses use the advertised maximum for both gear tiers; local gameplay and third-person visuals still use Level 8. Older observers do not have the new model. Server deployment is separate from the repository push.

Validation: native mesh indices/normals/UVs and unchanged transforms/animations; transparent icons; actual C# catalog, ledger and burst damage tests including save round trips; fishing drag regression harness; 26 Node session tests. Unity/Android Play validation remains pending.
