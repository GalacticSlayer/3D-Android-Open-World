import {readFileSync} from 'node:fs';
import assert from 'node:assert/strict';
import {tournamentIslands,tournamentTargets,tournamentSpeciesId} from '../../Server/runtime/tournament-species.mjs';
const read=p=>readFileSync(p,'utf8');
const csv=p=>read('Assets/Resources/FishingTuning/'+p+'.csv').trim().split(/\r?\n/).filter(x=>!x.startsWith('#')).map(x=>x.split(','));
const [head,...rows]=csv('BiomeBaitSpeciesChance');
const a=head.findIndex(x=>x.startsWith('33_')),j=head.findIndex(x=>x.startsWith('34_'));
assert(a>0&&j===a+1);
for(const row of rows){
 assert.equal(row.length,head.length);assert.equal(row.slice(2).reduce((s,x)=>s+ +x,0),100);
 assert(row.slice(2).every(x=>Number.isInteger(+x)&&+x>=0));
 assert.equal(+row[a],row[0]==='suncrest-reef'&&['worms','worms-legacy'].includes(row[1])?30:0);
 assert.equal(+row[j],0);
}
assert.equal(rows.length,45);
const stats=csv('FishStats').slice(1),red=stats.find(x=>x[0]==='1');
for(const id of [33,34]){
 const row=stats.find(x=>+x[0]===id);assert.deepEqual(row.slice(4),red.slice(4));
 assert(Math.abs(+row[2]-.01738*10**2.88/1000)<1e-8);
 assert(Math.abs(+row[3]-.01738*50**2.88/1000)<1e-8);
 const ranges=csv('BiomeFishWeights').slice(1).filter(x=>+x[1]===id);assert.equal(ranges.length,5);
 for(const x of ranges)assert.equal(+x[4]>0,x[0]==='suncrest-reef');
 assert.deepEqual(tournamentIslands.filter(x=>x.fish.some(f=>f.id===id)).map(x=>x.id),[0]);
}
assert(!tournamentTargets(tournamentIslands[0]).some(x=>x.id===34));assert.equal(tournamentSpeciesId(34),33);
const system=read('Assets/_Game/Scripts/Fishing/FishingSystem.cs');
assert(system.indexOf('hookedSpeciesId=FishCatalog.ResolveSizeVariant')>system.indexOf('hookedWeightKg *= Progression'));
const catalog=read('Assets/_Game/Scripts/Fishing/FishCatalog.cs');
assert(catalog.includes('weightKg<=FishSizeTable.WeightForLength(EmperorAngelfishId,.22f)'));
for(const name of ['EmperorAngelfish','JuvenileEmperorAngelfish']){
 const fbx=readFileSync('Assets/_Game/Reef/Source/'+name+'.fbx');
 assert(fbx.subarray(0,18).toString().startsWith('Kaydara FBX Binary'));
 for(const bone of ['Bone','Bone.001','Bone.002','Bone.003','Bone.004'])assert(fbx.includes(Buffer.from(bone)));
 assert(fbx.includes(Buffer.from('Armature|ArmatureAction')));
 assert(fbx.includes(Buffer.from('LayerElementUV')));
 const png=readFileSync('Assets/_Game/Reef/Source/'+name+'Texture.png');assert.equal(png.subarray(1,4).toString(),'PNG');
}
console.log('PASS: 45 bait tables, exclusive Suncrest angelfish, shared combat, physical endpoints, size selection order, both supplied animated assets and tournament family.');

// Physical boundary is length-based, not an independent 30% variant roll.
const weights=Array.from({length:10},(_,i)=>.01738*((i+1)*5)**2.88/1000);
const threshold=weights[3]+(weights[4]-weights[3])*.4;
const length=kg=>{let hi=1;while(hi<9&&weights[hi]<kg)hi++;return (hi+(kg-weights[hi-1])/(weights[hi]-weights[hi-1]))*.05;};
assert(Math.abs(length(threshold)-.22)<1e-8);
assert(length(threshold-1e-6)<.22);assert(length(threshold+1e-6)>.22);
assert(length(threshold*1.5)>.22,'Hotspot can promote a juvenile-size roll to adult');

const index=read('Assets/_Game/Scripts/ShopWorld/FishIndexCatalog.cs');
assert(index.includes('new[] { FishCatalog.EmperorAngelfishId, FishCatalog.JuvenileEmperorAngelfishId }'));
assert(index.includes('new[] { FishCatalog.MakoSharkId, FishCatalog.BattleScarredMakoSharkId }'));
const view=read('Assets/_Game/Scripts/ShopWorld/FishIndexView.cs');
assert(view.includes('FishIndexCatalog.FoundVariants(progress.Data,family)+" / "+variants.Length'));
assert(view.includes('int baitSpecies=emperorFamily?FishCatalog.EmperorAngelfishId:variant;'));
for(const id of [33,34]){
 assert(Math.abs(tournamentIslands[0].fish.find(f=>f.id===id).maxKg-.01738*50**2.88/1000*1.5)<1e-8);
}
console.log('PASS: 50 cm endpoint, 22 cm juvenile boundary, shared Mako-style index registration and updated server caps.');

// Regression: a 30 cm adult roll reduced to 18 cm by shallow-water quality
// must be classified using the final weight, before fight presentation begins.
const weightAtLength=m=>{const cm=m*100;const lo=Math.floor(cm/5)-1;return weights[lo]+(weights[lo+1]-weights[lo])*(cm/5-lo-1);};
const adultRoll=weightAtLength(.30), smallCatch=weightAtLength(.18);
assert(adultRoll>threshold && smallCatch<threshold);
const shallowQuality=smallCatch/adultRoll;
assert(shallowQuality>=.2 && shallowQuality<=1);
assert(adultRoll*shallowQuality<threshold);
const startFight=system.slice(system.indexOf('private void StartFight()'),system.indexOf('private void UpdateFight('));
assert(startFight.indexOf('castQuality.PrepareHookedFish()')>startFight.indexOf('fishHealthPoints=fishMaxHealth'));
assert(startFight.indexOf('ResolveSizeVariant')>startFight.indexOf('castQuality.PrepareHookedFish()'));
assert(startFight.indexOf('ResolveSizeVariant')<startFight.indexOf('hud.ShowFightMeters'));
const qualityRuntime=read('Assets/_Game/Scripts/Fishing/FishingCastQualityRuntime.cs');
assert(qualityRuntime.includes('if (!enabled || fishing == null || applied) return;'));
console.log('PASS: shallow-water 30 cm to 18 cm regression and synchronous, once-only final size selection.');
