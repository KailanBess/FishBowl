# External links, game view and closing FishBowl

Windows 1.31.0 removes the embedded Web workspace and its browser settings. Documentation, compatibility pages, saved links and remote-play clients open in the system default browser. FishBowl does not select, host, close or create a separate profile for that browser. Existing browser preferences and BrowserProfiles data from older releases are retained for compatibility and are no longer used.

## Moving the emulator inside Play

Select the running game, choose **Move window**, then drag its title bar to reposition it within Play. The game remains attached to the same emulator process. With the title bar focused, arrow keys move ten pixels and Shift+arrow moves one pixel. **Fit to Play**, double-clicking the title bar, or Enter/Escape on the title bar restores the full viewport. Switching workspaces keeps the position for that running session. Resizing keeps the frame within Play; fullscreen temporarily fits the game and restores the moved position when it ends. The game itself continues receiving normal input when focused.

## Game-only view

Play prefers identifiable renderer/game windows over larger launcher windows, removes window borders and standard native menus, and hides identifiable sibling frontend windows in the same verified session. External mode or Show emulator controls restores those windows. Unidentified or unrelated windows are not hidden. For emulators with controls drawn into their own client area, open Play > More > Game view settings and adjust the top/bottom/left/right edges. Values start at zero to preserve the full game image and are scaled for the renderer's DPI. Apply saves adjustments for the matched emulator; native games without a matched profile use session-only adjustments. Reset restores zero edges. Show emulator controls restores the uncropped view; Open in window restores native menus, styles and placement.

FishBowl remains the launcher and display host; the emulator still performs emulation. Renderer-managed menus and separate game windows vary by emulator and may need external mode or emulator launch-profile options. Verify each game-view adjustment so it does not crop the game itself.

## Closing with running games

FishBowl asks before closing games it launched or explicitly adopted into Play. Close games and exit posts normal close requests to verified process windows. Save or emulator confirmation dialogs remain available. If a game refuses to close or is still shutting down, FishBowl stays open; finish its prompt and retry. Cancel keeps playing. Keep games running is an explicit alternative that restores their own windows and exits FishBowl.

No process is force-terminated during normal app closure. Process/window ownership is checked by PID and process creation time before close requests. Unrelated emulator or personal browser processes are unaffected. Windows shutdown or task-manager closure does not show the interactive game confirmation.

Linux keeps its existing external-launch interface. See [Play validation](PLAY-VALIDATION.md) for game/controller/monitor checks.
