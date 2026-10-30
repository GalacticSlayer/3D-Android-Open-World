# Blue Tang

Stable ID 41; Paracanthurus hepatus (Pacific Blue Tang). Uses user-supplied FBX/PNG and preserved Blender source; shared SeaBassImporter, one authored swim take, and established procedural held motion. Editor auto-installs the prefab before play/build. Manual rebuild: Tools > Open World > Rebuild Blue Tang.

Source: https://www.fishbase.se/summary/Paracanthurus-hepatus.html (retrieved October 30, 2026). Maximum total length 31 cm. Bayesian family/body-shape length-weight estimate W(g)=0.02291*TL(cm)^2.97; this is an estimate, not a measured species-specific weight record. Gameplay range 10–31 cm, approximately 0.021380856–0.615700236 kg. The lower cutoff is a gameplay choice; shared five-centimetre interpolation applies. Existing depth/Charm/hotspot effects remain.

Red Snapper difficulty: .42 base, matching normalized size formula, HP 115–342 and yellow speed 3.152. Suncrest only, 30% shrimp, zero on other baits/regions. Previous Suncrest shrimp shares scaled by 0.7: Blue Lined Grouper/Napoleon 21% each, Picasso/Titan 14% each. Other rows remain unchanged, including White Skate and offshore tuning.

Tournament cap includes hotspot allowance and existing server Charm multiplier. Index/inventory/aquarium/network visuals use the stable species ID and shared presentation.

Checks: node Tools/BlueTang/verify_rules.mjs; python Tools/BlueTang/verify_fbx.py; node --test Server/tests/tournaments.test.mjs. Unity visual verification requires the editor.
