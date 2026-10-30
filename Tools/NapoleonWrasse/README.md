# Napoleon Wrasse

Stable ID 39; Cheilinus undulatus. Correct spelling used throughout game and repository filenames; the user supplied files named Napolean Wrasse. Original binary content is unchanged.

Uses the standard SeaBassImporter with the supplied single swim take, six-bone rig, texture and shared procedural held motion. Prefab auto-installs in editor and before builds. Manual rebuild: Tools > Open World > Rebuild Napoleon Wrasse.

Normal gameplay range 20–229 cm, approximately 0.108877273–155.660200125 kg. Minimum 20 cm is a gameplay choice; weight uses FishBase Bayesian W(g)=0.01445*TL(cm)^2.98. The shared five-centimetre table interpolates lengths. Existing size bonuses and depth reductions remain applicable.

Sources:
- https://fishbase.org/summary/Cheilinus-undulatus (length-weight model, published maximum weight 191 kg)
- https://repository.library.noaa.gov/view/noaa/9052/noaa_9052_DS1.pdf (reported maximum 229 cm total length; extreme size records have uncertainty)
FishBase labels 229 cm as standard length whereas NOAA reports total length. This implementation uses NOAA's total-length interpretation and the model-derived 155.66 kg at that length, rather than treating the separately reported 191 kg record as a paired measurement.

Suncrest Beach only (suncrest-reef / biome 0), 30% shrimp, zero on other baits. Suncrest shrimp totals: Napoleon 30%, Blue Lined Grouper 30%, Picasso 20%, Titan 20%; other fish zero on shrimp. Other baits/regions unchanged.

6.5x Red Snapper at matching size percentile after its difficulty clamp; HP 748–2223 (rounded), yellow speed 3.152. Tournament metadata allows hotspot and existing Charm multipliers. No variant.

Checks: node Tools/NapoleonWrasse/verify_rules.mjs; python Tools/NapoleonWrasse/verify_fbx.py; node --test Server/tests/tournaments.test.mjs. Unity visual verification remains required. Existing scarred-Mako tournament test failure predates this fish.
