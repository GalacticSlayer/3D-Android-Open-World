# The Windgrove Shoal — authoritative manual layout

Scene: `Assets/Scenes/PrototypeWorld.unity`.
Nine manually placed, manually scaled FBX prefab instances from commit `c6f309a2`
are preserved exactly. Never run the old automatic turbine-placement scripts.

## Fishing and discovery
- Stable biome index: **5**, replacing the former offshore oil-rig biome.
- Stable biome ID: `windgrove-shoal`; the retired `offshore-oil-rig` catch
  string is recognized as a legacy save alias for previously caught fish.
- Only **Black Marlin** spawn here, with the former offshore-rig tackle
  chances, size distribution, fight/HP and coin values.
- Zone is the union of 150-m horizontal circles around **each** manually
  placed wind turbine. All other open ocean belongs to its existing biome.
- Windgrove does not appear in the Island/Fish Index until the player
  physically enters any 150-m circle (boats and swimming count).
- Saved discovery then unlocks the region index and teleport.
- Teleport destination: 9 m **north** of the northernmost wind turbine,
  3 m above the base water level (not multiplied by FBX scale).
- The old rig terrain, platform and arrival marker have been removed from
  the scene. The rig scene installer is retired, so these cannot reappear.

## Turbine animation and anemometer
The directly placed FBX instance is kept intact. At runtime a lightweight
component finds the imported `Rotor blade 2` transform and spins it around
the shaft direction from the nacelle at 86.4 degrees/second. This works even
when the legacy FBX animation clip is absent/incompatible or culled and
continues through timeScale changes. Nonrotor transforms stay stationary;
no turbine root position, rotation or scale is changed.
The anemometer mast's base is aligned with the top service cap without
modifying the manually placed root or rebuilding the entire FBX.

## Ocean outside finite terrain
Outside terrain-map extents, ambient fish use deep open-water defaults.
Fishing hotspots use the ocean surface and still check real land within
15 m of the ripple footprint. Boat placement validates any mapped seabed
under the hull and solid collisions, but no longer demands terrain at
every hull point. Unmapped sea is not an invisible coastline.

## Testing in Unity
1. Pull main and let Unity import/compile the changed scripts.
2. Check all nine rotors in Play Mode; towers and anemometers stay fixed.
3. Enter Windgrove and confirm one unlock, one region and a working teleport.
4. Catch a Black Marlin with Worms and a lure; no other species should bite.
5. Fish, see ripple hotspots and place each boat over open water outside the
   last terrain tile, then test near a genuine shore and obstacle.
6. Verify tournament/server and the other islands behave unchanged.

`Tools/WindgroveShoal/verify_scene.py` verifies the saved scene layout and
the retired oil-rig removal. The code and tables can be statically checked,
but gameplay/visual validation requires a Unity runtime.
