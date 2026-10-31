using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CaughtFishRecord
{
    public int speciesId;
    public float weightKg;
    // Save-safe biome ID: null/empty on old catches, which retain global value.
    public string biomeId;
    public long caughtUtcTicks;
}

[Serializable]
internal class FishingInventorySaveData
{
    public List<CaughtFishRecord> fish =
        new List<CaughtFishRecord>();
}

public class FishingInventory : MonoBehaviour
{
    private const string SaveKey =
        "OpenWorld.FishingInventory.v1";

    [SerializeField]
    private List<CaughtFishRecord> fish =
        new List<CaughtFishRecord>();

    public bool IsFull => fish.Count>=ShopLedger.BagLimit;
    public event Action Changed;

    public IReadOnlyList<CaughtFishRecord> Fish => fish;

    private ShopProgress shop;
    private void Awake()
    {
        shop=GetComponent<ShopProgress>();
        if(shop!=null) { fish=shop.Data.bag; shop.Changed+=OnShopChanged; }
        else Load();
    }
    private void OnShopChanged() { fish=shop.Data.bag; Changed?.Invoke(); }
    private void OnDestroy() { if(shop!=null) shop.Changed-=OnShopChanged; }

    public int AddFish(
        int speciesId,
        float weightKg, bool countAsCatch=false,string biomeId=null)
    {
        if(IsFull || (shop!=null && shop.ReadOnly)) return -1;
        CaughtFishRecord record =
            new CaughtFishRecord
            {
                speciesId = FishCatalog.CanonicalId(speciesId),
                weightKg = weightKg,
                biomeId = biomeId,
                caughtUtcTicks =
                    DateTime.UtcNow.Ticks
            };

        fish.Add(record);
        if(countAsCatch && shop!=null)shop.Data.RecordCatch(record.speciesId,record.weightKg);
        Save();

        return fish.Count - 1;
    }

    public int AddExistingFish(
        CaughtFishRecord record)
    {
        if (record == null || IsFull || (shop!=null && shop.ReadOnly))
            return -1;

        fish.Add(record);
        Save();

        return fish.Count - 1;
    }

    public CaughtFishRecord RemoveFishAt(
        int index)
    {
        if (index < 0 ||
            index >= fish.Count)
        {
            return null;
        }

        CaughtFishRecord record =
            fish[index];

        fish.RemoveAt(index);
        Save();

        return record;
    }

    private void Load()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
            return;

        string json =
            PlayerPrefs.GetString(
                SaveKey,
                string.Empty
            );

        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            FishingInventorySaveData data =
                JsonUtility.FromJson<FishingInventorySaveData>(
                    json
                );

            if (data != null &&
                data.fish != null)
            {
                fish = data.fish;
                foreach(var record in fish)record.speciesId=FishCatalog.CanonicalId(record.speciesId);
            }
        }
        catch
        {
            fish =
                new List<CaughtFishRecord>();
        }
    }

    private void Save()
    {
        if(shop!=null) { shop.Save(); return; }
        FishingInventorySaveData data =
            new FishingInventorySaveData
            {
                fish = fish
            };

        string json =
            JsonUtility.ToJson(data);

        PlayerPrefs.SetString(
            SaveKey,
            json
        );

        PlayerPrefs.Save();

        Changed?.Invoke();
    }
}
