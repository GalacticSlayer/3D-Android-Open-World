import fs from 'node:fs';
import path from 'node:path';
import { randomBytes, createHash, timingSafeEqual } from 'node:crypto';

// Independent of room membership: a compact, persistent read-only Home showcase.
// Files use atomic replacement so interrupted writes cannot truncate a Home.
const alphabet='ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const codePattern=/^[A-HJ-NP-Z2-9]{8}$/;
const choices=new Set(['public','unlisted','private']);
const aquariumTypes=new Set(['nano','reef','lagoon','grand','ocean']);
const pondTypes=new Set(['pond','garden','lake']);
const nameOf=s=>typeof s==='string'?(s.replace(/[^a-zA-Z0-9 _-]/g,'').trim().slice(0,20)||'Angler'):'Angler';
const bad=(status,error)=>{throw Object.assign(new Error(error),{status})};
const hash=s=>createHash('sha256').update(s).digest();
const randomCode=()=>Array.from({length:8},()=>alphabet[randomBytes(1)[0]%alphabet.length]).join('');
const copy=x=>JSON.parse(JSON.stringify(x));
const finite=(n,lower,upper)=>typeof n==='number'&&Number.isFinite(n)&&n>=lower&&n<=upper;

export function createHomes({directory,clock=Date.now}={}){
  if(!directory)throw new Error('Home data directory is required');
  fs.mkdirSync(directory,{recursive:true,mode:0o700});
  const homes=new Map();
  // Server-owned index of hashed credentials, never exposed in public Homes.
  // A client with a surviving owner secret can recover its correct Home code.
  const ownerHomeByHash=new Map();
  // Transient presence is memory-only: no avatar or fish simulation goes to
  // disk, and leaving one Home does not affect other Homes or fishing rooms.
  const visitors=new Map();
  const presenceTtlMs=9000; // Mobile network pauses should not hide idle avatars.
  let lastPresenceSweep=0;
  for(const file of fs.readdirSync(directory)){
    if(!/^[A-HJ-NP-Z2-9]{8}\.json$/.test(file))continue;
    try{
      const h=JSON.parse(fs.readFileSync(path.join(directory,file),'utf8'));
      if(h.code===file.slice(0,-5)&&typeof h.ownerHash==='string'&&
         choices.has(h.visibility)&&Array.isArray(h.tanks))
      {
        homes.set(h.code,summary(h));
        ownerHomeByHash.set(h.ownerHash,h.code);
      }
    }catch(e){console.error('Ignoring unreadable Home snapshot',file,e.message)}
  }
  function read(code){return JSON.parse(fs.readFileSync(path.join(directory,code+'.json'),'utf8'))}
  function save(h){
    const dest=path.join(directory,h.code+'.json');
    const temp=dest+'.'+randomBytes(6).toString('hex')+'.tmp';
    try{fs.writeFileSync(temp,JSON.stringify(h),{mode:0o600,flag:'wx'});fs.renameSync(temp,dest)}
    finally{try{fs.unlinkSync(temp)}catch{}}
  }
  function owner(h,ownerKey){
    if(typeof ownerKey!=='string'||!/^[a-f0-9]{64}$/.test(ownerKey))return false;
    const given=hash(ownerKey),expected=Buffer.from(h.ownerHash,'hex');
    return expected.length===given.length&&timingSafeEqual(given,expected);
  }
  function getRecord(value){
    if(!value||typeof value!=='object')bad(400,'Invalid fish record.');
    if(!Number.isInteger(value.speciesId)||value.speciesId<0||value.speciesId>511||
       !finite(value.weightKg,0.001,1500)||
       (value.biomeId!=null &&
        (typeof value.biomeId!=='string'||value.biomeId.length>96||
         /[\x00-\x1f\x7f]/.test(value.biomeId))))
       bad(400,'Invalid fish record.');
    return {speciesId:value.speciesId,weightKg:Math.round(value.weightKg*1000)/1000,biomeId:value.biomeId};
  }
  function validateTanks(value){
    if(!Array.isArray(value)||value.length>128)bad(400,'Invalid aquarium count.');
    let total=0;const ids=new Set();
    return value.map(t=>{
      if(!t||typeof t!=='object'||typeof t.key!=='string'||!/^aq[1-9][0-9]{0,9}$/.test(t.key)||
        ids.has(t.key)||!aquariumTypes.has(t.id)|| !finite(t.x,-150,150)||
        !finite(t.y,-10,10)||!finite(t.z,-150,150)||!finite(t.yaw,-720,720)||
        !Array.isArray(t.fish)||t.fish.length>80)bad(400,'Invalid aquarium layout.');
      ids.add(t.key);
      total+=t.fish.length;
      if(total>4000)bad(400,'Home contains too many fish.');
      return {key:t.key,id:t.id,x:t.x,y:t.y,z:t.z,yaw:t.yaw,fish:t.fish.map(getRecord)};
    });
  }
  function validatePond(value){
    if(value==null)return null; // Old snapshots and Homes without a pond.
    // Unity JsonUtility serializes a null inline [Serializable] TankInfo
    // as an object with default fields instead of JSON null. A player who
    // has NOT purchased a pond therefore sends {id:"",fish:[]} (or {}).
    // Previously this rejected the ENTIRE Home update, freezing aquarium
    // positions and resident fish even when the tanks themselves were valid.
    // Only tolerate an entirely empty/default pond placeholder; a real
    // pond with an invalid tier, fish list, or location still fails closed.
    if(typeof value==='object' && !Array.isArray(value) &&
       (value.id==null || value.id==='') &&
       (value.key==null || value.key==='') &&
       (value.fish==null || Array.isArray(value.fish)&&value.fish.length===0) &&
       ['x','y','z','yaw'].every(k=>value[k]==null || value[k]===0) &&
       Object.keys(value).every(k=>['id','key','fish','x','y','z','yaw'].includes(k)))
      return null;
    if(typeof value!=='object'||Array.isArray(value)||
       !pondTypes.has(value.id)||!Array.isArray(value.fish)||
       value.fish.length>50)
       bad(400,'Invalid Home pond.');
    return {id:value.id,fish:value.fish.map(getRecord)};
  }
  // Store only compact summaries in memory. Full fish lists remain on disk and
  // are loaded ONLY for individual Home requests and owner updates.
  function publicInfo(h){
    if(!Array.isArray(h.tanks))
      return {code:h.code,name:h.name,visibility:h.visibility,updatedAt:h.updatedAt,
        version:h.version,aquariums:h.aquariums,fish:h.fish,biggest:h.biggest};
    const pondFish=h.pond?.fish??[];
    const fish=h.tanks.reduce((n,t)=>n+t.fish.length,pondFish.length);
    let biggest=null;
    for(const f of [...h.tanks.flatMap(t=>t.fish),...pondFish])
      if(biggest===null||f.weightKg>biggest.weightKg)
        biggest={speciesId:f.speciesId,weightKg:f.weightKg};
    return {code:h.code,name:h.name,visibility:h.visibility,updatedAt:h.updatedAt,
      version:h.version,aquariums:h.tanks.length,fish,biggest};
  }
  function summary(h){
    return {ownerHash:h.ownerHash,...publicInfo(h)};
  }
  function register(data){
    const name=nameOf(data?.name);
    if(homes.size>=10000)bad(503,'The public Home directory is currently at capacity.');
    let code;
    do{code=randomCode()}while(homes.has(code));
    const ownerKey=randomBytes(32).toString('hex');
    const h={code,ownerHash:hash(ownerKey).toString('hex'),name,visibility:'public',
      updatedAt:clock(),version:0,tanks:[],pond:null};
    save(h);homes.set(code,summary(h));
    ownerHomeByHash.set(h.ownerHash,code);
    return {ok:true,code,ownerKey,visibility:'public',version:0};
  }
  function resolveOwner(data){
    const credential=data?.ownerKey;
    if(typeof credential!=='string'||!/^[a-f0-9]{64}$/.test(credential))
      bad(403,'Home owner credential is missing or invalid.');
    const code=ownerHomeByHash.get(hash(credential).toString('hex'));
    if(!code||!homes.has(code))bad(404,'No published Home matches this owner credential.');
    return {ok:true,code,version:homes.get(code).version};
  }
  function update(data){
    const h=homes.get(data?.code);
    if(!h||!owner(h,data.ownerKey))bad(403,'Home ownership could not be verified.');
    if(!choices.has(data.visibility))bad(400,'Invalid Home privacy.');
    const tanks=validateTanks(data.tanks);
    const currentRecord=read(h.code);
    // Older Android clients only know aquariums. Their updates should not
    // silently wipe an already-published pond from a newer client.
    const pond=data.pond===undefined
      ?currentRecord.pond??null:validatePond(data.pond);
    const name=nameOf(data.name);
    // Idempotent when a client retries a timed-out upload.
    const next=JSON.stringify({tanks,pond,name,visibility:data.visibility});
    const current=JSON.stringify({tanks:currentRecord.tanks,pond:currentRecord.pond??null,
      name:h.name,visibility:h.visibility});
    if(next===current)return {ok:true,version:h.version,updatedAt:h.updatedAt};
    const changed={...currentRecord,tanks,pond,name,visibility:data.visibility,
      version:h.version+1,updatedAt:clock()};
    save(changed);homes.set(h.code,summary(changed));
    return {ok:true,version:changed.version,updatedAt:changed.updatedAt};
  }
  function list(data){
    const q=typeof data?.query==='string'?data.query.trim().toLowerCase().slice(0,40):'';
    const page=Number.isInteger(data?.page)?Math.max(0,Math.min(10000,data.page)):0;
    const sort=data?.sort==='fish'?'fish':data?.sort==='biggest'?'biggest':'recent';
    // Cosmetic self-exclusion happens before pagination so pages remain full and
    // totals reflect only other players' public aquariums.
    const exclude=typeof data?.excludeCode==='string'&&codePattern.test(data.excludeCode)
      ?data.excludeCode:'';
    const entries=[...homes.values()].filter(h=>h.visibility==='public'&&h.aquariums>0&&
      h.code!==exclude&&
      (!q||h.name.toLowerCase().includes(q)||h.code.toLowerCase().includes(q)))
      .map(publicInfo);
    entries.sort((a,b)=>sort==='fish'?b.fish-a.fish||b.updatedAt-a.updatedAt:
      sort==='biggest'?(b.biggest?.weightKg||0)-(a.biggest?.weightKg||0)||b.updatedAt-a.updatedAt:
      b.updatedAt-a.updatedAt||a.code.localeCompare(b.code));
    return {ok:true,homes:entries.slice(page*20,(page+1)*20),page,total:entries.length,more:(page+1)*20<entries.length};
  }
  function get(data,versionOnly=false){
    const code=typeof data?.code==='string'?data.code.replace(/[- ]/g,'').toUpperCase():'';
    if(!codePattern.test(code))bad(400,'Enter a valid eight-character Home code.');
    const h=homes.get(code);
    if(!h||h.visibility==='private'&&!owner(h,data.ownerKey))
      bad(404,'Home not found or private.');
    const info=publicInfo(h);
    if(versionOnly)return {ok:true,...info};
    const snapshot=read(h.code);
    return {ok:true,...info,tanks:copy(snapshot.tanks),pond:copy(snapshot.pond??null)};
  }
  function presence(data){
    const code=typeof data?.code==='string'?data.code.toUpperCase():'';
    if(!codePattern.test(code))bad(400,'Invalid Home code.');
    const home=homes.get(code);
    if(!home)bad(404,'Home not found or private.');

    const session=data?.session;
    if(typeof session!=='string' || !/^[a-f0-9]{32}$/.test(session))
      bad(400,'Invalid Home presence session.');
    const now=clock();
    // Remove abandoned rooms globally without a permanent timer. One sweep
    // every ten seconds keeps RAM bounded even for heavily browsed Homes.
    if(now-lastPresenceSweep>=10000)
    {
      lastPresenceSweep=now;
      for(const [roomCode,active] of visitors)
      {
        for(const [key,peer] of active)
          if(now-peer.seen>presenceTtlMs)active.delete(key);
        if(active.size===0)visitors.delete(roomCode);
      }
    }
    let room=visitors.get(code);
    if(!room){room=new Map();visitors.set(code,room);}
    for(const [key,peer] of room)
      if(now-peer.seen>presenceTtlMs)room.delete(key);
    // Authenticate the owner's first request, then cache that identity
    // only for this short-lived, random session. No ownership token is sent
    // with every 4 Hz movement packet.
    const previous=room.get(session);
    const authorized=previous?.authorized===true || owner(home,data.ownerKey);
    if(home.visibility==='private' && !authorized)
      bad(404,'Home not found or private.');
    if(data.leave===true)
    {
      room.delete(session);
    }
    else
    {
      const p=data.pose;
      if(!p || p.world!==2 ||
         ![p.x,p.y,p.z,p.yaw].every(n=>finite(n,-100000,100000)) ||
         (p.heldFishSpecies!==undefined &&
           (!Number.isInteger(p.heldFishSpecies) ||
             p.heldFishSpecies < -1 || p.heldFishSpecies > 511)) ||
         (p.heldFishWeight!==undefined &&
           !finite(p.heldFishWeight,0,1500)))
        bad(400,'Invalid Home player pose.');
      if(!room.has(session) && room.size>=12)
        bad(429,'This Home has reached its live visitor limit.');
      // Live drags and successful placements are separate. A completed
      // placement stays visually pinned until the persistent Home revision
      // arrives; canceled drags have no completed placement to publish.
      function aquariumPosition(value,label){
        if(value==null)return null;
        // Unity's JsonUtility may encode a null inline [Serializable]
        // AquariumMove as {} or its default-valued fields. The owner sends
        // both optional move and placed on *every* presence heartbeat.
        // Rejecting an empty placeholder here used to reject the entire
        // authenticated owner's heartbeat, while visitor heartbeats worked.
        // Exactly the same serialization issue previously blocked pond saves.
        if(typeof value==='object' && !Array.isArray(value) &&
           (value.key==null || value.key==='') &&
           ['x','y','z','yaw'].every(k=>value[k]==null || value[k]===0) &&
           Object.keys(value).every(k=>['key','x','y','z','yaw'].includes(k)))
          return null;
        if(!value || typeof value!=='object' || Array.isArray(value) ||
           typeof value.key!=='string' ||
           !/^aq[1-9][0-9]{0,9}$/.test(value.key) ||
           ![value.x,value.y,value.z].every(n=>finite(n,-150,150)) ||
           !finite(value.yaw,-720,720))
          bad(400,'Invalid '+label+'.');
        return {key:value.key,x:value.x,y:value.y,z:value.z,yaw:value.yaw};
      }
      const moving=authorized?aquariumPosition(data.move,'live aquarium preview'):null;
      const placed=authorized?aquariumPosition(data.placed,'completed aquarium placement'):null;
      room.set(session,{id:session,name:nameOf(data.name),
        seen:now,authorized,move:moving,placed,pose:{
        x:p.x,y:p.y,z:p.z,yaw:p.yaw,world:2,homeCode:code,
        swimming:p.swimming===true,sprinting:p.sprinting===true,
        rodEquipped:p.rodEquipped===true,reeling:p.reeling===true,
        hasFish:p.hasFish===true,lureInWater:p.lureInWater===true,
        heldFishSpecies:p.heldFishSpecies??-1,
        heldFishWeight:p.heldFishWeight??0,
        fishPosing:p.fishPosing===true,
        rodTier:Number.isInteger(p.rodTier)?Math.max(0,Math.min(7,p.rodTier)):0,
        reelTier:Number.isInteger(p.reelTier)?Math.max(0,Math.min(7,p.reelTier)):0,
        reelTime:finite(p.reelTime,0,10000000)?p.reelTime:0,
        castSequence:Number.isInteger(p.castSequence)&&p.castSequence>=0
          ?p.castSequence:0
      }});
    }
    // Return exactly the same peer structure as /v1/state. Keeping the
    // single owner's temporary tank drag separate guarantees idle players
    // always serialize as complete avatars, even when nothing is being moved.
    const players=[];
    let moving=null,placed=null;
    for(const [id,peer] of room)
    {
      if(peer.authorized)
      {
        if(peer.move!=null)moving=peer.move;
        if(peer.placed!=null)placed=peer.placed;
      }
      if(id!==session)players.push({id,name:peer.name,...peer.pose});
    }
    if(room.size===0)visitors.delete(code);
    // Merge read-only Home layout/fish updates into the same reliable heartbeat
    // that discovers other players. Never resend a large snapshot if its
    // revision has not changed; fish movement/physics are still client-sided.
    let snapshot=null;
    if(data.wantSnapshot===true)
    {
      const known=Number.isInteger(data.knownVersion)?data.knownVersion:-1;
      if(known!==home.version)
      {
        const record=read(code);
        snapshot={ok:true,...publicInfo(home),tanks:copy(record.tanks),
          pond:copy(record.pond??null)};
      }
    }
    return {ok:true,code,version:home.version,owner:authorized,
      otherCount:players.length,players,moving,placed,snapshot};
  }

  return {dispatch(route,data){
    if(route==='/v1/homes/presence')return presence(data);
    if(route==='/v1/homes/register')return register(data);
    if(route==='/v1/homes/resolve-owner')return resolveOwner(data);
    if(route==='/v1/homes/update')return update(data);
    if(route==='/v1/homes/list')return list(data);
    if(route==='/v1/homes/get')return get(data,false);
    if(route==='/v1/homes/check')return get(data,true);
    bad(404,'Not found.');
  },count:()=>homes.size};
}
