# Level 9 fishing gear

- Rod: 115–135 normal damage per burst, 19% chance of a double critical, 16% tension control, 7,800,000 coins.
- Reel: 2.6× starter retrieval speed (+160%) and 0.85× escape swim speed (15% reduction) across calm, neutral, angry and fragile states, 6,800,000 coins. This replaces Level 8's 10% reduction. The fixed Siren's Call pull and the duration of skill stun are unchanged.
- Each upgrade requires the corresponding Level 8 item. Both use appended saved tier ID 8. Older gear IDs, costs and stats are unchanged.

The two original Blender files are under `Source/`. The supplied FBXs and a single copy of the shared lightning texture are under `Assets/_Game/Fishing/RodReel/Level9/Source/`. The native Unity prefab meshes use the Blender files' authored corner UVs and frame-one reel pose. The FBX default pose alone does not preserve the animated reel parts' rest transforms. Materials use white base color and the original sRGB texture with repeat wrap. No Unity importer step or Blender installation is needed to use the checked-in runtime assets.

From the repository root, regenerate and validate with the same pipeline as Level 8:

```sh
blender -b --python-exit-code 1 --python Tools/Level8Gear/export_geometry.py -- --level 9
python Tools/Level8Gear/build_unity_assets.py --level 9
blender -b --python-exit-code 1 --python Tools/Level8Gear/render_icons.py -- --level 9
python Tools/Level8Gear/validate_assets.py
dotnet run --project Tools/Level8Gear/Checks
node --test Server/tests/sessions.test.mjs Server/tests/homes.test.mjs
```

Room and Home replication accept tier 8 in the updated runtime. On older servers, existing room capability negotiation limits the transmitted equipment pose to a supported tier without affecting local equipment or gameplay.
