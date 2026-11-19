using System;
using System.Linq;
using System.Text.Json;
class Program
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Main()
    {
        var jsonOptions=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
        var legacy=JsonSerializer.Deserialize<AbyssalGemCollection>(
            "{\"items\":[{\"id\":\"legacy\",\"level\":1,\"attributes\":[0,2,6,7],\"values\":[2,3,3,8]}],\"equippedId\":\"legacy\",\"dust\":100}",jsonOptions);
        legacy.Validate();
        Check(legacy.Equipped.Value(6)==3f && legacy.Bonus(2)==.03f,"Old integer saves retain line and percentage bonuses");
        for(int kind=0;kind<8;kind++)
        for(int tenth=AbyssalGemCollection.Minimum[kind]*10;tenth<=AbyssalGemCollection.Maximum[kind]*10;tenth++)
        {
            var fractional=new AbyssalGemCollection{dust=50};int call=0;
            var rolled=fractional.Create((min,max)=>{call++;return call==1?kind:call==2?tenth:min;});
            fractional.Equip(rolled.id);
            Check(rolled.values[0]==tenth/10f,"Every tenth including endpoints is obtainable on drop");
            Check(Math.Abs(fractional.Bonus(kind)-tenth/1000f)<.00001f,"Fractional bonus reaches gameplay");
            Check(fractional.Upgrade(rolled.id,(min,max)=>min==0?0:tenth,out int selected,out float increment),"Fractional upgrade succeeds");
            Check(selected==0 && increment==tenth/10f && Math.Abs(rolled.values[0]-tenth/5f)<.00001f,"Every tenth is obtainable on upgrade without truncation");
            fractional.Validate();
            var reloaded=JsonSerializer.Deserialize<AbyssalGemCollection>(JsonSerializer.Serialize(fractional,jsonOptions),jsonOptions);
            reloaded.Validate();
            Check(reloaded.Equipped.values.SequenceEqual(rolled.values),"Fractional bonuses survive save/reload exactly");
        }
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
        Check(data.BreakDown(gem.id) && data.Equipped==null && data.dust==dust+10,"Level 1 breakdown awards 10 and clears equipment");
        Check(!data.BreakDown(gem.id) && data.dust==dust+10,"No repeated redemption");
        Check(data.AwardCatch(10000,false,rng.Next)==0,"No dust while unequipped");
        for(int run=0;run<2000;run++)
        {
            data=new AbyssalGemCollection{dust=2975};gem=data.Create(rng.Next);data.Equip(gem.id);
            Check(gem.attributes.Distinct().Count()==4,"Secondary attributes are distinct");
            for(int i=0;i<4;i++)Check(gem.values[i]>=AbyssalGemCollection.Minimum[gem.attributes[i]] && gem.values[i]<=AbyssalGemCollection.Maximum[gem.attributes[i]],"Initial roll bounds");
            for(int n=1;n<15;n++)
            {
                float[] old=(float[])gem.values.Clone();int[] kinds=(int[])gem.attributes.Clone();int before=data.dust;
                Check(data.Upgrade(gem.id,rng.Next,out int slot,out float amount),"Upgrade succeeds");
                Check(data.dust==before-AbyssalGemCollection.UpgradeCost(n),"Correct dust charge");
                Check(gem.values.Where((v,i)=>v!=old[i]).Count()==1 && Math.Abs(gem.values[slot]-(old[slot]+amount))<.0001f,"Exactly one secondary upgraded");
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
        int invested=0;
        for(int gemLevel=1;gemLevel<=15;gemLevel++)
        {
            var refunds=new AbyssalGemCollection{dust=10000};
            var refundGem=refunds.Create(rng.Next);refunds.Equip(refundGem.id);
            for(int n=1;n<gemLevel;n++)refunds.Upgrade(refundGem.id,rng.Next,out _,out _);
            int balance=refunds.dust,expected=10+invested/5;
            Check(AbyssalGemCollection.BreakdownDust(gemLevel)==expected,"Preview matches 10 plus 20 percent invested at every level");
            Check(refunds.BreakDown(refundGem.id) && refunds.dust==balance+expected && refunds.Equipped==null,"Breakdown returns correct investment refund and unequips");
            Check(!refunds.BreakDown(refundGem.id) && refunds.dust==balance+expected,"Refund cannot be claimed twice");
            invested+=AbyssalGemCollection.UpgradeCost(gemLevel);
        }
        Check(AbyssalGemCollection.BreakdownDust(15)==605,"Max-level refund is 10 plus 595");
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
