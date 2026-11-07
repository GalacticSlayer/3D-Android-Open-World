# The Windgrove Shoal — manual turbine placement

The island display name remains The Windgrove Shoal. Save IDs are unchanged.

The original 30 generated placements are retired. The former scene installer
now removes only the original numbered WindgroveTurbine wrappers beneath
The Windgrove Shoal - Wind Turbines when PrototypeWorld opens, enters Play Mode,
or builds. It never creates replacement objects. Empty generated roots are
removed; unrelated children and protected user scenery are preserved.
A clean open scene is saved after cleanup. If already dirty, save normally.
This migration avoids distributing another scene edit that conflicts with
locally saved terrain and scenery.

Model for manual placement:
Assets/Resources/Environment/Windgrove/WindTurbine.fbx

Original supplied files retained at:
ArtSources/WindgroveShoal/SingleWindTurbine-original.fbx
ArtSources/WindgroveShoal/SingleWindTurbine.blend

Drag the imported FBX into the scene and adjust its placement and scale manually.
Do not add WindgroveTurbine to these direct model instances: it is the old
wrapper component and would instantiate another model child.
After the authored scene is supplied, animation playback will be configured
without changing the user's transforms or model-part proportions.

The imported FBX is now a byte-for-byte copy of the original supplied FBX.
No Blender re-export, part-transform corrections, or custom scaling are applied.
The material importer retains the existing approved URP colors.
Unity gameplay/visual validation must be performed in the Unity Editor.
The old placement/export/verification scripts are historical tooling only;
do not rerun place_turbines.py to repopulate the scene or prepare_turbine.py
to rewrite the supplied model. verify_turbine.py validates the retired modified
export and does not apply to the restored original FBX.

## Nine manually placed Windgrove turbines (from scene c6f309a2)
The nine user-authored FBX instances each define a 150m horizontal biome
radius, discovery trigger and fish-catching zone. These are biome 6 with
the stable ID windgrove-shoal. Existing biome 5 (offshore-oil-rig) keeps
the Black Marlin and oil-rig terrain, arrival and player unlock.
Windgrove is invisible in Island / Fish Index until entering any turbine
radius by boat, swimming or walking. Travel arrives 9m north of the
northernmost turbine at water level +3m. Root transforms and geometry stay
as saved by the user. The imported rotor clip plays continuously while
static parts are held in place, including the nacelle anemometer mast.
Until dedicated shoal species are provided, the editable Windgrove fishing
probabilities, weights, HP and coin ranges are seeded with non-exclusive offshore fish.
