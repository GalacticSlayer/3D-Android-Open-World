// Run from repository root: node Tools/Roosterfish/verify_rules.mjs
import fs from "node:fs";
import assert from "node:assert/strict";
const get = p => fs.readFileSync(p,"utf8");
const cat=get("Assets/_Game/Scripts/Fishing/FishCatalog.cs");
assert.match(cat,/RoosterfishId=47/);
const rows=get("Assets/Resources/FishingTuning/BiomeBaitSpeciesChance.csv").split(/\r?\n/).filter(x=>x&&!x.startsWith("#"));
const head=rows.shift().split(","),idx=head.indexOf("47_roosterfish"),marlin=head.indexOf("44_black_marlin"),mahi=head.indexOf("45_mahi_mahi"),blue=head.indexOf("46_blue_mahi_mahi");
assert(idx>0 && marlin>0 && mahi>0 && blue>0);
for(const row of rows){
 const c=row.split(",");
 assert.equal(c.length,head.length);
 assert.equal(c.slice(2).reduce((sum,n)=>sum+Number(n),0),100);
 const shrimp=c[0]==="windgrove-shoal"&&c[1]==="shrimp";
 assert.equal(Number(c[idx]),shrimp?60:0);
 if(shrimp){assert.equal(Number(c[marlin]),40);assert.equal(Number(c[mahi]),0);}
 if(c[0]==="windgrove-shoal"&&["worms","worms-legacy"].includes(c[1]))assert.equal(Number(c[mahi]),42);
 assert.equal(Number(c[blue]),0);
}
const weight=get("Assets/Resources/FishingTuning/BiomeFishWeights.csv");
for(const b of ["suncrest-reef","brinebreak-isle","deep-ocean","bluewater-cay","snapper-island"])
 assert(weight.includes(b+",47,Roosterfish,0,0"));
assert(weight.includes("windgrove-shoal,47,Roosterfish,2.5,50"));
assert(get("Assets/Resources/FishingTuning/BiomeFishStats.csv").includes("windgrove-shoal,47,Roosterfish,21500,36000,2600,28000"));
assert(get("Assets/_Game/Scripts/Fishing/FishSizeTable.cs").includes("RoosterfishCurve()"));
assert(get("Assets/_Game/Scripts/Fishing/FishVisualFactory.cs").includes('case FishCatalog.RoosterfishId:return "Fishing/Roosterfish";'));
assert(get("Assets/_Game/Scripts/ShopWorld/FishIndexCatalog.cs").includes("FishCatalog.MahiMahiId, FishCatalog.RoosterfishId"));
console.log("Roosterfish shrimp-only Windgrove rules and fish-index configuration: PASS");
