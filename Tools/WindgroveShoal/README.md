# The Windgrove Shoal

The former Offshore Oil Rig display name and discovery toast are now The Windgrove Shoal.
Biome ID 5, the `offshore-oil-rig` save ID, discovery flags, fishing behavior,
arrival lookup and terrain profile remain compatible with existing saves.

PrototypeWorld contains 30 saved, seeded placements under
**The Windgrove Shoal - Wind Turbines**, centered at (459.11584, -277.5, -3230.79875).
Move/rotate/scale each **Wind Turbine 01–30** parent in Edit Mode and save the scene.
Their positions are never recalculated, scattered, or snapped during play.
Deletion is also respected; no population manager recreates deleted placements.

On first editor import/open, the installer fills the placements with the model's
editable child hierarchy, then saves only if the scene was initially clean.
An already dirty scene stays dirty for the user's normal Save operation.
A build callback and runtime fallback cover first-open/build timing.
Each initial turbine is uniformly scaled to 900 m overall height (500 × the
saved 1.8 m player controller). All original part proportions are preserved.
The parent pivot is the bottom of the foundation; the initial foundation bases
are 280 m below sea level. At this deliberately enormous scale, blade sweeps
can overlap; move the saved parents to adjust the composition.

The supplied FBX exports the rotor take onto stationary objects as well.
`prepare_turbine.py` freezes those objects at their authored initial transforms
and retains the rotor's original 100-frame / 24-fps rotation. It checks stationary
parts, intermediate rotation and loop closure before exporting the Unity FBX.
The Blender source is preserved outside Assets so Unity does not depend on a
matching installed Blender version.

`WindgroveTurbine` samples the imported clip on the model child with unscaled time.
It runs continuously during gameplay, including offscreen and while timeScale is
zero, with independent phases. Moving the outer parent cannot be overwritten by
animation. Disabled/inactive objects resume at the current loop phase when enabled.
The usual Unity Edit Mode does not run the animation; enter Play Mode to preview it.

Materials retain the FBX's base colors and use URP Lit (Standard fallback).
Geometry and materials are shared across all 30 instances. No extra colliders or
large turbine shadows are added. Multiplayer/server code is untouched.

Validation: Blender export checks plus reimport verification and additive scene/
reference/placement checks. A Unity Editor and Android device run are still needed
for visual validation; Unity is not installed in the implementation environment.
