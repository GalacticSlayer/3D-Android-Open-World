import {readFileSync} from 'node:fs';
import {execFileSync} from 'node:child_process';
import assert from 'node:assert/strict';
import {tournamentIslands,tournamentTargets} from '../../Server/runtime/tournament-species.mjs';
const read=p=>readFileSync(p,'utf8');
const parse=s=>s.trim().split(/\r?\n/).filter(x=>!x.startsWith('#')).map(x=>x.split(','));
const dir='Assets/Resources/FishingTuning/';
const [h,...rows]=parse(read(dir+'BiomeBaitSpeciesChance.csv'));
const col=id=>h.findIndex(x=>x.startsWith(id+'_'));
assert.equal(h[col(35)],'35_panther_grouper');
const baseline=parse(execFileSync('git',['show','HEAD:'+dir+'BiomeBaitSpeciesChance.csv'],{encoding:'utf8'})).slice(1);
for(const [i,r] of rows.entries()){
 assert.equal(r.length,h.length);assert(r.slice(2).every(x=>Number.isInteger(+x)&&+x>=0));
 assert.equal(r.slice(2).reduce((s,x)=>s+ +x,0),100);
 const worms=r[0]==='suncrest-reef'&&['worms','worms-legacy'].includes(r[1]);
 assert.equal(+r[col(35)],worms?30:0);
 if(worms){
  assert.equal(+r[col(33)],30);assert.equal(+r[col(31)],1);assert.equal(+r[col(32)],1);
  for(const id of [27,28,29])assert.equal(r[col(id)],r[col(1)]);
 }else if(baseline[i].length===h.length-1)assert.deepEqual(r.slice(0,-1),baseline[i],'Existing other bait/biome odds unchanged');
}
assert.equal(rows.length,45);
const stats=parse(read(dir+'FishStats.csv')).slice(1),g=stats.find(x=>x[0]==='35'),red=stats.find(x=>x[0]==='1');
assert(g);assert.equal(+g[4],Math.round(+red[4]*2.5));assert.equal(+g[5],+red[5]*2.5);assert.equal(g[8],red[8]);
const weight=cm=>.01479*cm**3.08/1000;
assert(Math.abs(+g[2]-weight(10))<1e-8);assert(Math.abs(+g[3]-weight(70))<1e-8);
const ranges=parse(read(dir+'BiomeFishWeights.csv')).slice(1).filter(x=>x[1]==='35');assert.equal(ranges.length,5);
for(const r of ranges){if(r[0]==='suncrest-reef')assert.deepEqual(r.slice(3),g.slice(2,4));else assert.deepEqual(r.slice(3),['0','0']);}
const cat=read('Assets/_Game/Scripts/Fishing/FishCatalog.cs');assert(cat.includes('PantherGrouperId=35;'));assert(cat.includes('1.05f,30f,100'));
const system=read('Assets/_Game/Scripts/Fishing/FishingSystem.cs');
assert(system.includes('if(hookedSpeciesId==FishCatalog.PantherGrouperId)'));
assert(system.includes('sizeDifficulty * .26f) * fightQuality * 2.5f;'),'Multiplier follows Red Snapper clamp');
const installer=read('Assets/_Game/Editor/PantherGrouperInstaller.cs');
assert(installer.includes('SeaBassImporter.InstallModel("PantherGrouper", PrefabPath, Source);'));
assert(!installer.includes('Juvenile'));assert(!installer.includes('importHeldStruggle:true'));
assert(read('Assets/_Game/Scripts/Fishing/FishVisualFactory.cs').includes('case FishCatalog.PantherGrouperId:return "Fishing/PantherGrouper";'));
assert.deepEqual(tournamentIslands.filter(i=>i.fish.some(f=>f.id===35)).map(i=>i.id),[0]);
assert(Math.abs(tournamentIslands[0].fish.find(f=>f.id===35).maxKg-weight(70)*1.5)<1e-8);
assert(tournamentTargets(tournamentIslands[0]).some(f=>f.id===35));
console.log('PASS Panther Grouper: 45 normalized catch tables; Suncrest-only 30% worms; 10–70 cm model; 2.5x combat settings; importer and tournament integration.');
