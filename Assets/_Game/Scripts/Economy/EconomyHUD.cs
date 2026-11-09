using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EconomyHUD : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private EconomySystem economy;
    [SerializeField] private FishingInventory inventory;
    [SerializeField] private FishingSystem fishingSystem;
    [SerializeField] private HomeBaseSystem homeBase;
    [SerializeField] private AquariumSystem aquariums;

    private bool subscribed;

    private GameObject root;
    private Text coinText;
    private Font font;

    private GameObject marketPrompt;
    private GameObject shopPrompt;
    private GameObject tankPrompt;

    private GameObject panel;
    private Text panelTitle;
    private RectTransform listRoot;
    private Text panelMessage;

    private GameObject placementControls;

    private PlacedFishTank activeTank;

    private void Start()
    {
        Initialize(
            player,
            economy,
            inventory,
            fishingSystem,
            homeBase,
            aquariums
        );
    }

    public void Configure(
        Transform playerTransform,
        EconomySystem economySystem,
        FishingInventory fishingInventory,
        FishingSystem fishing,
        HomeBaseSystem baseSystem,
        AquariumSystem aquariumSystem)
    {
        player = playerTransform;
        economy = economySystem;
        inventory = fishingInventory;
        fishingSystem = fishing;
        homeBase = baseSystem;
        aquariums = aquariumSystem;
    }

    private void Initialize(
        Transform playerTransform,
        EconomySystem economySystem,
        FishingInventory fishingInventory,
        FishingSystem fishing,
        HomeBaseSystem baseSystem,
        AquariumSystem aquariumSystem)
    {
        player = playerTransform;
        economy = economySystem;
        inventory = fishingInventory;
        fishingSystem = fishing;
        homeBase = baseSystem;
        aquariums = aquariumSystem;

        if (root == null)
        {
            font =
                Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf"
                );

            BuildUi();
        }

        if (!subscribed)
        {
            if (economy != null)
                economy.Changed += RefreshCoins;

            if (inventory != null)
                inventory.Changed += RefreshOpenPanel;

            if (aquariums != null)
                aquariums.Changed += RefreshOpenPanel;

            subscribed = true;
        }

        RefreshCoins();
    }

    private void OnDestroy()
    {
        if (economy != null)
            economy.Changed -= RefreshCoins;

        if (inventory != null)
            inventory.Changed -= RefreshOpenPanel;

        if (aquariums != null)
            aquariums.Changed -= RefreshOpenPanel;
    }

    private void Update()
    {
        if (root == null)
            return;

        bool placing =
            aquariums != null &&
            aquariums.IsPlacing;

        placementControls.SetActive(
            placing
        );

        marketPrompt.SetActive(
            !placing &&
            panel != null &&
            !panel.activeSelf &&
            homeBase != null &&
            homeBase.IsNearFishMarket()
        );

        shopPrompt.SetActive(
            !placing &&
            panel != null &&
            !panel.activeSelf &&
            homeBase != null &&
            homeBase.IsNearTankShop()
        );

        activeTank =
            aquariums != null
                ? aquariums.GetNearestTank()
                : null;

        tankPrompt.SetActive(
            !placing &&
            panel != null &&
            !panel.activeSelf &&
            activeTank != null
        );
    }

    private void BuildUi()
    {
        root =
            new GameObject(
                "EconomyHUDRoot",
                typeof(RectTransform)
            );

        root.transform.SetParent(
            transform,
            false
        );

        Stretch(
            root.GetComponent<RectTransform>()
        );

        GameObject coinPanel =
            CreatePanel(
                "CoinCounter",
                root.transform,
                new Color(
                    0.04f,
                    0.06f,
                    0.07f,
                    0.82f
                )
            );

        RectTransform coinRect =
            coinPanel.GetComponent<RectTransform>();

        coinRect.anchorMin =
            new Vector2(0f, 1f);

        coinRect.anchorMax =
            new Vector2(0f, 1f);

        coinRect.pivot =
            new Vector2(0f, 1f);

        coinRect.sizeDelta =
            new Vector2(240f, 64f);

        coinRect.anchoredPosition =
            new Vector2(24f, -24f);

        Text coinIcon =
            CreateText(
                "CoinIcon",
                coinPanel.transform,
                "●",
                30,
                TextAnchor.MiddleCenter
            );

        coinIcon.color =
            new Color(
                1f,
                0.78f,
                0.20f,
                1f
            );

        RectTransform iconRect =
            coinIcon.rectTransform;

        iconRect.anchorMin =
            new Vector2(0f, 0f);

        iconRect.anchorMax =
            new Vector2(0f, 1f);

        iconRect.sizeDelta =
            new Vector2(58f, 0f);

        iconRect.anchoredPosition =
            new Vector2(6f, 0f);

        coinText =
            CreateText(
                "Coins",
                coinPanel.transform,
                "0",
                28,
                TextAnchor.MiddleLeft
            );

        RectTransform coinTextRect =
            coinText.rectTransform;

        coinTextRect.anchorMin =
            new Vector2(0f, 0f);

        coinTextRect.anchorMax =
            new Vector2(1f, 1f);

        coinTextRect.offsetMin =
            new Vector2(66f, 0f);

        coinTextRect.offsetMax =
            new Vector2(-10f, 0f);

        marketPrompt =
            CreateButton(
                "MarketPrompt",
                root.transform,
                "SELL FISH",
                new Vector2(
                    0f,
                    0.5f
                ),
                new Vector2(
                    0f,
                    0.5f
                ),
                new Vector2(
                    0f,
                    0.5f
                ),
                new Vector2(
                    180f,
                    0f
                ),
                new Vector2(
                    240f,
                    76f
                ),
                OpenMarket
            );

        shopPrompt =
            CreateButton(
                "ShopPrompt",
                root.transform,
                "TANK SHOP",
                new Vector2(
                    0f,
                    0.5f
                ),
                new Vector2(
                    0f,
                    0.5f
                ),
                new Vector2(
                    0f,
                    0.5f
                ),
                new Vector2(
                    180f,
                    -88f
                ),
                new Vector2(
                    240f,
                    76f
                ),
                OpenShop
            );

        tankPrompt =
            CreateButton(
                "TankPrompt",
                root.transform,
                "AQUARIUM",
                new Vector2(
                    0f,
                    0.5f
                ),
                new Vector2(
                    0f,
                    0.5f
                ),
                new Vector2(
                    0f,
                    0.5f
                ),
                new Vector2(
                    180f,
                    88f
                ),
                new Vector2(
                    240f,
                    76f
                ),
                OpenTank
            );

        BuildPanel();
        BuildPlacementControls();

        marketPrompt.SetActive(false);
        shopPrompt.SetActive(false);
        tankPrompt.SetActive(false);

        root.transform.SetAsLastSibling();
    }

    private void BuildPanel()
    {
        panel =
            CreatePanel(
                "EconomyPanel",
                root.transform,
                new Color(
                    0.025f,
                    0.045f,
                    0.055f,
                    0.97f
                )
            );

        RectTransform panelRect =
            panel.GetComponent<RectTransform>();

        panelRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        panelRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        panelRect.pivot =
            new Vector2(0.5f, 0.5f);

        panelRect.sizeDelta =
            new Vector2(700f, 760f);

        panelRect.anchoredPosition =
            Vector2.zero;

        panelTitle =
            CreateText(
                "Title",
                panel.transform,
                "",
                38,
                TextAnchor.MiddleLeft
            );

        RectTransform titleRect =
            panelTitle.rectTransform;

        titleRect.anchorMin =
            new Vector2(0f, 1f);

        titleRect.anchorMax =
            new Vector2(1f, 1f);

        titleRect.pivot =
            new Vector2(0.5f, 1f);

        titleRect.sizeDelta =
            new Vector2(0f, 80f);

        titleRect.offsetMin =
            new Vector2(24f, -80f);

        titleRect.offsetMax =
            new Vector2(-100f, 0f);

        CreateButton(
            "Close",
            panel.transform,
            "X",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-20f, -20f),
            new Vector2(70f, 60f),
            ClosePanel
        );

        GameObject listObject =
            new GameObject(
                "ListRoot",
                typeof(RectTransform)
            );

        listObject.transform.SetParent(
            panel.transform,
            false
        );

        listRoot =
            listObject.GetComponent<RectTransform>();

        listRoot.anchorMin =
            new Vector2(0f, 1f);

        listRoot.anchorMax =
            new Vector2(1f, 1f);

        listRoot.pivot =
            new Vector2(0.5f, 1f);

        listRoot.offsetMin =
            new Vector2(24f, -680f);

        listRoot.offsetMax =
            new Vector2(-24f, -100f);

        panelMessage =
            CreateText(
                "Message",
                panel.transform,
                "",
                22,
                TextAnchor.LowerCenter
            );

        RectTransform msgRect =
            panelMessage.rectTransform;

        msgRect.anchorMin =
            new Vector2(0f, 0f);

        msgRect.anchorMax =
            new Vector2(1f, 0f);

        msgRect.pivot =
            new Vector2(0.5f, 0f);

        msgRect.sizeDelta =
            new Vector2(0f, 64f);

        msgRect.offsetMin =
            new Vector2(22f, 16f);

        msgRect.offsetMax =
            new Vector2(-22f, 80f);

        panel.SetActive(false);
    }

    private void BuildPlacementControls()
    {
        placementControls =
            new GameObject(
                "PlacementControls",
                typeof(RectTransform)
            );

        placementControls.transform.SetParent(
            root.transform,
            false
        );

        Stretch(
            placementControls
                .GetComponent<RectTransform>()
        );

        CreateButton(
            "Confirm",
            placementControls.transform,
            "PLACE",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-190f, 250f),
            new Vector2(180f, 82f),
            ConfirmPlacement
        );

        CreateButton(
            "Cancel",
            placementControls.transform,
            "CANCEL",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-190f, 150f),
            new Vector2(180f, 72f),
            CancelPlacement
        );

        placementControls.SetActive(false);
    }

    private void OpenMarket()
    {
        if (fishingSystem != null)
            fishingSystem.EquipRod();

        panelTitle.text =
            "FISH MARKET";

        panel.SetActive(true);

        RebuildMarket();
    }

    private void RebuildMarket()
    {
        ClearList();

        IReadOnlyList<CaughtFishRecord> fish =
            inventory != null
                ? inventory.Fish
                : null;

        if (fish == null ||
            fish.Count == 0)
        {
            panelMessage.text =
                "Catch fish first, then bring them here to sell.";

            return;
        }

        panelMessage.text =
            "Rarer and heavier fish sell for more.";

        int count =
            Mathf.Min(
                fish.Count,
                8
            );

        for (int row = 0;
             row < count;
             row++)
        {
            int index =
                fish.Count - 1 - row;

            CaughtFishRecord record =
                fish[index];

            FishSpeciesDefinition species =
                FishCatalog.Get(
                    record.speciesId
                );

            int price =
                FishCatalog.GetSellValue(
                    record.speciesId,
                    record.weightKg, record.biomeId, record.offshoreNapoleon
                );

            int captured =
                index;

            CreateListButton(
                row,
                species.Name +
                "  " +
                record.weightKg
                    .ToString("0.00") +
                " kg    SELL " +
                price +
                " coins",
                () =>
                {
                    SellFish(captured);
                }
            );
        }

        CreateListButton(
            count,
            "SELL ALL",
            SellAllFish,
            new Color(
                0.18f,
                0.36f,
                0.18f,
                0.95f
            )
        );
    }

    private void SellFish(int index)
    {
        if (inventory == null ||
            economy == null ||
            index < 0 ||
            index >= inventory.Fish.Count)
        {
            return;
        }

        CaughtFishRecord record =
            inventory.Fish[index];

        int value =
            FishCatalog.GetSellValue(
                record.speciesId,
                record.weightKg, record.biomeId, record.offshoreNapoleon
            );

        CaughtFishRecord removed =
            inventory.RemoveFishAt(index);

        if (removed == null)
            return;

        economy.AddCoins(value);
        RebuildMarket();
    }

    private void SellAllFish()
    {
        if (inventory == null ||
            economy == null)
        {
            return;
        }

        int total = 0;

        for (int i =
                inventory.Fish.Count - 1;
             i >= 0;
             i--)
        {
            CaughtFishRecord record =
                inventory.Fish[i];

            total +=
                FishCatalog.GetSellValue(
                    record.speciesId,
                    record.weightKg, record.biomeId, record.offshoreNapoleon
                );

            inventory.RemoveFishAt(i);
        }

        economy.AddCoins(total);

        panelMessage.text =
            "Sold everything for " +
            total +
            " coins.";

        RebuildMarket();
    }

    private void OpenShop()
    {
        panelTitle.text =
            "TANK SHOP";

        panel.SetActive(true);

        RebuildShop();
    }

    private void RebuildShop()
    {
        ClearList();

        int owned =
            aquariums != null
                ? aquariums.UnplacedTankCount
                : 0;

        panelMessage.text =
            "Fish Tank: " +
            AquariumSystem.TankPrice +
            " coins   •   Unplaced: " +
            owned;

        CreateListButton(
            0,
            "BUY FISH TANK - " +
            AquariumSystem.TankPrice +
            " coins",
            BuyTank,
            new Color(
                0.10f,
                0.28f,
                0.36f,
                0.96f
            )
        );

        CreateListButton(
            1,
            owned > 0
                ? "PLACE FISH TANK (" +
                  owned +
                  " available)"
                : "PLACE FISH TANK (none owned)",
            StartPlacement,
            owned > 0
                ? new Color(
                    0.12f,
                    0.34f,
                    0.20f,
                    0.96f
                )
                : new Color(
                    0.16f,
                    0.16f,
                    0.16f,
                    0.85f
                )
        );
    }

    private void BuyTank()
    {
        if (aquariums == null)
            return;

        bool bought =
            aquariums.BuyTank();

        panelMessage.text =
            bought
                ? "Fish tank purchased!"
                : "Not enough coins.";

        RebuildShop();
    }

    private void StartPlacement()
    {
        if (aquariums == null ||
            aquariums.UnplacedTankCount <= 0)
        {
            panelMessage.text =
                "Buy a fish tank first.";

            return;
        }

        if (aquariums.BeginPlacement())
        {
            ClosePanel();
        }
    }

    private void ConfirmPlacement()
    {
        if (aquariums != null)
            aquariums.ConfirmPlacement();
    }

    private void CancelPlacement()
    {
        if (aquariums != null)
            aquariums.CancelPlacement();
    }

    private void OpenTank()
    {
        activeTank =
            aquariums != null
                ? aquariums.GetNearestTank()
                : null;

        if (activeTank == null)
            return;

        panelTitle.text =
            "AQUARIUM";

        panel.SetActive(true);

        RebuildTank();
    }

    private void RebuildTank()
    {
        ClearList();

        if (activeTank == null ||
            aquariums == null)
        {
            panelMessage.text =
                "Move closer to a fish tank.";

            return;
        }

        FishTankSaveRecord tank =
            aquariums.GetTankRecord(
                activeTank.TankId
            );

        if (tank == null)
            return;

        panelMessage.text =
            tank.fish.Count +
            "/" +
            AquariumSystem.TankCapacity +
            " fish in tank. Tap inventory fish to add or tank fish to take back.";

        int row = 0;

        if (inventory != null)
        {
            int inventoryCount =
                Mathf.Min(
                    inventory.Fish.Count,
                    4
                );

            for (int i = 0;
                 i < inventoryCount;
                 i++)
            {
                int index =
                    inventory.Fish.Count -
                    1 -
                    i;

                CaughtFishRecord record =
                    inventory.Fish[index];

                FishSpeciesDefinition species =
                    FishCatalog.Get(
                        record.speciesId
                    );

                int captured =
                    index;

                CreateListButton(
                    row++,
                    "ADD  " +
                    species.Name +
                    "  " +
                    record.weightKg
                        .ToString("0.00") +
                    " kg",
                    () =>
                    {
                        if (aquariums
                            .AddFishToTank(
                                activeTank.TankId,
                                captured
                            ))
                        {
                            RebuildTank();
                        }
                    },
                    new Color(
                        0.08f,
                        0.25f,
                        0.18f,
                        0.95f
                    )
                );
            }
        }

        int tankCount =
            Mathf.Min(
                tank.fish.Count,
                4
            );

        for (int i = 0;
             i < tankCount;
             i++)
        {
            CaughtFishRecord record =
                tank.fish[i];

            FishSpeciesDefinition species =
                FishCatalog.Get(
                    record.speciesId
                );

            int captured =
                i;

            CreateListButton(
                row++,
                "TAKE  " +
                species.Name +
                "  " +
                record.weightKg
                    .ToString("0.00") +
                " kg",
                () =>
                {
                    if (aquariums
                        .RemoveFishFromTank(
                            activeTank.TankId,
                            captured
                        ))
                    {
                        RebuildTank();
                    }
                },
                new Color(
                    0.24f,
                    0.17f,
                    0.08f,
                    0.95f
                )
            );
        }
    }

    private void RefreshOpenPanel()
    {
        RefreshCoins();

        if (panel == null ||
            !panel.activeSelf)
        {
            return;
        }

        if (panelTitle.text ==
            "FISH MARKET")
        {
            RebuildMarket();
        }
        else if (panelTitle.text ==
                 "TANK SHOP")
        {
            RebuildShop();
        }
        else if (panelTitle.text ==
                 "AQUARIUM")
        {
            RebuildTank();
        }
    }

    private void RefreshCoins()
    {
        if (coinText != null)
        {
            coinText.text =
                economy != null
                    ? economy.Coins.ToString()
                    : "0";
        }
    }

    private void ClosePanel()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    private void ClearList()
    {
        if (listRoot == null)
            return;

        for (int i =
                listRoot.childCount - 1;
             i >= 0;
             i--)
        {
            Destroy(
                listRoot
                    .GetChild(i)
                    .gameObject
            );
        }
    }

    private void CreateListButton(
        int row,
        string label,
        UnityEngine.Events.UnityAction action,
        Color? color = null)
    {
        GameObject buttonObject =
            CreatePanel(
                "Row_" + row,
                listRoot,
                color ??
                new Color(
                    0.08f,
                    0.12f,
                    0.14f,
                    0.94f
                )
            );

        RectTransform rect =
            buttonObject
                .GetComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(0f, 1f);

        rect.anchorMax =
            new Vector2(1f, 1f);

        rect.pivot =
            new Vector2(0.5f, 1f);

        rect.sizeDelta =
            new Vector2(0f, 62f);

        rect.anchoredPosition =
            new Vector2(
                0f,
                -row * 68f
            );

        Button button =
            buttonObject.AddComponent<Button>();

        button.onClick.AddListener(
            action
        );

        Text text =
            CreateText(
                "Label",
                buttonObject.transform,
                label,
                22,
                TextAnchor.MiddleLeft
            );

        RectTransform textRect =
            text.rectTransform;

        textRect.anchorMin =
            Vector2.zero;

        textRect.anchorMax =
            Vector2.one;

        textRect.offsetMin =
            new Vector2(16f, 0f);

        textRect.offsetMax =
            new Vector2(-10f, 0f);
    }

    private GameObject CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 size,
        UnityEngine.Events.UnityAction action)
    {
        GameObject gameObject =
            CreatePanel(
                name,
                parent,
                new Color(
                    0.05f,
                    0.24f,
                    0.31f,
                    0.92f
                )
            );

        RectTransform rect =
            gameObject
                .GetComponent<RectTransform>();

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition =
            position;

        Button button =
            gameObject.AddComponent<Button>();

        button.onClick.AddListener(
            action
        );

        Text text =
            CreateText(
                "Label",
                gameObject.transform,
                label,
                24,
                TextAnchor.MiddleCenter
            );

        Stretch(
            text.rectTransform
        );

        return gameObject;
    }

    private GameObject CreatePanel(
        string name,
        Transform parent,
        Color color)
    {
        GameObject gameObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

        gameObject.transform.SetParent(
            parent,
            false
        );

        gameObject
            .GetComponent<Image>()
            .color = color;

        return gameObject;
    }

    private Text CreateText(
        string name,
        Transform parent,
        string value,
        int size,
        TextAnchor alignment)
    {
        GameObject gameObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text)
            );

        gameObject.transform.SetParent(
            parent,
            false
        );

        Text text =
            gameObject.GetComponent<Text>();

        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;

        return text;
    }

    private static void Stretch(
        RectTransform rect)
    {
        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;
    }
}

