// node Tools/MahiMahi/verify_rules.mjs
import fs from "node:fs";
import assert from "node:assert/strict";
const read=(p)=>fs.readFileSync(p,"utf8");
const cat=read("Assets/_Game/Scripts/Fishing/FishCatalog.cs");
const odds=read("Assets/Resources/FishingTuning/BiomeBaitSpeciesChance.csv")
  .split(/\r?\n/).filter(x=>x && !x.startsWith("#"));
const fields=odds[0].split(",");
const mahi=fields.indexOf("45_mahi_mahi"), blue=fields.indexOf("46_blue_mahi_mahi");
const marlin=fields.indexOf("44_black_marlin");
assert(mahi>0 && blue>0 && marlin>0);
assert.match(cat,/BlueMahiMahiVariantChance=\.10f/);
assert.match(cat,/RollMahiMahiVariant/);
for(const row of odds.slice(1)){
 const parts=row.split(",");
 assert.equal(parts.length,fields.length);
 const isWind=parts[0]==="windgrove-shoal";
 const worms=parts[1]==="worms"||parts[1]==="worms-legacy";
 assert.equal(Number(parts[blue]),0);
 assert.equal(Number(parts[mahi]),isWind&&worms?42:0);
 assert.equal(parts.slice(2).reduce((sum,v)=>sum+Number(v),0),100);
 if(isWind)assert.equal(Number(parts[marlin]),worms?28:["shrimp","squid"].includes(parts[1])?40:100);
}
const weights=read("Assets/Resources/FishingTuning/BiomeFishWeights.csv");
for(const id of [45,46]) for(const biome of ["suncrest-reef","brinebreak-isle","deep-ocean","bluewater-cay","snapper-island","windgrove-shoal"])
 assert(weights.includes(biome+","+id+","),"missing "+biome+" "+id);
assert(read("Assets/_Game/Scripts/Fishing/FishSizeTable.cs").includes("GameplayCurve(4.32f,43)"));
assert(read("Assets/_Game/Scripts/ShopWorld/FishIndexCatalog.cs").includes("FishCatalog.BlueMahiMahiId"));
console.log("Mahi Mahi species, regional worm exclusivity, 90/10 variant split and tables: passed");
