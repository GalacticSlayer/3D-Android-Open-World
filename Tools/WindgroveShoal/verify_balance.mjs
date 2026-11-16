// Run from the repo root: node Tools/WindgroveShoal/verify_balance.mjs
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {tournamentIslands} from '../../Server/runtime/tournament-species.mjs';

const read = path => readFileSync(path, 'utf8');
const csv = file => read('Assets/Resources/FishingTuning/'+file+'.csv')
    .trim().split(/\r?\n/).filter(line=>!line.startsWith('#')).map(line=>line.split(','));
const script = path => read('Assets/_Game/Scripts/'+path+'.cs');
const wind = 'windgrove-shoal';
const order = [51,50,49,48,47,45,54,53,52,44];
const withVariant = [...new Set([...order,46])];
const ids = new Set(withVariant);
const baits = ['worms','shrimp','squid','lure:0','lure:1','lure:2','lure:3','lure:4'];
const [chanceHeader,...chances] = csv('BiomeBaitSpeciesChance');
const col = new Map(chanceHeader.slice(2).map((name,i)=>[+name.split('_')[0],i+2]));
assert.equal(col.size,chanceHeader.length-2);
assert.equal(chances.length,54);
assert.equal(new Set(chances.map(r=>r.slice(0,2).join('/'))).size,chances.length);
const windOdds = new Map();
for (const row of chances) {
    assert.equal(row.length,chanceHeader.length);
    assert(row.slice(2).every(v=>/^\d+$/.test(v)));
    assert.equal(row.slice(2).reduce((total,v)=>total+ +v,0),100);
    for (const [id,i] of col) {
        if (row[0]===wind ? (!ids.has(id) || id===46) : ids.has(id)) assert.equal(+row[i],0);
    }
    if (row[0]===wind) windOdds.set(row[1],row);
}
assert.deepEqual([...windOdds.get('worms').slice(2)],[...windOdds.get('worms-legacy').slice(2)]);
assert.deepEqual([...windOdds.keys()].sort(),[...baits,'worms-legacy'].sort());
for (const id of order) assert(baits.some(b=>+windOdds.get(b)[col.get(id)]>0));

const [, ...stats] = csv('FishStats');
const [, ...weights] = csv('BiomeFishWeights');
const [, ...biomeStats] = csv('BiomeFishStats');
const catalog = script('Fishing/FishCatalog');
const index = script('ShopWorld/FishIndexCatalog');
const reef = script('Reef/ReefCatalog');
const savedId = name => Number(catalog.match(new RegExp('const int '+name+'=(\\d+)'))?.[1]);
const idsFrom = str => [...str.matchAll(/FishCatalog\.(\w+Id)/g)].map(m=>savedId(m[1]));
const indexTable = index.match(/RegionOrder\s*=\s*\{([\s\S]*?)\n\s*\};/)[1];
assert.deepEqual(idsFrom(indexTable),order);
assert.match(index,/new\[\] \{ FishCatalog\.MahiMahiId, FishCatalog\.BlueMahiMahiId \}/);
assert.match(catalog,/BlueMahiMahiVariantChance=\.10f/);
const fallbackIds = idsFrom(reef.match(/WindgroveFallbackIds\s*=\s*\{([\s\S]*?)\};/)[1]);
assert.deepEqual(fallbackIds,[...order].sort((a,b)=>a-b));
const fallbackOdds = [...reef.match(/WindgroveFallbackOdds\s*=\s*\{([\s\S]*?)\};/)[1].matchAll(/\{ ([\d, ]+) \}/g)]
    .map(m=>m[1].split(',').map(Number));
assert.deepEqual(fallbackOdds,baits.map(b=>fallbackIds.map(id=>+windOdds.get(b)[col.get(id)])));

const tournament = tournamentIslands.find(i=>i.id===5);
assert(tournament);
for (const id of withVariant) {
    const global = stats.find(r=>+r[0]===id);
    const ranges = weights.filter(r=>+r[1]===id);
    const local = ranges.find(r=>r[0]===wind);
    const fight = biomeStats.filter(r=>+r[1]===id);
    assert.equal(ranges.length,6);
    assert(ranges.every(r=>r[0]===wind || r[3]==='0' && r[4]==='0'));
    assert.equal(fight.length,1);
    assert.deepEqual(fight[0],[wind,String(id),global[1],...global.slice(4,8)]);
    assert(0 < +global[2] && +global[2] <= +local[3] && +local[3] < +local[4] && +local[4] <= +global[3]);
    assert(+global[4]>0 && +global[5]>=+global[4] && +global[6]>0 && +global[7]>=+global[6] && +global[8]>0);
    const line = catalog.split('\n').find(l=>l.includes('FishSpeciesDefinition("'+global[1]+'"'));
    const m = line.match(/,([\d.]+)f,([\d.]+)f,([\d.]+)f,0f,(\d+)\)/);
    assert(m,'missing catalog tuning for '+id);
    assert.deepEqual([+m[1],+m[2],+m[4]],[+global[2],+global[3],+global[6]]);
    const serverFish = tournament.fish.find(f=>f.id===id);
    assert.equal(serverFish.name,global[1]);
    assert(Math.abs(serverFish.maxKg - 1.5 * +local[4]) < 0.000001);
}

const shop = script('ShopWorld/ShopCatalog');
const gallery = shop.match(/HabitatDefinition\("grand", "Grand Ocean Gallery", \d+, (\d+), ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, ([\d.]+)f/);
assert(gallery);
const [slots, individual, total, width, , depth] = gallery.slice(1).map(Number);
assert.deepEqual([slots,individual,total],[40,250,1200]);
assert.ok(Math.cbrt(individual/7.46)<0.48*Math.min(width,depth),'250 kg Black Marlin must have room to turn');
const ledger = script('ShopWorld/ShopLedger');
assert.match(ledger,/if \(fish\.weightKg>d\.maxFishKg\)/);
assert.match(ledger,/if \(weight>d\.totalKg\+0\.0001f\)/);
assert.match(script('ShopWorld/ShopHabitat'),/MAX \{d\.maxFishKg:0\.##\} KG PER FISH/);
console.log('Windgrove catch odds, variants, Fish Index order, matching tuning, tournament caps and Grand Ocean Gallery limits: PASS');

