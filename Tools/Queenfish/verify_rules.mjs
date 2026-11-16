// Run at the game repository root: node Tools/Queenfish/verify_rules.mjs
// Current Windgrove balance is covered by Tools/WindgroveShoal/verify_balance.mjs.
import fs from "node:fs";
import assert from "node:assert/strict";
const read=p=>fs.readFileSync(p,"utf8");
const cat=read("Assets/_Game/Scripts/Fishing/FishCatalog.cs");
assert.match(cat,/QueenfishId=49/);
const size=read("Assets/_Game/Scripts/Fishing/FishSizeTable.cs");
assert.match(size,/QueenfishCurve\(\)/);
assert.match(size,/0\.0135803431594\*Math\.Pow\(\(i\+1\)\*5\.0,2\.92\)/);
assert(read("Assets/_Game/Scripts/Fishing/FishVisualFactory.cs").includes(
    'case FishCatalog.QueenfishId:return "Fishing/Queenfish";'));
const w=read("Assets/Resources/FishingTuning/BiomeFishWeights.csv");
for(const b of ["suncrest-reef","brinebreak-isle","deep-ocean","bluewater-cay","snapper-island"])
    assert(w.includes(b+",49,Queenfish,0,0"));
assert(w.includes("windgrove-shoal,49,Queenfish,1,16"));
const ambient=read("Assets/_Game/Scripts/Fishing/AmbientFishManager.cs");
assert(ambient.includes("return FishCatalog.QueenfishId;"));
assert(ambient.includes("if(sample<5f/10f)return FishCatalog.QueenfishId;"));
assert(ambient.includes("FishCatalog.RollMahiMahiVariant(FishCatalog.MahiMahiId,Random.value)"));
console.log("Queenfish Windgrove availability, size curve, visual mapping and ambient pool: PASS");
