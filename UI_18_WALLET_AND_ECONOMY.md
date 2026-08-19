# Elyndor — UI/UX Specification 18 — Wallet & Economy

**Document:** `UI_18_WALLET_AND_ECONOMY.md`  
**Status:** Approved foundation  
**Platform:** Telegram Mini App  
**Orientation:** Mobile Portrait First  
**Depends on:**
- `26_CURRENCY_AND_ECONOMY_SYSTEM.md`
- `UI_01_GLOBAL_GAME_SHELL.md`

---

# 1. Назначение

Wallet UI gives a simple view of Gold/Crystal balances and their usage without exposing ledger internals to ordinary players.

---

# 2. Entry

Tap currencies in Global HUD:
```text
Gold / Crystal
→ Wallet
```

---

# 3. Wallet Summary

```text
КОШЕЛЁК

Gold
12 450

Crystal
35
```

No 6-currency dashboard.

---

# 4. Gold

Explain:
- earned from gameplay;
- merchants/quests/bosses/dungeons;
- tradeable;
- auction currency.

---

# 5. Crystal

Explain:
- rare/premium;
- may be earned in selected gameplay;
- future externally purchasable;
- not player-tradeable;
- not Auction currency.

---

# 6. Transactions

Optional recent history:
```text
+120 Gold  Quest
-250 Gold  Crafting
+2 400 Gold Auction sale
```

Player-facing summary, not immutable audit ledger UI.

---

# 7. External Purchase

Future Crystal purchase screen is separate payment integration.

Telegram Stars are not displayed as wallet currency.

---

# 8. Approved Decisions

1. HUD always shows Gold/Crystal.
2. Wallet opens from currency tap.
3. Gold tradeable.
4. Crystal non-tradeable.
5. Telegram Stars external only.
6. No paywall-only mandatory combat power.
