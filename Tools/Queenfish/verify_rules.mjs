// Run at the game repository root: node Tools/Queenfish/verify_rules.mjs
import fs from "node:fs";
import assert from "node:assert/strict";
const read=p=>fs.readFileSync(p,"utf8");
const cat=read("Assets/_Game/Scripts/Fishing/FishCatalog.cs");
assert.match(cat,/QueenfishId=49/);
assert.match(cat,/new FishSpeciesDefinition\("Queenfish"[^\n]+1f,16f,2\.20f/);
const size=read("Assets/_Game/Scripts/Fishing/FishSizeTable.cs");
assert.match(size,/QueenfishCurve\(\)/);
assert.match(size,/0\.0135803431594\*Math\.Pow\(\(i\+1\)\*5\.0,2\.92\)/);
assert(read("Assets/_Game/Scripts/Fishing/FishVisualFactory.cs").includes(
    'case FishCatalog.QueenfishId:return "Fishing/Queenfish";'));
const index=read("Assets/_Game/Scripts/ShopWorld/FishIndexCatalog.cs");
assert(index.includes("FishCatalog.RainbowRunnerId, FishCatalog.QueenfishId"));
const raw=read("Assets/Resources/FishingTuning/BiomeBaitSpeciesChance.csv")
    .split(/\r?\n/).filter(x=>x && !x.startsWith("#"));
const header=raw.shift().split(",");
const col=id=>header.findIndex(x=>x.startsWith(id+"_"));
const marlin=col(44),mahi=col(45),rooster=col(47),rainbow=col(48),queen=col(49);
assert([marlin,mahi,rooster,rainbow,queen].every(x=>x>0));
for(const line of raw){
    const c=line.split(",");
    assert.equal(c.length,header.length);
    assert.equal(c.slice(2).reduce((n,x)=>n+Number(x),0),100);
    const inZone=c[0]==="windgrove-shoal";
    assert.equal(Number(c[queen]),inZone&&c[1]==="squid"?20:0);
    if(inZone&&c[1]==="squid")assert.equal(Number(c[marlin]),40);
    if(inZone&&c[1]==="shrimp")assert.equal(Number(c[rooster]),60);
    if(inZone&&(c[1]==="worms"||c[1]==="worms-legacy")){
        assert.equal(Number(c[marlin]),28);
        assert.equal(Number(c[mahi]),42);
        assert.equal(Number(c[rainbow]),30);
    }
}
const w=read("Assets/Resources/FishingTuning/BiomeFishWeights.csv");
for(const b of ["suncrest-reef","brinebreak-isle","deep-ocean","bluewater-cay","snapper-island"])
    assert(w.includes(b+",49,Queenfish,0,0"));
assert(w.includes("windgrove-shoal,49,Queenfish,1,16"));
assert(read("Assets/Resources/FishingTuning/BiomeFishStats.csv")
    .includes("windgrove-shoal,49,Queenfish,21500,36000,2600,28000"));
const ambient=read("Assets/_Game/Scripts/Fishing/AmbientFishManager.cs");
assert(ambient.includes("return FishCatalog.QueenfishId;"));
assert(ambient.includes("if(sample<5f/7f)return FishCatalog.QueenfishId;"));
assert(ambient.includes("FishCatalog.RollMahiMahiVariant(FishCatalog.MahiMahiId,Random.value)"));
console.log("Queenfish only in Windgrove on squid, one of seven ambient families, fish index, and CSV tuning: PASS");
