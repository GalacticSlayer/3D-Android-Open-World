// Run from the repository root: node Tools/StripedMarlin/verify_rules.mjs
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {tournamentIslands} from '../../Server/runtime/tournament-species.mjs';

const read=path=>readFileSync(path,'utf8');
const csv=name=>read('Assets/Resources/FishingTuning/'+name+'.csv')
    .split(/\r?\n/).filter(s=>s&&!s.startsWith('#')).map(s=>s.split(','));
const scripts='Assets/_Game/Scripts/';
const [header,...odds]=csv('BiomeBaitSpeciesChance');
const wind='windgrove-shoal', id='52', black='44';
const col=id=>header.findIndex(name=>name.startsWith(id+'_'));
assert.equal(header[col(id)],'52_striped_marlin');
assert.equal(odds.length,54);
assert.equal(new Set(odds.map(row=>row.slice(0,2).join(','))).size,54);
for(const row of odds) {
    assert.equal(row.length,header.length);
    assert(row.slice(2).every(v=>/^\d+$/.test(v)));
    assert.equal(row.slice(2).reduce((sum,v)=>sum+Number(v),0),100);
    const eligible=row[0]===wind&&row[1]==='shrimp';
    assert.equal(+row[col(id)],eligible?30:0);
    if(eligible) assert.deepEqual([black,47,id].map(n=>+row[col(n)]),[40,30,30]);
}
const weights=csv('BiomeFishWeights').filter(row=>row[1]===id);
assert.equal(weights.length,6);
assert.equal(new Set(weights.map(row=>row[0])).size,6);
assert(weights.some(row=>row[0]===wind));
for(const row of weights) assert.deepEqual(row.slice(3),row[0]===wind?['30','120']:['0','0']);
const stats=csv('FishStats');
const biomeStats=csv('BiomeFishStats').filter(row=>row[0]===wind);
assert.deepEqual(stats.find(row=>row[0]===id).slice(2,4),['30','120']);
assert.deepEqual(stats.find(row=>row[0]===id).slice(4),stats.find(row=>row[0]===black).slice(4));
assert.deepEqual(biomeStats.find(row=>row[1]===id).slice(3),biomeStats.find(row=>row[1]===black).slice(3));

const catalog=read(scripts+'Fishing/FishCatalog.cs');
assert.match(catalog,/public const int StripedMarlinId=52/);
assert.match(catalog,/ActiveIds=\{[^}]+,StripedMarlinId(?:,|\})/);
const fight=name=>catalog.split('\n').find(line=>line.includes('new FishSpeciesDefinition("'+name+'"'))
    .match(/,([\d.]+f),0f,2600\)/)[1];
assert.equal(fight('Striped Marlin'),fight('Black Marlin'));
assert.match(read(scripts+'Fishing/FishVisualFactory.cs'),/case FishCatalog\.StripedMarlinId:return "Fishing\/StripedMarlin";/);
assert.match(read(scripts+'ShopWorld/FishIndexCatalog.cs'),/FishCatalog\.GoldenPompanoId, FishCatalog\.StripedMarlinId/);
assert.match(read(scripts+'Reef/ReefCatalog.cs'),
    /if\(shrimp\)\s*\{\s*offshore\[FishCatalog\.RoosterfishId\] = 30f;\s*offshore\[FishCatalog\.StripedMarlinId\] = 30f;/);
assert.match(read(scripts+'Fishing/AmbientFishManager.cs'),
    /if\(biome!=WindgroveShoalZone\.BiomeId\)\s*return ReefCatalog\.Roll[\s\S]*?if\(sample<7f\/8f\)return FishCatalog\.GoldenPompanoId;\s*return FishCatalog\.StripedMarlinId;/);

const size=read(scripts+'Fishing/FishSizeTable.cs');
assert.match(size,/StripedMarlinCurve\(\) \/\/ ID 52/);
const curve=size.match(/private static float\[\] StripedMarlinCurve\(\)[\s\S]*?new float\[(\d+)\][\s\S]*?double cm=\(i\+1\)\*([\d.]+);[\s\S]*?\(cm-([\d.]+)\)\/([\d.]+)\);[\s\S]*?\(cm\+([\d.]+)\)\/([\d.]+)\);[\s\S]*?Math\.Pow\(10,([-\d.]+)\)\*Math\.Pow\(maleLjfl,([\d.]+)\);[\s\S]*?Math\.Pow\(10,([-\d.]+)\)\*Math\.Pow\(femaleLjfl,([\d.]+)\);[\s\S]*?\(maleKg\+femaleKg\)\*0\.5/);
assert(curve);
const [samples,step,ma,mb,fa,fb,mlog,mp,flog,fp]=curve.slice(1).map(Number);
assert.deepEqual([samples,step,ma,mb,fa,fb,mlog,mp,flog,fp],[65,5,6.084,1.257,2.93,1.294,-5.696,3.233,-5.756,3.267]);
const model=Array.from({length:samples},(_,i)=>{
    const cm=(i+1)*step;
    return (10**mlog*Math.max(1,(cm-ma)/mb)**mp+10**flog*Math.max(1,(cm+fa)/fb)**fp)/2;
});
assert(model.every((w,i)=>i===0||w>model[i-1]));
function length(kg) {
    const hi=model.findIndex(w=>w>=kg);
    assert(hi>0);
    return (hi+(kg-model[hi-1])/(model[hi]-model[hi-1]))*step/100;
}
assert(Math.abs(length(30)-2.11)<.005);
assert(Math.abs(length(120)-3.23)<.005);
assert(length(30)<length(60)&&length(60)<length(120));

const fbx=readFileSync('Assets/_Game/Reef/Source/StripedMarlin.fbx');
assert.equal(fbx.subarray(0,18).toString(),'Kaydara FBX Binary');
assert(fbx.includes('Armature|ArmatureAction'));
const png=readFileSync('Assets/_Game/Reef/Source/StripedMarlinTexture.png');
assert.equal(png.subarray(0,8).toString('hex'),'89504e470d0a1a0a');
assert.deepEqual([png.readUInt32BE(16),png.readUInt32BE(20)],[1774,887]);
assert.equal(readFileSync('ArtSources/StripedMarlin/Striped Marlin.blend').subarray(0,7).toString(),'BLENDER');
const importer=read('Assets/_Game/Editor/StripedMarlinImporter.cs');
assert.match(importer,/swimTakeContains: "ArmatureAction", discardEmptyRenderers: true/);
assert.match(importer,/PlaceMouthBehindBill\(\);/);
assert.match(importer,/bill\.z -= \.15f;\s*mouth\.position = root\.transform\.TransformPoint\(bill - new Vector3\(0f, \.025f, 0f\)\);/);
assert.match(importer,/lineAnchor\.SetParent\(mouth\.parent, false\);[\s\S]*?lineAnchor\.position = root\.transform\.TransformPoint\(bill - new Vector3\(0f, \.0164f, 0f\)\);/);
assert.match(importer,/"StripedMarlin:" \+ PrefabRevision/);
const holding=read(scripts+'Fishing/FishingSystem.cs');
assert.match(holding,/heldFishLineAnchor = FindDeepChildByName\(heldFishVisual\.transform, "AuthoredHeldLineAnchor"\);/);
assert.match(holding,/\(heldFishLineAnchor != null \? heldFishLineAnchor : heldFishMouthMarker\)\.position/);
assert.match(holding,/heldFishVisual\.transform\.position \+=\s*desiredMouthWorld -\s*heldFishMouthMarker\.position/);
assert.match(importer,/IPreprocessBuildWithReport/);
const fish=tournamentIslands.find(i=>i.id===5).fish.find(f=>f.id===52);
assert.deepEqual(fish,{id:52,name:'Striped Marlin',maxKg:180});
assert(tournamentIslands.filter(i=>i.id!==5).every(i=>!i.fish.some(f=>f.id===52)));
console.log('Striped Marlin: Windgrove-only shrimp odds, Black Marlin difficulty, size curve, Fish Index, authored assets and tournament validation PASS');
