import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {tournamentSpeciesId,tournamentIslands} from '../../Server/runtime/tournament-species.mjs';
const csv=n=>readFileSync('Assets/Resources/FishingTuning/'+n+'.csv','utf8')
 .trim().split(/\r?\n/).filter(x=>x&&!x.startsWith('#')).map(x=>x.split(','));
const [, ...stats]=csv('FishStats'),[,...weights]=csv('BiomeFishWeights'),
      [header,...odds]=csv('BiomeBaitSpeciesChance'),[,...biomeStats]=csv('BiomeFishStats');
const fish=new Map(stats.map(x=>[+x[0],x]));
assert.equal(stats.length,67-1); // Active IDs 0..67, excluding retired ID4 and boss ID43.
assert.equal(weights.length,7*stats.length);
assert.equal(odds.length,63);
for(const [id,base] of [[55,35],[56,32],[57,23],[58,36],[59,36],[60,21],[61,15],[62,19],[63,60],[64,56],[65,58],[66,55],[67,57]]){
 const a=fish.get(id),b=fish.get(base);assert(a&&b);
 assert.deepEqual(a.slice(2,4),b.slice(2,4));
 assert.equal(a[8],b[8]);
 if(id===59){assert.equal(+a[4],+b[4]*50);assert.equal(+a[5],+b[5]*50);}
 else if(id>=60 && id<=62){assert.equal(+a[4],+b[4]*40);assert.equal(+a[5],+b[5]*40);assert.deepEqual(a.slice(6),b.slice(6));}
 else if(id>=63)assert.deepEqual(a.slice(4),b.slice(4));
 else assert.deepEqual(a.slice(4),b.slice(4));
 for(const biome of ['suncrest-reef','brinebreak-isle','deep-ocean','bluewater-cay','snapper-island','windgrove-shoal','oceanix-datacenter']){
  const w=weights.find(x=>x[0]===biome&&+x[1]===id);assert(w);
  if(biome==='oceanix-datacenter')assert.deepEqual(w.slice(3),a.slice(2,4));
  else assert.deepEqual(w.slice(3),['0','0']);
 }
}
for(const row of odds){
 assert.equal(row.length,header.length);
 assert.equal(row.slice(2).reduce((s,n)=>s+(+n),0),100);
 const chance=id=>+row[header.findIndex(h=>h.startsWith(id+'_'))];
 if(row[0]!=='oceanix-datacenter'){
  for(const id of [55,56,57,58,59,60,61,62,63,64,65,66,67])assert.equal(chance(id),0);
 }else{
  assert.equal(chance(59),0); // Variant is selected after its family bite.
  if(row[1]==='worms'||row[1]==='worms-legacy')assert.equal(chance(58),100);
  if(row[1]==='shrimp')for(const id of [64,65,66,67])assert.equal(chance(id),25);
  if(row[1]==='squid'){
   for(const id of [63,66,61,62])assert.equal(chance(id),25);
   for(const id of [55,56,57,58,59,60,64,65,67])assert.equal(chance(id),0);
  }
  if(row[1].startsWith('lure:'))assert.equal(chance(66),100);
  if(row[1]!=='squid')for(const id of [60,61,62])assert.equal(chance(id),0);
 }
}
for(const id of [55,56,57,58,59,60,61,62,63,64,65,66,67])assert(biomeStats.some(x=>x[0]==='oceanix-datacenter'&&+x[1]===id));
assert.equal(tournamentSpeciesId(59),65);
assert(tournamentIslands.find(x=>x.id===6).fish.some(x=>x.id===59));
console.log('Oceanix species and variant tuning: PASS');

// Scene and preview regressions: keep family portrait and teleport coordinates
// independent from the removed temporary Grass_0410 mesh.
const indexView=readFileSync('Assets/_Game/Scripts/ShopWorld/FishIndexView.cs','utf8');
assert(indexView.includes('snapshots.Attach(picture,family,PreviewWeight(family),null,true,true,true);'));
assert(!indexView.includes('Mahi Mahi bites are 90%'));
assert(!indexView.includes('Shrimp catches Magma Volt; worms catch Cybernetic'));
const scenery=JSON.parse(readFileSync('Assets/_Game/EditableFishingMap/UserPlacedSceneryManifest.json','utf8'));
assert.deepEqual(scenery.objects.map(x=>x.name),['Oceanix_Datacenter']);
const gameScene=readFileSync('Assets/Scenes/PrototypeWorld.unity','utf8');
assert(!gameScene.includes('Grass_0410') && !gameScene.includes('1024244694') && !gameScene.includes('1024244695'));
assert(gameScene.includes('value: Oceanix_Datacenter'));
const teleport=readFileSync('Assets/_Game/Scripts/Islands/OceanixDatacenterZone.cs','utf8');
assert(teleport.includes('new Vector3(-249.255f, 5.647f, -3073.303f)'));
assert(!teleport.includes('arrival.position'));
console.log('Oceanix index portrait, teleport and scenery cleanup: PASS');

// New squid species must have independent saved IDs, models, and Oceanix caps.
const fishIndex=readFileSync('Assets/_Game/Scripts/ShopWorld/FishIndexCatalog.cs','utf8');
const visualFactory=readFileSync('Assets/_Game/Scripts/Fishing/FishVisualFactory.cs','utf8');
for(const [id,name] of [[60,'AureliaMangroveSnapper'],[61,'ChronoReefShark'],[62,'CrimsonCybrosMakoShark']]){
 assert(fishIndex.includes('FishCatalog.'+name+'Id'));
 assert(visualFactory.includes('Fishing/'+name));
 assert(tournamentIslands.find(x=>x.id===6).fish.some(x=>x.id===id));
}
console.log('Oceanix squid-only fish, region validation and 40x health: PASS');

// Auto-import checks: Unity must import the originals straight from Downloads.
const squidImporter=readFileSync('Assets/_Game/Editor/OceanixSquidFishInstaller.cs','utf8');
for(const name of ['AureliaMangroveSnapper','ChronoReefShark','CrimsonCybrosMakoShark'])
 assert(squidImporter.includes('"'+name+'"'));
for(const original of [
 'Aurelia Mangrove Snapper.fbx','Aurelia Mangrove Snapper Texture.png',
 'Chrono Reef Shark.fbx','Chrono Reef Shark Texture.png',
 'Crimson Cybros Mako shark.fbx','Crimson Cybros Mako Shark Texture.png'])
 assert(squidImporter.includes('"'+original+'"'));
assert(squidImporter.includes('swimTakeContains:"ArmatureAction"'));
assert(squidImporter.includes('OnPreprocessBuild(BuildReport report)'));
assert(squidImporter.includes('File.Copy(source,destination,false)'));
console.log('Oceanix downloaded FBX/PNG auto-import and swim-take wiring: PASS');

// Chrono/Crimson sharks export Bone.004.R, while Aurelia uses Bone.004.
// The naming fallback is opt-in so older fish imports remain unchanged.
const fishImporter=readFileSync('Assets/_Game/Editor/SeaBassImporter.cs','utf8');
assert(fishImporter.includes('bool allowRightSuffixedRig=false'));
assert(fishImporter.includes('t.name=="Bone.004"'));
assert(fishImporter.includes('t.name=="Bone.004.R"'));
assert(fishImporter.includes('if(tail==null && allowRightSuffixedRig)'));
assert(squidImporter.includes('allowRightSuffixedRig:true'));
console.log('Oceanix squid fish rig compatibility: standard and .R bone chains PASS');

// Crimson also has unsuffixed fin bones; use the whole .R spine in the
// presentation controller to prevent aquarium turns from distorting it.
const snapperPresentation=readFileSync('Assets/_Game/Scripts/Fishing/RedSnapperPresentation.cs','utf8');
assert(snapperPresentation.includes('FindBone(bones, "Bone.004.R") != null'));
assert(snapperPresentation.includes('string suffix = rightSpine ? ".R" : string.Empty;'));
for(const name of ['Bone.001','Bone.002','Bone.003','Bone.004'])
 assert(snapperPresentation.includes('"'+name+'" + suffix'));
console.log('Oceanix .R spine swimming animation continuity: PASS');

// Five Synthetic families use their original cybernetic variants as 10% rolls.
// Keep old catch IDs, preserve independent worm-only Cybernetic Coral Grouper.
const fishCatalog=readFileSync('Assets/_Game/Scripts/Fishing/FishCatalog.cs','utf8');
const syntheticInstaller=readFileSync('Assets/_Game/Editor/OceanixSyntheticFishInstaller.cs','utf8');
const variantPairs=[[63,60,'SyntheticMangroveSnapper'],[64,56,'SyntheticTitanTriggerfish'],
                    [65,58,'SyntheticCoralGrouper'],[66,55,'SyntheticPantherGrouper'],
                    [67,57,'SyntheticGreatBarracuda']];
assert(fishCatalog.includes('OceanixSyntheticVariantChance=.10f'));
assert(fishCatalog.includes('RollOceanixSyntheticVariant'));
assert(fishCatalog.includes('RollMagmaVoltVariant'));
const fishIndex=readFileSync('Assets/_Game/Scripts/ShopWorld/FishIndexCatalog.cs','utf8');
const visual=readFileSync('Assets/_Game/Scripts/Fishing/FishVisualFactory.cs','utf8');
for(const [normal,variant,name] of variantPairs){
 assert(fishCatalog.includes(name+'Id='+normal));
 assert(fishIndex.includes('FishCatalog.'+name+'Id'));
 assert(visual.includes('Fishing/'+name));
 assert(syntheticInstaller.includes('"'+name+'"'));
 assert.equal(tournamentSpeciesId(variant),normal);
 assert.equal(tournamentSpeciesId(normal),normal);
 assert(tournamentIslands.find(x=>x.id===6).fish.some(x=>x.id===normal));
 const a=fish.get(normal),b=fish.get(variant);
 assert.deepEqual(a.slice(2),b.slice(2));
}
const worm=odds.find(row=>row[0]==='oceanix-datacenter'&&row[1]==='worms');
assert.equal(+worm[header.findIndex(x=>x.startsWith('58_'))],100);
for(const row of odds){
 const chance=id=>+row[header.findIndex(x=>x.startsWith(id+'_'))];
 if(row[0]==='oceanix-datacenter'){
  if(row[1]==='shrimp')assert.deepEqual([64,65,66,67].map(chance),[25,25,25,25]);
  if(row[1]==='squid')assert.deepEqual([63,66,61,62].map(chance),[25,25,25,25]);
  if(row[1].startsWith('lure:'))assert.equal(chance(66),100);
 }
 for(const id of [55,56,57,60])assert.equal(chance(id),0);
}
assert(syntheticInstaller.includes('CopyIfMissing(download+".fbx"'));
assert(syntheticInstaller.includes('CopyIfMissing(download+" Texture.png"'));
assert(syntheticInstaller.includes('swimTakeContains:"ArmatureAction"'));
console.log('Oceanix Synthetic variants, odds, import and save compatibility: PASS');
