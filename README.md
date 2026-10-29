# TopBar

A Windows 11-style taskbar that lives at the **top** of your main screen and hides itself until you need it.

Windows 11 won't let you move its taskbar to the top, and the tweaks that try are buggy with auto-hide. TopBar is its own little app instead. Windows' real taskbar just stays hidden at the bottom.

## What it does

- **Hides at the top.** Push your mouse to the very top of your main screen (or tap the Windows key) and it slides down. Move away and it tucks back up.
- **Windows 11 layout.** Start button, pinned apps and open apps sit together in the middle.
- **One icon per app.** A short grey line under an icon means it's open. A long orange line means it's the one you're using.
- **Live previews.** Hover an open app to see a live picture of each of its windows. Click one to jump to it, or hit the ✕ to close it.
- **Right corner:**
  - **CPU / GPU / RAM**: temps, watts and memory. Click them to open Task Manager.
  - **Up arrow**: Windows' hidden tray icons.
  - **Bluetooth**: Quick Settings, Bluetooth & devices, add a device, cast.
  - **Speaker**: volume slider, mute, and one-click switching between speakers/headsets and mics.
  - **Clock**: click it for the calendar and notifications.
- **Game guard.** It won't drop down over a fullscreen game when you aim at the top of the screen. You can still open it with the Windows key.

## What you need

- **64-bit Windows 10 or 11**
- That's it. TopBar is built on your PC by the C# compiler that's already part of Windows. Nothing to install, no admin rights needed.

## Install

1. On this page, click the green **Code** button, then **Download ZIP**.
2. **Before you unzip it:** right-click the ZIP → **Properties** → tick **Unblock** at the bottom (if it's there) → **OK**. This stops Windows from blocking the files because they came from the internet.
3. Unzip it somewhere it can stay, like `Documents\TopBar`. Don't run it from inside the ZIP.
4. Double-click **`Build and Run TopBar.bat`**.
   - A black window builds TopBar (a couple of seconds) and starts it.
   - It creates `TopBar.exe` in the same folder.
5. Push your mouse to the very top of your main screen. There it is.

> **Heads-up:** on first start TopBar switches Windows' own taskbar to **auto-hide** so the two don't fight. To get the normal taskbar back later: **Settings → Personalization → Taskbar → Taskbar behaviors →** untick **Automatically hide the taskbar**.

## Adding your own shortcuts (pinned apps)

Your pinned apps are just **shortcut files in a folder**. Whatever shortcuts are in that folder show up on the bar.

### Step 1: Open the Pinned folder

**Right-click an empty part of the bar → Edit pinned apps (drop shortcuts in this folder).**

A folder called `Pinned` opens (it's inside your TopBar folder). The first time, TopBar fills it with apps it finds on your PC, like File Explorer, Chrome, Edge, Steam, Discord and Task Manager.

### Step 2: Put a shortcut in it

Use whichever way is easiest:

**A. From the Start menu (easiest)**
1. Open Start → **All** (all apps).
2. Find the app, then **drag it straight into the Pinned folder**. Windows makes a shortcut for you.

**B. From a desktop shortcut you already have**
1. Click the shortcut on your desktop and press **Ctrl + C**.
2. Click inside the Pinned folder and press **Ctrl + V**.

**C. From the program file itself**
1. Find the program's `.exe` (usually in `C:\Program Files\...`).
2. Right-click it → **Show more options → Create shortcut**.
3. Move that new shortcut into the Pinned folder.

> **Steam / Epic games:** copy the game's desktop shortcut in (way B). It'll launch fine, but it may show a plain icon instead of the game's icon.

### Step 3: Put them in order

Pins are sorted by their **file name**. That's why the starter ones have numbers in front, like `1 File Explorer` and `2 Chrome`. The number never shows on the bar.

To change the order, **rename** the shortcuts (click one, press **F2**):

```
1 File Explorer
2 Chrome
3 Discord
4 Spotify
9 Task Manager
```

### Step 4: Reload

**Right-click the bar → Reload pinned apps.** Your new pins show up right away.

### Removing a pin

Delete its shortcut from the Pinned folder, then **right-click the bar → Reload pinned apps**. Deleting a shortcut never deletes the app itself.

## Using it

| Do this | What happens |
|---|---|
| Mouse to the very top of the main screen | Bar slides down |
| Windows key | Bar shows for a few seconds |
| Click an app | Bring it up / minimize it. If it has several windows, each click flips to the next one |
| Middle-click an app | Open another one |
| Right-click an app | List its windows, open another, close |
| Hover an open app | Live previews of its windows |
| Click the stats | Task Manager |
| Middle-click the speaker | Quick mute |
| Right-click empty bar | Options: game guard, Task Manager, bar size, pins, close TopBar |

**Bar size:** right-click the bar → **Bar size** → 100 / 125 / 150 / 175%. TopBar restarts at the new size and remembers it.

## Start it with Windows (optional)

1. Press **Windows + R**, type `shell:startup`, press Enter.
2. Right-click `TopBar.exe` in your TopBar folder → **Show more options → Create shortcut**.
3. Move that shortcut into the Startup folder that opened.

## Updating

Download the new ZIP, copy the new `src` folder and `Build and Run TopBar.bat` over your old ones, then double-click **`Build and Run TopBar.bat`**. Your pins and settings stay.

## Uninstall

1. Right-click the bar → **Close TopBar**.
2. Delete the TopBar folder (and its Startup shortcut, if you made one).
3. Get the normal taskbar back: **Settings → Personalization → Taskbar → Taskbar behaviors →** untick **Automatically hide the taskbar**.

## Troubleshooting

- **"Windows protected your PC" when running the .bat:** you skipped the Unblock step. Click **More info → Run anyway**, or redo step 2 of Install.
- **Antivirus complains about TopBar.exe:** it's a brand-new program built on your own PC, so it isn't signed. The full source is in `src\TopBar.cs` if you want to look it over.
- **Bar doesn't show:** it only lives on your **main** screen (Settings → System → Display → "Make this my main display"). Push the mouse all the way to the top edge.
- **GPU shows `--`:** GPU stats need an NVIDIA card with its driver installed (they come from `nvidia-smi`).
- **Something else:** check `TopBar.log` in the TopBar folder.

## License

MIT. Do whatever you want with it.
