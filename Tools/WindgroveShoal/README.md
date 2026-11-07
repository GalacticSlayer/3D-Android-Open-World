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
