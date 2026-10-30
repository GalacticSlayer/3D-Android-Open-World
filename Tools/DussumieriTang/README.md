# Dussumieri Tang

Stable ID 42, Acanthurus dussumieri. User-supplied Blender source, FBX and texture; shared model installer, authored swim and shared held presentation. Manual rebuild: Tools > Open World > Rebuild Dussumieri Tang.

FishBase: https://www.fishbase.se/summary/acanthurus-dussumieri (October 30, 2026). Maximum total length 54 cm. Bayesian species/genus-body-shape estimate W(g)=0.02951*TL(cm)^2.95. Gameplay minimum 10 cm; weights 0.026300815–3.806543534 kg. This is an estimated length-weight relationship, not a weight record. Shared 5 cm interpolation and existing size bonuses apply.

2.5x Red Snapper fight difficulty at matching size percentile, after clamping; HP 288–855. Suncrest only: 30% squid, zero other baits/regions. Existing Suncrest squid shares scaled to 70% using largest-remainder integer rounding. All other rows unchanged. Tournament cap includes existing hotspot and Charm allowances.

Validation: python Tools/DussumieriTang/verify_fbx.py; node Tools/DussumieriTang/verify_rules.mjs; node --test Server/tests/tournaments.test.mjs. Unity visual testing requires the editor.
