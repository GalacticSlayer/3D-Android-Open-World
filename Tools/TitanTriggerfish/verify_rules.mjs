import {readFileSync} from 'node:fs';
import assert from 'node:assert/strict';
import {tournamentIslands} from '../../Server/runtime/tournament-species.mjs';
const read=path=>readFileSync(path,'utf8');
const dir='Assets/Resources/FishingTuning/';
const lines=read(dir+'BiomeBaitSpeciesChance.csv').split(/\r?\n/)
  .filter(x=>x && !x.startsWith('#'));
const header=lines.shift().split(',');
const find=id=>header.findIndex(x=>x.startsWith(id+'_'));
const titan=find(32),picasso=find(31),red=find(1),winter=find(27),roughtail=find(28),leopard=find(29);
assert.equal(titan,33,'Titan stable ID column');
assert.equal(picasso,32,'Picasso ID unchanged');
let count=0,shrimp=0,homeOther=0;
for(const line of lines){
 const columns=line.split(',');
 assert.equal(columns.length,header.length);
 const values=columns.slice(2).map(Number);
 assert.ok(values.every(v=>Number.isInteger(v)&&v>=0));
 assert.equal(values.reduce((a,b)=>a+b,0),100,line);
 const home=columns[0]==='suncrest-reef';
 const isShrimp=columns[1]==='shrimp';
 const isWorm=['worms','worms-legacy'].includes(columns[1]);
 if(home){
   assert.equal(+columns[titan],isShrimp?20:isWorm?1:3);
   assert.equal(+columns[picasso],isShrimp?20:isWorm?1:3);
   if(isShrimp)shrimp++;else homeOther++;
   assert.equal(columns[red],columns[winter],'Red Snapper/Flounder parity');
   assert.equal(columns[red],columns[roughtail],'Red Snapper/Roughtail parity');
   assert.equal(columns[red],columns[leopard],'Red Snapper/Leopard parity');
 } else {
   assert.equal(+columns[titan],0);
   assert.equal(+columns[picasso],0);
 }
 count++;
}
assert.equal(count,45);
assert.equal(shrimp,1);
assert.equal(homeOther,8);
const stats=read(dir+'FishStats.csv').split(/\r?\n/);
const p=stats.find(x=>x.startsWith('31,Picasso Triggerfish,')).split(',');
const t=stats.find(x=>x.startsWith('32,Titan Triggerfish,')).split(',');
assert.ok(Math.abs(+t[4]-2* +p[4])<.0001,'2x min HP');
assert.ok(Math.abs(+t[5]-2* +p[5])<.0001,'2x max HP');
const catalog=read('Assets/_Game/Scripts/Fishing/FishCatalog.cs');
assert.ok(catalog.includes('public const int TitanTriggerfishId=32;'));
assert.ok(catalog.includes('11.32f,.84f,3f,80'),'2x base fight difficulty');
const weightRows=read(dir+'BiomeFishWeights.csv').split(/\r?\n/)
  .filter(x=>x.includes(',32,Titan Triggerfish,'));
assert.equal(weightRows.length,5);
assert.ok(weightRows.includes('suncrest-reef,32,Titan Triggerfish,0.1,11.32'));
assert.equal(weightRows.filter(x=>x.endsWith(',0,0')).length,4);
const size=read('Assets/_Game/Scripts/Fishing/FishSizeTable.cs');
assert.ok(size.includes('BalistoidesViridescensCurve()'));
assert.ok(size.includes('0.03631*Math.Pow(lengthCm,2.93)/1000.0'));
const kg=cm=>.03631*Math.pow(cm,2.93)/1000;
assert.ok(kg(15)>.1&&kg(15)<.103);
assert.ok(kg(75)>11.3&&kg(75)<11.35);
const where=tournamentIslands.flatMap(island=>island.fish.filter(f=>f.id===32)
  .map(f=>({id:island.id,maxKg:f.maxKg})));
assert.equal(where.length,1);
assert.equal(where[0].id,0);
assert.ok(where[0].maxKg>=11.32*1.5-.0001);
const importer=read('Assets/_Game/Editor/TitanTriggerfishInstaller.cs');
assert.ok(importer.includes('SeaBassImporter.InstallModel("TitanTriggerfish", PrefabPath, Source);'));
assert.ok(!importer.includes('importHeldStruggle:true'),'Supplied FBX has a single swim take');
assert.ok(importer.includes('FindLocalFile(false)')&&importer.includes('FindLocalFile(true)'));
console.log('PASS Titan Triggerfish:',count,'complete chance tables, 20% shrimp for both triggerfish, 2x fight difficulty, scientific sizes, animations, island/server restrictions');
