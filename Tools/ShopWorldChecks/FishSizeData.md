# Fish length data

Game length means straight, neutral-pose **total length (snout to tail tip)**, with 1 Unity world unit = 1 metre. FishBase-style Bayesian length–weight estimates supply W in grams from total length L in centimetres: W = a L^b. These are population/model estimates, not exact individual weights. We sample them every 5 cm into checked-in tables and linearly interpolate length by weight at runtime. Below the first interval the result has a 5 cm floor. Above a table's authored range, open-ocean gameplay continues that species' final length/weight trend with a safety cap of 1.8× the tabulated maximum length; existing save weights are not rewritten.

Generic names are resolved as game-design assumptions: Sea Bass = European sea bass, Yellowtail = yellowtail amberjack/kingfish, Black Spot Goatfish = blackspot goatfish, Bonito = Atlantic bonito, Black Sea Bass = Centropristis striata, Striped Bass = Morone saxatilis, and Spotted Sand Bass = Paralabrax maculatofasciatus. Limits below describe the tabulated curve range, not claims of record lengths. Coastal catch ceilings remain controlled separately by `FishCatalog`.

| Game species ID | Scientific name | a | b | Tabulated length | Source/assumption |
|---|---|---:|---:|---:|---|
| 0 | Scomber australasicus | 0.00891 | 3.06 | 0.55 m | Existing checked-in curve |
| 1 | Lutjanus campechanus | 0.01479 | 2.96 | 1.00 m | Existing checked-in curve |
| 2 | Dicentrarchus labrax | 0.00933 | 3.02 | 1.00 m | Existing checked-in curve |
| 3 | Seriola lalandi | 0.01862 | 2.93 | 2.00 m | Existing checked-in curve |
| 4 | Thunnus albacares | 0.01413 | 3.03 | 2.00 m | Retired ID retained for save compatibility |
| 5 | Thunnus albacares | 0.01413 | 3.03 | 2.00 m | Existing checked-in curve |
| 6 | Parupeneus cyclostomus | 0.01259 | 3.09 | 0.50 m | Existing checked-in curve |
| 7 | Parupeneus spilurus | 0.01259 | 3.04 | 0.50 m | Existing checked-in curve |
| 8 | Tuna-family gameplay approximation | 0.01413 | 3.03 | 2.00 m | Bigeye gameplay curve retained from existing tuna sizing |
| 9 | Sarda sarda (Bonito) | 0.00851 | 3.06 | 1.00 m | Existing checked-in curve |
| 10 | Centropristis striata (Black Sea Bass) | 0.01148 | 3.04 | 0.70 m | Existing checked-in curve |
| 11 | Morone saxatilis (Striped Bass) | 0.00893 | 3.065 | 2.00 m | New species curve sampled every 5 cm; large-fish range intentionally retained |
| 12 | Paralabrax maculatofasciatus (Spotted Sand Bass) | 0.01164 | 3.044 | 0.60 m | New species curve sampled every 5 cm |
| 50 | Scomberomorus maculatus (Atlantic Spanish Mackerel) | 5.4935e-9 (kg, mm) | 3.0025 | 1.05 m | [SEDAR 28, Table 2.7](https://sedarweb.org/documents/pw7-13-sedar-28-south-atlantic-spanish-mackerel-stock-assessment-report/), regions and sexes combined, total length. The normal 0.5–5.89 kg range uses the [published maximum weight](https://fishbase.se/FieldGuide/FieldGuideSummary.php?c_code=840&genusname=Scomberomorus&speciesname=maculatus) and gives about 0.45–1.02 m. The largest lengths are a short extrapolation from the study's 978 mm sample maximum. |

Striped Bass is intentionally allowed to grow far beyond the existing Sea Bass while sharing Sea Bass fight tuning at the same weight. Spotted Sand Bass shares Black Sea Bass fight tuning. Their normal coastal min/max weights are configured in `FishCatalog`; far-offshore scaling can exceed those coastal maxima without shrinking or changing near-shore fish.

Rod/reel first-person presentation is intentionally enlarged. Fish world dimensions are driven by these length curves and are not presentation-scaled.
| 51 | Trachinotus anak (Golden / Oyster Pompano) | 0.2097 (g, cm) | 2.3385 | 0.60 m | [Vu et al. (2026), Fig. 4](https://doi.org/10.47853/FAS.2026.e30), 327 fish, total length. The game range of 0.25–2.9 kg gives about 0.21–0.59 m. The upper weight is a model estimate near the [59.6 cm published total length](https://fishbase.se/FieldGuide/FieldGuideSummary.php?GenusName=Trachinotus&SpeciesName=anak&pda=1&sps=), not a measured record. |
