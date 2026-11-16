// Run from the repository root: node Tools/QueenslandGrouper/verify_rules.mjs
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {tournamentIslands} from '../../Server/runtime/tournament-species.mjs';

const read=path=>readFileSync(path,'utf8');
const csv=name=>read('Assets/Resources/FishingTuning/'+name+'.csv')
    .split(/\r?\n/).filter(row=>row&&!row.startsWith('#')).map(row=>row.split(','));
const scripts='Assets/_Game/Scripts/';
const wind='windgrove-shoal', id='53', black='44';
const [header,...odds]=csv('BiomeBaitSpeciesChance');
const column=id=>header.findIndex(name=>name.startsWith(id+'_'));
assert.equal(header[column(id)],'53_queensland_grouper');
assert.equal(odds.length,54);
assert.equal(new Set(odds.map(row=>row.slice(0,2).join(','))).size,54);
for(const row of odds) {
    assert.equal(row.length,header.length);
    assert(row.slice(2).every(v=>/^\d+$/.test(v)));
    assert.equal(row.slice(2).reduce((sum,v)=>sum+Number(v),0),100);
    const eligible=row[0]===wind&&row[1]==='lure:0';
    assert.equal(+row[column(id)],eligible?30:0);
    if(eligible) assert.equal(+row[column(black)],40);
}
const weights=csv('BiomeFishWeights').filter(row=>row[1]===id);
assert.equal(weights.length,6);
assert.equal(new Set(weights.map(row=>row[0])).size,6);
assert.equal(weights.filter(row=>row[0]===wind).length,1);
for(const row of weights) assert.deepEqual(row.slice(3),row[0]===wind?['30','180']:['0','0']);
const stats=csv('FishStats');
const biomeStats=csv('BiomeFishStats').filter(row=>row[0]===wind);
assert.deepEqual(stats.find(row=>row[0]===id).slice(2,4),['30','180']);
assert.deepEqual(stats.find(row=>row[0]===id).slice(4),stats.find(row=>row[0]===black).slice(4));
assert.deepEqual(biomeStats.find(row=>row[1]===id).slice(3),biomeStats.find(row=>row[1]===black).slice(3));
const catalog=read(scripts+'Fishing/FishCatalog.cs');
assert.match(catalog,/public const int QueenslandGrouperId=53/);
assert.match(catalog,/ActiveIds=\{[^}]+,QueenslandGrouperId(?:,|\})/);
const fight=name=>catalog.split('\n').find(line=>line.includes('new FishSpeciesDefinition("'+name+'"'))
    .match(/,([\d.]+f),0f,2600\)/)[1];
assert.equal(fight('Queensland Grouper'),fight('Black Marlin'));
assert.match(read(scripts+'Fishing/FishVisualFactory.cs'),/case FishCatalog\.QueenslandGrouperId:return "Fishing\/QueenslandGrouper";/);
assert.match(read(scripts+'ShopWorld/FishIndexCatalog.cs'),/FishCatalog\.StripedMarlinId, FishCatalog\.QueenslandGrouperId/);
assert.match(read(scripts+'Reef/ReefCatalog.cs'),
    /bool heavyJig = bait==ShopCatalog\.StarterLure && ShopCatalog\.ActiveLureVariant==0;[\s\S]*?if\(heavyJig\)\s*\{\s*offshore\[FishCatalog\.QueenslandGrouperId\] = 30f;/);
assert.match(read(scripts+'Fishing/AmbientFishManager.cs'),
    /if\(biome!=WindgroveShoalZone\.BiomeId\)\s*return ReefCatalog\.Roll[\s\S]*?if\(sample<8f\/10f\)return FishCatalog\.StripedMarlinId;\s*if\(sample<9f\/10f\)return FishCatalog\.QueenslandGrouperId;/);

const size=read(scripts+'Fishing/FishSizeTable.cs');
assert.match(size,/QueenslandGrouperCurve\(\),? \/\/ ID 53/);
const curve=size.match(/private static float\[\] QueenslandGrouperCurve\(\)[\s\S]*?new float\[(\d+)\][\s\S]*?\(([\d.]+)\*Math\.Pow\(\(i\+1\)\*([\d.]+),([\d.]+)\)\/([\d.]+)\)/);
assert(curve);
const [count,a,step,b,toKg]=curve.slice(1).map(Number);
assert.deepEqual([count,a,step,b,toKg],[54,.01148,5,3.04,1000]);
const model=Array.from({length:count},(_,i)=>a*((i+1)*step)**b/toKg);
function length(kg) {
    const hi=model.findIndex(w=>w>=kg);
    assert(hi>0);
    return (hi+(kg-model[hi-1])/(model[hi]-model[hi-1]))*step/100;
}
for(const [kg,m] of [[30,1.29],[180,2.33],[270,2.66]]) assert(Math.abs(length(kg)-m)<.005);

const assetsRoot='Assets/_Game/Reef/Source/';
const importer=read('Assets/_Game/Editor/QueenslandGrouperImporter.cs');
assert.match(importer,/swimTakeContains: "ArmatureAction\.002", discardEmptyRenderers: true/);
assert.match(importer,/CenterMouthAnchor\(\);/);
assert.match(importer,/IPreprocessBuildWithReport/);
const fbx=readFileSync(assetsRoot+'QueenslandGrouper.fbx');
assert.equal(fbx.subarray(0,18).toString(),'Kaydara FBX Binary');
assert(fbx.includes('ArmatureAction.002'));
const png=readFileSync(assetsRoot+'QueenslandGrouperTexture.png');
assert.equal(png.subarray(0,8).toString('hex'),'89504e470d0a1a0a');
assert.deepEqual([png.readUInt32BE(16),png.readUInt32BE(20)],[1536,1024]);
const blend=readFileSync('ArtSources/QueenslandGrouper/Queensland Grouper.blend');
assert.equal(blend.subarray(0,7).toString(),'BLENDER');
assert(blend.subarray(-64).includes('ENDB'));
const fish=tournamentIslands.find(i=>i.id===5).fish.find(f=>f.id===53);
assert.deepEqual(fish,{id:53,name:'Queensland Grouper',maxKg:270});
assert(tournamentIslands.filter(i=>i.id!==5).every(i=>!i.fish.some(f=>f.id===53)));
if(process.argv[2]){
    const path=process.argv[2].replace(/\/$/,'');
    const digest=bytes=>createHash('sha256').update(bytes).digest('hex');
    for(const [bytes,file] of [[fbx,'Queensland Grouper.fbx'],[png,'Queensland Grouper Texture.png'],[blend,'Queensland Grouper.blend']])
        assert.equal(digest(bytes),digest(readFileSync(path+'/'+file)));
}
console.log('Queensland Grouper: Windgrove Heavy Jig odds, Black Marlin fight tuning, sizing, Fish Index, authored assets and tournaments PASS');
