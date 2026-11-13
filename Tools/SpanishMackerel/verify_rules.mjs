// Run from the repository root: node Tools/SpanishMackerel/verify_rules.mjs
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {tournamentIslands} from '../../Server/runtime/tournament-species.mjs';

const tuning='Assets/Resources/FishingTuning/';
const scripts='Assets/_Game/Scripts/';
const read=path=>readFileSync(path,'utf8');
const rows=file=>read(tuning+file).trim().split(/\r?\n/)
    .filter(row=>row&&!row.startsWith('#')).map(row=>row.split(','));
const [, ...stats]=rows('FishStats.csv');
const [, ...biomeStats]=rows('BiomeFishStats.csv');
const [, ...weights]=rows('BiomeFishWeights.csv');
const [header,...chances]=rows('BiomeBaitSpeciesChance.csv');
const wind='windgrove-shoal';
const id='50';
const col=species=>header.findIndex(key=>key.startsWith(species+'_'));
const catalog=read(scripts+'Fishing/FishCatalog.cs');
assert.match(catalog,/public const int SpanishMackerelId=50/);
assert.match(catalog,/ActiveIds=\{[^}]*,49,SpanishMackerelId\}/);
assert.match(catalog,/new FishSpeciesDefinition\("Spanish Mackerel"[^\n]+\.5f,5\.89f,2\.20f,0f,2600\)/);
assert.deepEqual(stats.find(row=>row[0]===id).slice(2,4),['0.5','5.89']);
assert.deepEqual(stats.find(row=>row[0]===id).slice(4),stats.find(row=>row[0]==='44').slice(4));
assert.deepEqual(biomeStats.find(row=>row[0]===wind&&row[1]===id).slice(3),
    biomeStats.find(row=>row[0]===wind&&row[1]==='44').slice(3));
assert.equal(weights.filter(row=>row[1]===id).length,6);
for (const row of weights.filter(row=>row[1]===id))
    assert.deepEqual(row.slice(3),row[0]===wind?['0.5','5.89']:['0','0']);

assert.equal(header.at(-1),'50_spanish_mackerel');
assert.equal(chances.length,54);
assert.equal(new Set(chances.map(row=>row.slice(0,2).join(','))).size,54);
const [marlin,mahi,rooster,rainbow,queen,mackerel]=[44,45,47,48,49,50].map(col);
assert([marlin,mahi,rooster,rainbow,queen,mackerel].every(c=>c>1));
for (const row of chances) {
    assert.equal(row.length,header.length);
    assert(row.slice(2).every(v=>/^\d+$/.test(v)));
    assert.equal(row.slice(2).reduce((sum,v)=>sum+Number(v),0),100);
    const [biome,bait]=row;
    assert.equal(+row[mackerel],biome===wind&&bait==='squid'?30:0);
    if (biome!==wind) continue;
    const expected=bait==='squid'?[40,0,0,0,30,30]:
        bait==='shrimp'?[40,0,60,0,0,0]:
        ['worms','worms-legacy'].includes(bait)?[28,42,0,30,0,0]:[100,0,0,0,0,0];
    assert.deepEqual([marlin,mahi,rooster,rainbow,queen,mackerel].map(c=>+row[c]),expected);
}
const reef=read(scripts+'Reef/ReefCatalog.cs');
assert.match(reef,/if\(squid\)\s*\{\s*offshore\[FishCatalog\.QueenfishId\] = 30f;\s*offshore\[FishCatalog\.SpanishMackerelId\] = 30f;/);
assert.match(read(scripts+'ShopWorld/FishIndexCatalog.cs'),/FishCatalog\.QueenfishId, FishCatalog\.SpanishMackerelId/);
assert.match(read(scripts+'Fishing/FishVisualFactory.cs'),/case FishCatalog\.SpanishMackerelId:return "Fishing\/SpanishMackerel";/);
assert.match(read(scripts+'Fishing/AmbientFishManager.cs'),
    /if\(sample<5f\/6f\)return FishCatalog\.QueenfishId;\s*return FishCatalog\.SpanishMackerelId;/);

const size=read(scripts+'Fishing/FishSizeTable.cs');
assert.match(size,/SpanishMackerelCurve\(\) \/\/ ID 50/);
const curve=size.match(/private static float\[\] SpanishMackerelCurve\(\)[\s\S]*?new float\[(\d+)\][\s\S]*?row\[i\]=\(float\)\(([\d.e-]+)\*Math\.Pow\(\(i\+1\)\*([\d.]+),([\d.]+)\)\)/);
assert(curve);
const [samples,a,step,b]=curve.slice(1).map(Number);
assert.deepEqual([a,step,b],[5.4935e-9,50,3.0025]);
const model=Array.from({length:samples},(_,i)=>a*((i+1)*step)**b);
const length=kg=>{
    const hi=model.findIndex(upper=>upper>=kg);
    assert(hi>0);
    return (hi+(kg-model[hi-1])/(model[hi]-model[hi-1]))*.05;
};
assert(Math.abs(length(.5)-.45)<.005);
assert(Math.abs(length(5.89)-1.02)<.005);
assert(length(.5)<length(2)&&length(2)<length(5.89));

const fbx=readFileSync('Assets/_Game/Reef/Source/SpanishMackerel.fbx');
assert.equal(fbx.subarray(0,18).toString(),'Kaydara FBX Binary');
assert(fbx.includes('Armature|ArmatureAction.001'));
const png=readFileSync('Assets/_Game/Reef/Source/SpanishMackerelTexture.png');
assert.equal(png.subarray(0,8).toString('hex'),'89504e470d0a1a0a');
assert.deepEqual([png.readUInt32BE(16),png.readUInt32BE(20)],[1672,941]);
const importer=read('Assets/_Game/Editor/SpanishMackerelImporter.cs');
assert.match(importer,/SeaBassImporter\.InstallModel\(Name, PrefabPath, Source/);
assert.match(importer,/swimTakeContains: "ArmatureAction\.001"/);
assert.match(importer,/IPreprocessBuildWithReport/);

const tournament=tournamentIslands.find(i=>i.id===5);
assert(tournament);
for (const row of weights.filter(row=>row[0]===wind&&+row[4]>0)) {
    const fish=tournament.fish.find(f=>f.id===+row[1]);
    assert(fish);
    assert.equal(fish.name,row[2]);
    assert(Math.abs(fish.maxKg-Number(row[4])*1.5)<1e-9);
}
console.log('Spanish Mackerel: Windgrove-only Squid odds, Marlin stats, size curve, index, ambient pool, authored assets, tournament table PASS');
