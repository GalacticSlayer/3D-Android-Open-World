// Run from the repository root: node Tools/SpanishMackerel/verify_rules.mjs
// Current Windgrove balance is covered by Tools/WindgroveShoal/verify_balance.mjs.
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {tournamentIslands} from '../../Server/runtime/tournament-species.mjs';

const tuning='Assets/Resources/FishingTuning/';
const scripts='Assets/_Game/Scripts/';
const read=path=>readFileSync(path,'utf8');
const rows=file=>read(tuning+file).trim().split(/\r?\n/)
    .filter(row=>row&&!row.startsWith('#')).map(row=>row.split(','));
const [, ...weights]=rows('BiomeFishWeights.csv');
const wind='windgrove-shoal';
const id='50';
const catalog=read(scripts+'Fishing/FishCatalog.cs');
assert.match(catalog,/public const int SpanishMackerelId=50/);
assert.match(catalog,/ActiveIds=\{[^}]*,49,SpanishMackerelId,GoldenPompanoId(?:,|\})/);
assert.equal(weights.filter(row=>row[1]===id).length,6);
for (const row of weights.filter(row=>row[1]===id))
    assert.deepEqual(row.slice(3),row[0]===wind?['0.5','5.89']:['0','0']);

assert.match(read(scripts+'Fishing/FishVisualFactory.cs'),/case FishCatalog\.SpanishMackerelId:return "Fishing\/SpanishMackerel";/);
assert.match(read(scripts+'Fishing/AmbientFishManager.cs'),
    /if\(sample<5f\/10f\)return FishCatalog\.QueenfishId;\s*if\(sample<6f\/10f\)return FishCatalog\.SpanishMackerelId;/);

const size=read(scripts+'Fishing/FishSizeTable.cs');
assert.match(size,/SpanishMackerelCurve\(\), \/\/ ID 50/);
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
console.log('Spanish Mackerel: Windgrove availability, sizing, authored assets and tournament validation PASS');
