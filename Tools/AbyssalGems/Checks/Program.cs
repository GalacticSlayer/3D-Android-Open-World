using System;
using System.Linq;
using System.Text.Json;
class Program
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Main()
    {
        var shop=new ShopProgress();var inventory=new FishingInventory{component=shop};
        typeof(FishingInventory).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(inventory,null);
        Check(inventory.AddFish(43,420,true,catchHealth:220000)==0,"Boss accepted into bag");
        Check(shop.SaveCount==1 && shop.LastSaved.bag.Count==1 && shop.LastSaved.gems.items.Count==1,"Fish and boss gem persisted together");
        shop.Data.gems.Equip(shop.Data.gems.items[0].id);
        inventory.AddFish(1,2,true,catchHealth:10000);
        Check(shop.LastSaved.gems.dust==10 && shop.NotifiedDust==10 && shop.LastSaved.bag.Count==2,"Catch dust saved alongside fish, then notification fires");
        inventory.AddExistingFish(new CaughtFishRecord{speciesId=43,weightKg=420});
        Check(shop.Data.gems.items.Count==1 && shop.Data.gems.dust==10,"Habitat withdrawal cannot duplicate boss drops or dust");
        shop.ReadOnly=true;int count=inventory.Fish.Count;
        Check(inventory.AddFish(43,420,true,catchHealth:220000)==-1 && inventory.Fish.Count==count && shop.Data.gems.items.Count==1,"Read-only catch grants nothing");
        shop.ReadOnly=false;
        while(inventory.Fish.Count<50)inventory.AddExistingFish(new CaughtFishRecord{speciesId=1});
        int saves=shop.SaveCount;
        Check(inventory.AddFish(43,420,true,catchHealth:220000)==-1 && shop.SaveCount==saves && shop.Data.gems.items.Count==1 && shop.Data.gems.dust==10,"Full bag grants no catch, gem or dust");
        var rng=new Random(43112);
        var data=new AbyssalGemCollection();data.Validate();
        Check(data.AwardCatch(220000,true,rng.Next)==0,"First boss gives gem but no dust without equipped gem");
        Check(data.items.Count==1 && data.Equipped==null,"Drop does not silently equip");
        var gem=data.items[0];data.Equip(gem.id);
        Check(data.AwardCatch(220000,true,rng.Next)==30 && data.items.Count==2,"Each subsequent boss gives exactly one gem and 30 dust");
        Check(data.Equipped==gem,"New drop does not replace equipped gem");
        int dust=data.dust;int level=gem.level;
        Check(!data.Upgrade(gem.id,rng.Next,out _,out _) && data.dust==dust && gem.level==level,"Cannot upgrade without dust");
        Check(!data.BreakDown("missing") && data.dust==dust,"Missing gem is not redeemable");
        Check(data.BreakDown(gem.id) && data.Equipped==null && data.dust==dust+100,"Breakdown awards 100 and clears equipment");
        Check(!data.BreakDown(gem.id) && data.dust==dust+100,"No repeated redemption");
        Check(data.AwardCatch(10000,false,rng.Next)==0,"No dust while unequipped");
        for(int run=0;run<2000;run++)
        {
            data=new AbyssalGemCollection{dust=2975};gem=data.Create(rng.Next);data.Equip(gem.id);
            Check(gem.attributes.Distinct().Count()==4,"Secondary attributes are distinct");
            for(int i=0;i<4;i++)Check(gem.values[i]>=AbyssalGemCollection.Minimum[gem.attributes[i]] && gem.values[i]<=AbyssalGemCollection.Maximum[gem.attributes[i]],"Initial roll bounds");
            for(int n=1;n<15;n++)
            {
                int[] old=(int[])gem.values.Clone();int[] kinds=(int[])gem.attributes.Clone();int before=data.dust;
                Check(data.Upgrade(gem.id,rng.Next,out int slot,out int amount),"Upgrade succeeds");
                Check(data.dust==before-AbyssalGemCollection.UpgradeCost(n),"Correct dust charge");
                Check(gem.values.Where((v,i)=>v!=old[i]).Count()==1 && gem.values[slot]==old[slot]+amount,"Exactly one secondary upgraded");
                Check(kinds.SequenceEqual(gem.attributes),"Upgrade never rerolls attribute kinds");
                Check(amount>=AbyssalGemCollection.Minimum[kinds[slot]] && amount<=AbyssalGemCollection.Maximum[kinds[slot]],"Upgrade roll bounds");
                Check(Math.Abs(gem.ScalePiercer-(1.5f+n*.05f))<.0001,"Primary increases every level");
                data.Validate();
            }
            Check(data.dust==0 && gem.level==15 && Math.Abs(gem.ScalePiercer-2.2f)<.0001,"Level cap and total cost");
            data.dust=9999;
            Check(!data.Upgrade(gem.id,rng.Next,out _,out _) && data.dust==9999,"Max-level button cannot charge dust");
            var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
            string json=JsonSerializer.Serialize(data,options);
            data=JsonSerializer.Deserialize<AbyssalGemCollection>(json,options);data.Validate();
            Check(data.Equipped.id==gem.id && data.Equipped.level==15 && data.Equipped.values.SequenceEqual(gem.values),"Save reload retains exact rolls and equipped ID");
        }
        int previous=0;
        for(int hp=0;hp<250000;hp+=37)
        {
            int reward=AbyssalGemCollection.CatchDust(hp,false);
            Check(reward>=previous && reward>=2 && reward<=25,"Dust bounded and increases monotonically with HP");previous=reward;
        }
        data=new AbyssalGemCollection();gem=data.Create(rng.Next);gem.attributes[1]=gem.attributes[0];
        bool rejected=false;try{data.Validate();}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"Malformed duplicate attributes are rejected instead of silently replacing saves");
        Console.WriteLine($"PASS: {checks} gem drop, roll, progression, economy, persistence and breakdown checks.");
    }
}
