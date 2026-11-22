OCEANIX DATACENTER — READY-TO-PLACE UNITY PREFAB

Drag Oceanix_Datacenter.prefab into your scene. Press Play: lights animate
automatically. Do not drag the raw FBX if you want the configured lights and
colliders. No material assignment or script setup is required.

Target: Unity 6000.3.24f1, Universal Render Pipeline 17.3.0 (this repository).
48,176 Unity triangles (48,240 in Blender); metre scale; four server compartments; two submerged
cooling pods; enlarged platform; current right-side staircase.

All 12 URP surface materials are included and assigned. This model uses
material colours, metallic/smoothness values and emissive surfaces, not bitmap
texture maps. There are no external texture files to locate or download.

Lights loop indefinitely in Play Mode and in builds:
  Server strips: smooth rainbow, 4 seconds per loop.
  Blue/pink accents: blue -> pink -> blue, 5.333 seconds per full loop.
  Cooling strips: blue on/off pulse, 2.667 seconds per loop.
  Control-house / roof lights: constant blue.
Warm utility lights retain their warm colour. Emission is built into the
materials; a scene's Bloom setting controls the optional surrounding glow.
No real-time point lights or extra lighting setup are needed for the strips.

The root has OceanixLightCycle attached and enabled. It uses time-based
continuous cycles and creates shared material copies per prefab instance,
without modifying source materials. The timing fields remain editable.

Colliders are already serialized: separate boxes for the hull, server units,
equipment, underwater units and plumbing; an exact, non-convex MeshCollider
for the current staircase. Walkways are not covered by a single equipment
bounding box. This is a static structure: no Rigidbody is required. Use your
player controller's stair step handling for the approximately 18 cm risers.

Keep this entire Oceanix folder and its .meta files together when copying it.
Alternatively import the supplied Oceanix_Datacenter.unitypackage.
The Blender source is stored outside Assets under ArtSources/Oceanix to avoid
requiring Blender during Unity imports. No existing game scenes are changed.

Validation.txt records the editor/import and Play Mode checks.
