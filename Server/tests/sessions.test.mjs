import test from 'node:test';
import assert from 'node:assert/strict';
import { createSessionServer, PROTOCOL } from '../runtime/sessions.mjs';

const pose = (x=0,world=0) => ({x,y:2,z:1,yaw:30,world,swimming:false});
async function fixture(t,options={}) {
  const server=createSessionServer(options);
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  t.after(()=>{server.closeAllConnections();server.close();});
  return async (path,body={},token='')=>{
    const r=await fetch(`http://127.0.0.1:${server.address().port}${path}`,{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+token},body:JSON.stringify(body)});
    return {status:r.status,...await r.json()};
  };
}
const hello = {protocol:PROTOCOL,build:'1.0',name:'Tester',pose:pose()};
test('two players open/join and exchange walking poses without crossing rooms',async t=>{
  const post=await fixture(t);
  const a=await post('/v1/open',hello);assert.equal(a.ok,true);assert.equal(a.code.length,4);
  const b=await post('/v1/join',{...hello,code:a.code,name:'Guest'});assert.equal(b.players.length,2);assert.equal(b.hostId,a.id);
  const c=await post('/v1/open',hello);
  const moved=await post('/v1/state',{pose:pose(12)},b.token);assert.equal(moved.ok,true);
  const seen=await post('/v1/state',{pose:pose(4)},a.token);
  assert.equal(seen.players.find(p=>p.id===b.id).x,12);
  assert.equal(seen.players.some(p=>p.id===c.id),false);
  assert.equal(seen.players.some(p=>'token' in p),false);
  assert.equal((await post('/v1/state',{pose:pose()},'wrong')).status,401);
});
test('version mismatch, missing rooms, invalid positions, and malformed body rejected',async t=>{
  const post=await fixture(t);const a=await post('/v1/open',hello);
  assert.equal((await post('/v1/join',{...hello,code:a.code,build:'different'})).status,409);
  assert.equal((await post('/v1/join',{...hello,code:'0000'})).status,404);
  assert.equal((await post('/v1/open',{...hello,protocol:'old'})).status,409);
  assert.equal((await post('/v1/state',{pose:pose(100001)},a.token)).status,400);
  assert.equal((await post('/v1/state',{pose:{x:'0'}},a.token)).status,400);
  assert.equal((await post('/v1/state',null,a.token)).status,400);
});
test('guest leaves alone; host leaves and closes the room',async t=>{
  const post=await fixture(t);const a=await post('/v1/open',hello);
  const b=await post('/v1/join',{...hello,code:a.code});
  await post('/v1/leave',{},b.token);
  assert.equal((await post('/v1/state',{pose:pose()},a.token)).players.length,1);
  const c=await post('/v1/join',{...hello,code:a.code});
  await post('/v1/leave',{},a.token);
  assert.equal((await post('/v1/state',{pose:pose()},c.token)).status,401);
});
test('idle disconnected rooms expire and release their capacity',async t=>{
  let now=0;const post=await fixture(t,{clock:()=>now,timeout:100});
  const a=await post('/v1/open',hello);now=101;
  assert.equal((await post('/v1/state',{pose:pose()},a.token)).status,401);
  assert.equal((await post('/v1/join',{...hello,code:a.code})).status,404);
});
test('room capacity is eight; rate limiting bounds attempts',async t=>{
  let now=0;const post=await fixture(t,{clock:()=>now});const a=await post('/v1/open',hello);
  for(let i=0;i<7;i++){now+=1100;assert.equal((await post('/v1/join',{...hello,code:a.code})).ok,true);}
  now+=1100;assert.equal((await post('/v1/join',{...hello,code:a.code})).status,409);
  await post('/v1/join',{...hello,code:'0000'});await post('/v1/join',{...hello,code:'0000'});await post('/v1/join',{...hello,code:'0000'});
  assert.equal((await post('/v1/join',{...hello,code:'0000'})).status,429);
});
test('oversized and non-JSON requests cannot crash the server',async t=>{
  const server=createSessionServer();await new Promise(r=>server.listen(0,'127.0.0.1',r));
  t.after(()=>{server.closeAllConnections();server.close();});
  const url=`http://127.0.0.1:${server.address().port}`;
  assert.equal((await fetch(url+'/v1/open',{method:'POST',body:'hello'})).status,415);
  assert.equal((await fetch(url+'/v1/open',{method:'POST',headers:{'Content-Type':'application/json'},body:'{bad'})).status,400);
  try {
    const oversized=await fetch(url+'/v1/state',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({padding:'x'.repeat(5000)})});
    assert.equal(oversized.status,413);
  } catch(e) { assert.equal(e.cause?.code,'UND_ERR_SOCKET'); }
  assert.equal((await fetch(url+'/health')).status,200);
});

test('observer receives equipped rod and timed cast, then unequip; old clients default safely',async t=>{
  const post=await fixture(t);
  const host=await post('/v1/open',hello);
  const guest=await post('/v1/join',{...hello,code:host.code});
  const fishing={...pose(3),rodEquipped:true,rodTier:5,castSequence:1,castAge:.2,castDuration:.72};
  await post('/v1/state',{pose:fishing},guest.token);
  const seen=(await post('/v1/state',{pose:pose()},host.token)).players;
  const observed=seen.find(p=>p.id===guest.id);
  for(const key of ['rodEquipped','rodTier','castSequence','castAge','castDuration']) assert.equal(observed[key],fishing[key]);
  const old=seen.find(p=>p.id===host.id);
  assert.equal(old.rodEquipped,false);assert.equal(old.castAge,-1);assert.equal(old.castSequence,0);
  await post('/v1/state',{pose:{...fishing,rodEquipped:false,castAge:-1}},guest.token);
  const unequipped=(await post('/v1/state',{pose:pose()},host.token)).players.find(p=>p.id===guest.id);
  assert.equal(unequipped.rodEquipped,false);assert.equal(unequipped.castAge,-1);
  assert.equal(unequipped.castSequence,1);
  for(const change of [{rodTier:6},{rodTier:9},{castSequence:-1},{castSequence:1.5},{castAge:'0'},{castAge:61},{castDuration:0}])
    assert.equal((await post('/v1/state',{pose:{...fishing,...change}},guest.token)).status,400);
});

test('reeling, fish pulling, and paused lure poses preserve the sender animation clock',async t=>{
  const post=await fixture(t);
  const host=await post('/v1/open',hello);
  const guest=await post('/v1/join',{...hello,code:host.code});
  const base={...pose(),rodEquipped:true,castSequence:1,castAge:-1};
  async function exchange(state) {
    assert.equal((await post('/v1/state',{pose:{...base,...state}},guest.token)).ok,true);
    return (await post('/v1/state',{pose:pose()},host.token)).players.find(p=>p.id===guest.id);
  }
  for(const state of [
    {hasFish:true,lureInWater:false,reeling:true,reelTime:.35},
    {hasFish:true,lureInWater:false,reeling:false,reelTime:.35},
    {hasFish:false,lureInWater:true,reeling:true,reelTime:.55},
    {hasFish:false,lureInWater:true,reeling:false,reelTime:.55},
    {hasFish:false,lureInWater:true,reeling:false,reelTime:.55},
    {hasFish:false,lureInWater:true,reeling:true,reelTime:.7},
    {hasFish:false,lureInWater:false,reeling:false,reelTime:.7}
  ]) {
    const observed=await exchange(state);
    for(const key of Object.keys(state)) assert.equal(observed[key],state[key]);
  }
  const old=(await post('/v1/state',{pose:pose()},host.token)).players.find(p=>p.id===host.id);
  assert.equal(old.reelTime,0);assert.equal(old.hasFish,false);assert.equal(old.lureInWater,false);assert.equal(old.reeling,false);
  for(const reelTime of [-1,'1',10000001])
    assert.equal((await post('/v1/state',{pose:{...base,reelTime}},guest.token)).status,400);
});

test('observer receives moving fishing state and held fish identity; clearing removes the catch',async t=>{
  const post=await fixture(t);
  const host=await post('/v1/open',hello);
  const guest=await post('/v1/join',{...hello,code:host.code});
  async function exchange(fields) {
    assert.equal((await post('/v1/state',{pose:{...pose(),...fields}},guest.token)).ok,true);
    return (await post('/v1/state',{pose:pose()},host.token)).players.find(p=>p.id===guest.id);
  }
  const walking=await exchange({x:4,rodEquipped:true,reeling:true,lureInWater:true});
  assert.equal(walking.x,4);assert.equal(walking.reeling,true);
  const fighting=await exchange({x:6,rodEquipped:true,reeling:false,hasFish:true});
  assert.equal(fighting.x,6);assert.equal(fighting.hasFish,true);
  const held=await exchange({heldFishSpecies:21,heldFishWeight:2.75,rodEquipped:false});
  assert.equal(held.heldFishSpecies,21);assert.equal(held.heldFishWeight,2.75);assert.equal(held.rodEquipped,false);
  const switched=await exchange({heldFishSpecies:5,heldFishWeight:15});
  assert.equal(switched.heldFishSpecies,5);assert.equal(switched.heldFishWeight,15);
  const cleared=await exchange({rodEquipped:true,heldFishSpecies:-1,heldFishWeight:0});
  assert.equal(cleared.heldFishSpecies,-1);
  const legacy=await exchange({});assert.equal(legacy.heldFishSpecies,-1);assert.equal(legacy.heldFishWeight,0);
  for (const fields of [{heldFishSpecies:-2},{heldFishSpecies:1.5},{heldFishSpecies:1,heldFishWeight:0},{heldFishWeight:-1},{heldFishWeight:'3'}])
    assert.equal((await post('/v1/state',{pose:{...pose(),...fields}},guest.token)).status,400);
});


test('four-character room codes are distinct and accept lowercase with surrounding spaces',async t=>{
  const post=await fixture(t);
  const first=await post('/v1/open',hello);
  const second=await post('/v1/open',hello);
  assert.match(first.code,/^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}$/);
  assert.match(second.code,/^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}$/);
  assert.notEqual(first.code,second.code);
  const joined=await post('/v1/join',{...hello,code:'  '+first.code.toLowerCase()+'  '});
  assert.equal(joined.ok,true);assert.equal(joined.hostId,first.id);
});

test('sprint state reaches observers, clears on walking, and defaults off for older clients and swimming',async t=>{
  const post=await fixture(t);
  const host=await post('/v1/open',hello);
  const guest=await post('/v1/join',{...hello,code:host.code});
  for(const fields of [{sprinting:true},{sprinting:false},{},{sprinting:true,swimming:true}]){
    const sent=await post('/v1/state',{pose:{...pose(8),...fields}},guest.token);
    assert.equal(sent.ok,true);
    const seen=await post('/v1/state',{pose:pose()},host.token);
    assert.equal(seen.players.find(p=>p.id===guest.id).sprinting,fields.sprinting===true && fields.swimming!==true);
  }
});

const rowboat={key:'boat1',kind:'rowboat',x:0,y:0,z:0,pitch:2,yaw:30,roll:1};
const ride=(owner,seat,extra={})=>({...pose(),rideOwner:owner.id,rideKey:'boat1',rideSeat:seat,rideRevision:1,boatInputY:1,...extra});
test('shared rowboat reserves owner front and guest rear and broadcasts independent rowing',async t=>{
  const post=await fixture(t);
  const owner=await post('/v1/open',{...hello,pose:{...pose(),boat:rowboat}});
  const guest=await post('/v1/join',{...hello,code:owner.code});
  let reply=await post('/v1/state',{pose:ride(owner,1)},guest.token);
  assert.equal(reply.players.find(p=>p.id===guest.id).rideSeat,0);
  assert.equal(reply.players.find(p=>p.id===guest.id).boatInputY,0);
  await post('/v1/state',{pose:ride(owner,2)},guest.token);
  reply=await post('/v1/state',{pose:ride(owner,1,{boat:rowboat})},owner.token);
  assert.deepEqual(reply.players.find(p=>p.id===owner.id).boat,rowboat);
  for(const [player,seat] of [[owner,1],[guest,2]]) {
    const observed=reply.players.find(p=>p.id===player.id);
    assert.equal(observed.rideSeat,seat);assert.equal(observed.boatInputY,1);
  }
  reply=await post('/v1/state',{pose:{...pose(),boat:rowboat}},owner.token);
  assert.equal(reply.players.find(p=>p.id===guest.id).rideSeat,2,'rear is never promoted when owner leaves seat');
  const third=await post('/v1/join',{...hello,code:owner.code});
  reply=await post('/v1/state',{pose:ride(owner,2)},third.token);
  assert.equal(reply.players.find(p=>p.id===third.id).rideSeat,-1,'empty owner place remains reserved');
  await post('/v1/leave',{},guest.token);
  reply=await post('/v1/state',{pose:ride(owner,2)},third.token);
  assert.equal(reply.players.find(p=>p.id===third.id).rideSeat,2);
});

test('boat removal, replacement, world travel and owner disconnect release riders',async t=>{
  const post=await fixture(t);const host=await post('/v1/open',hello);
  const owner=await post('/v1/join',{...hello,code:host.code,pose:{...pose(),boat:rowboat}});
  for(const change of [{boat:null},{boat:{...rowboat,key:'replacement'}},{boat:rowboat,world:1}]) {
    await post('/v1/state',{pose:{...pose(),boat:rowboat}},owner.token);
    let reply=await post('/v1/state',{pose:ride(owner,2)},host.token);
    assert.equal(reply.players.find(p=>p.id===host.id).rideSeat,2);
    reply=await post('/v1/state',{pose:{...pose(),...change}},owner.token);
    assert.equal(reply.players.find(p=>p.id===host.id).rideSeat,-1);
  }
  await post('/v1/state',{pose:{...pose(),boat:rowboat}},owner.token);
  await post('/v1/state',{pose:ride(owner,2)},host.token);
  await post('/v1/leave',{},owner.token);
  const reply=await post('/v1/state',{pose:ride(owner,2)},host.token);
  assert.equal(reply.players.find(p=>p.id===host.id).rideSeat,-1);
});

test('boat capacity, distance, world, identity and malformed states are checked',async t=>{
  const post=await fixture(t);
  const owner=await post('/v1/open',{...hello,pose:{...pose(),boat:{...rowboat,kind:'raft'}}});
  await post('/v1/state',{pose:ride(owner,1,{boat:{...rowboat,kind:'raft'}})},owner.token);
  const guest=await post('/v1/join',{...hello,code:owner.code});
  let reply=await post('/v1/state',{pose:ride(owner,1)},guest.token);
  assert.equal(reply.players.find(p=>p.id===guest.id).rideSeat,-1);
  await post('/v1/state',{pose:{...pose(),boat:rowboat}},owner.token);
  for(const fields of [{x:100},{world:1},{rideKey:'wrong'},{rideOwner:guest.id}]) {
    reply=await post('/v1/state',{pose:ride(owner,2,fields)},guest.token);
    assert.equal(reply.players.find(p=>p.id===guest.id).rideSeat,-1);
  }
  for(const fields of [{boat:{...rowboat,kind:'unknown'}},{boatInputX:2},{rideSeat:1.5},{rideRevision:-1},{rideX:20}])
    assert.equal((await post('/v1/state',{pose:{...pose(),...fields}},guest.token)).status,400);
});

test('stale rowing input stops before disconnected boat disappears',async t=>{
  let now=0;const post=await fixture(t,{clock:()=>now});
  const owner=await post('/v1/open',{...hello,pose:{...pose(),boat:rowboat}});
  const guest=await post('/v1/join',{...hello,code:owner.code});
  await post('/v1/state',{pose:ride(owner,1,{boat:rowboat})},owner.token);
  now=1100;
  let observed=(await post('/v1/state',{pose:pose()},guest.token)).players.find(p=>p.id===owner.id);
  assert.equal(observed.boatInputY,0);assert.deepEqual(observed.boat,rowboat);
  now=3100;
  observed=(await post('/v1/state',{pose:pose()},guest.token)).players.find(p=>p.id===owner.id);
  assert.equal(observed.boat,null);
});

test('Unity empty boat object allows fresh open, join and polling without placing a boat',async t=>{
  const post=await fixture(t);
  const empty={key:'',kind:'',x:0,y:0,z:0,pitch:0,yaw:0,roll:0};
  const fresh={...pose(),boat:empty,rideOwner:'',rideKey:'',rideSeat:-1,rideRevision:0};
  const host=await post('/v1/open',{...hello,pose:fresh});
  assert.equal(host.ok,true);assert.equal(host.players[0].boat,null);
  const guest=await post('/v1/join',{...hello,code:host.code,pose:fresh});
  assert.equal(guest.ok,true);assert.equal(guest.players.length,2);
  for(const boat of [empty,{},null,{...empty,key:null,kind:null}]) {
    const reply=await post('/v1/state',{pose:{...fresh,boat}},guest.token);
    assert.equal(reply.ok,true);
    assert.equal(reply.players.find(p=>p.id===guest.id).boat,null);
  }
  await post('/v1/state',{pose:{...pose(),boat:rowboat}},host.token);
  await post('/v1/state',{pose:ride(host,2)},guest.token);
  const removed=await post('/v1/state',{pose:fresh},host.token);
  assert.equal(removed.ok,true);
  assert.equal(removed.players.find(p=>p.id===host.id).boat,null);
  assert.equal(removed.players.find(p=>p.id===guest.id).rideSeat,-1);
  for(const boat of [[],false,{...empty,key:'boat1'},{...empty,kind:'rowboat'},{...empty,x:1},{...empty,x:'0'}])
    assert.equal((await post('/v1/state',{pose:{...fresh,boat}},host.token)).status,400);
});


test('observer receives lure and bait endpoints through existing snapshots, and clears stale or inactive lines',async t=>{
  let now=0;const post=await fixture(t,{clock:()=>now});
  const host=await post('/v1/open',hello);
  const guest=await post('/v1/join',{...hello,code:host.code});
  for(const lineKind of [1,2,3]) {
    const state={...pose(),rodEquipped:true,lineKind,lureVariant:3,lineX:12.34,lineY:-.25,lineZ:22.5,hasFish:lineKind!==1};
    assert.equal((await post('/v1/state',{pose:state},guest.token)).ok,true);
    const peer=(await post('/v1/state',{pose:pose()},host.token)).players.find(p=>p.id===guest.id);
    for(const key of ['lineKind','lureVariant','lineX','lineY','lineZ'])assert.equal(peer[key],state[key]);
  }
  now=1100;
  assert.equal((await post('/v1/state',{pose:pose()},host.token)).players.find(p=>p.id===guest.id).lineKind,0);
  for(const extra of [{rodEquipped:false},{swimming:true},{world:1},{lineKind:0}]) {
    const reply=await post('/v1/state',{pose:{...pose(),rodEquipped:true,lineKind:2,lineX:1,lineY:0,lineZ:2,...extra}},guest.token);
    assert.equal(reply.players.find(p=>p.id===guest.id).lineKind,0);
  }
  for(const fields of [{lineKind:4},{lineKind:1.5},{lureVariant:5},{lineKind:1},{lineX:'2'},{lineZ:100001}])
    assert.equal((await post('/v1/state',{pose:{...pose(),...fields}},guest.token)).status,400);
  const legacy=await post('/v1/state',{pose:pose()},guest.token);
  assert.equal(legacy.players.find(p=>p.id===guest.id).lineKind,0);
});


test('HTTP tournament invitations, scoring, snapshots and receipt recovery work without leaking wager secrets',async t=>{
 let now=1000;const post=await fixture(t,{clock:()=>now,timeout:1000000,tournamentRandom:()=>0});
 const state={...pose(),islandMask:31,castSequence:10};
 const host=await post('/v1/open',{...hello,pose:state});const guest=await post('/v1/join',{...hello,code:host.code,pose:state});
 const id='1'.repeat(32),ht='2'.repeat(32),gt='3'.repeat(32);
 const send=(p,tournament,castSequence=10)=>post('/v1/tournament',{pose:{...state,castSequence},tournament},p.token);
 let reply=await send(host,{op:'create',id,ticket:ht,island:0,wager:75,balance:100});assert.equal(reply.ok,true);assert.equal(reply.tournament.phase,'inviting');
 assert.equal(JSON.stringify(reply).includes(ht),false);
 reply=await send(guest,{op:'join',id,ticket:gt,balance:100});assert.equal(reply.tournament.phase,'countdown');
 now=6000;await post('/v1/state',{pose:state},host.token);
 reply=await send(guest,{op:'catch',id,island:0,species:reply.tournament.species,cast:11,kg:2},11);assert.equal(reply.ok,true);
 now=366000;reply=await post('/v1/state',{pose:state},host.token);
 assert.equal(reply.tournament.winnerId,guest.id);
 const payout=await post('/v1/tournament-receipt',{ticket:gt,cancelMissing:true});assert.equal(payout.payout,150);assert.equal(payout.ready,true);
});

test('renaming updates only the authenticated player and persists when older polls omit the name',async t=>{
 const post=await fixture(t);
 const host=await post('/v1/open',hello);
 const guest=await post('/v1/join',{...hello,name:'Guest',code:host.code});
 let reply=await post('/v1/state',{pose:pose(),name:'  New_Angler-2!  ',id:host.id},guest.token);
 assert.equal(reply.players.find(p=>p.id===guest.id).name,'New_Angler-2');
 assert.equal(reply.players.find(p=>p.id===host.id).name,'Tester');
 reply=await post('/v1/state',{pose:pose()},guest.token);
 assert.equal(reply.players.find(p=>p.id===guest.id).name,'New_Angler-2');
 reply=await post('/v1/state',{pose:pose(),name:'x'.repeat(30)},guest.token);
 assert.equal(reply.players.find(p=>p.id===guest.id).name,'x'.repeat(20));
 reply=await post('/v1/state',{pose:pose(),name:' <>! '},guest.token);
 assert.equal(reply.players.find(p=>p.id===guest.id).name,'Angler');
});

test('rename during a tournament preserves scores and uses the new name in final results',async t=>{
 let now=1000;const post=await fixture(t,{clock:()=>now,timeout:1000000,tournamentRandom:()=>0});
 const state={...pose(),islandMask:31,castSequence:10};
 const host=await post('/v1/open',{...hello,pose:state});
 const guest=await post('/v1/join',{...hello,name:'Before',code:host.code,pose:state});
 const id='a'.repeat(32),ht='b'.repeat(32),gt='c'.repeat(32);
 await post('/v1/tournament',{pose:state,tournament:{op:'create',id,ticket:ht,island:0,wager:75,balance:100}},host.token);
 let reply=await post('/v1/tournament',{pose:state,tournament:{op:'join',id,ticket:gt,balance:100}},guest.token);
 now=6000;await post('/v1/state',{pose:state},host.token);
 reply=await post('/v1/tournament',{pose:{...state,castSequence:11},tournament:{op:'catch',id,island:0,species:reply.tournament.species,cast:11,kg:2}},guest.token);
 reply=await post('/v1/state',{pose:{...state,castSequence:11},name:'After'},guest.token);
 const entry=reply.tournament.entries.find(e=>e.id===guest.id);
 assert.equal(entry.name,'After');assert.equal(entry.points,200);assert.equal(entry.catches,1);
 now=366000;reply=await post('/v1/state',{pose:state},host.token);
 assert.equal(reply.tournament.winnerName,'After');assert.equal(reply.tournament.biggestName,'After');
 reply=await post('/v1/state',{pose:state,name:'Next match'},guest.token);
 assert.equal(reply.tournament.winnerName,'After');assert.equal(reply.tournament.biggestName,'After');
 assert.equal((await post('/v1/tournament-receipt',{ticket:gt,cancelMissing:true})).payout,150);
});

test('lightning zaps replicate as validated cosmetic events and keep sequence through casts',async t=>{
  const post=await fixture(t);
  const host=await post('/v1/open',hello);
  const guest=await post('/v1/join',{...hello,code:host.code,name:'Zap Angler'});
  const line={...pose(),rodEquipped:true,lineKind:3,hasFish:true,
    lineX:23.5,lineY:0,lineZ:-8.25,lightningSerial:1,lightningZap:0};
  assert.equal((await post('/v1/state',{pose:line},guest.token)).ok,true);
  let player=(await post('/v1/state',{pose:pose()},host.token)).players.find(p=>p.id===guest.id);
  assert.equal(player.lightningSerial,1);
  assert.equal(player.lightningZap,0);
  assert.equal(player.lineX,23.5);
  // Every numbered zap is a new event, including later zaps reusing audio 4.
  for(let index=1;index<8;index++){
    const reply=await post('/v1/state',{pose:{...line,lightningSerial:index+1,lightningZap:index}},guest.token);
    const peer=reply.players.find(p=>p.id===guest.id);
    assert.equal(peer.lightningSerial,index+1);
    assert.equal(peer.lightningZap,index);
  }
  // Keep the serial while the rod is stowed or there is no hooked fish,
  // so the observer knows a subsequent lightning event is new, not a replay.
  let reply=await post('/v1/state',{pose:{...pose(),lightningSerial:8,lightningZap:7}},guest.token);
  player=reply.players.find(p=>p.id===guest.id);
  assert.equal(player.lineKind,0);
  assert.equal(player.lightningSerial,8);
  assert.equal(player.lightningZap,7);
  reply=await post('/v1/state',{pose:{...line,lightningSerial:9,lightningZap:0}},guest.token);
  player=reply.players.find(p=>p.id===guest.id);
  assert.equal(player.lightningSerial,9);
  assert.equal(player.lightningZap,0);
  for(const extra of [{lightningSerial:-1},{lightningSerial:1.4},{lightningSerial:2147483648},
    {lightningZap:-1},{lightningZap:8},{lightningZap:2.5},{lightningZap:'3'}]){
    assert.equal((await post('/v1/state',{pose:{...line,...extra}},guest.token)).status,400);
  }
});

test('successful cast splash and ripple events replicate independently of fishing line state',async t=>{
  let now=1000;
  const post=await fixture(t,{clock:()=>now,timeout:1000000});
  const host=await post('/v1/open',hello);
  const guest=await post('/v1/join',{...hello,name:'Caster',code:host.code});
  const landing={...pose(5),rodEquipped:true,castSequence:7,castImpactSequence:3,
    castImpactAge:.18,castImpactX:25.24,castImpactY:0.42,castImpactZ:-6.38};
  let reply=await post('/v1/state',{pose:landing},guest.token);
  assert.equal(reply.ok,true);
  let remote=reply.players.find(p=>p.id===guest.id);
  for(const key of ['castImpactSequence','castImpactAge','castImpactX','castImpactY','castImpactZ'])
    assert.equal(remote[key],landing[key]);
  assert.equal(remote.lineKind,0); // Water impact remains visible without a line-state packet.
  // Repeated snapshots retain the same event ID. Clients deduplicate it.
  remote=(await post('/v1/state',{pose:pose()},host.token)).players.find(p=>p.id===guest.id);
  assert.equal(remote.castImpactSequence,3);
  reply=await post('/v1/state',{pose:{...landing,castImpactSequence:4,castImpactAge:.06}},guest.token);
  assert.equal(reply.players.find(p=>p.id===guest.id).castImpactSequence,4);
  // Older impacts are carried as baselines but not replayed by observers.
  reply=await post('/v1/state',{pose:{...landing,castImpactAge:60}},guest.token);
  assert.equal(reply.players.find(p=>p.id===guest.id).castImpactAge,60);
  now=2200;
  reply=await post('/v1/state',{pose:pose()},host.token);
  assert.equal(reply.players.find(p=>p.id===guest.id).castImpactAge,-1);
  // No splashes in interior/shopping worlds.
  reply=await post('/v1/state',{pose:{...landing,world:1}},guest.token);
  remote=reply.players.find(p=>p.id===guest.id);
  assert.equal(remote.castImpactSequence,0);
  assert.equal(remote.castImpactAge,-1);
  // Invalid IDs/ages/coordinates must not be broadcast.
  const invalid=[
    {castImpactSequence:-1},{castImpactSequence:1.2},{castImpactSequence:2147483648},
    {castImpactAge:61},{castImpactAge:-2},{castImpactAge:'0'},
    {castImpactX:NaN},{castImpactY:'sea'},{castImpactZ:100001}
  ];
  for(const wrong of invalid){
    const trial={...landing,...wrong};
    assert.equal((await post('/v1/state',{pose:trial},guest.token)).status,400);
  }
  assert.equal((await post('/v1/state',{pose:{...pose(),castImpactAge:.15}},guest.token)).status,400);
  assert.equal((await post('/v1/state',{pose:{...pose(),castImpactSequence:2,castImpactAge:.1}},guest.token)).status,400);
});

test('motorboat right seat requires a driver, is exclusive, and never transmits steering',async t=>{
  const post=await fixture(t), boat={...rowboat,kind:'sailboat'};
  const owner=await post('/v1/open',{...hello,pose:{...pose(),boat}});
  const guest=await post('/v1/join',{...hello,code:owner.code});
  const third=await post('/v1/join',{...hello,code:owner.code});
  const state=async(player,seat,extra={})=>post('/v1/state',{pose:ride(owner,seat,extra)},player.token);
  let reply=await state(guest,2);
  assert.equal(reply.players.find(p=>p.id===guest.id).rideSeat,0,'cannot sit before helm is occupied');
  await state(owner,1,{boat});
  reply=await state(guest,2,{boatInputX:1,boatInputY:1});
  let seated=reply.players.find(p=>p.id===guest.id);
  assert.equal(seated.rideSeat,2);assert.equal(seated.boatInputX,0);assert.equal(seated.boatInputY,0);
  reply=await state(third,2);
  assert.equal(reply.players.find(p=>p.id===third.id).rideSeat,0,'right seat cannot be claimed twice');
  reply=await state(third,1);
  assert.equal(reply.players.find(p=>p.id===third.id).rideSeat,0,'driver cannot be displaced');
  await state(owner,0,{boat});
  reply=await state(guest,2);
  assert.equal(reply.players.find(p=>p.id===guest.id).rideSeat,2,'passenger remains seated without promotion');
  reply=await state(guest,0);
  assert.equal(reply.players.find(p=>p.id===guest.id).rideSeat,0,'stand up releases chair');
  // Ownership does not decide the motorboat seat: a guest may drive the owner.
  await state(third,1);
  reply=await state(owner,2,{boat,boatInputX:-1});
  seated=reply.players.find(p=>p.id===owner.id);
  assert.equal(seated.rideSeat,2);assert.equal(seated.boatInputX,0);
  await state(owner,0,{boat});
  reply=await state(guest,2);
  assert.equal(reply.players.find(p=>p.id===guest.id).rideSeat,2,'released chair is reusable');
  reply=await post('/v1/state',{pose:{...pose(),boat:null}},owner.token);
  assert.equal(reply.players.find(p=>p.id===guest.id).rideSeat,-1,'removing boat releases passenger');
});

test('right passenger seat is not enabled on yachts or rafts',async t=>{
  const post=await fixture(t);
  const owner=await post('/v1/open',hello);
  const guest=await post('/v1/join',{...hello,code:owner.code});
  for(const kind of ['yacht','raft']){
    const boat={...rowboat,kind};
    await post('/v1/state',{pose:ride(owner,1,{boat})},owner.token);
    const reply=await post('/v1/state',{pose:ride(owner,2)},guest.token);
    assert.notEqual(reply.players.find(p=>p.id===guest.id).rideSeat,2);
  }
});

test('server advertises the supported rod tier on open, join and polls',async t=>{
  const post=await fixture(t);
  const host=await post('/v1/open',{...hello,pose:{...pose(),rodEquipped:false,rodTier:0,castSequence:0}});
  assert.equal(host.ok,true);assert.equal(host.maxRodTier,5);
  const guest=await post('/v1/join',{...hello,code:host.code});assert.equal(guest.maxRodTier,5);
  const update=await post('/v1/state',{pose:{...pose(),rodEquipped:true,rodTier:host.maxRodTier}},host.token);
  assert.equal(update.ok,true);assert.equal(update.maxRodTier,5);
  assert.equal(update.players.find(p=>p.id===host.id).rodTier,5);
});
