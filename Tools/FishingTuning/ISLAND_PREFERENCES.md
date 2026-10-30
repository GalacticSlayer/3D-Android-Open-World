# Suncrest Beach, Snapper Island and Brinebreak Isle

Updated 2026-10-30 from the supplied ordered fish lists. Suncrest Beach uses the
existing `suncrest-reef` tuning key. Spotted Seabass means Spotted Sand Bass;
Crankbait means Fire Shad Crankbait; Spoon means Metal Spoon. Current lure keys:
`lure:0` Heavy Jig, `lure:1` Light Jig, `lure:2` Fire Shad Crankbait,
`lure:3` Metal Spoon, `lure:4` Popper. Saved IDs and cosmetic assets are unchanged.

## Data and compilation

`IslandPreferenceProfiles.json` contains 29 per-fish preference allocations,
each totaling 100% across eight distinct tackle choices. Like the offshore
profiles, these are design shares. Actual equipped-tackle catch probabilities
are stored in `Assets/Resources/FishingTuning/BiomeBaitSpeciesChance.csv`.

Run `python3 Tools/FishingTuning/apply_island_preferences.py` from the repository
root. It reuses the existing offshore compiler: normalize each tackle column
across that region's listed fish, then use largest remainders to assign whole
percentages totaling 100%. Ties follow list order. Legacy worms duplicate worms.
No extra rarity multiplier or runtime selection mechanic is introduced.

Run `python3 Tools/FishingTuning/verify_island_preferences.py` to check profiles,
all 45 runtime rows, preferred tackle, exact availability, moved weights,
tournament metadata and preserved offshore/stat data against the baseline.

## Guideline adjustments

Lure-preferring fish use 30% primary and two fixed 15% secondary lure shares.
The top three sum to 60%; other lures receive 12–15%. Natural baits total 10–15%
with worms lowest, tapering to zero on the final fish of each list. Secondary
choices are fixed configuration, not rerolled during play; other lures may tie
with secondaries at 15% when bait share reaches 10%.

Bait-preferring fish use 5–18% combined lure share, generally increasing with
list position. Their preferred bait shares are 37–55%. Three baits cannot cover
80–100% while the highest bait is capped at 25%. Brinebreak has no worm-preferring
fish, so its primary bait shares are strengthened to 50–55% to keep the named
preferred bait best after normalization. Worm fans rank shrimp second, squid
third; shrimp fans rank squid second, worms third; squid fans rank shrimp second.
These preference caps do not apply to the normalized runtime odds.

## Spawn lists and model audit

Only listed base species remain in these three catch tables. Bluewater Cay and
Deep Ocean chance rows, weight rows and tournament metadata are unchanged.
Winter Flounder, Leopard Stingray and Roughtail Stingray move from Suncrest to
Brinebreak with their existing 0.25–1.541871456 kg base ranges. All other retained
species keep their existing regional ranges. Disabled slots use the existing
0,0 availability sentinel. Species-wide stats and all C# mechanics are untouched.

Across these lists plus the prior offshore lists, all 40 base species are covered.
Two existing model variants are not explicitly named:

- Juvenile Emperor Angelfish (34): retained under the listed Emperor Angelfish.
  It has no separate roll chance. The existing size-based appearance rule and
  regional range remain unchanged, as does its tournament validation entry.
- Battle-Scarred Mako Shark (20): remains excluded by the earlier offshore update.
  This change does not alter either offshore region or the model itself.

Server tournament species metadata mirrors these spawn lists, including the
Emperor size variant. Tournament scoring, family grouping and travel mechanics
are unchanged. The Excel report distinguishes design shares from runtime odds,
shows base weight ranges and unchanged stat endpoints, and records this audit.
