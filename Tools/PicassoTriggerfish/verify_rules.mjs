import {readFileSync} from 'node:fs';
import assert from 'node:assert/strict';
import {tournamentIslands} from '../../Server/runtime/tournament-species.mjs';

const root='Assets/Resources/FishingTuning/';
const rows=readFileSync(root+'BiomeBaitSpeciesChance.csv','utf8')
  .trim().split(/\r?\n/).filter(line=>line && !line.startsWith('#'));
const header=rows.shift().split(',');
const newIndex=header.findIndex(x=>x.startsWith('31_picasso_triggerfish'));
assert.equal(newIndex,32,'Retain Picasso stable species ID');
assert.ok(header[33].startsWith('32_titan_triggerfish'));
const redIndex=header.findIndex(x=>x.startsWith('1_red_snapper'));
const flounderIndex=header.findIndex(x=>x.startsWith('27_winter_flounder'));
const stingrayIndex=header.findIndex(x=>x.startsWith('28_roughtail_stingray'));
const leopardIndex=header.findIndex(x=>x.startsWith('29_leopard_stingray'));
let total=0,home=0;
for(const line of rows){
 const columns=line.split(',');
 assert.equal(columns.length,header.length);
 assert.equal(columns.slice(2).reduce((sum,x)=>sum+Number(x),0),100);
 assert.ok(columns.slice(2).every(x=>Number.isInteger(+x)&&+x>=0));
 if(columns[0]==='suncrest-reef'){
   assert.equal(+columns[newIndex],columns[1]==='shrimp'?20:['worms','worms-legacy'].includes(columns[1])?1:3,'Picasso shrimp odds 20%, worms 1%, other Suncrest odds 3%');
   assert.equal(columns[redIndex],columns[flounderIndex],'Keep Red Snapper/Flounder parity');
   assert.equal(columns[redIndex],columns[stingrayIndex],'Keep Roughtail parity');
   assert.equal(columns[redIndex],columns[leopardIndex],'Keep Leopard parity');
   home++;
 }else assert.equal(+columns[newIndex],0,'No triggerfish in other biomes');
 total++;
}
assert.equal(total,45);
assert.equal(home,9);
const stats=readFileSync(root+'FishStats.csv','utf8').split(/\r?\n/);
const red=stats.find(x=>x.startsWith('1,Red Snapper,')).split(',');
const picasso=stats.find(x=>x.startsWith('31,Picasso Triggerfish,')).split(',');
assert.deepEqual(picasso.slice(4,6),red.slice(4,6),'Match health');
assert.equal(picasso[8],red[8],'Match swimming speed');
assert.equal(+picasso[2],.06);
assert.equal(+picasso[3],.56);
const ranges=readFileSync(root+'BiomeFishWeights.csv','utf8').split(/\r?\n/)
  .filter(x=>x.includes(',31,Picasso Triggerfish,'));
assert.equal(ranges.length,5);
assert.equal(ranges.filter(x=>x.startsWith('suncrest-reef,31,Picasso Triggerfish,0.06,0.56')).length,1);
assert.equal(ranges.filter(x=>!x.startsWith('suncrest-reef,') && x.endsWith(',0,0')).length,4);
assert.deepEqual(tournamentIslands.flatMap(i=>i.fish.filter(f=>f.id===31).map(f=>i.id)),[0]);
assert.ok(tournamentIslands[0].fish.find(f=>f.id===31).maxKg>=.56*1.5-1e-9);
const catalog=readFileSync('Assets/_Game/Scripts/Fishing/FishCatalog.cs','utf8');
const curve=readFileSync('Assets/_Game/Scripts/Fishing/FishSizeTable.cs','utf8');
assert.ok(catalog.includes('new FishSpeciesDefinition("Picasso Triggerfish"'));
assert.ok(catalog.includes('.06f,.56f,.42f,3f,40'));
assert.ok(curve.includes('0.1559*Math.Pow(totalLengthCm,2.4059)'));
const size=lengthCm=>0.1559*Math.pow(lengthCm,2.4059)/1000;
assert.ok(size(12)>.05&&size(12)<.1);
assert.ok(size(15)>.09&&size(15)<.13);
assert.ok(size(30)>.5&&size(30)<.57);
console.log('PASS Picasso Triggerfish:',total,'catch rows, realistic length-weight curve, Red Snapper combat, tournament limits, Suncrest exclusivity');
