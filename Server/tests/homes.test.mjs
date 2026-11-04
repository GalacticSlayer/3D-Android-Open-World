import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {createHomes} from '../runtime/homes.mjs';
import {createSessionServer} from '../runtime/sessions.mjs';

const temp=()=>mkdtempSync(join(tmpdir(),'aquarium-homes-test-'));
const fish=(kg=12)=>({speciesId:8,biomeId:0,weightKg:kg});
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
  const created=await send('/v1/homes/register',{name:'Offline Fish'});
  assert.equal(created.ok,true);
  const tanks=[tank(),{...tank('aq2'),fish:Array(80).fill(fish(20))},
    {...tank('aq3'),fish:Array(80).fill(fish(25))}];
  const upload={code:created.code,ownerKey:created.ownerKey,name:'Offline Fish',visibility:'public',tanks};
  assert(JSON.stringify(upload).length>4096);
  assert.equal((await send('/v1/homes/update',upload)).ok,true);
  assert.equal((await send('/v1/homes/list',{})).total,1);
  assert.equal((await send('/v1/homes/get',{code:created.code})).tanks.length,3);
  const invalid=await send('/v1/homes/update',{...upload,ownerKey:'a'.repeat(64)});
  assert.equal(invalid.status,403);
  assert.equal((await send('/v1/state',{})).status,401);
});
