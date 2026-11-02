# Held fish poses

POSE appears immediately below MENU / TRAVEL while holding a fish. It toggles the
supplied character pose and opens third person when needed. Movement, swimming,
seating, opening a menu, changing perspective, or putting the fish away ends it.

Fish shorter than 1.6 m, Roughtail Stingray, Leopard Stingray and White Skate use
the small pose with the existing right-hand string and struggle. Other fish at
1.6 m or longer use the big pose, centered across the forearms with a mesh-measured
belly support point. The big pose suppresses the string, sway and struggle.
Normal held behavior resumes when the pose ends. Fish size and gameplay are unchanged.

Unity imports both supplied FBXs as Generic clips and updates FemaleRemoteAvatar
on editor load, entering Play Mode, and before builds. The source FBX files are
unchanged. Both supplied pose clips loop at authored speed while posing. The big
pose adjusts only the visual root height after animation sampling, keeping its
lowest foot at the calibrated standing foot clearance. Camera focus follows the
posed hips. Fish centering uses the rendered bounds; belly clearance still uses
the sampled mesh. The existing fish and character orientations are preserved.

Multiplayer adds an optional `fishPosing` boolean. Update the server using its
existing Server/ops/update.sh workflow to relay it; older servers ignore the
field and still support ordinary holding. Local/offline poses work independently.

Checks:
- `python Tools/FishHoldingPose/verify_fbx.py` validates both complete skeleton
  hierarchies against the existing holding FBX and bone names against the model.
- `node --test Server/tests/sessions.test.mjs` verifies pose replication, clearing,
  malformed values, old-client defaults, and the existing multiplayer sessions.
- Unity visual checks: fish just below/exactly/above 1.6 m; all three ray/skate
  species above 1.6 m; toggle back during a struggle; put away/swap fish; movement;
  orbit camera; observer joins while posing. Inspect arm contact on differently
  shaped large fish. No Unity editor is installed in the implementation environment.

The full server baseline has seven existing tournament failures relating to
species/weight expectations; the session tests pass.
