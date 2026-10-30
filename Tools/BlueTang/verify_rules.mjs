import {readFileSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import assert from 'node:assert/strict';
import {tournamentIslands,tournamentTargets} from '../../Server/runtime/tournament-species.mjs';
const read=p=>readFileSync(p,'utf8');
const parse=s=>s.trim().split(/\r?\n/).filter(x=>!x.startsWith('#')).map(x=>x.split(','));
const dir='Assets/Resources/FishingTuning/';
const [h,...rows]=parse(read(dir+'BiomeBaitSpeciesChance.csv'));
const col=id=>h.findIndex(x=>x.startsWith(id+'_'));
assert(col(41)>=2);
const baseline=parse(execFileSync('git',['show','HEAD:'+dir+'BiomeBaitSpeciesChance.csv'],{encoding:'utf8'})).slice(1);
for(const [i,r] of rows.entries()){
 assert.equal(r.length,h.length);assert(r.slice(2).every(x=>Number.isInteger(+x)&&+x>=0));
 assert.equal(r.slice(2).reduce((s,x)=>s+ +x,0),100);
 const shrimp=r[0]==='suncrest-reef'&&r[1]==='shrimp';
 assert.equal(+r[col(41)],shrimp?30:0);
 if(shrimp){
  assert.equal(+r[col(37)],21);assert.equal(+r[col(39)],21);assert.equal(+r[col(31)],14);assert.equal(+r[col(32)],14);
  for(const id of [27,28,29])assert.equal(r[col(id)],r[col(1)]);
 }else assert.deepEqual(baseline[i].length===r.length?r:r.slice(0,-1),baseline[i],'All other bait/biome odds unchanged');
}
assert.equal(rows.length,45);
const stats=parse(read(dir+'FishStats.csv')).slice(1),g=stats.find(x=>x[0]==='41'),red=stats.find(x=>x[0]==='1');
assert(g);assert.equal(+g[4],Math.round(+red[4]));assert.equal(+g[5],+red[5]);assert.equal(g[8],red[8]);
const weight=cm=>.02291*cm**2.97/1000;
assert(Math.abs(+g[2]-weight(10))<1e-8);assert(Math.abs(+g[3]-weight(31))<1e-8);
const ranges=parse(read(dir+'BiomeFishWeights.csv')).slice(1).filter(x=>x[1]==='41');assert.equal(ranges.length,5);
for(const r of ranges){if(r[0]==='suncrest-reef')assert.deepEqual(r.slice(3),g.slice(2,4));else assert.deepEqual(r.slice(3),['0','0']);}
const cat=read('Assets/_Game/Scripts/Fishing/FishCatalog.cs');assert(cat.includes('BlueTangId=41;'));assert(cat.includes('.42f,0f,40'));
const installer=read('Assets/_Game/Editor/BlueTangInstaller.cs');
assert(installer.includes('SeaBassImporter.InstallModel("BlueTang", PrefabPath, Source);'));
assert(!installer.includes('Juvenile'));assert(!installer.includes('importHeldStruggle:true'));
assert(read('Assets/_Game/Scripts/Fishing/FishVisualFactory.cs').includes('case FishCatalog.BlueTangId:return "Fishing/BlueTang";'));
assert.deepEqual(tournamentIslands.filter(i=>i.fish.some(f=>f.id===41)).map(i=>i.id),[0]);
assert(Math.abs(tournamentIslands[0].fish.find(f=>f.id===41).maxKg-weight(31)*1.5)<1e-8);
assert(tournamentTargets(tournamentIslands[0]).some(f=>f.id===41));
console.log('PASS Blue Tang: 45 normalized catch tables; Suncrest-only 30% shrimp; 10–31 cm model; 1x combat settings; importer and tournament integration.');
