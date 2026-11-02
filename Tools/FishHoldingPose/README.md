# Held fish pose

POSE appears below MENU / TRAVEL while holding a fish. Every species and size uses
Female Holding a Small Fish Pose, looping at its authored speed. The existing
right-hand string, positioning, orientation, sway and fish struggle are unchanged.
The big-fish pose and its size-based selection have been removed.

Movement, swimming, seating, opening a menu, changing perspective, or putting the
fish away ends posing. Local/offline and multiplayer avatars use the same pose.

Unity imports the small-pose FBX as Generic and updates FemaleRemoteAvatar on
editor load, entering Play Mode, and before builds.

Checks:
- `python Tools/FishHoldingPose/verify_fbx.py` validates the small-pose skeleton.
- `node --test Server/tests/sessions.test.mjs` checks network pose replication.
- In Unity, check small and large fish, rays/skates, toggling, and observers.
