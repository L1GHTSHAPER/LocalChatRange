# LocalChatRange

![LightShaper](tools/assets/lightshaper-wordmark.png)

[Source code on GitHub](https://github.com/L1GHTSHAPER/LocalChatRange) | [Report an issue](https://github.com/L1GHTSHAPER/LocalChatRange/issues) | [Thunderstore](https://thunderstore.io/c/on-together/p/LightShaper/LocalChatRange/)

A BepInEx mod for [On Together](https://store.steampowered.com/app/2688490/On_Together/) that shows how far your **local text chat** reaches.

**♥ Enjoying the mod? Leave a like on [Thunderstore](https://thunderstore.io/c/on-together/p/LightShaper/LocalChatRange/) and a ⭐ on [GitHub](https://github.com/L1GHTSHAPER/LocalChatRange) — it helps the project grow!**

Local messages are only delivered to players within **5 m** of you. This mod draws that area on the ground around your character and puts a ring under every player who will receive your local messages, so you know who can read you before you hit Enter.

## Settings menu

The range side button opens/closes settings. Tabs: **General**, **Range**, **Appearance**, **Quality**. Quality options collapse. Color fields accept `#RRGGBB` or `#RRGGBBAA` and show a swatch with its alpha. **F8** and `/chatrange` still toggle the area.

Cream panels, warm brown text, coral accents, rounded controls and game fonts. Side buttons form one group, show the mod name and the settings hotkey (where available), and move away from visible UI panels. If both edges are blocked, the buttons wait until space becomes available. Existing hotkeys, commands and configuration keys are preserved. Menus scroll on smaller screens; changes save automatically. Where shown, **Apply** saves a field draft. Invalid values keep the saved setting.

## Features

- The area is drawn **on the surface** around your character: it follows slopes, stairs, terraces and drops.
- The shape matches the game's own check exactly. The game measures a straight 3D distance, so the area shrinks on hills and in front of drops, and continues through walls (players behind a thin wall still receive local messages).
- Green rings under the players in range (the same distance test the game runs on their side).
- The radius is **read from the game code** at startup, so the mod stays correct if a game update changes it.
- Three visibility modes: always, only on the Local chat tab, or only while you type in the Local tab.
- Purely client-side: nothing is sent to other players and nothing about the game rules changes.

## Usage

| Action | How |
|---|---|
| Show / hide the area | **F8** (configurable), or type `/chatrange` |
| Turn on / off | `/chatrange on`, `/chatrange off` |
| Choose when it is drawn | `/chatrange mode always` \| `local` \| `typing` |
| Rings under players in range | `/chatrange players on` \| `off` |
| Current state | `/chatrange status` |
| Command list | `/chatrange help` |

`/lcr` is a short alias for `/chatrange`. Commands are handled on your side and are never posted to the chat. If [CommandAPI](https://thunderstore.io/c/on-together/p/jaide/CommandAPI/) is installed, the command is also listed by its `/help` and suggested by CommandTypeahead.

## Configuration

`BepInEx/config/ontogether.localchatrange.cfg` (created on first launch; editable from the mod manager's Config editor).

| Section | Key | Default | Description |
|---|---|---|---|
| General | `Enabled` | `true` | Show the area. |
| General | `ToggleKey` | `F8` | Show/hide hotkey (ignored while typing). |
| General | `VisibilityMode` | `Always` | `Always`, `LocalTabOnly`, `WhileTypingLocal`. |
| General | `HighlightPlayersInRange` | `true` | Rings under players who receive your local messages. |
| Range | `AutoDetectRadius` | `true` | Read the distance from the game code. |
| Range | `Radius` | `5` | Distance used if auto-detection is off or fails. |
| Appearance | `FillColor` | `8CD9FF1F` | Area colour, `RRGGBBAA`. |
| Appearance | `OutlineColor` | `8CD9FFE6` | Edge colour, `RRGGBBAA`. |
| Appearance | `OutlineWidth` | `0.08` | Edge width in metres. |
| Appearance | `OutlineOnTop` | `false` | Draw the edge over walls and furniture. |
| Appearance | `PlayerMarkerColor` | `8CFF8CE6` | Ring colour for players in range. |
| Quality | `Segments` | `64` | Points around the circle. |
| Quality | `Rings` | `8` | Surface samples from the centre to the edge; raise it to follow stairs more closely. |
| Quality | `EdgeRefineSteps` | `4` | Extra samples that place the edge precisely. |
| Quality | `UpdateInterval` | `0.05` | Seconds between surface re-samples while moving. |
| Quality | `MaxStepUp` | `2` | Surfaces higher than this above your feet (ceilings, upper floors) are ignored. |
| Quality | `SurfaceOffset` | `0.03` | Lift above the surface to avoid flickering. |

## How the range works

When a local message arrives, the game drops it if the distance between the sender's position (at the moment of sending) and the receiver's position is greater than 5 m. Both the chat panel and the speech bubble use this check. The mod draws every surface point a standing player could occupy within that distance of you, and checks other players with the very same test.

Only the floor you are on is drawn: a player on a balcony right above you may also be within 5 m, but surfaces more than `MaxStepUp` above your feet are not drawn.

## Installation

**Thunderstore Mod Manager / r2modman:** install from the mod list, or use *Settings -> Import local mod* with the package zip.

**Manual:** install [BepInExPack](https://thunderstore.io/c/on-together/p/BepInEx/BepInExPack/) and copy `LocalChatRange.dll` into `BepInEx/plugins/`.

---

## Русский

**♥ Нравится мод? Поставьте лайк на [Thunderstore](https://thunderstore.io/c/on-together/p/LightShaper/LocalChatRange/) и ⭐ звезду на [GitHub](https://github.com/L1GHTSHAPER/LocalChatRange) — это помогает проекту расти!**

Меню открывает и закрывает боковая кнопка радиуса. Вкладки: **Общее**, **Радиус**, **Вид**, **Качество**. Параметры качества сворачиваются. Цвет задаётся как `#RRGGBB` или `#RRGGBBAA`; образец показывает прозрачность. **F8** и `/chatrange` по-прежнему переключают область.

Кремовые панели, коричневый текст, коралловые акценты и скруглённые элементы. Боковые кнопки собраны в одну группу; при наведении видны название мода и клавиша настроек, если она есть. Группа избегает видимых игровых панелей; когда места нет, кнопки скрываются до освобождения края. Настройки и прежние клавиши сохранены. Низкие окна прокручиваются, изменения сохраняются автоматически; кнопка «Применить», где она есть, сохраняет введённое значение.

Мод показывает зону действия **локального текстового чата** (5 м): область рисуется на поверхности вокруг персонажа и повторяет рельеф, а под игроками, которые получат ваше локальное сообщение, появляются зелёные кольца.

- **F8** или `/chatrange` — показать/скрыть область; `/chatrange on|off`.
- `/chatrange mode always|local|typing` — показывать всегда, только на вкладке Local или только пока вы печатаете во вкладке Local.
- `/chatrange players on|off` — кольца под игроками в радиусе; `/chatrange status` — текущее состояние.
- Радиус считывается из кода игры при запуске; настройки — в `BepInEx/config/ontogether.localchatrange.cfg`.

## Building from source

Requires Windows, .NET SDK 6.0 or newer, an installed copy of On Together, and BepInEx 5 (for example, a Thunderstore Mod Manager / r2modman profile).

From the repository directory, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -GameDir "C:\path\to\On-Together" -BepInExCore "C:\path\to\profile\BepInEx\core"
```

`GameDir` must contain `OnTogether.exe` and `OnTogether_Data\Managed`. `BepInExCore` must contain `BepInEx.dll` and `0Harmony.dll`. Game and BepInEx assemblies are referenced locally and are not distributed in this repository.

The build creates the plugin DLL in `src/bin/Release/` and the installable Thunderstore archive in `dist/`. Ready-to-install archives are also available in [GitHub Releases](https://github.com/L1GHTSHAPER/LocalChatRange/releases).


Side settings buttons are opaque squares with rounded corners, a dark brown outline and proportionate icons, sized to match the game's right-hand controls. They hide with the native controls in Desktop mode, including tooltips and pointer hit areas.

Боковые кнопки настроек стали непрозрачными и квадратными: скруглённые углы, коричневая обводка и значки без растягивания. Размер соответствует высоте игровых кнопок справа. В Desktop-режиме они скрываются вместе с игровыми; подсказки и области нажатия также отключаются.
