// Run from the repository root: node Tools/PotatoGrouper/verify_rules.mjs [upload-directory]
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {join} from 'node:path';
import {tournamentIslands} from '../../Server/runtime/tournament-species.mjs';

const read=path=>readFileSync(path,'utf8');
const csv=name=>read('Assets/Resources/FishingTuning/'+name+'.csv')
    .split(/\r?\n/).filter(row=>row&&!row.startsWith('#')).map(row=>row.split(','));
const scripts='Assets/_Game/Scripts/';
const wind='windgrove-shoal', id='54', black='44', queensland='53';
const [header,...odds]=csv('BiomeBaitSpeciesChance');
const column=id=>header.findIndex(name=>name.startsWith(id+'_'));
assert.equal(header[column(id)],'54_potato_grouper');
assert.equal(odds.length,54);
assert.equal(new Set(odds.map(row=>row.slice(0,2).join(','))).size,odds.length);
for(const row of odds) {
    assert.equal(row.length,header.length);
    assert(row.slice(2).every(v=>/^\d+$/.test(v)));
    assert.equal(row.slice(2).reduce((sum,v)=>sum+Number(v),0),100);
    const eligible=row[0]===wind && row[1]==='lure:0';
    assert.equal(+row[column(id)],eligible?30:0);
    assert.equal(+row[column(queensland)],eligible?30:0);
    if(eligible) assert.equal(+row[column(black)],40);
}
const weights=csv('BiomeFishWeights').filter(row=>row[1]===id);
assert.deepEqual(weights.map(row=>row[0]).sort(),[...new Set(odds.map(row=>row[0]))].sort());
assert(weights.some(row=>row[0]===wind));
for(const row of weights) assert.deepEqual(row.slice(3),row[0]===wind?['15','110']:['0','0']);
const stats=csv('FishStats');
const biomeStats=csv('BiomeFishStats').filter(row=>row[1]===id);
assert.deepEqual(stats.find(row=>row[0]===id).slice(1,4),['Potato Grouper','15','110']);
assert.deepEqual(stats.find(row=>row[0]===id).slice(4),stats.find(row=>row[0]===black).slice(4));
assert.deepEqual(biomeStats,[[wind,id,'Potato Grouper',
    ...csv('BiomeFishStats').find(row=>row[0]===wind&&row[1]===black).slice(3)]]);
const catalog=read(scripts+'Fishing/FishCatalog.cs');
assert.match(catalog,/public const int PotatoGrouperId=54;/);
assert.match(catalog,/ActiveIds=\{[^}]+,PotatoGrouperId\}/);
const definition=name=>catalog.split('\n').find(line=>line.includes('new FishSpeciesDefinition("'+name+'"'));
assert.match(definition('Potato Grouper'),/,15f,110f,2\.20f,0f,2600\)/);
assert.equal(definition('Potato Grouper').match(/,2\.20f,0f,2600\)/)[0],
    definition('Black Marlin').match(/,2\.20f,0f,2600\)/)[0]);
assert.match(read(scripts+'Fishing/FishVisualFactory.cs'),/case FishCatalog\.PotatoGrouperId:return "Fishing\/PotatoGrouper";/);
assert.match(read(scripts+'ShopWorld/FishIndexCatalog.cs'),/FishCatalog\.QueenslandGrouperId, FishCatalog\.PotatoGrouperId \}/);
const reef=read(scripts+'Reef/ReefCatalog.cs');
assert.match(reef,/if\s*\(biome == WindgroveShoalZone\.BiomeId\)[\s\S]*?bool heavyJig = bait==ShopCatalog\.StarterLure && ShopCatalog\.ActiveLureVariant==0;[\s\S]*?if\(heavyJig\)\s*\{\s*offshore\[FishCatalog\.QueenslandGrouperId\] = 30f;\s*offshore\[FishCatalog\.PotatoGrouperId\] = 30f;\s*\}/);
assert.match(read(scripts+'Fishing/AmbientFishManager.cs'),
    /if\(biome!=WindgroveShoalZone\.BiomeId\)\s*return ReefCatalog\.Roll[\s\S]*?if\(sample<9f\/10f\)return FishCatalog\.QueenslandGrouperId;\s*return FishCatalog\.PotatoGrouperId;/);

const size=read(scripts+'Fishing/FishSizeTable.cs');
assert.match(size,/PotatoGrouperCurve\(\) \/\/ ID 54/);
const curve=size.match(/private static float\[\] PotatoGrouperCurve\(\)[\s\S]*?new float\[(\d+)\][\s\S]*?\(([\d.]+)\*Math\.Pow\(\(i\+1\)\*([\d.]+),([\d.]+)\)\)/);
assert(curve);
const [count,a,step,b]=curve.slice(1).map(Number);
assert.deepEqual([count,a,step,b],[45,.00001,5,3.07]);
const model=Array.from({length:count},(_,i)=>a*((i+1)*step)**b);
function length(kg) {
    const hi=model.findIndex(w=>w>=kg);
    assert(hi>0);
    return (hi+(kg-model[hi-1])/(model[hi]-model[hi-1]))*step/100;
}
for(const [kg,m] of [[15,1.03],[110,1.97],[165,2.24]]) assert(Math.abs(length(kg)-m)<.005);

const source='Assets/_Game/Reef/Source/';
const importer=read('Assets/_Game/Editor/PotatoGrouperImporter.cs');
assert.match(importer,/swimTakeContains: "ArmatureAction\.002", discardEmptyRenderers: true/);
assert.match(importer,/CenterMouthAnchor\(\);/);
assert.match(importer,/IPreprocessBuildWithReport/);
const fbx=readFileSync(source+'PotatoGrouper.fbx');
assert.equal(fbx.subarray(0,18).toString(),'Kaydara FBX Binary');
assert(fbx.includes('ArmatureAction.002'));
const png=readFileSync(source+'PotatoGrouperTexture.png');
assert.equal(png.subarray(0,8).toString('hex'),'89504e470d0a1a0a');
assert.deepEqual([png.readUInt32BE(16),png.readUInt32BE(20)],[1536,1024]);
const blend=readFileSync('ArtSources/PotatoGrouper/Potato Grouper.blend');
assert.equal(blend.subarray(0,7).toString(),'BLENDER');
assert(blend.subarray(-64).includes('ENDB'));
const fish=tournamentIslands.find(i=>i.id===5).fish.find(f=>f.id===54);
assert.deepEqual(fish,{id:54,name:'Potato Grouper',maxKg:165});
assert(tournamentIslands.filter(i=>i.id!==5).every(i=>!i.fish.some(f=>f.id===54)));
if(process.argv[2])
    for(const [bytes,file] of [[fbx,'Potato Grouper.fbx'],[png,'Potato Grouper Texture.png'],[blend,'Potato Grouper.blend']])
        assert(bytes.equals(readFileSync(join(process.argv[2],file))),file+' differs from the upload');
console.log('Potato Grouper: Windgrove Heavy Jig odds, Black Marlin fight tuning, sizing, Fish Index, authored assets and tournaments PASS');
