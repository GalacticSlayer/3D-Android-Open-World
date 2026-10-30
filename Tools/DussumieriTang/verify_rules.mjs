import {readFileSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import assert from 'node:assert/strict';
import {tournamentIslands,tournamentTargets} from '../../Server/runtime/tournament-species.mjs';
const read=p=>readFileSync(p,'utf8');
const parse=s=>s.trim().split(/\r?\n/).filter(x=>!x.startsWith('#')).map(x=>x.split(','));
const dir='Assets/Resources/FishingTuning/';
const [h,...rows]=parse(read(dir+'BiomeBaitSpeciesChance.csv'));
const col=id=>h.findIndex(x=>x.startsWith(id+'_'));
assert(col(42)>=2);
const baseline=parse(execFileSync('git',['show','HEAD:'+dir+'BiomeBaitSpeciesChance.csv'],{encoding:'utf8'})).slice(1);
for(const [i,r] of rows.entries()){
 assert.equal(r.length,h.length);assert(r.slice(2).every(x=>Number.isInteger(+x)&&+x>=0));
 assert.equal(r.slice(2).reduce((s,x)=>s+ +x,0),100);
 const squid=r[0]==='suncrest-reef'&&r[1]==='squid';
 assert.equal(+r[col(42)],squid?30:0);
 if(squid){
  assert.equal(r.slice(2,-1).reduce((sum,x)=>sum+ +x,0),70);
 }else assert.deepEqual(baseline[i].length===r.length?r:r.slice(0,-1),baseline[i],'All other bait/biome odds unchanged');
}
assert.equal(rows.length,45);
assert(read('Assets/_Game/Scripts/Fishing/FishingSystem.cs').includes('if(hookedSpeciesId==FishCatalog.PantherGrouperId || hookedSpeciesId==FishCatalog.DussumieriTangId)'));
assert(read('Assets/_Game/Scripts/Reef/ReefCatalog.cs').includes('starter[FishCatalog.DussumieriTangId]=30f;'));
const stats=parse(read(dir+'FishStats.csv')).slice(1),g=stats.find(x=>x[0]==='42'),red=stats.find(x=>x[0]==='1');
assert(g);assert.equal(+g[4],Math.round(+red[4]*2.5));assert.equal(+g[5],+red[5]*2.5);assert.equal(g[8],red[8]);
const weight=cm=>.02951*cm**2.95/1000;
assert(Math.abs(+g[2]-weight(10))<1e-8);assert(Math.abs(+g[3]-weight(54))<1e-8);
const ranges=parse(read(dir+'BiomeFishWeights.csv')).slice(1).filter(x=>x[1]==='42');assert.equal(ranges.length,5);
for(const r of ranges){if(r[0]==='suncrest-reef')assert.deepEqual(r.slice(3),g.slice(2,4));else assert.deepEqual(r.slice(3),['0','0']);}
const cat=read('Assets/_Game/Scripts/Fishing/FishCatalog.cs');assert(cat.includes('DussumieriTangId=42;'));assert(cat.includes('1.05f,0f,100'));
const installer=read('Assets/_Game/Editor/DussumieriTangInstaller.cs');
assert(installer.includes('SeaBassImporter.InstallModel("DussumieriTang", PrefabPath, Source);'));
assert(!installer.includes('Juvenile'));assert(!installer.includes('importHeldStruggle:true'));
assert(read('Assets/_Game/Scripts/Fishing/FishVisualFactory.cs').includes('case FishCatalog.DussumieriTangId:return "Fishing/DussumieriTang";'));
assert.deepEqual(tournamentIslands.filter(i=>i.fish.some(f=>f.id===42)).map(i=>i.id),[0]);
assert(Math.abs(tournamentIslands[0].fish.find(f=>f.id===42).maxKg-weight(54)*1.5)<1e-8);
assert(tournamentTargets(tournamentIslands[0]).some(f=>f.id===42));
console.log('PASS Dussumieri Tang: 45 normalized catch tables; Suncrest-only 30% squid; 10–54 cm model; 2.5x combat settings; importer and tournament integration.');
