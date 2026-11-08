import { randomUUID } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync, renameSync } from 'node:fs';
import { tournamentIslands } from './tournament-species.mjs';

// Index families differ from tournament target grouping: rays/trevallies/barracudas
// are separate required species. Only Mako and Emperor alternate forms are variants.
const family = id => ({20:19,34:33})[id] ?? id;
export const requiredBossSpecies = [...new Set(tournamentIslands.filter(z=>[0,1,3,4].includes(z.id)).flatMap(z=>z.fish.map(f=>family(f.id))))];
export function createBosses({clock=Date.now, random=Math.random, ledgerPath=null}={}) {
  const receipts = ledgerPath && existsSync(ledgerPath) ? JSON.parse(readFileSync(ledgerPath,'utf8')) : {};
  const fail = message => {throw Object.assign(new Error(message),{status:409});};
  function save(){if(ledgerPath){writeFileSync(ledgerPath+'.tmp',JSON.stringify(receipts));renameSync(ledgerPath+'.tmp',ledgerPath);}}
  function release(b){b.owner='';b.lease='';b.unhookedAt=clock();b.updated=clock();}
  function tick(room){
    const b=room.boss;if(!b || b.phase!=='active')return;
    const now=clock();
    if(now>=b.ends){b.phase='expired';b.owner='';b.lease='';return;}
    if(b.owner && (now-b.heartbeat>=5000 || !room.players.has(b.owner))){
      // Start the five-second healing grace at the actual stale lease boundary.
      const lost=Math.min(now,b.heartbeat+5000);release(b);b.unhookedAt=lost;b.updated=lost;
    }
    if(!b.owner){const from=Math.max(b.updated,b.unhookedAt+5000);b.hp=Math.min(220000,b.hp+Math.max(0,now-from)*20000/60000);}
    b.updated=now;
  }
  function view(room){tick(room);const b=room.boss;if(!b)return null;
    return {id:b.id,phase:b.phase,owner:b.owner,lease:b.lease,hp:Math.ceil(b.hp),maxHp:220000,x:b.x,y:b.y,z:b.z,kg:b.kg,
      secondsLeft:Math.max(0,(b.ends-clock())/1000),serverTime:clock()};
  }
  function receipt(data){
    const ticket=data.ticket;if(typeof ticket!=='string'||!/^[a-f0-9]{32}$/.test(ticket))fail('Invalid summon ticket.');
    if(!receipts[ticket]){receipts[ticket]={accepted:false};save();}
    return {ok:true,accepted:receipts[ticket].accepted};
  }
  function action(p,c){
    if(!c || typeof c.op!=='string')fail('Invalid boss command.');tick(p.room);
    if(c.op==='summon'){
      if(typeof c.ticket!=='string'||!/^[a-f0-9]{32}$/.test(c.ticket))fail('Invalid summon ticket.');
      if(receipts[c.ticket]){if(!receipts[c.ticket].accepted)fail('Summon was cancelled.');return {accepted:true};}
      if(p.room.boss?.phase==='active')fail('The Leviathan is already awake.');
      if(p.pose.world!==0)fail('Return to the fishing islands.');
      const caught=new Set((Array.isArray(c.caught)?c.caught:[]).filter(Number.isInteger).map(family));
      if(!requiredBossSpecies.every(id=>caught.has(id)))fail('Catch every species on all four islands first.');
      if(!Number.isInteger(c.balance)||c.balance<100000)fail('Summoning costs 100,000 coins.');
      if(!['x','y','z'].every(k=>Number.isFinite(c[k])&&Math.abs(c[k])<100000))fail('Invalid Bluewater spawn.');
      // Geometry is authored in Unity; the summoner supplies the inspected south-rock point.
      const now=clock();const b={id:randomUUID(),phase:'active',x:c.x,y:c.y,z:c.z,kg:400+40*random(),hp:220000,
        ends:now+900000,owner:'',lease:'',unhookedAt:now,updated:now,attempts:new Map()};
      receipts[c.ticket]={accepted:true};try{save();}catch(e){delete receipts[c.ticket];throw e;}
      p.room.boss=b;return {accepted:true};
    }
    const b=p.room.boss;
    if(!b || c.id!==b.id)fail('This encounter has ended.');
    if(c.op==='claim'){
      if(b.phase!=='active')fail('The Leviathan has returned to the abyss.');
      if(!Number.isInteger(c.cast)||c.cast<1||c.cast!==p.pose.castSequence)fail('Invalid boss cast.');
      const key=p.id+':'+c.cast;
      if(b.attempts.has(key))return {hooked:b.owner===p.id && b.attempts.get(key)===b.lease};
      if(b.attempts.size>10000)fail('Too many casts in this encounter.');
      b.attempts.set(key,'miss');
      if(b.owner || p.pose.world!==0 || !p.pose.rodEquipped || p.pose.swimming || p.pose.lureVariant!==4 || p.pose.lineKind!==1 ||
        Math.hypot(p.pose.lineX-b.x,p.pose.lineZ-b.z)>80 || random()>=.5)return {hooked:false};
      b.owner=p.id;b.lease=randomUUID();b.damage=0;b.heartbeat=clock();b.attempts.set(key,b.lease);return {hooked:true};
    }
    // Catch retries are safe even after the encounter is closed.
    if(c.op==='catch'&&b.phase==='caught'&&b.catcher===p.id&&b.catchLease===c.lease)return {caught:true};
    if(b.phase!=='active'||b.owner!==p.id||b.lease!==c.lease)fail('The boss is no longer on your line.');
    if(!['damage','release','catch'].includes(c.op))fail('Invalid boss operation.');
    if(!Number.isInteger(c.damage)||c.damage<0||c.damage>220000)fail('Invalid boss damage.');
    // Cumulative damage per lease makes retries/out-of-order reports idempotent.
    if(c.damage>b.damage){b.hp=Math.max(0,b.hp-(c.damage-b.damage));b.damage=c.damage;}
    b.heartbeat=clock();
    if(c.op==='release')release(b);
    if(c.op==='catch'){
      if(b.hp>0)fail('Wear down the Leviathan before landing it.');
      b.phase='caught';b.catcher=p.id;b.catchLease=b.lease;b.owner='';b.lease='';return {caught:true};
    }
    return {};
  }
  function leave(p){const b=p.room.boss;if(b?.phase==='active'&&b.owner===p.id)release(b);}
  return {tick,view,action,receipt,leave};
}
