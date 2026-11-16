// Run from repository root: node Tools/Roosterfish/verify_rules.mjs
// Current Windgrove balance is covered by Tools/WindgroveShoal/verify_balance.mjs.
import fs from "node:fs";
import assert from "node:assert/strict";
const get = p => fs.readFileSync(p,"utf8");
const cat=get("Assets/_Game/Scripts/Fishing/FishCatalog.cs");
assert.match(cat,/RoosterfishId=47/);
const weight=get("Assets/Resources/FishingTuning/BiomeFishWeights.csv");
for(const b of ["suncrest-reef","brinebreak-isle","deep-ocean","bluewater-cay","snapper-island"])
 assert(weight.includes(b+",47,Roosterfish,0,0"));
assert(weight.includes("windgrove-shoal,47,Roosterfish,2.5,50"));
assert(get("Assets/_Game/Scripts/Fishing/FishSizeTable.cs").includes("RoosterfishCurve()"));
assert(get("Assets/_Game/Scripts/Fishing/FishVisualFactory.cs").includes('case FishCatalog.RoosterfishId:return "Fishing/Roosterfish";'));
console.log("Roosterfish Windgrove availability, size curve and visual mapping: PASS");
