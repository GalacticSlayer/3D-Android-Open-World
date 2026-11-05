import http from 'node:http';
import { createBosses } from './bosses.mjs';
import { createTournaments } from './tournaments.mjs';
import { boatPose, acceptBoatRide, pruneBoatRiders } from './boats.mjs';
import { randomBytes, randomInt } from 'node:crypto';
import path from 'node:path';
import os from 'node:os';
import fs from 'node:fs';
import { createHomes } from './homes.mjs';

export const PROTOCOL = 'fishing-walk-v1';
const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const playerName = value => value.replace(/[^a-zA-Z0-9 _-]/g, '').trim().slice(0,20) || 'Angler';
export function createSessionServer({ clock = Date.now, timeout = 30000, ledgerPath = null, homesDirectory = null, tournamentRandom, bossRandom } = {}) {
  const tournaments=createTournaments({clock,ledgerPath,random:tournamentRandom});
  const bosses=createBosses({clock,random:bossRandom,ledgerPath:ledgerPath?ledgerPath+'.bosses':null});
  // Directory persists separately from multiplayer rooms and tournament escrow.
  // Tests without a storage path receive an isolated temporary directory.
  const homeFolder=homesDirectory || (ledgerPath?path.join(path.dirname(ledgerPath),'homes'):
    fs.mkdtempSync(path.join(os.tmpdir(),'fishing-homes-')));
  let homes;
  try{homes=createHomes({directory:homeFolder,clock})}
  catch(e)
  {
    console.error('Home directory unavailable; preserving normal multiplayer service:',e.message);
    homes={dispatch:()=>fail(503,'Home directory temporarily unavailable.')};
  }
  const rooms = new Map(), sessions = new Map(), limits = new Map();
  const homeRegistrationLimits=new Map();
  const fail = (status, message) => { throw Object.assign(new Error(message), { status }); };
  const snapshot = room => [...room.players.values()].map(p => ({ id: p.id, name: p.name, ...p.pose,
    boat:clock()-p.seen>3000?null:p.pose.boat,
    lineKind:clock()-p.seen>1000?0:p.pose.lineKind,
    // Stale players retain their last event ID, but never replay the effect.
    castImpactAge:clock()-p.seen>1000?-1:p.pose.castImpactAge,
    boatInputX:clock()-p.seen>1000?0:p.pose.boatInputX,
    boatInputY:clock()-p.seen>1000?0:p.pose.boatInputY }));
  function remove(p) {
    tournaments.leave(p);bosses.leave(p);
    sessions.delete(p.token);
    p.room.players.delete(p.id);
    pruneBoatRiders(p.room);
    if (p.id === p.room.host) {
      for (const peer of p.room.players.values()) sessions.delete(peer.token);
      rooms.delete(p.room.code);
    }
  }
  function expire() {
    const now = clock();
    for(const room of rooms.values()){tournaments.tick(room);bosses.tick(room);}
    for (const p of sessions.values()) if (now - p.seen > timeout) remove(p);
    for (const [ip, limit] of limits) if (now - limit.start > 60000) limits.delete(ip);
    for (const [ip, limit] of homeRegistrationLimits)
      if(now-limit.start>3600000)homeRegistrationLimits.delete(ip);
  }
  const timer = setInterval(expire, 5000); timer.unref();
  function pose(value) {
    if (!value || !['x','y','z','yaw'].every(k => typeof value[k] === 'number' && Number.isFinite(value[k]))) fail(400, 'Invalid player position.');
    if (['x','y','z'].some(k => Math.abs(value[k]) > 100000) || ![0,1,2].includes(value.world)) fail(400, 'Invalid world position.');
    // Optional fields preserve compatibility with older walking-only clients.
    for (const [key, max] of [['rodTier',6],['reelTier',6],['castSequence',2147483647]])
      if (value[key] !== undefined && (!Number.isInteger(value[key]) || value[key] < 0 || value[key] > max)) fail(400, 'Invalid fishing animation state.');
    if (value.castAge !== undefined && (typeof value.castAge !== 'number' || !Number.isFinite(value.castAge) || value.castAge < -1 || value.castAge > 60)) fail(400, 'Invalid cast age.');
    if (value.castDuration !== undefined && (typeof value.castDuration !== 'number' || !Number.isFinite(value.castDuration) || value.castDuration < .1 || value.castDuration > 60)) fail(400, 'Invalid cast duration.');
    if (value.reelTime !== undefined && (typeof value.reelTime !== 'number' || !Number.isFinite(value.reelTime) || value.reelTime < 0 || value.reelTime > 10000000)) fail(400, 'Invalid reel animation time.');
    if (value.fishPosing !== undefined && typeof value.fishPosing !== 'boolean') fail(400, 'Invalid fish pose.');
    if (value.heldFishSpecies !== undefined && (!Number.isInteger(value.heldFishSpecies) || value.heldFishSpecies < -1 || value.heldFishSpecies > 4096)) fail(400, 'Invalid held fish.');
    if (value.heldFishWeight !== undefined && (typeof value.heldFishWeight !== 'number' || !Number.isFinite(value.heldFishWeight) || value.heldFishWeight < 0 || value.heldFishWeight > 1000000)) fail(400, 'Invalid held fish weight.');
    if (value.heldFishSpecies >= 0 && !(value.heldFishWeight > 0)) fail(400, 'Missing held fish weight.');
    if(value.islandMask!==undefined && (!Number.isInteger(value.islandMask)||value.islandMask<0||value.islandMask>31))fail(400,'Invalid island unlocks.');
    if(value.homeCode!==undefined && value.homeCode!==null && value.homeCode!=='' &&
       (typeof value.homeCode!=='string'||!/^[A-HJ-NP-Z2-9]{8}$/.test(value.homeCode)))
       fail(400,'Invalid Home visitor location.');
    const lineKind=value.lineKind??0, lureVariant=value.lureVariant??0;
    if(!Number.isInteger(lineKind)||lineKind<0||lineKind>3||!Number.isInteger(lureVariant)||lureVariant<0||lureVariant>4)
      fail(400,'Invalid fishing line state.');
    for(const key of ['lineX','lineY','lineZ'])
      if(value[key]!==undefined&&(typeof value[key]!=='number'||!Number.isFinite(value[key])||Math.abs(value[key])>100000))
        fail(400,'Invalid fishing line position.');
    if(lineKind && !['lineX','lineY','lineZ'].every(key=>typeof value[key]==='number'))fail(400,'Missing fishing line position.');
    // Presentation-only events. Never accept damage, health or arbitrary effect coordinates.
    if(value.lightningSerial!==undefined && (!Number.isInteger(value.lightningSerial)||value.lightningSerial<0||value.lightningSerial>2147483647))
      fail(400,'Invalid lightning event sequence.');
    if(value.lightningZap!==undefined && (!Number.isInteger(value.lightningZap)||value.lightningZap<0||value.lightningZap>7))
      fail(400,'Invalid lightning zap index.');
    // Water-impact events are cosmetic. Validate their location, age and ID
    // before allowing them into other players' snapshots.
    const impactSequence=value.castImpactSequence??0, impactAge=value.castImpactAge??-1;
    if(!Number.isInteger(impactSequence)||impactSequence<0||impactSequence>2147483647)
      fail(400,'Invalid cast impact sequence.');
    if(typeof impactAge!=='number'||!Number.isFinite(impactAge)||impactAge< -1||impactAge>60)
      fail(400,'Invalid cast impact age.');
    if(impactSequence && ['castImpactX','castImpactY','castImpactZ'].some(
      key=>typeof value[key]!=='number'||!Number.isFinite(value[key])||Math.abs(value[key])>100000))
      fail(400,'Invalid cast impact position.');
    if(!impactSequence && impactAge>=0)fail(400,'Invalid cast impact event.');
    const visibleLine=value.rodEquipped===true && value.swimming!==true && value.world===0 ? lineKind : 0;
    return { islandMask:(value.islandMask??1)|1, lineKind:visibleLine,lureVariant:visibleLine?lureVariant:0,
      lineX:visibleLine?value.lineX:0,lineY:visibleLine?value.lineY:0,lineZ:visibleLine?value.lineZ:0,
      // Keep the counter across casts/fights so observers see the first zap too.
      lightningSerial:value.world===0?(value.lightningSerial??0):0,
      lightningZap:value.world===0?(value.lightningZap??0):0,
      castImpactSequence:value.world===0?impactSequence:0,
      castImpactAge:value.world===0?impactAge:-1,
      castImpactX:value.world===0&&impactSequence?value.castImpactX:0,
      castImpactY:value.world===0&&impactSequence?value.castImpactY:0,
      castImpactZ:value.world===0&&impactSequence?value.castImpactZ:0,
      ...boatPose(value), x: value.x, y: value.y, z: value.z, yaw: ((value.yaw % 360) + 360) % 360,
      world: value.world, homeCode:value.world===2?(value.homeCode||''):'',
      swimming: value.swimming === true,
      sprinting: value.sprinting === true && value.swimming !== true,
      rodEquipped: value.rodEquipped === true, rodTier: value.rodTier ?? 0,
      reelTier: value.reelTier ?? 0,
      castSequence: value.castSequence ?? 0, castAge: value.castAge ?? -1,
      castDuration: value.castDuration ?? .72,
      reeling: value.reeling === true, hasFish: value.hasFish === true,
      lureInWater: value.lureInWater === true, reelTime: value.reelTime ?? 0,
      fishPosing: value.fishPosing === true && value.heldFishSpecies >= 0 && value.heldFishWeight > 0 &&
        value.rodEquipped !== true && value.swimming !== true && !(value.rideSeat > 0),
      heldFishSpecies: value.heldFishSpecies ?? -1, heldFishWeight: value.heldFishWeight ?? 0 };
  }
  function add(room, data) {
    const p = { id: randomBytes(8).toString('hex'), token: randomBytes(32).toString('hex'),
      name: typeof data.name === 'string' ? playerName(data.name) : 'Angler',
      pose: pose(data.pose), seen: clock(), room };
    room.players.set(p.id, p); sessions.set(p.token, p);
    acceptBoatRide(p,null);
    if (!room.host) room.host = p.id;
    return p;
  }
  function dispatch(path, data, token) {
    if(path.startsWith('/v1/homes/'))return homes.dispatch(path,data);
    expire();
    if(path==='/v1/boss-receipt')return bosses.receipt(data);
    if(path==='/v1/tournament-receipt')return {ok:true,...tournaments.receipt(data)};
    if (path === '/v1/open' || path === '/v1/join') {
      if (data.protocol !== PROTOCOL || typeof data.build !== 'string' || data.build.length > 100) fail(409, 'Install the same updated game version on both devices.');
      if (sessions.size >= 32) fail(503, 'Server is full. Try again later.');
      pose(data.pose); // Validate before allocating a room.
      let room;
      if (path === '/v1/open') {
        if (rooms.size >= 8) fail(503, 'All rooms are in use. Try again later.');
        let code;
        do { code = Array.from({length:4}, () => alphabet[randomInt(alphabet.length)]).join(''); } while (rooms.has(code));
        room = { code, build: data.build, players: new Map(), host: null };
        rooms.set(code, room);
      } else {
        room = rooms.get(typeof data.code === 'string' ? data.code.trim().toUpperCase() : '');
        if (!room) fail(404, 'Room not found. Ask the host for the current code.');
        if (room.build !== data.build) fail(409, 'Both players need the same game version.');
        if (room.players.size >= 8) fail(409, 'This room is full (8 players).');
      }
      const p = add(room, data);
      return { ok:true, id:p.id, token:p.token, code:room.code, hostId:room.host, players:snapshot(room),tournament:tournaments.view(room),boss:bosses.view(room),bossReady:true,maxRodTier:6 };
    }
    const p = sessions.get(token);
    if (!p) fail(401, 'Session ended. The host may have left. Open or join again.');
    if (path === '/v1/leave') { remove(p); return {ok:true}; }
    if (path === '/v1/tournament-catches') return tournaments.catches(p,data);
    if (path !== '/v1/state' && path !== '/v1/tournament' && path !== '/v1/boss') fail(404, 'Not found.');
    const previous=p.pose;
    p.pose = pose(data.pose); p.seen = clock();
    if(typeof data.name==='string') {
      p.name=playerName(data.name);
      // Keep active tournament entries tied to the same identity and score.
      const tournament=p.room.tournament;
      if(tournament && ['inviting','countdown','running'].includes(tournament.phase)) {
        const entry=tournament.entries.find(e=>e.id===p.id);
        if(entry)entry.name=p.name;
      }
    }
    acceptBoatRide(p,previous); pruneBoatRiders(p.room);
    if(path==='/v1/tournament')tournaments.action(p,data.tournament);
    const bossResult=path==='/v1/boss'?bosses.action(p,data.boss):{};
    return { ok:true, code:p.room.code, hostId:p.room.host, players:snapshot(p.room),tournament:tournaments.view(p.room),boss:bosses.view(p.room),bossReady:true,maxRodTier:6,...bossResult };
  }
  const server = http.createServer({ requestTimeout:5000, headersTimeout:5000, maxHeaderSize:8192 }, async (req, res) => {
    const reply = (status, value) => { if (!res.destroyed) { res.writeHead(status, {'Content-Type':'application/json','Cache-Control':'no-store'}); res.end(JSON.stringify(value)); } };
    const ip = req.socket.remoteAddress;
    let limit = limits.get(ip);
    if (!limit || clock() - limit.start > 1000) {
      if (!limit && limits.size >= 4096) { reply(503,{ok:false,error:'Busy. Try again.'}); req.resume(); return; }
      limit = { start:clock(), count:0, opens:0 }; limits.set(ip,limit);
    }
    if (++limit.count > 100) { reply(429,{ok:false,error:'Too many requests.'});req.resume();return; }
    // Explicit capability marker lets Android clients and developers detect
    // an Oracle server still running an old Home presence release.
    if (req.method === 'GET' && req.url === '/health') {
      reply(200,{ok:true,protocol:PROTOCOL,homePresenceProtocol:4,homeUploadDiagnostics:true,unityNullPondCompatible:true});
      return;
    }
    if (req.method !== 'POST' || !['/v1/open','/v1/join','/v1/state','/v1/leave','/v1/tournament','/v1/tournament-receipt','/v1/tournament-catches','/v1/boss','/v1/boss-receipt',
      '/v1/homes/register','/v1/homes/resolve-owner','/v1/homes/update','/v1/homes/list','/v1/homes/get','/v1/homes/check','/v1/homes/presence'].includes(req.url)) {reply(404,{ok:false,error:'Not found.'});req.resume();return;}
    if ((req.url === '/v1/open' || req.url === '/v1/join') && ++limit.opens > 4) {reply(429,{ok:false,error:'Wait a moment before trying again.'});req.resume();return;}
    if(req.url==='/v1/homes/register')
    {
      let registration=homeRegistrationLimits.get(ip);
      if(!registration || clock()-registration.start>3600000)
      {
        registration={start:clock(),count:0};
        homeRegistrationLimits.set(ip,registration);
      }
      if(++registration.count>4)
      {reply(429,{ok:false,error:'Too many Home registrations from this connection.'});req.resume();return;}
    }
    if (!(req.headers['content-type'] || '').startsWith('application/json')) {reply(415,{ok:false,error:'JSON required.'});req.resume();return;}
    // Minimal owner-upload diagnostics. Never log ownerKey, fish lists, names
    // or full request bodies. These messages allow a stale published Home to
    // be investigated from server logs without rebuilding an Android APK.
    let homeUploadCode='unknown';
    try {
      let size=0; const chunks=[];
      // Only Home layout uploads need a larger limit; 10 Hz movement stays at 4 KB.
      const maxBytes=req.url==='/v1/homes/update'?262144:4096;
      for await (const chunk of req) { size+=chunk.length; if(size>maxBytes){fail(413,'Request too large.');} chunks.push(chunk); }
      let data;
      try {data=JSON.parse(Buffer.concat(chunks).toString('utf8'));} catch {fail(400,'Invalid JSON.');}
      if (!data || typeof data !== 'object' || Array.isArray(data)) fail(400,'Invalid request.');
      if(req.url==='/v1/homes/update' && typeof data.code==='string' &&
         /^[A-HJ-NP-Z2-9]{8}$/.test(data.code))
        homeUploadCode=data.code;
      const outcome=dispatch(req.url,data,(req.headers.authorization || '').replace(/^Bearer /,''));
      if(req.url==='/v1/homes/update')
        console.info('[HOME UPLOAD ACCEPTED]',homeUploadCode,'version',
          outcome.version,'aquariums',Array.isArray(data.tanks)?data.tanks.length:'invalid');
      reply(200,outcome);
    } catch (e) {
      if(req.url==='/v1/homes/update')
        console.warn('[HOME UPLOAD REJECTED]',homeUploadCode,
          'HTTP',e.status || 500,'reason',e.status ? e.message : 'Server error');
      reply(e.status || 500,{ok:false,error:e.status ? e.message : 'Server error.'});
    }
  });
  server.maxConnections=128;
  server.keepAliveTimeout=5000;
  server.on('close',()=>clearInterval(timer));
  return server;
}


