// Run from repository root: node Tools/RainbowRunner/verify_rules.mjs
import fs from "node:fs";
import assert from "node:assert/strict";
const load = p => fs.readFileSync(p, "utf8");
const cat=load("Assets/_Game/Scripts/Fishing/FishCatalog.cs");
assert.match(cat,/public const int RainbowRunnerId=48/);
assert.match(cat,/new FishSpeciesDefinition\("Rainbow Runner"[^\n]+\.5f,17\.05f,2\.20f/);
const curve=load("Assets/_Game/Scripts/Fishing/FishSizeTable.cs");
assert.match(curve,/RainbowRunnerCurve\(\)/);
assert.match(curve,/0\.00977\*Math\.Pow\(\(i\+1\)\*5\.0,2\.84\)/);
assert(load("Assets/_Game/Scripts/Fishing/FishVisualFactory.cs").includes(
    'case FishCatalog.RainbowRunnerId:return "Fishing/RainbowRunner";'));
assert(load("Assets/_Game/Scripts/ShopWorld/FishIndexCatalog.cs").includes(
    "FishCatalog.RoosterfishId, FishCatalog.RainbowRunnerId"));
const odds=load("Assets/Resources/FishingTuning/BiomeBaitSpeciesChance.csv")
    .split(/\r?\n/).filter(s=>s&&!s.startsWith("#"));
const columns=odds.shift().split(",");
const slot=id=>columns.findIndex(c=>c.startsWith(id+"_"));
const marlin=slot(44),mahi=slot(45),blue=slot(46),rooster=slot(47),rainbow=slot(48);
assert([marlin,mahi,blue,rooster,rainbow].every(i=>i>0));
for(const line of odds) {
    const cells=line.split(",");
    assert.equal(cells.length,columns.length);
    assert.equal(cells.slice(2).reduce((sum,x)=>sum+Number(x),0),100);
    const worms=cells[0]==="windgrove-shoal"&&["worms","worms-legacy"].includes(cells[1]);
    const shrimp=cells[0]==="windgrove-shoal"&&cells[1]==="shrimp";
    assert.equal(Number(cells[rainbow]),worms?30:0);
    if(worms){
        assert.equal(Number(cells[marlin]),28);
        assert.equal(Number(cells[mahi]),42);
    }
    if(shrimp) {
        assert.equal(Number(cells[rooster]),60);
        assert.equal(Number(cells[marlin]),40);
    }
    assert.equal(Number(cells[blue]),0);
}
const weights=load("Assets/Resources/FishingTuning/BiomeFishWeights.csv");
for(const b of ["suncrest-reef","brinebreak-isle","deep-ocean","bluewater-cay","snapper-island"])
    assert(weights.includes(b+",48,Rainbow Runner,0,0"));
assert(weights.includes("windgrove-shoal,48,Rainbow Runner,0.5,17.05"));
const world=load("Assets/_Game/Scripts/Fishing/AmbientFishManager.cs");
assert(world.includes("return FishCatalog.RainbowRunnerId;"));
assert(world.includes("return FishCatalog.RoosterfishId;"));
assert(world.includes("FishCatalog.RollMahiMahiVariant(FishCatalog.MahiMahiId,Random.value)"));
assert(world.includes("if(biome!=WindgroveShoalZone.BiomeId)"));
console.log("Rainbow Runner tuning, Windgrove exclusivity, bait, sizing, visuals, and ambient distribution PASS");
