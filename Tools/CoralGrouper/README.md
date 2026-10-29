# Coral Grouper (stable species ID 36)

The supplied red/orange, blue-spotted, rounded-tail reference is implemented as Cephalopholis miniata (Coral Hind / Coral Grouper). Normal catch range: 10–50 cm total length. Species-specific FishBase Bayesian model W(g)=0.01148*TL(cm)^3.04 gives 0.012587570–1.678072426 kg; estimates, not measured specimen weights. Five-centimetre interpolation uses the shared size pipeline. Existing pond, depth-quality, hotspot and Charm size mechanics remain in place.
Source: https://www.fishbase.se/summary/Cephalopholis-miniata.html (accessed 2026-10-29), reports 50 cm TL maximum.

Suncrest Beach only (suncrest-reef, biome 0). Worm and legacy-worm chance 30%; all other baits and all other islands 0%. Emperor and Panther remain 30% each; the remaining 10% is apportioned proportionally in whole percentages, preserving Red Snapper/Flounder/Stingray parity. This reduces both triggerfish worm odds to 1%; their shrimp odds stay 20% each and other bait odds stay unchanged. All 45 rows sum to 100%. No new variant family or additional random roll.

Base difficulty 1.26 = 3*.42 Red Snapper. Effective fight difficulty uses Red Snapper's clamped formula at the same size percentile and cast quality, then multiplies by 3 after the clamp. HP 345–1026 = 3*(115–342); yellow swim speed 3.152 m/s is unchanged. Sell range 120–900 follows the scaled-species convention. Tournament max is 2.517108640 kg including hotspot, with the existing 2x Charm allowance.

Original .blend is in ArtSources; supplied FBX and texture are in Assets/_Game/Reef/Source. The auto-installer generates Resources/Fishing/CoralGrouper.prefab using SeaBassImporter, the authored looping swim take, RedSnapperPresentation, mouth anchor and standard held motion. Used by catch/fight, fish index, inventory, aquarium and multiplayer. Manual rebuild: Tools > Open World > Rebuild Coral Grouper. No Blender export required after pull.

Checks: python Tools/CoralGrouper/verify_fbx.py; node Tools/CoralGrouper/verify_rules.mjs; node --test Server/tests/*.test.mjs. Unity Editor is unavailable here: actual import/rendering/animation still needs visual verification in Unity.
