# Nassau Grouper

Stable ID 38, Epinephelus striatus. User-supplied FBX, texture and Blender source. Uses the shared importer and indexed animation convention: first take for swimming, second for held struggle. Rig contains six bones, UVs, skin weights and two animated takes. Editor auto-installer generates the prefab; Tools > Open World > Rebuild Nassau Grouper rebuilds manually.

Size source: https://www.fishbase.se/summary/epinephelus-striatus
Reported maxima: 122 cm TL and 25 kg. Bayesian estimate W(g)=0.01148*TL(cm)^3.05. Normal gameplay weight capped at 25 kg, corresponding to approximately 119.7 cm on that curve; minimum 10 cm is a gameplay choice. Five-centimetre interpolation follows the existing size-table convention. Existing hotspot, Charm and shallow-water effects remain applicable.

Snapper Isle only (snapper-island, biome 4). Shrimp chance 30%; zero on other baits and biomes. Existing Snapper shrimp shares are proportionally reduced to 70% using largest-remainder integer rounding; all other rows unchanged.

Difficulty is 4x Red Snapper at matching size percentile after the normal clamp, HP 460–1368, yellow speed 3.152. Tournament cap 37.5 kg before the existing Charm allowance. No variant.

Checks: node Tools/NassauGrouper/verify_rules.mjs; python Tools/NassauGrouper/verify_fbx.py; node --test --test-name-pattern Nassau Server/tests/tournaments.test.mjs. Unity visual validation remains required.
