import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync,rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createTournaments } from '../runtime/tournaments.mjs';
import { tournamentIslands, tournamentTargets, tournamentSpeciesId } from '../runtime/tournament-species.mjs';

const tid='a'.repeat(32),ht='b'.repeat(32),gt='c'.repeat(32),third='d'.repeat(32);
function setup(options={}) {
  let now=1000;
  const game=createTournaments({clock:()=>now,random:()=>0,...options});
  const room={host:'host',players:new Map()};
  function player(id,mask=31){const p={id,name:id,room,pose:{islandMask:mask,castSequence:10,world:0}};room.players.set(id,p);return p;}
  const host=player('host'),guest=player('guest');
  const create={op:'create',id:tid,ticket:ht,island:0,wager:100,balance:1000};
  const joinGuest={op:'join',id:tid,ticket:gt,balance:500};
  const start=()=>{game.action(host,create);game.action(guest,joinGuest);now+=5000;game.tick(room);};
  return {game,room,host,guest,create,joinGuest,player,start,setTime:t=>now=t,getTime:()=>now};
}
function catchFish(f,p,weight,cast=11,extra={}){
 p.pose.castSequence=cast;
 return f.game.action(p,{op:'catch',id:tid,cast,island:0,species:f.room.tournament.target?.id??0,kg:weight,...extra});
}
test('only host creates; locked players and insufficient balances cannot wager',()=>{
 const f=setup();assert.throws(()=>f.game.action(f.guest,f.create),/Only the host/);
 f.host.pose.islandMask=1;assert.throws(()=>f.game.action(f.host,{...f.create,island:3}),/not unlocked/);
 assert.throws(()=>f.game.action(f.host,{...f.create,wager:0}),/Wager/);
 assert.throws(()=>f.game.action(f.host,{...f.create,balance:99}),/Not enough/);
 f.guest.pose.islandMask=1;f.host.pose.islandMask=31;
 const v=f.game.action(f.host,{...f.create,island:3});assert.equal(v.phase,'cancelled');assert.equal(v.entries.find(e=>e.id==='guest').status,'locked');
 assert.throws(()=>f.game.action(f.guest,f.joinGuest),/closed/);
 assert.deepEqual(f.game.receipt({ticket:ht}),{ready:true,refund:true,payout:100});
});
test('all decisions reveal a random island species, then exactly six minutes and winner takes pool',()=>{
 const f=setup();let v=f.game.action(f.host,f.create);
 assert.equal(v.phase,'inviting');assert.equal(v.species,-1);
 v=f.game.action(f.guest,f.joinGuest);assert.equal(v.phase,'countdown');assert.equal(v.startSeconds,5);
 assert(tournamentIslands[0].fish.some(x=>x.id===v.species));
 f.setTime(5999);f.game.tick(f.room);assert.equal(f.room.tournament.phase,'countdown');
 f.setTime(6000);f.game.tick(f.room);assert.equal(f.room.tournament.phase,'running');
 catchFish(f,f.host,1);catchFish(f,f.guest,2);catchFish(f,f.host,.5,12);
 f.setTime(365999);assert.equal(f.game.view(f.room).phase,'running');
 f.setTime(366000);v=f.game.view(f.room);assert.equal(v.phase,'ended');assert.equal(v.winnerId,'guest');assert.equal(v.pool,200);
 assert.equal(v.entries.find(e=>e.id==='host').points,150);assert.equal(v.biggestName,'guest');assert.equal(v.biggestKg,2);
 assert.deepEqual(f.game.receipt({ticket:gt}),{ready:true,refund:false,payout:200});
 assert.deepEqual(f.game.receipt({ticket:ht}),{ready:true,refund:false,payout:0});
 assert.throws(()=>catchFish(f,f.host,2,13),/not accepting/);
});
test('retry-safe join and catch; wrong island, species, old cast and excessive weight do not score',()=>{
 const f=setup();f.game.action(f.host,f.create);f.game.action(f.host,f.create);
 f.game.action(f.guest,f.joinGuest);f.game.action(f.guest,f.joinGuest);
 f.setTime(6000);f.game.tick(f.room);
 assert.throws(()=>catchFish(f,f.guest,1,10),/new tournament casts/);
 assert.throws(()=>catchFish(f,f.guest,1,11,{island:3}),/does not match/);
 assert.throws(()=>catchFish(f,f.guest,1,11,{species:999}),/does not match/);
 assert.throws(()=>catchFish(f,f.guest,999999,11),/Invalid tournament catch/);
 catchFish(f,f.guest,1,11);catchFish(f,f.guest,1,11);
 assert.equal(f.game.view(f.room).entries.find(e=>e.id==='guest').points,100);
 assert.equal(f.game.view(f.room).pool,200);
 assert(!JSON.stringify(f.game.view(f.room)).includes(gt));
});
test('invitation timeout, declines, and no qualifying catches refund all stakes',()=>{
 const f=setup();f.game.action(f.host,f.create);f.setTime(46000);
 assert.equal(f.game.view(f.room).phase,'cancelled');assert.equal(f.game.receipt({ticket:ht}).refund,true);
 const g=setup();g.game.action(g.host,g.create);g.game.action(g.guest,{op:'decline',id:tid});
 assert.equal(g.game.view(g.room).phase,'cancelled');assert.equal(g.game.receipt({ticket:ht}).payout,100);
 const h=setup();h.start();h.setTime(366000);assert.equal(h.game.view(h.room).winnerId,'');
 for(const ticket of [ht,gt])assert.equal(h.game.receipt({ticket}).refund,true);
});
test('ties resolve by biggest individual catch then first score; disconnected active guest can still win',()=>{
 const f=setup();f.start();catchFish(f,f.host,1);catchFish(f,f.host,1,12);catchFish(f,f.guest,2);
 f.game.leave(f.guest);f.room.players.delete('guest');f.setTime(366000);
 assert.equal(f.game.view(f.room).winnerId,'guest');assert.equal(f.game.receipt({ticket:gt}).payout,200);
 const g=setup();g.start();catchFish(g,g.host,1);g.setTime(7000);catchFish(g,g.guest,1);g.setTime(366000);
 assert.equal(g.game.view(g.room).winnerId,'host');
});
test('host disconnect cancels, lobby guest leaves refunds, and unknown tickets close before refund',()=>{
 const f=setup();f.start();f.game.leave(f.host);
 for(const ticket of [ht,gt])assert.equal(f.game.receipt({ticket}).refund,true);
 const g=setup();g.player('third');g.game.action(g.host,g.create);g.game.action(g.guest,g.joinGuest);g.game.leave(g.guest);g.room.players.delete('guest');
 assert.equal(g.game.receipt({ticket:gt}).payout,100);assert.equal(g.game.view(g.room).pool,100);
 const h=setup();h.game.action(h.host,h.create);
 assert.equal(h.game.receipt({ticket:third,cancelMissing:true}).refund,true);
 assert.throws(()=>h.game.action(h.guest,{...h.joinGuest,ticket:third}),/closed or used/);
});
test('durable escrow refunds interrupted games on restart and preserves completed payouts for recovery',t=>{
 const dir=mkdtempSync(join(tmpdir(),'fishing-tournament-'));t.after(()=>rmSync(dir,{recursive:true,force:true}));
 const ledgerPath=join(dir,'escrow.json');const f=setup({ledgerPath});f.start();
 const recovery=createTournaments({ledgerPath});
 for(const ticket of [ht,gt])assert.deepEqual(recovery.receipt({ticket}),{ready:true,refund:true,payout:100});
 assert.throws(()=>{const r=setup({ledgerPath});r.game.action(r.host,r.create);},/closed or used/);
 const path2=join(dir,'finished.json'),g=setup({ledgerPath:path2});g.start();catchFish(g,g.guest,2);g.setTime(366000);g.game.tick(g.room);
 const finished=createTournaments({ledgerPath:path2});
 for(let i=0;i<3;i++)assert.deepEqual(finished.receipt({ticket:gt}),{ready:true,refund:false,payout:200});
});

test('eight-player pool pays once, excluding a declined entry, and stays inside supported coin bounds',()=>{
 const f=setup();const players=[f.host,f.guest];for(let i=2;i<8;i++)players.push(f.player('p'+i));
 f.game.action(f.host,{...f.create,wager:1000000,balance:1000000});
 for(let i=1;i<7;i++)f.game.action(players[i],{op:'join',id:tid,ticket:(i+4).toString(16).repeat(32),balance:1000000});
 f.game.action(players[7],{op:'decline',id:tid});f.setTime(6000);f.game.tick(f.room);
 catchFish(f,f.host,1);f.setTime(366000);const view=f.game.view(f.room);
 assert.equal(view.pool,7000000);assert.equal(view.winnerId,'host');
 assert.equal(f.game.receipt({ticket:ht}).payout,7000000);
 assert.equal(f.game.view(f.room).entries.find(e=>e.id==='p7').status,'declined');
});

test('Deep Ocean is a valid species tournament biome',()=>{
 const f=setup();
 f.game.action(f.host,{...f.create,island:2});
 const view=f.game.action(f.guest,f.joinGuest);
 assert.equal(view.island,2);assert.equal(view.mode,'species');
 assert(tournamentIslands[2].fish.some(species=>species.id===view.species));
 f.setTime(6000);f.game.tick(f.room);
 f.guest.pose.castSequence=11;
 const landed=f.game.action(f.guest,{op:'catch',id:tid,cast:11,island:2,species:view.species,kg:1});
 assert.equal(landed.entries.find(e=>e.id==='guest').catches,1);
});
test('100 percent Charm on top of hotspot max is accepted, but excessive weights are rejected',()=>{
 const f=setup({random:()=>6}); // Suncrest Bigeye Tuna, 9 kg hotspot maximum.
 f.start();
 assert.equal(f.room.tournament.target.id,8);
 catchFish(f,f.guest,17.99,11);
 assert.throws(()=>catchFish(f,f.guest,18.02,12),/Invalid tournament catch/);
 assert.equal(f.game.view(f.room).entries.find(e=>e.id==='guest').points,1799);
});
test('Map-wide total-weight tournament accepts species from every biome with per-biome weight limits and on-demand catch lists',()=>{
 const f=setup();
 assert.throws(()=>f.game.action(f.host,{...f.create,mode:'weight',island:2}),/Invalid tournament format/);
 f.game.action(f.host,{...f.create,mode:'weight'});
 let view=f.game.action(f.guest,f.joinGuest);
 assert.equal(view.mode,'weight');assert.equal(view.species,-1);
 f.setTime(6000);f.game.tick(f.room);
 assert.equal(view.islandName,'Entire Fishing Map');
 catchFish(f,f.guest,17.9,11,{island:0,species:8});
 catchFish(f,f.guest,14,12,{island:1,species:3});
 catchFish(f,f.guest,100,13,{island:2,species:8}); // Exceeds Suncrest cap, legal Deep Ocean Bigeye.
 catchFish(f,f.guest,200,14,{island:3,species:19});
 catchFish(f,f.guest,50,15,{island:4,species:23});
 assert.throws(()=>catchFish(f,f.guest,2,16,{island:0,species:19}),/does not match/);
 assert.throws(()=>catchFish(f,f.guest,2,16,{island:99,species:8}),/does not match/);
 assert.throws(()=>catchFish(f,f.guest,2,16,{island:1.5,species:8}),/does not match/);
 assert.throws(()=>catchFish(f,f.guest,271,16,{island:2,species:8}),/Invalid tournament catch/);
 view=f.game.view(f.room);
 const row=view.entries.find(e=>e.id==='guest');
 assert.equal(row.catches,5);assert(Math.abs(row.totalKg-381.9)<1e-5);
 assert.equal(row.points,38190);assert.equal(view.biggestSpecies,19);
 assert(!JSON.stringify(view).includes('history')); // 10 Hz snapshots do not leak full lists.
 assert.throws(()=>f.game.catches(f.host,{id:tid,player:f.guest.id}),/not available/);
 f.setTime(366000);view=f.game.view(f.room);
 assert.equal(view.winnerId,'guest');
 const list=f.game.catches(f.host,{id:tid,player:f.guest.id});
 assert.equal(list.catches.length,5);
 assert.deepEqual(list.catches.map(c=>c.name),['Bigeye Tuna','Yellowtail','Bigeye Tuna','Mako Shark','Great Barracuda']);
 assert(Math.abs(list.totalKg-381.9)<1e-5);
 assert.throws(()=>f.game.catches(f.host,{id:tid,player:'unknown'}),/not found/);
});

test('Winter Flounder is accepted only in Suncrest with the matching hotspot and Charm ceiling',()=>{
 const island=tournamentIslands.find(i=>i.id===0);
 const fish=island.fish.find(f=>f.id===27);
 assert.ok(fish);
 for(const other of tournamentIslands.filter(i=>i.id!==0))assert.equal(other.fish.some(f=>f.id===27),false);
 const f=setup({random:()=>tournamentTargets(island).findIndex(s=>s.id===27)});
 f.start();assert.equal(f.room.tournament.target.id,27);
 catchFish(f,f.guest,fish.maxKg*2,11);
 assert.throws(()=>catchFish(f,f.guest,fish.maxKg*2+.02,12),/Invalid tournament catch/);
 assert.throws(()=>catchFish(f,f.guest,1,12,{island:1}),/does not match/);
});

test('Roughtail Stingray is accepted only in Suncrest with the matching hotspot and Charm ceiling',()=>{
 const island=tournamentIslands.find(i=>i.id===0);
 const fish=island.fish.find(f=>f.id===28);
 assert.ok(fish);
 for(const other of tournamentIslands.filter(i=>i.id!==0))assert.equal(other.fish.some(f=>f.id===28),false);
 const f=setup({random:()=>tournamentTargets(island).findIndex(s=>s.id===28)});
 f.start();assert.equal(f.room.tournament.target.id,28);
 catchFish(f,f.guest,fish.maxKg*2,11);
 assert.throws(()=>catchFish(f,f.guest,fish.maxKg*2+.02,12),/Invalid tournament catch/);
 assert.throws(()=>catchFish(f,f.guest,1,12,{island:1}),/does not match/);
});

test('Leopard Stingray is accepted only in Suncrest with the matching hotspot and Charm ceiling',()=>{
 const island=tournamentIslands.find(i=>i.id===0);
 const fish=island.fish.find(f=>f.id===29);
 assert.ok(fish);
 for(const other of tournamentIslands.filter(i=>i.id!==0))assert.equal(other.fish.some(f=>f.id===29),false);
 const f=setup({random:()=>tournamentTargets(island).findIndex(s=>s.id===28)});
 f.start();assert.equal(f.room.tournament.target.id,28);
 catchFish(f,f.guest,fish.maxKg*2,11);
 assert.throws(()=>catchFish(f,f.guest,fish.maxKg*2+.02,12),/Invalid tournament catch/);
 assert.throws(()=>catchFish(f,f.guest,1,12,{island:1}),/does not match/);
});

test('Mahogany Snapper is a Snapper Island-only catch, with hotspot and Charm ceilings',()=>{
 const island=tournamentIslands.find(i=>i.id===4);
 const fish=island.fish.find(f=>f.id===30);
 assert.ok(fish);
 for(const other of tournamentIslands.filter(i=>i.id!==4))
  assert.equal(other.fish.some(f=>f.id===30),false);
 const f=setup();
 f.game.action(f.host,{...f.create,mode:'weight'});
 f.game.action(f.guest,f.joinGuest);
 f.setTime(6000);f.game.tick(f.room);
 catchFish(f,f.guest,fish.maxKg*2,11,{island:4,species:30});
 assert.throws(()=>catchFish(f,f.guest,fish.maxKg*2+.02,12,{island:4,species:30}),/Invalid tournament catch/);
 assert.throws(()=>catchFish(f,f.guest,1,12,{island:0,species:30}),/does not match/);
});

test('Titan Triggerfish tournament catch validates only in Suncrest and enforces an authoritative weight ceiling',()=>{
 const island=tournamentIslands.find(x=>x.id===0);
 const targetIndex=island.fish.findIndex(x=>x.id===32);
 assert(targetIndex>=0,'Suncrest tournament should include Titan Triggerfish');
 const fish=island.fish[targetIndex];
 assert.equal(fish.name,'Titan Triggerfish');
 assert(Math.abs(fish.maxKg-16.98)<.0001,'normal weight including 50% hotspot');
 for(const other of tournamentIslands.filter(x=>x.id!==0))
   assert(!other.fish.some(f=>f.id===32),'Titan is exclusive to Suncrest');
 const f=setup({random:()=>tournamentTargets(island).findIndex(x=>x.id===32)});
 f.start();
 assert.equal(f.room.tournament.target.id,32);
 catchFish(f,f.host,1);
 assert.throws(()=>catchFish(f,f.guest,fish.maxKg*2+1,11),/Invalid tournament catch/);
});

for(const [variant,normal,islandId,kg] of [[20,19,2,200],[23,22,4,50],[26,24,3,100],[29,28,0,1],[34,33,0,.04]]) {
 test(`variant ${variant} shares normal ${normal} target and scoring`,()=>{
  const island=tournamentIslands[islandId],choices=tournamentTargets(island);
  assert(!choices.some(f=>f.id===variant));
  assert.equal(tournamentSpeciesId(variant),normal);
  const f=setup({random:()=>choices.findIndex(x=>x.id===normal)});
  f.create.island=islandId;f.start();
  assert.equal(f.room.tournament.target.id,normal);
  catchFish(f,f.host,kg,11,{island:islandId,species:variant});
  catchFish(f,f.guest,kg,11,{island:islandId,species:normal});
  const entries=f.game.view(f.room).entries;
  assert.equal(entries.find(x=>x.id==='host').points,Math.max(1,Math.round(kg*100)));
  assert.equal(entries.find(x=>x.id==='host').points,entries.find(x=>x.id==='guest').points);
  assert.throws(()=>catchFish(f,f.host,kg,12,{island:(islandId+1)%5,species:variant}),/does not match/);
  assert.throws(()=>catchFish(f,f.host,kg,12,{island:islandId,species:0}),/does not match/);
 });
}
test('angelfish total-weight catches remain Suncrest-only',()=>{
 const f=setup();f.create.mode='weight';f.start();
 catchFish(f,f.host,.4,11,{species:33});catchFish(f,f.host,.04,12,{species:34});
 assert.throws(()=>catchFish(f,f.host,.4,13,{island:1,species:33}),/does not match/);
 assert.throws(()=>catchFish(f,f.host,10,13,{species:33}),/Invalid tournament catch/);
});

test('Panther Grouper target and total-weight catches are Suncrest-only with bonus ceilings',()=>{
 const island=tournamentIslands[0],fish=island.fish.find(f=>f.id===35);
 assert(fish);assert(Math.abs(fish.maxKg-(.01479*70**3.08/1000)*1.5)<1e-8);
 for(const other of tournamentIslands.slice(1))assert(!other.fish.some(f=>f.id===35));
 for(const mode of ['species','weight']){
  const f=setup({random:()=>tournamentTargets(island).findIndex(x=>x.id===35)});
  f.create.mode=mode;f.start();
  if(mode==='species')assert.equal(f.room.tournament.target.id,35);
  catchFish(f,f.host,fish.maxKg*2,11,{species:35});
  assert.equal(f.game.view(f.room).entries.find(x=>x.id==='host').points,Math.round(fish.maxKg*2*100));
  assert.throws(()=>catchFish(f,f.host,fish.maxKg*2+.02,12,{species:35}),/Invalid tournament catch/);
  assert.throws(()=>catchFish(f,f.host,1,12,{island:1,species:35}),/does not match/);
 }
});

test('Coral Grouper target and total-weight catches are Suncrest-only with bonus ceilings',()=>{
 const island=tournamentIslands[0],fish=island.fish.find(f=>f.id===36);
 assert(fish);assert(Math.abs(fish.maxKg-(.01148*50**3.04/1000)*1.5)<1e-8);
 for(const other of tournamentIslands.slice(1))assert(!other.fish.some(f=>f.id===36));
 for(const mode of ['species','weight']){
  const f=setup({random:()=>tournamentTargets(island).findIndex(x=>x.id===36)});
  f.create.mode=mode;f.start();
  if(mode==='species')assert.equal(f.room.tournament.target.id,36);
  catchFish(f,f.host,fish.maxKg*2,11,{species:36});
  assert.equal(f.game.view(f.room).entries.find(x=>x.id==='host').points,Math.round(fish.maxKg*2*100));
  assert.throws(()=>catchFish(f,f.host,fish.maxKg*2+.02,12,{species:36}),/Invalid tournament catch/);
  assert.throws(()=>catchFish(f,f.host,1,12,{island:1,species:36}),/does not match/);
 }
});
