// Run from the repository root: node Tools/StripedMarlin/verify_rules.mjs
// Current Windgrove balance is covered by Tools/WindgroveShoal/verify_balance.mjs.
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {tournamentIslands} from '../../Server/runtime/tournament-species.mjs';

const read=path=>readFileSync(path,'utf8');
const csv=name=>read('Assets/Resources/FishingTuning/'+name+'.csv')
    .split(/\r?\n/).filter(s=>s&&!s.startsWith('#')).map(s=>s.split(','));
const scripts='Assets/_Game/Scripts/';
const wind='windgrove-shoal', id='52';
const weights=csv('BiomeFishWeights').filter(row=>row[1]===id);
assert.equal(weights.length,6);
assert.equal(new Set(weights.map(row=>row[0])).size,6);
assert(weights.some(row=>row[0]===wind));
for(const row of weights) assert.deepEqual(row.slice(3),row[0]===wind?['30','120']:['0','0']);
assert.match(read(scripts+'Fishing/FishCatalog.cs'),/const int StripedMarlinId=52[\s\S]*?ActiveIds=\{[^}]+,StripedMarlinId(?:,|\})/);
assert.match(read(scripts+'Fishing/FishVisualFactory.cs'),/case FishCatalog\.StripedMarlinId:return "Fishing\/StripedMarlin";/);
assert.match(read(scripts+'Fishing/AmbientFishManager.cs'),
    /if\(biome!=WindgroveShoalZone\.BiomeId\)\s*return ReefCatalog\.Roll[\s\S]*?if\(sample<7f\/10f\)return FishCatalog\.GoldenPompanoId;\s*if\(sample<8f\/10f\)return FishCatalog\.StripedMarlinId;/);

const size=read(scripts+'Fishing/FishSizeTable.cs');
assert.match(size,/StripedMarlinCurve\(\),? \/\/ ID 52/);
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
console.log('Striped Marlin: Windgrove availability, sizing, authored assets and tournament validation PASS');
