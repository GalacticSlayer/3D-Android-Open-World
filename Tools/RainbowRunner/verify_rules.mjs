// Run from repository root: node Tools/RainbowRunner/verify_rules.mjs
// Current Windgrove balance is covered by Tools/WindgroveShoal/verify_balance.mjs.
import fs from "node:fs";
import assert from "node:assert/strict";
const load = p => fs.readFileSync(p, "utf8");
const cat=load("Assets/_Game/Scripts/Fishing/FishCatalog.cs");
assert.match(cat,/public const int RainbowRunnerId=48/);
const curve=load("Assets/_Game/Scripts/Fishing/FishSizeTable.cs");
assert.match(curve,/RainbowRunnerCurve\(\)/);
assert.match(curve,/0\.00977\*Math\.Pow\(\(i\+1\)\*5\.0,2\.84\)/);
assert(load("Assets/_Game/Scripts/Fishing/FishVisualFactory.cs").includes(
    'case FishCatalog.RainbowRunnerId:return "Fishing/RainbowRunner";'));
const weights=load("Assets/Resources/FishingTuning/BiomeFishWeights.csv");
for(const b of ["suncrest-reef","brinebreak-isle","deep-ocean","bluewater-cay","snapper-island"])
    assert(weights.includes(b+",48,Rainbow Runner,0,0"));
assert(weights.includes("windgrove-shoal,48,Rainbow Runner,1,17.05"));
const world=load("Assets/_Game/Scripts/Fishing/AmbientFishManager.cs");
assert(world.includes("return FishCatalog.RainbowRunnerId;"));
assert(world.includes("return FishCatalog.RoosterfishId;"));
assert(world.includes("FishCatalog.RollMahiMahiVariant(FishCatalog.MahiMahiId,Random.value)"));
assert(world.includes("if(biome!=WindgroveShoalZone.BiomeId)"));
console.log("Rainbow Runner tuning, Windgrove exclusivity, sizing, visuals, and ambient distribution PASS");
