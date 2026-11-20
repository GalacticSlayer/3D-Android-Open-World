# Abyssal Gems

A confirmed Leviathan catch adds one individually identified Level 1 gem to the shared shop ledger. New gems have Scale Piercer (1.50x) and four distinct secondary attributes, sampled uniformly without replacement from the eight requested attributes. Each secondary receives one initial roll in its stated upgrade range, in uniformly sampled 0.1 increments (including both endpoints). Gems do not auto-equip; manage them in Equipment → Gems. Equipment has fixed Fishing Rods, Reels and Lines, and Gems category tabs; the Fish Bag only contains fish.

Each upgrade adds 0.05x to Scale Piercer and selects exactly one of the existing four secondary attributes uniformly, then rolls its inclusive increase in 0.1 increments. Totals are rounded to one decimal after addition; existing whole-number saves retain their values. The four attribute types never change. Level 15 caps Scale Piercer at 2.20x. Only the equipped gem supplies bonuses; loadout changes, breakdown and upgrades require no active cast.

## Dust economy

Dust is a shared wallet, earned only with a gem equipped when a fish is successfully added to the bag. A capped gem continues collecting dust for other gems. Full-bag releases and habitat transfers award nothing. A catch, its dust and its gem drop are saved in a single ledger snapshot. Leviathan uses the existing server-confirmed landing path.

Ordinary catch reward: `clamp(2 + floor(sqrt(maximum fish HP) / 12), 2, 25)`.
Leviathan: 30 dust. Breakdown: 10 base dust plus 20% of the cumulative upgrade costs, after explicit irreversible-action confirmation showing the refund. Level 1 returns 10; level 15 returns 605 (10 + 20% of 2,975). Breaking down the equipped gem clears the slot and bonuses. Existing gems use their level to reconstruct the same upgrade investment; no save migration is needed.

| Fish maximum HP | Dust |
| --- | ---: |
| 100 | 2 |
| 1,000 | 4 |
| 10,000 | 10 |
| 50,000 | 20 |
| 100,000 | 25 |
| Leviathan | 30 |

Upgrade from level L costs `50 + 25*(L-1)` dust: 50, 75, 100, 125, 150, 175, 200, 225, 250, 275, 300, 325, 350, 375. Total Level 1→15: 2,975 dust. Approximately 119 catches at 25 dust, or 298 catches averaging 10 dust, before salvaging additional gems. This makes repeated boss drops useful without allowing one breakdown to fund late levels.

## Combat semantics

- Tension control multiplies the existing control divisor by `1 + bonus/100`.
- Tension recovery multiplies negative tension changes by `1 + bonus/100`.
- Attack Percentage multiplies normal reel bursts, high-drag ticks and the charged skill base attack, once.
- Skill Damage affects the charged skill and its Lightning Strike sequence. Scale Piercer applies only if the displayed combat state is Angry at activation; delayed lightning snapshots this multiplier. Existing damage caps are applied before gem multipliers, and Hardened Scales mitigation still applies afterward.
- Crit Chance adds percentage points to the rod's roll. Crit Damage adds to the base 2x multiplier, e.g. +4% yields 2.04x.
- Extra Line extends the equipped line's real maximum, including the late-update limiter; cast distance stays 5–30 m.
- Lure Saver rolls once on a snapped line with a pending lure, with effective probability capped at 100%. It does not rescue consumable bait or an abandoned/interrupted session. A saved lure uses the existing successful lure resolution so it cannot be deducted again.

## Presentation

The third hotbar slot shows the supplied gem art, its level, and a 2.5-second cyan pulse with the awarded dust amount. Clicking it opens the shared shop modal. Scale Piercer sits at the pentagon apex, four secondary cards surround the gem, and the right panel describes the selected attribute. The footer shows the next upgrade cost or MAX LEVEL. A successful upgrade plays the supplied Gem Upgrade Noise clip once and opens a navy/teal enhancement banner over a translucent grey backdrop. It shows the gem, level change, and old/new values for Scale Piercer and the upgraded secondary. Continue or Escape dismisses the banner; covered controls cannot trigger another upgrade. Your original PNGs are stored byte-for-byte as TextAssets, decoded once and cached; no generated replacement art or per-frame texture work.

## Verification

Run these with .NET 8:

```
dotnet run --project Tools/AbyssalGems/Checks/Checks.csproj
dotnet run --project Tools/Level8Gear/Checks/Checks.csproj
dotnet run --project Tools/FishingDrag/Checks/Checks.csproj
dotnet run --project Tools/AnglerProgression/Checks/Checks.csproj
```

These cover 2,000 complete gem progressions, distinct starting rolls, inclusive increment ranges, exact costs, cap enforcement, legacy integer save loading, every possible fractional drop/upgrade increment, fractional save/reload, one-time breakdown, reward eligibility, combat stacking and unequipping, existing equipment, drag, charged skills, Lightning and angler progression. Full Unity compilation/play-mode and Android visual/input validation remain required; the standalone tests use Unity stubs. The older ShopWorldChecks harness cannot build due to pre-existing missing progression, geography and tuning dependencies; the current Level8Gear harness compiles and exercises the real ShopLedger instead.

Unity acceptance: land a server-confirmed Leviathan; inspect and equip the drop; catch an ordinary fish; verify the pulse and dust after restart; select all five cards; upgrade and verify one secondary changes alongside Scale Piercer; test cancel/escape/confirm on breakdown; test Angry versus other states and line-snap lure recovery; verify layout on wide and narrow landscape screens.

A successfully saved Leviathan catch queues a themed “Abyssal Gem Acquired” card below the XP/level notices, with the gem artwork on the left. It shares XP slide timing/easing and lasts 1.5 times the accompanying XP animation’s full duration (or the standard notice duration if no XP is awarded). It never triggers for habitat transfers or rejected catches.

## Gem Reset Dial

The first gem permanently unlocks the supplied Gem Reset Dial. Existing saves containing gems gain this unlock during validation. Both gem lists sort by descending level, preserving acquisition order for ties; the dial is always last in the regular list. Reset Gems hides the category tabs and wallet, raises the list, and changes Close to Cancel. Cancel/Escape returns to the regular Gems list; when a confirmation is open, it dismisses that confirmation first.

Resetting requires confirmation and no active cast. It retains the gem ID, equipped state, and all four attribute types/order; level returns to 1 (Scale Piercer 1.50x), with each secondary independently rerolled in its original inclusive starting range at 0.1 increments. Resetting is free, refunds no dust, and permanently discards the old upgrade investment. Level-one gems may also be reset. Subsequent upgrade and breakdown costs use the new level. Checks cover reset bounds, every fractional roll, persistence, unlock migration, equipped state, sorting, and post-reset economy.

Unity acceptance for reset: check the supplied icon and matching row layout, centered darker red dial button, tabs hidden/list raised in reset mode, one Reset Gem action per row, warning readability, confirmation cancellation with no changes, reordering after reset, return via Cancel/Escape, and the persisted unlock after all gems are broken down.
