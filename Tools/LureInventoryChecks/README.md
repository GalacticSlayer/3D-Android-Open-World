Run from the repository root with Mono installed:

```sh
mcs -out:/tmp/LureInventoryChecks.exe Tools/LureInventoryChecks/Program.cs Assets/_Game/Scripts/ShopWorld/ShopLedger.cs Assets/_Game/Scripts/ShopWorld/ShopCatalog.cs
mono /tmp/LureInventoryChecks.exe
```

Tests production ledger/catalog migration, prices, repeat purchases, catches, escapes, duplicate cleanup, equipment changes, depleted stock, interrupted fights, and unchanged bait use. Unity gameplay and UI need a separate play-mode check.
