// Run from the repository root: node Tools/GoldenPompano/verify_rules.mjs
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {tournamentIslands} from '../../Server/runtime/tournament-species.mjs';

const read=path=>readFileSync(path,'utf8');
const csv=name=>read('Assets/Resources/FishingTuning/'+name+'.csv')
    .split(/\r?\n/).filter(s=>s&&!s.startsWith('#')).map(s=>s.split(','));
const scripts='Assets/_Game/Scripts/';
const [head,...odds]=csv('BiomeBaitSpeciesChance');
const wind='windgrove-shoal', id='51';
const col=id=>head.findIndex(name=>name.startsWith(id+'_'));
assert.equal(head[col(id)],'51_golden_pompano');
assert.equal(odds.length,54);
for(const row of odds) {
    assert.equal(row.length,head.length);
    assert(row.slice(2).every(v=>/^\d+$/.test(v)));
    assert.equal(row.slice(2).reduce((sum,v)=>sum+Number(v),0),100);
    const eligible=row[0]===wind&&row[1]==='squid';
    assert.equal(Number(row[col(id)]),eligible?20:0);
    if(eligible) assert.deepEqual([44,49,50,51].map(id=>Number(row[col(id)])),[40,20,20,20]);
}
const weights=csv('BiomeFishWeights').filter(row=>row[1]===id);
assert.equal(weights.length,6);
assert.equal(new Set(weights.map(row=>row[0])).size,6);
for(const row of weights) assert.deepEqual(row.slice(3),row[0]===wind?['0.25','2.9']:['0','0']);
assert.equal(weights.filter(row=>row[0]===wind).length,1);
const stats=csv('FishStats');
assert.deepEqual(stats.find(row=>row[0]===id).slice(2,4),['0.25','2.9']);
assert.deepEqual(stats.find(row=>row[0]===id).slice(4),stats.find(row=>row[0]==='44').slice(4));
const biomeStats=csv('BiomeFishStats').filter(row=>row[0]===wind);
assert.deepEqual(biomeStats.find(row=>row[1]===id).slice(3),biomeStats.find(row=>row[1]==='44').slice(3));
assert.match(read(scripts+'Fishing/FishCatalog.cs'),/const int GoldenPompanoId=51[\s\S]*?"Golden Pompano"[^\n]+\.25f,2\.9f,2\.20f[\s\S]*?ActiveIds=\{[^}]+,GoldenPompanoId\}/);
assert.match(read(scripts+'Fishing/FishVisualFactory.cs'),/case FishCatalog\.GoldenPompanoId:return "Fishing\/GoldenPompano";/);
assert.match(read(scripts+'ShopWorld/FishIndexCatalog.cs'),/FishCatalog\.SpanishMackerelId, FishCatalog\.GoldenPompanoId/);
assert.match(read(scripts+'Reef/ReefCatalog.cs'),/if\(squid\)\s*\{\s*offshore\[FishCatalog\.QueenfishId\] = 20f;\s*offshore\[FishCatalog\.SpanishMackerelId\] = 20f;\s*offshore\[FishCatalog\.GoldenPompanoId\] = 20f;/);
assert.match(read(scripts+'Fishing/AmbientFishManager.cs'),
    /if\(biome!=WindgroveShoalZone\.BiomeId\)\s*return ReefCatalog\.Roll[\s\S]*?if\(sample<6f\/7f\)return FishCatalog\.SpanishMackerelId;\s*return FishCatalog\.GoldenPompanoId;/);

const size=read(scripts+'Fishing/FishSizeTable.cs');
assert.match(size,/GoldenPompanoCurve\(\) \/\/ ID 51/);
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
console.log('Golden Pompano: Windgrove-only Squid odds, Marlin stats, size curve, fish index, authored assets and tournament rules PASS');
