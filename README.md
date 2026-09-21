# Progression Cheat

**English** · [Русский](README.ru.md)

A BepInEx cheat menu for Valheim. One window where you pick what to give yourself, what to
throw away, which skills to set, and which cheats to run. Nothing is granted or levelled
automatically — every change is a button you press.

![version](https://img.shields.io/badge/version-2.1.0-blue)
![game](https://img.shields.io/badge/Valheim-1.0%20Deep%20North-orange)
![loader](https://img.shields.io/badge/BepInEx-5.4.23-green)

---

## Requirements

- Valheim (tested on the 1.0 "Deep North" build, Unity 6000.0.75)
- [BepInEx 5.4.23](https://github.com/BepInEx/BepInEx/releases) x64 for Windows

## Install

1. Install BepInEx into the Valheim folder and start the game once so it creates
   `BepInEx/plugins/`.
2. Drop `ProgressionCheat.dll` into `BepInEx/plugins/`.
3. Start the game. `BepInEx/LogOutput.log` should contain
   `Progression Cheat 2.1.0 loaded`.

The Valheim folder is usually
`…/Steam/steamapps/common/Valheim`.

## Open the menu

Press **F6** in game. Press it again, or click **Закрыть**, to close.

While the window is open your character stops moving and attacking, the camera holds
still, and the cursor is free. Drag the window by its title bar.

> The menu is in Russian. The tabs, in order, are **Items**, **Inventory**, **Cheats**,
> **Skills**.

---

## Items tab — give yourself things

Everything in the game, in one searchable list.

| Control | What it does |
|---|---|
| **Поиск** (Search) | Matches the localised name or the prefab name (`Wood`, `SwordIron`) |
| **Только открытое мной** (Known only) | On: only items this character has picked up at least once. Off: everything in the game |
| **Показывать недоступные** (Show unavailable) | On: also lists what the game normally keeps out of reach — quest items and items from content this install does not have. Those rows are tagged `квест` or `DLC` |
| **Категории** (Categories) | `Ресурсы` = materials, food and arrows. `Все типы` = everything. Then one button per item type: `Material`, `Trophy`, `OneHandedWeapon`, `Tool`, … |
| **Кол-во** (Amount) | How many a button hands over. `-10` / `+10` step it |
| Per row: `+1`, `+<amount>`, `Стак` | Give one, give the amount, or give a full stack of that item |
| **Открыть всё** (Unlock everything) | Marks every item in the game as discovered — see below |
| **Выдать всё из списка** | Gives everything currently filtered into the list |

**Открыть всё** changes only what your character *knows*, it puts nothing in your hands.
After it runs, "known only" stops hiding anything, and every recipe and every building
piece shows up at the workbench and in the build menu.

Your inventory is 32 slots. If there is no room the item is not granted and a line appears
at the bottom of the window. Set `DropIfInventoryFull = true` in the config if you would
rather have the overflow land on the ground.

## Inventory tab — throw things away

The list of what is in your bag right now. This is the fast way to undo a grant that got
out of hand.

- **Поиск** — same search as the items tab.
- Per row: `-1`, `-<amount>`, `Всё` (the whole stack). Worn gear is tagged `надет`.
- **Удалить всё, кроме надетого** — delete everything except what you are wearing.
- **Удалить всё** — delete everything.

Both delete-all buttons ask twice: the first click arms the button and changes its label,
a second click within four seconds goes through.

Items simply vanish, nothing drops on the ground. Anything worn is taken off first —
otherwise the game keeps the model on your character and the stat bonus with it.

## Cheats tab

Four checkboxes. All of them affect your character only.

| Checkbox | What it does |
|---|---|
| **Бессмертие** (Immortal) | No damage gets through at all, health stays full |
| **Бесконечная стамина** (Infinite stamina) | Running, jumping and swinging cost nothing |
| **Бесконечный эйтр** (Infinite eitr) | The same for magic |
| **Полёт** (Fly) | Fly. **Space** up, **Left Ctrl** down, **Shift** faster |

The first three are written to the config and survive a restart. Flight is not: starting a
session already airborne is a surprise, not a convenience.

Flight controls only work with the menu **closed** — while the window is up the game takes
no input. Turn flight off in mid-air and you will fall; without immortality that hurts.

## Skills tab

Every skill gets a number field, a 0–100 slider, `-1` / `+1` buttons and its current value.
Changes apply immediately and are saved with the character.

At the top: `Поставить всем: [number] Применить` (set them all), plus `Все на 0` and
`Все на 100`.

---

## Configuration

`BepInEx/config/alakey.valheim.progressioncheat.cfg`, created on first run. Read at
startup; the cheat checkboxes and "show unavailable" are written back the moment you
toggle them.

| Key | Default | Meaning |
|---|---|---|
| `Hotkeys.ToggleMenu` | `F6` | Menu key. Combos work: `LeftShift+F6`. Names come from `UnityEngine.InputSystem.Key` |
| `UI.Scale` | `0` | Window scale. `0` picks one from the screen height. Try `1.5` if the text is too small |
| `Items.DefaultAmount` | `50` | What the "amount" field starts at |
| `Items.KnownOnly` | `true` | Startup state of the "known only" checkbox |
| `Items.ShowUnavailable` | `false` | Startup state of the "show unavailable" checkbox |
| `Items.DropIfInventoryFull` | `false` | `false` — refuse to grant when there is no room. `true` — drop the overflow on the ground |
| `Items.MarkAsCheated` | `false` | Tag granted items with the cheated flag the console uses |
| `Cheats.Immortal` | `false` | Immortality |
| `Cheats.InfiniteStamina` | `false` | Infinite stamina |
| `Cheats.InfiniteEitr` | `false` | Infinite eitr |
| `Skills.DisableSkillCap` | `true` | Turns off the total-skill cap. Without this the game quietly lowers your other skills back down |

## Can other players see this?

No. The plugin sends nothing over the network and touches only `Player.m_localPlayer` —
your inventory, your skills, your health and stamina. All of it lands in your own
character file (`.fch`) on your own disk.

Three caveats:

- The plugin is not tied to one world. It works everywhere, including someone else's
  server.
- Granted items are real. Dropped on the ground or put in a shared chest, they become part
  of the shared world.
- Flight is the one cheat visible from outside: the state is written into the character's
  ZDO, because that is how the game's own flight mode works.

## Uninstall

Delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` and the `BepInEx` folder
from the game directory. Or set `enabled = false` in `doorstop_config.ini` to switch it off
temporarily. To remove just this plugin, delete
`BepInEx/plugins/ProgressionCheat.dll`.

---

## Building from source

```
build.cmd
```

Compiles every `.cs` in this folder straight into `BepInEx/plugins/ProgressionCheat.dll`
using the C# compiler that ships with Windows
(`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`) — no .NET SDK needed. That
compiler is C# 5 only, which is why the code avoids string interpolation, `nameof` and
`?.`. Close the game first: a running Valheim holds the DLL open.

| File | What is in it |
|---|---|
| `ProgressionCheat.cs` | Plugin entry point, config, hotkey, Harmony patches |
| `CheatMenu.cs` | The window itself |
| `Cheats.cs` | Immortality, stamina, eitr, flight |
| `PlayerAccess.cs` | "Unlock everything": the discovered-items and recipe sets |
| `SkillAccess.cs` | Reading and writing a skill level |

## How it works

**Blocking input while the menu is open.** `Player.TakeInput()` looks like the obvious
hook, but nothing in the game calls it. The live gate is `PlayerController`, and almost
everything that has to go quiet behind a window — movement, camera, cursor lock, hotbar,
chat, minimap — keys off `InventoryGui.IsVisible()`. So that is what gets patched: while
the menu is up it answers "yes" and the whole game behaves as if a normal GUI were open.
`PlayerController.TakeInput()` (which skips the check when a gamepad is active) and
`GameCamera.UpdateMouseCapture()` are patched as backstops.

**Immortality** hooks `Character.ApplyDamage()`. Everything funnels through it — weapons,
falls, burning, poison, smoke. `Character.Damage()` and `RPC_Damage()` sit in front of it,
but the status effects jump straight to `ApplyDamage`, so it is the single point that
catches every source. The game's own `Player.SetGodMode()` is deliberately not used: it
does not stop damage, it only keeps health from reaching zero, and on the first hit it
stamps a "cheater" flag into the character's ZDO.

**Stamina and eitr** work the same way — a prefix on `UseStamina` / `UseEitr` stops the
spend, and the bar is topped back up once a frame to cover anything that writes the value
behind the patch's back.

**Flight** is a mode the game already has (`Player.m_debugFly`); no devcommands are needed
for it. The checkbox only keeps it at the chosen state. `ToggleDebugFly()` also writes the
ZDO, so it is called only when the two disagree.

**Skill levels** are written straight into `Skills.Skill.m_level`. `Skills.GetSkillLevel()`
is no good for reading them back: it floors the value and adds buff modifiers, so it does
not report what is actually in the save.

**"Unlock everything"** fills `m_knownMaterial` and `m_knownRecipes` directly by
reflection. The public `Player.AddKnownItem()` queues a "new item" popup per item, and on
five hundred items the HUD would be working through that queue for half an hour.
