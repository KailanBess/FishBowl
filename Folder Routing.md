# FishBowl folder routing

FishBowl keeps folder shortcuts aligned with known emulator installations. Adding an emulator immediately shows detected paths in the Folders tab. Existing profiles benefit when selected, too. No games need to be added to FishBowl.

## Two save types

- In-game saves are the progress saved through a game's own save system, including virtual memory cards, battery saves and console user data.
- Save states are emulator snapshots of a running session. They have their own shortcut and are never substituted for in-game saves.

An emulator can use several save folders. FishBowl shows separate rows for Dolphin's GameCube and Wii storage, Azahar Plus's SD/NAND storage, and detected console users. SD/NAND/title containers can include installed content as well as saves.

## How to use the controls

- Open opens an existing directory in Explorer.
- Refresh detected folders rereads the emulator's saved configuration. Close the emulator's settings dialog first so its changes reach disk.
- Choose sets a manual shortcut for that category in FishBowl. It does not move saves or configure the emulator. A manual save-category override replaces that category's detected rows with your chosen folder.
- Auto clears that category's override and restores detection.
- Edit information exposes the same optional overrides. Leave a folder field blank for automatic detection.

Paths that do not exist yet are marked “folder missing” and cannot be opened. FishBowl does not create them. Paths that depend on the currently loaded ROM, or cannot be determined, stay unassigned rather than pointing to an unrelated directory.

Old SaveFolder entries remain as “Previous save shortcut (unclassified).” Use “Use as in-game saves” or “Use as save states” to keep the old path under its correct category. This sets a manual override for that category.

## Automatic detection coverage

| Emulator | Routing used |
| --- | --- |
| Azahar Plus | Local user directory or roaming Azahar data; enabled custom SD/NAND paths in qt-config.ini; states and logs. |
| DeSmuME | PathSettings in desmume.ini; Battery, StateSlots, States and Screenshots remain distinct. |
| Dolphin | Portable marker, Windows user-directory registry settings, legacy Documents folder or roaming data; configured GameCube cards/GCI folders and Wii NAND. |
| Cemu | Portable directory, legacy local settings.xml or roaming data; mlc_path for usr/save. |
| Vita3K | config.yml pref-path and user save directories. |
| Eden | Local user or roaming data; configured NAND and custom save root, keeping user/system saves distinct. |
| Ryujinx | Portable or roaming user-data layout; Switch save directory. |
| PCSX2 | Portable markers or Documents data; Folders settings for memory cards, states, screenshots and logs. |
| DuckStation | Portable marker, local AppData or legacy Documents; configured memory cards, states and screenshots. |
| melonDS | Local or roaming melonDS.toml / melonDS.ini; instance 0 save and state paths. |
| PPSSPP | Local memory stick or installed.txt storage; SAVEDATA and PPSSPP_STATE. |
| RetroArch | Global retroarch.cfg directory settings. Core/content overrides may use other directories. |
| RPCS3 | Portable/config root and vfs.yml virtual HDD settings; separate user savedata and savestates. |
| mGBA | Roaming or portable.ini configuration root; explicit game-save, state and screenshot paths. |
| MAME | Global mame.ini directory settings, including its INI search folders; NVRAM and states. |

## Linux routing

On Linux the same rules read each emulator's Linux storage. A Flatpak (launched through its exports/bin launcher or .desktop entry) uses its sandbox folders under ~/.var/app/<app ID>/config, /data and /cache; native programs and AppImages use the XDG folders ($XDG_CONFIG_HOME, default ~/.config; $XDG_DATA_HOME, default ~/.local/share). Portable markers beside the program are honoured as on Windows. `~`, `$HOME` and other environment variables in emulator settings are expanded.

| Emulator | Linux storage used |
| --- | --- |
| Azahar Plus | Portable user folder, else ~/.config/azahar-emu (qt-config.ini) and ~/.local/share/azahar-emu (sdmc, nand, states, log). |
| DeSmuME | ~/.config/desmume for configuration; save folders are chosen manually. |
| Dolphin | portable.txt, $DOLPHIN_EMU_USERPATH, legacy ~/.dolphin-emu, else ~/.config/dolphin-emu (Dolphin.ini) with GC, Wii, StateSaves, ScreenShots and Logs in ~/.local/share/dolphin-emu. |
| Cemu | Portable folder, else ~/.config/Cemu (settings.xml) and ~/.local/share/Cemu (mlc01, screenshots, log). |
| Vita3K | config.yml beside the program, in ~/.config/Vita3K or ~/.local/share/Vita3K/Vita3K; pref-path decides save storage. |
| Eden | Portable user folder, else ~/.config/eden (qt-config.ini) and ~/.local/share/eden (nand, log). |
| Ryujinx | Portable folder, else ~/.config/Ryujinx. |
| PCSX2 | Portable markers, else ~/.config/PCSX2. |
| DuckStation | portable.txt, else ~/.local/share/duckstation. |
| melonDS | melonDS.toml beside the program, else ~/.config/melonDS. |
| PPSSPP | memstick/installed.txt beside the program, else ~/.config/ppsspp. |
| RetroArch | retroarch.cfg beside the program, else ~/.config/retroarch. |
| RPCS3 | $RPCS3_CONFIG_DIR, else ~/.config/rpcs3; logs in ~/.cache/rpcs3. |
| mGBA | portable.ini, else ~/.config/mgba. |
| MAME | mame.ini beside the program, else ~/.mame (the Flatpak's persisted ~/.mame). |

Every emulator remains supported as a launcher. Other emulators, custom builds, shortcuts/scripts with storage arguments, and per-game overrides may require a manual choice. FishBowl cannot infer a universal folder layout from every executable. Selecting a preset does not enable save states in an emulator that lacks them.

The detector only reads configuration. It does not edit emulator settings, move/copy/delete saves, scan game libraries, or write to console storage. Notes and controller notes remain your own information rather than altering emulator settings.

## Implementation references

The detection rules were checked against these project sources:

- [Azahar Plus user paths](https://github.com/AzaharPlus/AzaharPlus/blob/master/src/common/file_util.cpp) and [configuration](https://github.com/AzaharPlus/AzaharPlus/blob/master/src/citra_qt/configuration/config.cpp)
- [DeSmuME path keys](https://github.com/TASEmulators/desmume/blob/master/desmume/src/path.h)
- [Dolphin Windows user-directory selection](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/UICommon/UICommon.cpp) and [storage settings](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/Core/Config/MainSettings.cpp)
- [Cemu Windows directory selection](https://github.com/cemu-project/Cemu/blob/main/src/gui/wxgui/CemuApp.cpp) and [MLC configuration](https://github.com/cemu-project/Cemu/blob/main/src/config/CemuConfig.cpp)
- [Vita3K configuration](https://github.com/Vita3K/Vita3K/blob/master/vita3k/config/include/config/config.h)
- [Eden custom save-root implementation](https://git.eden-emu.dev/eden-emu/eden/pulls/3154/files)
- [PCSX2 folder settings](https://github.com/PCSX2/pcsx2/blob/master/pcsx2/Pcsx2Config.cpp)
- [DuckStation folder settings](https://github.com/stenzek/duckstation/blob/master/src/core/settings.cpp)
- [melonDS configuration](https://github.com/melonDS-emu/melonDS/blob/master/src/frontend/qt_sdl/Config.cpp)
- [PPSSPP Windows memory-stick selection](https://github.com/hrydgard/ppsspp/blob/master/Windows/main.cpp)
- [RetroArch directory settings](https://github.com/libretro/RetroArch/blob/master/configuration.c)
- [RPCS3 virtual filesystem](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Emu/vfs_config.h) and [save-state paths](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Emu/savestate_utils.cpp)
- [mGBA configuration roots](https://github.com/mgba-emu/mgba/blob/master/src/core/config.c) and [save directories](https://github.com/mgba-emu/mgba/blob/master/src/core/directories.c)
- [MAME output directories](https://github.com/mamedev/mame/blob/master/src/emu/emuopts.cpp)
