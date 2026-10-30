# White Skate (species ID 40)

GalacticSlayer/3D-Android-Open-World. New independent species, Brinebreak Isle only.
The supplied model, UVs and weights are retained. Swimming keeps the exact installed
Leopard Stingray motion. HeldStruggle uses the `Stingray Struggling` action from
the supplied WhiteSkate.blend, baked onto the existing compatible 29-bone rig.
All 30 frames of each take are verified after FBX round trip against their
respective sources (maximum error under 0.0001). Flatfish previews, held yaw
(16 degrees), shared mouth anchoring, aquarium, fishing and multiplayer
presentation are reused.

Packed 4096x4096 albedo and metallic/roughness maps are extracted directly from
the Blender material links. PNG pixels are checked against the decoded original.
No UV remapping, resizing, or image generation is involved. Material data uses
linear colour space, B=metallic/G=roughness; the colour atlas is sRGB.

Stats, difficulty, sale values, yellow speed, and length curve match Leopard.
Normal home weight range is the same 0.25–1.541871456 kg. Brinebreak bite profile
copies Leopard's current Suncrest profile: worms 1%, shrimp 0%, squid 9%, each
lure 16%. Other Brinebreak probabilities scale proportionally using largest
remainders to retain whole percentages summing to 100. Other islands are unchanged.
Fallback tuning also excludes White Skate from every other island.

White Skate has its own Brinebreak tournament target (the existing stingray
family is Suncrest-only). Server weight ceiling includes the existing hotspot
and Charm allowances. No stat bonuses or gameplay mechanics are added.

Unity installs automatically after import, before Play and before builds.
Manual rebuild: Tools > Open World > Rebuild White Skate.

Reproduction/checks:

    blender -b --python Tools/WhiteSkate/export_skate.py
    python Tools/WhiteSkate/convert_textures.py
    blender -b --python Tools/WhiteSkate/verify_export.py
    python Tools/WhiteSkate/verify_rules.py
    node --test --test-name-pattern='White Skate' Server/tests/tournaments.test.mjs

Unity Editor is unavailable here; actual Unity compilation and Play Mode require
local verification. The broader server suite has a pre-existing Mako variant
20/19 tournament test failure; the new White Skate test passes.
