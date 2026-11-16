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
| 51 | Trachinotus anak (Golden / Oyster Pompano) | 0.2097 (g, cm) | 2.3385 | 0.60 m | [Vu et al. (2026), Fig. 4](https://doi.org/10.47853/FAS.2026.e30), 327 fish, total length. The game range of 0.25–2.9 kg gives about 0.21–0.59 m. The upper weight is a model estimate near the [59.6 cm published total length](https://fishbase.se/FieldGuide/FieldGuideSummary.php?GenusName=Trachinotus&SpeciesName=anak&pda=1&sps=), not a measured record. |
| 52 | Kajikia audax (Striped Marlin) | 10^-5.696 / 10^-5.756 (kg, LJFL cm) | 3.233 / 3.267 | 3.25 m | [Chiang et al., Table 1](https://isc.fra.go.jp/pdf/BILL/ISC07_MARLIN%26SWO_1/WP_06_Chiang.pdf), eastern Taiwan samples. For each total length, invert the male and female lower-jaw fork length conversions and average the two whole-weight predictions. A base weight of 30–120 kg, based on the [AFMA common weight range](https://www.afma.gov.au/species/striped-marlin), maps to about 2.11–3.23 m measured bill-to-tail rather than lower-jaw-to-fork. The high end extrapolates beyond the paired total-length samples. |
| 53 | Epinephelus lanceolatus (Queensland Grouper) | 0.01148 (g, cm) | 3.04 | 2.70 m | [FishBase Bayesian genus/body-shape estimate](https://fishbase.se/summary/epinephelus-lanceolatus). The 30–180 kg gameplay range is about 1.29–2.33 m total length. The standard tournament weight allowance is 270 kg (~2.66 m); the full 2.70 m table follows the reported species length, not a measured length/weight pair. |

Striped Bass is intentionally allowed to grow far beyond the existing Sea Bass while sharing Sea Bass fight tuning at the same weight. Spotted Sand Bass shares Black Sea Bass fight tuning. Their normal coastal min/max weights are configured in `FishCatalog`; far-offshore scaling can exceed those coastal maxima without shrinking or changing near-shore fish.

Rod/reel first-person presentation is intentionally enlarged. Fish world dimensions are driven by these length curves and are not presentation-scaled.
| 54 | Epinephelus tukula (Potato Grouper) | 0.00001 (kg, cm) | 3.07 | 2.25 m | [Grandcourt (2005), Aldabra Atoll, Fig. 4d](https://repository.si.edu/server/api/core/bitstreams/4717f04d-cd24-432e-922f-a87843e1a90e/content), fit from 62 specimens (up to 128.4 cm). The 15–110 kg base range is about 1.03–1.97 m TL. The upper end is extrapolated and uses [FishBase’s 110 kg published maximum](https://fishbase.se/summary/Epinephelus-tukula.html). The 165 kg hotspot allowance is gameplay-only (~2.24 m TL). |
