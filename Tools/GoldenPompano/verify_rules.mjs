// Run from the repository root: node Tools/GoldenPompano/verify_rules.mjs
// Current Windgrove balance is covered by Tools/WindgroveShoal/verify_balance.mjs.
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {tournamentIslands} from '../../Server/runtime/tournament-species.mjs';

const read=path=>readFileSync(path,'utf8');
const csv=name=>read('Assets/Resources/FishingTuning/'+name+'.csv')
    .split(/\r?\n/).filter(s=>s&&!s.startsWith('#')).map(s=>s.split(','));
const scripts='Assets/_Game/Scripts/';
const wind='windgrove-shoal', id='51';
const weights=csv('BiomeFishWeights').filter(row=>row[1]===id);
assert.equal(weights.length,6);
assert.equal(new Set(weights.map(row=>row[0])).size,6);
for(const row of weights) assert.deepEqual(row.slice(3),row[0]===wind?['0.25','2.9']:['0','0']);
assert.equal(weights.filter(row=>row[0]===wind).length,1);
assert.match(read(scripts+'Fishing/FishCatalog.cs'),/const int GoldenPompanoId=51[\s\S]*?ActiveIds=\{[^}]+,GoldenPompanoId(?:,|\})/);
assert.match(read(scripts+'Fishing/FishVisualFactory.cs'),/case FishCatalog\.GoldenPompanoId:return "Fishing\/GoldenPompano";/);
assert.match(read(scripts+'Fishing/AmbientFishManager.cs'),
    /if\(biome!=WindgroveShoalZone\.BiomeId\)\s*return ReefCatalog\.Roll[\s\S]*?if\(sample<7f\/10f\)return FishCatalog\.GoldenPompanoId;/);

const size=read(scripts+'Fishing/FishSizeTable.cs');
assert.match(size,/GoldenPompanoCurve\(\), \/\/ ID 51/);
const curve=size.match(/private static float\[\] GoldenPompanoCurve\(\)[\s\S]*?new float\[(\d+)\][\s\S]*?row\[i\]=\(float\)\(([\d.]+)\*Math\.Pow\(\(i\+1\)\*([\d.]+),([\d.]+)\)\/1000\.0\)/);
assert(curve);
const [samples,a,step,b]=curve.slice(1).map(Number);
assert.deepEqual([samples,a,step,b],[12,.2097,5,2.3385]);
const model=Array.from({length:samples},(_,i)=>a*((i+1)*step)**b/1000);
const length=kg=>{
    const hi=model.findIndex(w=>w>=kg);
    assert(hi>0);
    return (hi+(kg-model[hi-1])/(model[hi]-model[hi-1]))*.05;
};
assert(Math.abs(length(.25)-.21)<.005);
assert(Math.abs(length(2.9)-.59)<.005);
assert(length(.25)<length(1)&&length(1)<length(2.9));
const fbx=readFileSync('Assets/_Game/Reef/Source/GoldenPompano.fbx');
assert.equal(fbx.subarray(0,18).toString(),'Kaydara FBX Binary');
assert(fbx.includes('ArmatureAction'));
const png=readFileSync('Assets/_Game/Reef/Source/GoldenPompanoTexture.png');
assert.equal(png.subarray(0,8).toString('hex'),'89504e470d0a1a0a');
assert.deepEqual([png.readUInt32BE(16),png.readUInt32BE(20)],[1774,887]);
assert.equal(readFileSync('ArtSources/GoldenPompano/Golden Pompano.blend').subarray(0,7).toString(),'BLENDER');
const importer=read('Assets/_Game/Editor/GoldenPompanoImporter.cs');
assert.match(importer,/swimTakeContains: "ArmatureAction", discardEmptyRenderers: true/);
assert.match(importer,/IPreprocessBuildWithReport/);
const windFish=tournamentIslands.find(i=>i.id===5).fish.find(f=>f.id===51);
assert.equal(windFish.name,'Golden Pompano');
assert(Math.abs(windFish.maxKg-2.9*1.5)<1e-9);
assert(tournamentIslands.filter(i=>i.id!==5).every(i=>!i.fish.some(f=>f.id===51)));
console.log('Golden Pompano: Windgrove availability, sizing, authored assets and tournament validation PASS');
