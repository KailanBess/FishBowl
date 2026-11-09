# FishBowl for iPhone and iPad

FishBowl for iOS is an early native SwiftUI library preview for iOS and iPadOS 17 or later. iPhone uses bottom tabs and iPad uses a sidebar. Both offer searchable game cards, list view, favorites, and saved theme and accent preferences.

## Run on a Mac

1. Open `ios/FishBowl.xcodeproj` in Xcode 16 or later.
2. Choose the **FishBowl** scheme and an iPhone or iPad simulator.
3. Press **Run**. To use a physical device, choose your Apple developer team under **Signing & Capabilities** and a bundle ID your team can sign.
4. Open **My Games → Add Games** and select your own supported cartridge or disc images from Files.

The importer copies selected files into the app sandbox and saves a separate local library index. Desktop libraries are not synced. This preview has no gameplay runtime. No games, firmware, keys, or emulator binaries are distributed.

## Simulator smoke check

Import a permitted test file, search for it by title or platform, and add it to Favorites. Restart FishBowl and confirm the game, favorite, and appearance preferences persist. Check portrait and landscape and an iPad split view, with larger Dynamic Type enabled.
