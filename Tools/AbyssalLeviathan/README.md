# Abyssal Leviathan encounter

The new event-only species is ID 43. The supplied Blend and original FBX are
preserved in ArtSources. The runtime FBX changes bone labels only, matching the
existing Mako spine/presentation contract. Its mesh, UVs, skinning and animation
arrays are preserved. The existing SharkSpeciesSetup auto-installs the textured
prefab on Editor load / Play / build, selecting the ArmatureAction swim take
instead of the auxiliary Empty/fin takes. No Blender installation is required.
The shared Mako presentation handles swimming, turns, held flopping, thumbnails,
and the mouth attachment. The supplied texture and blood PNG are unchanged.

## Rules

- Open/join a multiplayer room, then visit the existing tournament stand.
- The summon button is disabled until the player's saved index contains every
  species available on Suncrest, Snapper, Brinebreak and Bluewater. Deep Ocean is
  not an additional requirement. Either Mako or Emperor form satisfies its
  family; rays, trevallies and barracudas remain separate species.
- Any eligible room member may summon; one encounter at a time, 10,000 coins.
  Debit is saved first; idempotent server receipts settle or refund interrupted
  requests. Accepted summon fees remain spent if the host subsequently leaves.
- The center is south (-Z) of the entire Bluewater collider cluster, 200 metres
  beyond its southernmost bound. Authored rocks and scene transforms are untouched.
- 80 m radius ring, emitted every 1.5 seconds; expands over 4.5 seconds and fades
  toward the outer radius using the lightning ripple wave-following shader.
- Each successful Popper landing in the radius gets exactly one server-side 50%
  roll. A miss resumes ordinary fishing. Only one angler can hold the boss at a
  time; retries never reroll. Other baits/lures retain normal fish selection.
- 150,000 HP, effective difficulty 2.1, base yellow swim speed 12 m/s (ordinary
  mood, drag and ability modifiers still apply). HP/weight bypass depth, biome,
  hotspot and Charm bonuses. One weight is rolled per summon: 400–440 kg;
  length interpolates from 3.2–3.4 m in all saved/held/display contexts.
- Server-owned health is shared across re-hooks, including hooks by different
  players. Client-simulated ordinary burst/skill damage is reported cumulatively
  per exclusive hook lease, preventing duplicated damage on retry.
- Healing starts after five unhooked seconds at 10,000 HP/minute, capped at full.
  A lost client lease releases after five seconds without a heartbeat, followed
  by the same five-second healing grace. Normal disconnect releases immediately.
- All room members, including late joiners, see the HP and the 15-minute deadline.
  Zero HP still requires landing the fish with normal mechanics. The server
  acknowledges one catch before the client adds it to the bag. Full bags release
  the boss. Expiry cancels an ongoing fight. Host departure closes the room.
- No new sell reward was specified, so the fallback sell formula uses the Mako
  base value (2,340). This can be tuned separately.

## Deployment and checks

Both the updated Unity client and Server/runtime are required. Old server
snapshots lack bossReady, so the summon button remains disabled and no fee is
charged. The checked-in Oracle updater currently fetches EthanL1245/main;
ensure the runtime changes reach the VM's actual deployment source before using
this feature. Publishing only to GalacticSlayer/main does not prove deployment.

Run:

```
node --test Server/tests/bosses.test.mjs Server/tests/sessions.test.mjs
python Tools/AbyssalLeviathan/verify_fbx.py
```

Verified in this environment: all 31 boss/session tests pass; FBX has a skinned,
UV-mapped mesh (2,228 vertices) and animated curves. Full backend suite: 54/61
pass, with the identical seven pre-existing tournament failures reproduced from
unmodified HEAD. Those fixtures use older biome/variant assignments.

Unity Editor compilation, generated-prefab rendering and Android playtesting
remain required. Test two clients plus a late joiner; a locked/completed index;
insufficient coins; simultaneous summons; Popper/non-Popper casts inside/outside
80 m; HP persistence after a line break; five-second healing grace; deadline
while hooked; full bag; host/guest disconnect; held/aquarium model and texture.
As with existing tournaments, saves, unlock claims, geometry and damage originate
in the client; this does not introduce a tamper-proof server economy.
