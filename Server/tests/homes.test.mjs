import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {createHomes} from '../runtime/homes.mjs';
import {createSessionServer} from '../runtime/sessions.mjs';

const temp=()=>mkdtempSync(join(tmpdir(),'aquarium-homes-test-'));
const fish=(kg=12)=>({speciesId:8,biomeId:'suncrest',weightKg:kg});
const tank=(key='aq1',weight=12)=>({
  key,id:'reef',x:12,y:0,z:22,yaw:90,fish:[fish(weight)]
});
test('public by default, directory search, sorting, pagination, and snapshot versions',()=>{
  const dir=temp();let now=100000;
  try{
    const api=createHomes({directory:dir,clock:()=>now});
    const a=api.dispatch('/v1/homes/register',{name:'Reef Keeper'});
    const b=api.dispatch('/v1/homes/register',{name:'Shark Hunter'});
    assert.equal(a.visibility,'public');
    assert.equal(api.dispatch('/v1/homes/list',{}).total,0); // empty houses are not showcased
    const upload={code:a.code,ownerKey:a.ownerKey,name:'Reef Keeper',visibility:'public',tanks:[tank()]};
    const saved=api.dispatch('/v1/homes/update',upload);
    assert.equal(saved.version,1);
    assert.equal(api.dispatch('/v1/homes/update',upload).version,1); // idempotent
    now+=2000;
    api.dispatch('/v1/homes/update',{...upload,tanks:[tank('aq1',28)]});
    assert.equal(api.dispatch('/v1/homes/get',{code:a.code}).tanks[0].fish[0].weightKg,28);
    assert.equal(api.dispatch('/v1/homes/check',{code:a.code}).tanks,undefined);
    api.dispatch('/v1/homes/update',{...upload,code:b.code,ownerKey:b.ownerKey,
      name:'Shark Hunter',tanks:[tank('aq2',80)]});
    const listed=api.dispatch('/v1/homes/list',{query:'shark'});
    assert.equal(listed.total,1);
    assert.equal(listed.homes[0].code,b.code);
    const ranked=api.dispatch('/v1/homes/list',{sort:'biggest'});
    assert.equal(ranked.homes[0].code,b.code);
    assert.equal(ranked.homes[0].biggest.weightKg,80);
    // The requesting player's published Home must not occupy a directory slot,
    // affect its total, or appear in searches.
    const others=api.dispatch('/v1/homes/list',{excludeCode:a.code});
    assert.equal(others.total,1);
    assert.deepEqual(others.homes.map(h=>h.code),[b.code]);
    assert.equal(api.dispatch('/v1/homes/list',{query:'reef',excludeCode:a.code}).total,0);
    assert.equal(api.dispatch('/v1/homes/list',{page:1}).homes.length,0);
  }finally{rmSync(dir,{recursive:true,force:true})}
});

test('unlisted accepts a code but does not appear publicly; private rejects visitors',()=>{
  const dir=temp();
  try{
    const api=createHomes({directory:dir});
    const owner=api.dispatch('/v1/homes/register',{name:'Secret Home'});
    const base={code:owner.code,ownerKey:owner.ownerKey,name:'Secret Home',tanks:[tank()]};
    api.dispatch('/v1/homes/update',{...base,visibility:'unlisted'});
    assert.equal(api.dispatch('/v1/homes/list',{}).total,0);
    assert.equal(api.dispatch('/v1/homes/get',{code:owner.code}).tanks.length,1);
    api.dispatch('/v1/homes/update',{...base,visibility:'private'});
    assert.equal(api.dispatch('/v1/homes/list',{}).total,0);
    assert.throws(()=>api.dispatch('/v1/homes/get',{code:owner.code}),
      e=>e.status===404);
    assert.throws(()=>api.dispatch('/v1/homes/check',{code:owner.code}),
      e=>e.status===404);
    assert.equal(api.dispatch('/v1/homes/get',{code:owner.code,ownerKey:owner.ownerKey}).tanks.length,1);
    assert.throws(()=>api.dispatch('/v1/homes/update',{...base,ownerKey:'0'.repeat(64),visibility:'public'}),
      e=>e.status===403);
    assert.equal(api.dispatch('/v1/homes/check',{code:owner.code,ownerKey:owner.ownerKey}).visibility,'private');
  }finally{rmSync(dir,{recursive:true,force:true})}
});

test('reject invalid IDs, duplicate aquariums, fish, positions and unauthorized writes',()=>{
  const dir=temp();
  try{
    const api=createHomes({directory:dir});
    const a=api.dispatch('/v1/homes/register',{});
    const base={code:a.code,ownerKey:a.ownerKey,visibility:'public',name:'Tester'};
    assert.throws(()=>api.dispatch('/v1/homes/update',{...base,tanks:[tank('wrong')]}));
    assert.throws(()=>api.dispatch('/v1/homes/update',{...base,tanks:[tank(),tank()]}));
    assert.throws(()=>api.dispatch('/v1/homes/update',{...base,tanks:[{...tank(),x:10000}]}));
    assert.throws(()=>api.dispatch('/v1/homes/update',{...base,tanks:[{...tank(),fish:[{...fish(),speciesId:-1}]}]}));
    assert.throws(()=>api.dispatch('/v1/homes/update',{...base,tanks:[{...tank(),fish:Array(81).fill(fish())}]}));
    assert.equal(api.dispatch('/v1/homes/list',{}).total,0);
  }finally{rmSync(dir,{recursive:true,force:true})}
});

test('offline profiles survive server restart without a multiplayer room',()=>{
  const dir=temp();
  try{
    let api=createHomes({directory:dir});
    const home=api.dispatch('/v1/homes/register',{name:'Offline Angler'});
    api.dispatch('/v1/homes/update',{code:home.code,ownerKey:home.ownerKey,
      name:'Offline Angler',visibility:'public',tanks:[tank()]});
    api=createHomes({directory:dir});
    assert.equal(api.dispatch('/v1/homes/list',{}).total,1);
    assert.equal(api.dispatch('/v1/homes/get',{code:home.code}).tanks[0].fish.length,1);
    api.dispatch('/v1/homes/update',{code:home.code,ownerKey:home.ownerKey,
      name:'Offline Angler',visibility:'public',tanks:[tank(),tank('aq2')]});
    assert.equal(api.dispatch('/v1/homes/get',{code:home.code}).aquariums,2);
  }finally{rmSync(dir,{recursive:true,force:true})}
});

test('visiting Homes include the true pond and all pond fish; no-pond snapshots stay solid',()=>{
  const dir=temp();
  try{
    let api=createHomes({directory:dir});
    const owner=api.dispatch('/v1/homes/register',{name:'Pond Collector'});
    const base={code:owner.code,ownerKey:owner.ownerKey,
      name:'Pond Collector',visibility:'public',tanks:[tank()]};
    api.dispatch('/v1/homes/update',{...base,pond:null});
    assert.equal(api.dispatch('/v1/homes/get',{code:owner.code}).pond,null);
    const pond={id:'garden',fish:[fish(18),fish(30),fish(42)]};
    const saved=api.dispatch('/v1/homes/update',{...base,pond});
    const live=api.dispatch('/v1/homes/get',{code:owner.code});
    assert.deepEqual(live.pond,pond);
    assert.equal(live.aquariums,1); // Pond isn't counted as an aquarium.
    assert.equal(live.fish,4); // Pond residents are included.
    assert.equal(live.biggest.weightKg,42);
    assert.equal(saved.version,2);
    assert.equal(api.dispatch('/v1/homes/update',{...base,pond}).version,2);
    api=createHomes({directory:dir});
    assert.deepEqual(api.dispatch('/v1/homes/get',{code:owner.code}).pond,pond);
    // Pre-update clients omit pond entirely; preserve what is already hosted.
    api.dispatch('/v1/homes/update',{...base,name:'Pond Collector Two'});
    assert.deepEqual(api.dispatch('/v1/homes/get',{code:owner.code}).pond,pond);
    assert.throws(()=>api.dispatch('/v1/homes/update',
      {...base,pond:{id:'invalid',fish:[]}}),e=>e.status===400);
    assert.throws(()=>api.dispatch('/v1/homes/update',
      {...base,pond:{id:'lake',fish:Array(51).fill(fish())}}),e=>e.status===400);
    api.dispatch('/v1/homes/update',{...base,pond:null});
    assert.equal(api.dispatch('/v1/homes/get',{code:owner.code}).pond,null);
  }finally{rmSync(dir,{recursive:true,force:true})}
});

test('same-Home idle avatars remain visible; fish revision and owner tank moves stay separate',()=>{
  const dir=temp();let now=1000;
  try{
    const api=createHomes({directory:dir,clock:()=>now});
    const owner=api.dispatch('/v1/homes/register',{name:'Owner'});
    const second=api.dispatch('/v1/homes/register',{name:'Elsewhere'});
    for(const h of [owner,second])
      api.dispatch('/v1/homes/update',{code:h.code,ownerKey:h.ownerKey,
        name:'Player',visibility:'public',tanks:[tank()]});
    const pose={world:2,x:4000,y:200,z:4010,yaw:90,swimming:false};
    const a='a'.repeat(32),b='b'.repeat(32),c='c'.repeat(32);
    let response=api.dispatch('/v1/homes/presence',{code:owner.code,
      session:a,ownerKey:owner.ownerKey,name:'Owner',pose});
    assert.equal(response.players.length,0);
    response=api.dispatch('/v1/homes/presence',{code:owner.code,
      session:b,name:'Visitor',pose:{...pose,x:4002}});
    assert.equal(response.players.length,1);
    assert.equal(response.otherCount,1); // Independent of avatar deserialization.
    assert.equal(response.players[0].name,'Owner');
    assert.equal(response.version,1);
    assert.equal(response.moving,null); // Idle players are full avatars.
    // Repeated idle polls must not require an aquarium drag to show a player.
    for(let i=0;i<3;i++)
    {
      now+=2000;
      api.dispatch('/v1/homes/presence',{code:owner.code,
        session:a,name:'Owner',pose});
      response=api.dispatch('/v1/homes/presence',{code:owner.code,
        session:b,name:'Visitor',pose:{...pose,x:4002}});
      assert.equal(response.players.length,1);
      assert.equal(response.otherCount,1);
      assert.equal(response.players[0].name,'Owner');
      assert.equal(response.players[0].homeCode,owner.code);
      assert.equal(response.players[0].move,undefined);
      assert.equal(response.moving,null);
    }
    // Fish contents still use one persistent revision, not networked fish
    // positions. Presence advertises the new version immediately.
    const changed=api.dispatch('/v1/homes/update',{
      code:owner.code,ownerKey:owner.ownerKey,
      name:'Owner',visibility:'public',tanks:[{...tank(),fish:[fish(12),fish(15)]}]});
    response=api.dispatch('/v1/homes/presence',{code:owner.code,
      session:b,name:'Visitor',pose});
    assert.equal(response.version,changed.version);
    assert.equal(response.otherCount,1);
    assert.equal(api.dispatch('/v1/homes/get',{code:owner.code}).tanks[0].fish.length,2);
    const move={key:'aq1',x:5,y:0,z:12,yaw:80};
    api.dispatch('/v1/homes/presence',{code:owner.code,session:a,
      name:'Owner',pose,move}); // Owner credential cached, not resent.
    response=api.dispatch('/v1/homes/presence',{code:owner.code,
      session:b,name:'Visitor',pose,move:{...move,x:50}});
    assert.deepEqual(response.moving,move);
    assert.equal(response.players[0].move,undefined);
    assert.equal(response.placed,null); // A drag is not a placement.
    // The visitor submitted a different move, but only the owner's
    // authenticated preview was ever broadcast.
    assert.notEqual(response.moving.x,50);
    api.dispatch('/v1/homes/presence',{code:owner.code,
      session:a,name:'Owner',pose}); // Cancel: there is no committed placement.
    response=api.dispatch('/v1/homes/presence',{code:owner.code,
      session:b,name:'Visitor',pose});
    assert.equal(response.moving,null);
    assert.equal(response.placed,null);
    assert.equal(api.dispatch('/v1/homes/get',{code:owner.code}).tanks[0].x,12);

    const finalPlacement={key:'aq1',x:24,y:0,z:18,yaw:135};
    api.dispatch('/v1/homes/presence',{code:owner.code,session:a,
      name:'Owner',pose,placed:finalPlacement});
    response=api.dispatch('/v1/homes/presence',{code:owner.code,
      session:b,name:'Visitor',pose,placed:{...finalPlacement,x:999}});
    assert.deepEqual(response.placed,finalPlacement);
    assert.equal(response.moving,null);
    // The placement announcement is visual only. The persistent snapshot is
    // deliberately unchanged until the authenticated Home update succeeds.
    assert.equal(api.dispatch('/v1/homes/get',{code:owner.code}).tanks[0].x,12);
    response=api.dispatch('/v1/homes/presence',{code:second.code,
      session:c,name:'Other Home',pose});
    assert.equal(response.players.length,0); // Same world, different Home.
    now+=10000;
    response=api.dispatch('/v1/homes/presence',{code:owner.code,
      session:b,name:'Visitor',pose});
    assert.equal(response.players.length,0); // Disconnected owner expired.
    api.dispatch('/v1/homes/update',{code:owner.code,
      ownerKey:owner.ownerKey,name:'Owner',visibility:'private',tanks:[tank()]});
    assert.throws(()=>api.dispatch('/v1/homes/presence',
      {code:owner.code,session:b,name:'Visitor',pose}),e=>e.status===404);
    assert.equal(api.dispatch('/v1/homes/presence',{code:owner.code,
      session:a,ownerKey:owner.ownerKey,name:'Owner',pose}).ok,true);
  }finally{rmSync(dir,{recursive:true,force:true})}
});

test('room-free Home heartbeat sends avatars and only changed aquarium/fish snapshots',()=>{
  const dir=temp();let now=1000;
  try{
    const api=createHomes({directory:dir,clock:()=>now});
    const owner=api.dispatch('/v1/homes/register',{name:'Home Owner'});
    const other=api.dispatch('/v1/homes/register',{name:'Different Home'});
    const meta={code:owner.code,ownerKey:owner.ownerKey,
      name:'Home Owner',visibility:'public'};
    const initial={...tank(),x:15,y:0,z:18,yaw:30,fish:[fish(8)]};
    const first=api.dispatch('/v1/homes/update',
      {...meta,tanks:[initial],pond:null});
    api.dispatch('/v1/homes/update',{code:other.code,
      ownerKey:other.ownerKey,name:'Other',visibility:'public',tanks:[tank()]});

    // No fishing-room token or join/open API is used. These are exactly
    // Home-specific, compact world-2 client poses.
    const homePose={world:2,x:4000,y:200,z:4004,yaw:90,
      heldFishSpecies:-1,heldFishWeight:0,rodTier:7,reelTier:7,
      swimming:false,sprinting:false,rodEquipped:false,hasFish:false};
    const a='a'.repeat(32),b='b'.repeat(32),c='c'.repeat(32);
    let r=api.dispatch('/v1/homes/presence',{
      code:owner.code,session:a,name:'Owner',ownerKey:owner.ownerKey,
      pose:homePose,knownVersion:-1,wantSnapshot:false});
    assert.equal(r.otherCount,0);
    assert.equal(r.snapshot,null);

    r=api.dispatch('/v1/homes/presence',{
      code:owner.code,session:b,name:'Visitor',pose:{...homePose,x:4005},
      knownVersion:-1,wantSnapshot:true});
    assert.equal(r.otherCount,1);
    assert.equal(r.players[0].name,'Owner'); // Idle, no tank movement.
    assert.equal(r.snapshot.version,first.version);
    assert.equal(r.snapshot.tanks[0].x,15);
    assert.equal(r.snapshot.tanks[0].fish.length,1);
    const baseline=r.snapshot.version;

    // No duplicate fish lists/geometry in normal 4Hz presence traffic.
    for(let i=0;i<5;i++)
    {
      now+=250;
      api.dispatch('/v1/homes/presence',{
        code:owner.code,session:a,name:'Owner',pose:homePose});
      r=api.dispatch('/v1/homes/presence',{
        code:owner.code,session:b,name:'Visitor',pose:homePose,
        knownVersion:baseline,wantSnapshot:true});
      assert.equal(r.otherCount,1);
      assert.equal(r.players[0].name,'Owner');
      assert.equal(r.snapshot,null);
    }

    // The owner moved the aquarium while offline: once it reconnects and
    // publishes its latest saved state, a single heartbeat returns both
    // final position AND the full fish additions (no extra GET/check).
    const moved={...initial,x:40,z:36,yaw:150,fish:[fish(8),fish(12)]};
    const updated=api.dispatch('/v1/homes/update',
      {...meta,tanks:[moved],pond:{id:'pond',fish:[fish(20)]}});
    r=api.dispatch('/v1/homes/presence',{
      code:owner.code,session:b,name:'Visitor',pose:homePose,
      knownVersion:baseline,wantSnapshot:true});
    assert.equal(r.version,updated.version);
    assert.equal(r.snapshot.version,updated.version);
    assert.equal(r.snapshot.tanks[0].x,40);
    assert.equal(r.snapshot.tanks[0].z,36);
    assert.equal(r.snapshot.tanks[0].yaw,150);
    assert.equal(r.snapshot.tanks[0].fish.length,2);
    assert.equal(r.snapshot.pond.fish.length,1);
    assert.equal(r.players[0].name,'Owner');

    const reduced={...moved,fish:[fish(12)]};
    const removed=api.dispatch('/v1/homes/update',
      {...meta,tanks:[reduced],pond:{id:'pond',fish:[]}});
    r=api.dispatch('/v1/homes/presence',{
      code:owner.code,session:b,name:'Visitor',pose:homePose,
      knownVersion:updated.version,wantSnapshot:true});
    assert.equal(r.snapshot.version,removed.version);
    assert.equal(r.snapshot.tanks[0].fish.length,1);
    assert.equal(r.snapshot.tanks[0].x,40);
    assert.equal(r.snapshot.pond.fish.length,0);

    // Other Homes are separate even if both Unity worlds share coordinates.
    r=api.dispatch('/v1/homes/presence',{
      code:other.code,session:c,name:'Other',pose:homePose,
      knownVersion:0,wantSnapshot:true});
    assert.equal(r.otherCount,0);
    assert.equal(r.players.length,0);
    assert.equal(r.snapshot.tanks.length,1);
  }finally{rmSync(dir,{recursive:true,force:true})}
});

test('completed aquarium positions and fish changes survive repeated visits and server restart',()=>{
  const dir=temp();
  try{
    let api=createHomes({directory:dir});
    const home=api.dispatch('/v1/homes/register',{name:'Permanent Home'});
    const owner={code:home.code,ownerKey:home.ownerKey,
      name:'Permanent Home',visibility:'public'};
    const original={...tank(),fish:[fish(8)]};
    const first=api.dispatch('/v1/homes/update',{...owner,tanks:[original]});
    const final={...original,x:48,y:0,z:30,yaw:155,
      fish:[fish(8),fish(19),fish(31)]};
    const placed=api.dispatch('/v1/homes/update',{...owner,tanks:[final]});
    assert.equal(placed.version,first.version+1);
    let visitor=api.dispatch('/v1/homes/get',{code:home.code});
    assert.equal(visitor.tanks[0].x,48);
    assert.equal(visitor.tanks[0].yaw,155);
    assert.equal(visitor.tanks[0].fish.length,3);
    assert.equal(api.dispatch('/v1/homes/update',
      {...owner,tanks:[final]}).version,placed.version);
    // Removing fish must persist without changing the aquarium's position.
    const reduced={...final,fish:[fish(8),fish(31)]};
    api.dispatch('/v1/homes/update',{...owner,tanks:[reduced]});
    api=createHomes({directory:dir});
    visitor=api.dispatch('/v1/homes/get',{code:home.code});
    assert.equal(visitor.tanks[0].x,48);
    assert.equal(visitor.tanks[0].z,30);
    assert.equal(visitor.tanks[0].yaw,155);
    assert.deepEqual(visitor.tanks[0].fish,reduced.fish);
    // A second actual placement supersedes the first, permanently.
    const movedAgain={...reduced,x:11,z:43,yaw:260};
    api.dispatch('/v1/homes/update',{...owner,tanks:[movedAgain]});
    api=createHomes({directory:dir});
    visitor=api.dispatch('/v1/homes/get',{code:home.code});
    assert.equal(visitor.tanks[0].x,11);
    assert.equal(visitor.tanks[0].z,43);
    assert.equal(visitor.tanks[0].yaw,260);
    assert.deepEqual(visitor.tanks[0].fish,reduced.fish);
  }finally{rmSync(dir,{recursive:true,force:true})}
});

test('HTTP Home endpoints work without opening a room and support >4 KB uploads',async t=>{
  const dir=temp();
  const server=createSessionServer({homesDirectory:dir});
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  t.after(()=>{server.closeAllConnections();server.close();rmSync(dir,{recursive:true,force:true})});
  const send=async (url,data)=>{
    const r=await fetch('http://127.0.0.1:'+server.address().port+url,{
      method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(data)});
    return {status:r.status,...await r.json()};
  };
  const health=await fetch('http://127.0.0.1:'+server.address().port+'/health');
  assert.equal((await health.json()).homePresenceProtocol,3);
  const created=await send('/v1/homes/register',{name:'Offline Fish'});
  assert.equal(created.ok,true);
  const tanks=[tank(),{...tank('aq2'),fish:Array(80).fill(fish(20))},
    {...tank('aq3'),fish:Array(80).fill(fish(25))}];
  const upload={code:created.code,ownerKey:created.ownerKey,name:'Offline Fish',visibility:'public',tanks};
  assert(JSON.stringify(upload).length>4096);
  assert.equal((await send('/v1/homes/update',upload)).ok,true);
  assert.equal((await send('/v1/homes/list',{})).total,1);
  assert.equal((await send('/v1/homes/get',{code:created.code})).tanks.length,3);
  const homePresence=await send('/v1/homes/presence',{
    code:created.code,session:'1'.repeat(32),ownerKey:created.ownerKey,
    name:'Offline Fish',pose:{world:2,x:4000,y:200,z:4010,yaw:0}
  });
  assert.equal(homePresence.ok,true);
  assert.equal(homePresence.owner,true);
  assert.equal(homePresence.players.length,0);
  assert.equal(homePresence.otherCount,0);
  // The Android Home client uses this flat world-2 pose format.
  const visitingPose={world:2,x:4003,y:200,z:4012,yaw:175,
    heldFishSpecies:-1,heldFishWeight:0,swimming:false,
    sprinting:false,rodEquipped:false,reeling:false,hasFish:false,
    lureInWater:false,fishPosing:false,rodTier:7,reelTier:7};
  const guestPresence=await send('/v1/homes/presence',{
    code:created.code,session:'2'.repeat(32),name:'Guest',
    pose:visitingPose
  });
  assert.equal(guestPresence.ok,true);
  assert.equal(guestPresence.otherCount,1);
  assert.equal(guestPresence.players[0].name,'Offline Fish');
  const ownerPresence=await send('/v1/homes/presence',{
    code:created.code,session:'1'.repeat(32),name:'Offline Fish',
    pose:{...visitingPose,x:4000}
  });
  assert.equal(ownerPresence.otherCount,1);
  assert.equal(ownerPresence.players[0].id,'2'.repeat(32));
  assert.equal(ownerPresence.players[0].rodTier,7);
  const invalid=await send('/v1/homes/update',{...upload,ownerKey:'a'.repeat(64)});
  assert.equal(invalid.status,403);
  assert.equal((await send('/v1/state',{})).status,401);
});
