# WalletHud

**Файл:** `Assets/Script/Core/UI/WalletHud.cs`

Цифры монет и рубинов в углу. В режиме стройки [[PlayerBuilder]] под балансом цена выбранного здания; при линии — «имя × N = сумма».

По клику — магазин обмена рубинов на монеты ([[Economy.CoinsPerRuby]]).

Слушает `OnChanged` у [[PlayerWallet]].
