# Panther Grouper (stable species ID 35)

Uses the supplied Panther Grouper FBX, PNG atlas and original Blender file. FBX/PNG are committed under Assets/_Game/Reef/Source; Blender stays in ArtSources. The auto-installer uses SeaBassImporter, a looping Swim clip, RedSnapperPresentation, the animated lip anchor and standard CaughtFishFlop held motion. The same prefab serves fights, bag, index, aquarium and multiplayer. No Blender installation is required. Rebuild manually with Tools > Open World > Rebuild Panther Grouper.

## Physical size
Chromileptes altivelis (also called humpback grouper). FishBase reports 70 cm total length maximum. Normal catch range chosen for the game: 10–70 cm. Bayesian species estimate W(g)=0.01479*TL(cm)^3.08 gives 0.017781491–7.126399046 kg. These are model estimates, not measured specimen weights. The existing five-centimetre interpolation is used. Pond, cast-quality, hotspot and Charm mechanics are unchanged and may produce sizes outside the normal catch range.
Sources: https://www.fishbase.org/summary/6457 and https://www.fishbase.se/summary/Chromileptes-altivelis.html (retrieved 2026-10-28).

## Balance and location
Suncrest Beach only (suncrest-reef, biome 0), 30% of worm/legacy-worm bites. Unspecified other baits/lures: 0%. Every other biome has zero chance and unavailable weight bounds. Emperor retains 30% worms; Picasso/Titan retain 3% worms and 20% shrimp each. Other Suncrest worm weights are reduced, preserving positive existing entries and Red Snapper/Flounder/Stingray parity. All 45 rows total 100%; all other original rows are unchanged.

Base difficulty 1.05 (2.5 * Red Snapper .42). Effective fight difficulty is explicitly 2.5 times Red Snapper at equal weight percentiles and cast quality, applied after the normal clamp. HP endpoints 288–855 are 2.5x Red Snapper, rounded to integers. Yellow swim speed remains 3.152 m/s; difficulty does not enlarge or accelerate the authored animation. Value range 100–750 coins follows the existing scaled species convention.

Server validation includes this species only at Suncrest, with 10.689598570 kg hotspot max plus the existing 2x Charm allowance. No new variant family.

Validation: node Tools/PantherGrouper/verify_rules.mjs; python Tools/PantherGrouper/verify_fbx.py; node --test Server/tests/*.test.mjs. Unity import/visual verification must be performed in Unity; it is not installed in this environment.
