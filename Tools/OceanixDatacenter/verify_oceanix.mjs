// Run from repository root: node Tools/OceanixDatacenter/verify_oceanix.mjs
import assert from 'node:assert/strict';
import {existsSync, readFileSync} from 'node:fs';
import {tournamentIslands} from '../../Server/runtime/tournament-species.mjs';

const source = p => readFileSync(p, 'utf8');
const csv = name => source('Assets/Resources/FishingTuning/'+name+'.csv')
    .split(/\r?\n/).filter(l=>l && !l.startsWith('#')).map(l=>l.split(','));
const oceanix='oceanix-datacenter', id=55, origin=35;
const [statsHeader,...stats]=csv('FishStats');
const pick=id=>stats.find(row=>+row[0]===id);
const base=pick(origin),ultraviolet=pick(id);
assert(ultraviolet && base);
assert.deepEqual(ultraviolet.slice(2),base.slice(2),'UV fish physical/HP/speed/coin stats must equal Panther Grouper');

const [weightHeader,...weights]=csv('BiomeFishWeights');
const [chanceHeader,...chances]=csv('BiomeBaitSpeciesChance');
const [biomeHeader,...biomeStats]=csv('BiomeFishStats');
assert.deepEqual(weightHeader,['biomeId','speciesId','speciesName','minKg','maxKg']);
const speciesIds=stats.map(row=>+row[0]);
const biomes=new Set(weights.map(row=>row[0]));
assert.equal(biomes.size,7);
assert.equal(chances.length,7*9);
assert.equal(weights.length,7*speciesIds.length);
const byWeight=new Map(weights.map(row=>[row[0]+'|'+row[1],row]));
assert.equal(byWeight.size,weights.length);
for(const biome of biomes)for(const species of speciesIds)
    assert(byWeight.has(biome+'|'+species), 'missing weight row for '+biome+'/'+species);
for(const row of weights) {
    if(row[0]!==oceanix) assert(+row[1]!==id || row[3]==='0' && row[4]==='0');
    if(row[0]===oceanix) {
        if(+row[1]===id) assert.deepEqual(row.slice(3),['0.3','10']);
        else assert.deepEqual(row.slice(3),['0','0']);
    }
}
assert.equal(chanceHeader.at(-1),'55_ultraviolet_panther_grouper');
for(const row of chances) {
    assert.equal(row.length,chanceHeader.length);
    assert.equal(row.slice(2).reduce((total,n)=>total+Number(n),0),100);
    if(row[0]===oceanix) {
        assert.equal(+row.at(-1),100);
        assert(row.slice(2,-1).every(x=>x==='0'));
    }else assert.equal(+row.at(-1),0);
}
const local=biomeStats.find(row=>row[0]===oceanix && +row[1]===id);
const old=biomeStats.find(row=>row[0]==='suncrest-reef' && +row[1]===origin);
assert(local && old);assert.deepEqual(local.slice(3),old.slice(3));
assert.equal(biomeStats.filter(row=>row[0]===oceanix).length,1);

const scene=source('Assets/Scenes/PrototypeWorld.unity');
assert(scene.includes('value: Oceanix_Datacenter'));
assert(scene.includes('value: Grass_0410'));
assert(scene.includes('m_TransformParent: {fileID: 384836157}'));
assert(scene.includes('- {fileID: 1273346794}') && scene.includes('- {fileID: 1024244695}'));
const manifest=JSON.parse(source('Assets/_Game/EditableFishingMap/UserPlacedSceneryManifest.json'));
assert.equal(manifest.placedObjectCount,2);
assert.deepEqual(manifest.objects.map(o=>o.name).sort(),['Grass_0410','Oceanix_Datacenter']);

const zone=source('Assets/_Game/Scripts/Islands/OceanixDatacenterZone.cs');
assert.match(zone,/const int BiomeId = 6;/);
assert.match(zone,/const float Radius = 150f;/);
assert.match(zone,/Grass_0410/);
assert.match(zone,/67\.5f/);
const catalog=source('Assets/_Game/Scripts/Fishing/FishCatalog.cs');
assert.match(catalog,/const int UltravioletPantherGrouperId=55;/);
const visuals=source('Assets/_Game/Scripts/Fishing/FishVisualFactory.cs');
assert.match(visuals,/case FishCatalog\.UltravioletPantherGrouperId/);
assert(existsSync('Assets/_Game/Reef/Source/UltravioletPantherGrouper.fbx'));
assert(existsSync('Assets/_Game/Reef/Source/UltravioletPantherGrouperTexture.png'));
assert(existsSync('ArtSources/UltravioletPantherGrouper.blend'));
assert(existsSync('Assets/_Game/Editor/UltravioletPantherGrouperInstaller.cs'));

const server=tournamentIslands.find(item=>item.id===6);
assert(server && server.fish.length===1);
assert.equal(server.fish[0].id,id);
assert.equal(server.fish[0].maxKg,15);
console.log('Oceanix datacenter scene, protected markers, single-species bait tables, Panther stat parity, asset imports and multiplayer catch cap: PASS');
