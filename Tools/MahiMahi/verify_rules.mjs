// node Tools/MahiMahi/verify_rules.mjs
// Current Windgrove balance is covered by Tools/WindgroveShoal/verify_balance.mjs.
import fs from "node:fs";
import assert from "node:assert/strict";
const read=(p)=>fs.readFileSync(p,"utf8");
const cat=read("Assets/_Game/Scripts/Fishing/FishCatalog.cs");
assert.match(cat,/BlueMahiMahiVariantChance=\.10f/);
assert.match(cat,/RollMahiMahiVariant/);
const weights=read("Assets/Resources/FishingTuning/BiomeFishWeights.csv");
for(const id of [45,46]) for(const biome of ["suncrest-reef","brinebreak-isle","deep-ocean","bluewater-cay","snapper-island","windgrove-shoal"])
 assert(weights.includes(biome+","+id+","),"missing "+biome+" "+id);
assert(read("Assets/_Game/Scripts/Fishing/FishSizeTable.cs").includes("GameplayCurve(4.32f,43)"));
assert(read("Assets/_Game/Scripts/ShopWorld/FishIndexCatalog.cs").includes("FishCatalog.BlueMahiMahiId"));
console.log("Mahi Mahi regional weight rows, size curve and 90/10 variant split: PASS");
