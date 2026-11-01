import test from 'node:test';
import assert from 'node:assert/strict';
import {createBosses,requiredBossSpecies} from '../runtime/bosses.mjs';
import {createSessionServer,PROTOCOL} from '../runtime/sessions.mjs';
import {mkdtempSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
const ticket=n=>n.toString(16).padStart(32,'0');
function fixture(random=()=>.25,ledgerPath=null){
 let now=1000;const api=createBosses({clock:()=>now,random,ledgerPath});
 const room={players:new Map()};
 const make=id=>{const p={id,room,pose:{world:0,rodEquipped:true,swimming:false,lureVariant:4,lineKind:1,lineX:0,lineZ:-200,castSequence:1}};room.players.set(id,p);return p;};
 const a=make('a'),b=make('b');
 const summon=(p=a,n=1,extra={})=>api.action(p,{op:'summon',ticket:ticket(n),caught:requiredBossSpecies,balance:10000,x:0,y:0,z:-200,...extra});
 const claim=(p=a)=>api.action(p,{op:'claim',id:room.boss.id,cast:p.pose.castSequence});
 const act=(op,damage=0,p=a,lease=room.boss.lease)=>api.action(p,{op,id:room.boss.id,lease,damage});
 return {api,room,a,b,summon,claim,act,advance:ms=>{now+=ms;api.tick(room);}};
}
test('unlock excludes only actual variants; all four islands, not Deep Ocean',()=>{
 const f=fixture();assert(!requiredBossSpecies.includes(20));assert(!requiredBossSpecies.includes(34));
 for(const id of [19,33,29,28,23,22,26,24,40,42])assert(requiredBossSpecies.includes(id));
 assert.throws(()=>f.summon(f.a,1,{caught:requiredBossSpecies.slice(1)}));
 assert.throws(()=>f.summon(f.a,1,{balance:9999}));
 f.summon(f.a,1,{caught:requiredBossSpecies.map(id=>id===19?20:id===33?34:id)});
 assert.equal(f.room.boss.hp,150000);assert.equal(f.room.boss.kg,410);assert.equal(f.api.view(f.room).secondsLeft,900);
 assert.throws(()=>f.summon(f.b,2));assert.equal(f.summon().accepted,true);
});
test('50 percent boundary, one roll per cast, Popper and 80 m landing area',()=>{
 let roll=.5;const f=fixture(()=>roll);f.summon();assert.equal(f.claim().hooked,false);
 roll=.49;assert.equal(f.claim().hooked,false);
 f.a.pose.castSequence++;f.a.pose.lureVariant=3;assert.equal(f.claim().hooked,false);
 f.a.pose.castSequence++;f.a.pose.lureVariant=4;f.a.pose.lineX=80.01;assert.equal(f.claim().hooked,false);
 f.a.pose.castSequence++;f.a.pose.lineX=80;assert.equal(f.claim().hooked,true);
 assert.equal(f.claim().hooked,true);assert.equal(f.claim(f.b).hooked,false);
});
test('shared cumulative damage is retry safe and persists across different anglers',()=>{
 const f=fixture();f.summon();f.claim();f.act('damage',12345);f.act('damage',12345);f.act('damage',500);
 assert.equal(f.api.view(f.room).hp,137655);
 assert.throws(()=>f.act('damage',50000,f.b));assert.throws(()=>f.act('damage',NaN));assert.throws(()=>f.act('catch',12345));
 f.act('release',13000);assert.equal(f.room.boss.hp,137000);f.b.pose.castSequence++;assert(f.claim(f.b).hooked);
 f.act('damage',7000,f.b);assert.equal(f.room.boss.hp,130000);
});
test('heals 10,000 per minute only after five unhooked seconds, capped at maximum',()=>{
 const f=fixture();f.summon();f.claim();f.act('release',30000);
 f.advance(4999);assert.equal(f.room.boss.hp,120000);f.advance(1);assert.equal(f.room.boss.hp,120000);
 f.advance(60000);assert.equal(f.room.boss.hp,130000);f.advance(120000);assert.equal(f.room.boss.hp,150000);
 f.a.pose.castSequence++;f.claim();f.act('damage',50000);
 for(let i=0;i<30;i++){f.advance(2000);f.act('damage',50000);}assert.equal(f.room.boss.hp,100000);
});
test('stale hook and disconnect release ownership; 15 minute expiry cannot be caught',()=>{
 const f=fixture();f.summon();f.claim();f.act('damage',10000);f.advance(5000);assert.equal(f.room.boss.owner,'');
 f.advance(5000);assert.equal(f.room.boss.hp,140000);f.advance(6000);assert.equal(f.room.boss.hp,141000);
 f.b.pose.castSequence++;f.claim(f.b);f.api.leave(f.b);assert.equal(f.room.boss.owner,'');
 f.advance(900000);assert.equal(f.api.view(f.room).phase,'expired');assert.throws(()=>f.claim());assert.throws(()=>f.act('catch',150000));
});
test('one catch only, retry acknowledgment, and summon receipts survive restart',()=>{
 const dir=mkdtempSync(join(tmpdir(),'boss-test-'));try{
 const path=join(dir,'receipts.json');const f=fixture(()=>.25,path);f.summon();f.claim();const lease=f.room.boss.lease;
 assert(f.act('catch',150000).caught);assert(f.act('catch',150000,f.a,lease).caught);assert.throws(()=>f.act('catch',150000,f.b,lease));
 const restarted=fixture(()=>.25,path);assert(restarted.api.receipt({ticket:ticket(1)}).accepted);
 assert.equal(restarted.api.receipt({ticket:ticket(2)}).accepted,false);assert.throws(()=>restarted.summon(restarted.a,2));
 }finally{rmSync(dir,{recursive:true});}
});
test('HTTP snapshots replicate boss to current and late-joining players',async()=>{
 const server=createSessionServer({bossRandom:()=>.25});await new Promise(r=>server.listen(0,'127.0.0.1',r));
 const url='http://127.0.0.1:'+server.address().port;
 const pose={x:0,y:0,z:0,yaw:0,world:0,rodEquipped:true,lineKind:1,lureVariant:4,lineX:0,lineY:0,lineZ:-200,castSequence:1};
 const post=async(path,data,token)=>(await fetch(url+path,{method:'POST',headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},body:JSON.stringify(data)})).json();
 try{
 const a=await post('/v1/open',{protocol:PROTOCOL,build:'boss',pose});
 const s=await post('/v1/boss',{pose,boss:{op:'summon',ticket:ticket(1),caught:requiredBossSpecies,balance:10000,x:0,y:0,z:-200}},a.token);assert(s.accepted);
 const b=await post('/v1/join',{protocol:PROTOCOL,build:'boss',pose,code:a.code});assert.equal(b.boss.id,s.boss.id);
 const c=await post('/v1/boss',{pose,boss:{op:'claim',id:s.boss.id,cast:1}},a.token);assert(c.hooked);
 await post('/v1/boss',{pose,boss:{op:'damage',id:s.boss.id,lease:c.boss.lease,damage:1234}},a.token);
 const state=await post('/v1/state',{pose},b.token);assert.equal(state.boss.hp,148766);
 }finally{server.closeAllConnections();await new Promise(r=>server.close(r));}
});
