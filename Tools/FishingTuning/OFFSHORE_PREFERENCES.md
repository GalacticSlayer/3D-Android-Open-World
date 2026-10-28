# Bluewater Cay and Deep Ocean preferences

Updated 2026-10-28. Only these two regions are retuned. Difficulty, health,
speed, value, bite timing, and selection mechanics are unchanged.

`OffshorePreferenceProfiles.json` records each fish's tackle preferences, totaling
100% across the eight distinct tackle choices. These are design shares, not the
probability of catching that fish with equipped tackle. List position determines
worm taper and tie-breaking; no additional rarity multiplier is introduced.

Run `python3 Tools/FishingTuning/apply_offshore_preferences.py` to compile them
into `BiomeBaitSpeciesChance.csv`. For each region and equipped tackle, divide
each fish's share by the sum of all eligible fish's shares, multiply by 100, then
allocate whole-number percentages by largest remainder (ties use list order).
Every runtime tackle row totals exactly 100%. The legacy worm key copies worms.
A fish's runtime percentages across different tackle do not need to total 100%.

## Guideline adjustments

- Lure-preferring fish: primary 30%, two selected secondary lures 15% each,
  top three total 60%. Secondary choices are fixed data, not rerolled at runtime.
- Remaining lures receive 12–15% each. Natural baits total 10–15%, with worms
  lowest and tapering to zero. With only five lures and non-primary lures capped
  at 15%, at least 10% must remain on natural bait.
- Bait-preferring fish: three baits must supply 80–100%, making a 25% preferred
  bait ceiling impossible while keeping that bait highest. Preferred shares
  are therefore 36–40%. Worm fans retain shrimp second and squid third.
  Shrimp fans retain squid second and worms third. Squid fans use shrimp second.
- Lure shares for bait fans total 5–18%, increasing broadly down each list.
- Exact spawn lists: Greater Amberjack is disabled in Deep Ocean; the separate
  Battle-Scarred Mako entry is disabled in both regions. Standard Mako remains.
- Newly enabled Bluewater Yellowtail and Blacktip use their existing Deep Ocean
  weight ranges (1.4–14.625 kg and 12–30 kg). All other retained ranges are unchanged.
- The server tournament species metadata matches these spawn changes; tournament
  selection, family mappings, scoring, and travel rules are unchanged.

The Excel report includes the preference shares, the actual equipped-tackle
catch probabilities, spawn ranges, preferred tackle and unchanged fish stats.
