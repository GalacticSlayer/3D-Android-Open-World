# Blue Lined Grouper

Stable species ID 37, Cephalopholis formosa (bluelined hind). Authored FBX, PNG and Blender source supplied by the user. Imported through the shared SeaBassImporter for index, held fish, inventory, aquarium and world presentation; one swim take and the established procedural held motion.

Normal gameplay range: 10–34 cm. The lower cutoff is a gameplay choice. FishBase Bayesian length-weight estimate: W(g) = 0.00955 * TL(cm)^2.98, giving 0.009120179–0.349792541 kg. The shared five-centimetre table approximates intermediate lengths. Existing hotspot, Charm and depth adjustments still apply.

Sources:
- https://www.fishbase.se/summary/cephalopholis-formosa (34 cm maximum and estimated length-weight coefficients)
- https://www.kahaku.go.jp/research/db/zoology/Fishes_of_Andaman_Sea/contents/serranidae/05.html (identification and 34 cm maximum)

Suncrest only: shrimp 30%, all other baits zero. Both triggerfish retain 20% shrimp each; other shrimp shares fit the remaining 30%, preserving snapper/flatfish parity. Other rows and offshore tuning are unchanged.

Combat follows Coral Grouper: three times Red Snapper at equal size percentile, after its difficulty clamp; HP 345–1026, yellow speed 3.152. Tournament metadata includes hotspot caps, with the existing Charm allowance applied server-side.

Checks: `node Tools/BlueLinedGrouper/verify_rules.mjs`, `python Tools/BlueLinedGrouper/verify_fbx.py`, `node --test Server/tests/tournaments.test.mjs`.
Unity automatically generates the prefab; manual rebuild: Tools > Open World > Rebuild Blue Lined Grouper. Unity visual verification requires the editor.
