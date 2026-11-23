import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {tournamentSpeciesId,tournamentIslands} from '../../Server/runtime/tournament-species.mjs';
const csv=n=>readFileSync('Assets/Resources/FishingTuning/'+n+'.csv','utf8')
 .trim().split(/\r?\n/).filter(x=>x&&!x.startsWith('#')).map(x=>x.split(','));
const [, ...stats]=csv('FishStats'),[,...weights]=csv('BiomeFishWeights'),
      [header,...odds]=csv('BiomeBaitSpeciesChance'),[,...biomeStats]=csv('BiomeFishStats');
const fish=new Map(stats.map(x=>[+x[0],x]));
assert.equal(stats.length,59-1); // IDs 0..59 with retired ID 4 and boss ID 43 excluded.
assert.equal(weights.length,7*stats.length);
assert.equal(odds.length,63);
for(const [id,base] of [[55,35],[56,32],[57,23],[58,36],[59,36]]){
 const a=fish.get(id),b=fish.get(base);assert(a&&b);
 assert.deepEqual(a.slice(2,4),b.slice(2,4));
 assert.equal(a[8],b[8]);
 if(id===59){assert.equal(+a[4],+b[4]*50);assert.equal(+a[5],+b[5]*50);}
 else assert.deepEqual(a.slice(4),b.slice(4));
 for(const biome of ['suncrest-reef','brinebreak-isle','deep-ocean','bluewater-cay','snapper-island','windgrove-shoal','oceanix-datacenter']){
  const w=weights.find(x=>x[0]===biome&&+x[1]===id);assert(w);
  if(biome==='oceanix-datacenter')assert.deepEqual(w.slice(3),a.slice(2,4));
  else assert.deepEqual(w.slice(3),['0','0']);
 }
}
for(const row of odds){
 assert.equal(row.length,header.length);
 assert.equal(row.slice(2).reduce((s,n)=>s+(+n),0),100);
 const chance=id=>+row[header.findIndex(h=>h.startsWith(id+'_'))];
 if(row[0]!=='oceanix-datacenter'){
  for(const id of [55,56,57,58,59])assert.equal(chance(id),0);
 }else{
  assert.equal(chance(59),0); // Variant is selected after its family bite.
  if(row[1]==='worms'||row[1]==='worms-legacy')assert.equal(chance(58),100);
  if(row[1]==='shrimp')for(const id of [55,56,57,58])assert.equal(chance(id),25);
  if(row[1]==='squid'||row[1].startsWith('lure:'))assert.equal(chance(55),100);
 }
}
for(const id of [55,56,57,58,59])assert(biomeStats.some(x=>x[0]==='oceanix-datacenter'&&+x[1]===id));
assert.equal(tournamentSpeciesId(59),58);
assert(tournamentIslands.find(x=>x.id===6).fish.some(x=>x.id===59));
console.log('Oceanix species and variant tuning: PASS');
