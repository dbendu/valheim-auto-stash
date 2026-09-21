# Auto Stash

Give every chest a filter, then press **Z** to put your loot away: each item goes to the nearby chest that accepts it. Lock inventory slots with **Alt+click** to keep your tools, food and potions where they are.

*Русское описание — ниже.*

## Features

- **Chest filters.** Open a chest and press **Filter** under its window. Pick whole categories (Materials, Food, Meads, Trophies, Fish, Weapons, Armor, Ammo, Tools, Other) and/or individual items from a searchable grid. **Add chest contents** picks everything the chest currently holds. Categories also cover items you find later. Chests without a filter never receive anything.
- **Stash with one key.** Press **Z** (or the **Stash** button next to the inventory) to move items into filtered chests within 10 m:
  - chests where the item is picked individually come before chests that accept it through a category;
  - among equal chests, one that already holds the item comes first, then the closest one;
  - existing stacks are topped up before free slots are used, and whatever does not fit overflows into the next matching chest. Nothing is ever dropped.
- **Locked slots.** **Alt+click** an inventory slot to lock or unlock it; locked slots get a blue frame. Items in locked slots are never stashed, and by default the game's own **Place stacks** skips them too. Locks belong to the slot and are saved with your character.
- **Never stashed:** equipped items, quest items, the item on your cursor, and ExtraSlots' special slots (equipment, quick slots, food, ammo).
- Works with chests, carts and ship storage. Wards and private chests are respected.
- English and Russian interface (follows the game language).

## Multiplayer

Client-side only: install it just on your own game. It does not need to be on the server or on other players' games.

Chests are filled through the same ownership handshake the game uses for **Place stacks**, so it works on vanilla servers and next to players without the mod. Chests someone else has open are skipped. Filters are stored in the chest itself, so everyone with the mod sees the same filters.

## Configuration

Edit `BepInEx/config/dbendu.AutoStash.cfg` (created on first launch), or change the settings in game with [BepInEx Configuration Manager](https://thunderstore.io/c/valheim/p/Azumatt/Official_BepInEx_ConfigurationManager/) (F1).

| Section | Setting | Default | Description |
| --- | --- | --- | --- |
| General | `Enabled` | `true` | Turns the whole mod on or off. |
| General | `StashKey` | `Z` | Stash key. It does not fire while Alt or Ctrl is held, unless they are part of the shortcut. |
| General | `Radius` | `10` | Maximum chest distance in metres (2–50). |
| Locked slots | `ToggleModifier` | `LeftAlt` | Hold it and left-click a slot to lock or unlock it. |
| Locked slots | `ProtectFromPlaceStacks` | `true` | Keep locked slots out of the game's **Place stacks** as well. |
| Interface | `InventoryButton` | `true` | Show the **Stash** button next to the inventory. |
| Interface | `ShowUndiscoveredItems` | `false` | List every item in the filter editor, not only the ones your character has discovered. |

## Compatibility

- **ExtraSlots:** its special slots are recognised and never stashed or locked.
- Not tested together with other mods that move items into chests (AzuAutoStore, Quick Stack Store and similar); they do the same job, so one of them is usually enough.

## Installation

**Mod manager (r2modman, Thunderstore Mod Manager, Gale):** install **Auto Stash**; BepInExPack Valheim is installed automatically.

**Manual:** install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/), then put `AutoStash.dll` into `BepInEx/plugins/AutoStash/`.

Removing the mod is safe: filters and locks are stored as plain data that the game ignores.

---

## Auto Stash — на русском

Настройте, что принимает каждый сундук, и нажмите **Z**: вещи из инвентаря сами разложатся по ближайшим подходящим сундукам. Нужные вещи можно защитить: **Alt+клик** по слоту блокирует его.

- **Фильтр сундука.** Откройте сундук и нажмите **«Фильтр»** под его окном. Выберите категории (материалы, еда, зелья, трофеи, рыба, оружие, броня, боеприпасы, инструменты, прочее) и отдельные предметы из сетки с поиском. Кнопка **«Добавить содержимое»** выбирает всё, что уже лежит в сундуке. Категории действуют и на предметы, найденные позже. Сундук без фильтра ничего не получает.
- **Раскладка.** Клавиша **Z** или кнопка **«Разложить»** рядом с инвентарём. Вещи уходят в сундуки в радиусе 10 м:
  - сначала в сундуки, где предмет выбран отдельно, потом в сундуки с подходящей категорией;
  - при равенстве — в сундук, где такой предмет уже лежит, затем в ближайший;
  - сначала дополняются неполные стопки, потом занимаются свободные ячейки. Остаток идёт в следующий подходящий сундук или остаётся в инвентаре.
- **Блокировка слотов.** **Alt+клик** по слоту блокирует его или снимает блокировку, заблокированный слот обведён голубой рамкой. Вещи из таких слотов не раскладываются. По умолчанию их не трогает и игровое **«Сложить»**. Блокировка сохраняется в персонаже.
- **Никогда не раскладываются:** надетые вещи, квестовые предметы, вещь на курсоре и спецслоты ExtraSlots.
- Мод нужен только на клиенте. Он работает на обычных серверах и рядом с игроками без мода. Настройки — в `BepInEx/config/dbendu.AutoStash.cfg` или в игре через Configuration Manager (F1).
