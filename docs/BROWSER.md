# Web, game view and closing FishBowl

## Browser setup

Windows Web displays an actual installed browser window inside its viewport using borderless owned-window hosting. Its original rendering context and DPI are preserved instead of reparenting its GPU window or starting desktop kiosk mode. Firefox is the default; Browser settings can select Microsoft Edge or Google Chrome and an optional executable path. This is native-window integration, not a bundled Gecko/WebView2 control. Install your chosen browser separately and keep it updated. FishBowl does not download a browser automatically.

Open Web or Tools > Web browser. Enter an http/https address or search words and press Go. Nothing loads automatically on FishBowl startup. Browser settings is available in Web, Tools and General settings. A changed browser choice applies after closing the current Web session.

Firefox starts in a normal window with a separate profile using Mozilla's [profile, new-instance and new-window command-line options](https://firefox-source-docs.mozilla.org/browser/CommandLineParameters.html). Edge/Chrome use a separate user-data directory and a normal browser window so address-bar navigation remains available. Subsequent Go actions focus the exact owned browser window and submit the address through its address bar; they do not start a second process or replace the session. If Windows cannot grant keyboard focus, FishBowl reports the problem and leaves the current session intact. Release held shortcut keys and retry from Web. Back, Forward and Reload send native browser commands to the verified window. The address box shows the last submitted address/search, not a synchronized current-page address. Browser permissions, downloads and page dialogs remain browser-managed.

Browsing data is stored under FishBowl's data directory in BrowserProfiles, separately from personal browser profiles. Copying a portable FishBowlData folder also copies those browser cookies and logins. Close Web before transferring or changing browser profile files.

Switching Home, Emulators, Library, Play and Web keeps the browser running. Full screen has a visible exit button. Open in window restores the browser window and changes to Return to Web. Press that same button to reattach the browser; Show in app also retries the same verified process. Personal browser instances are never selected by executable name or closed. If a renderer cannot be hosted, use external mode. Native Firefox window integration is not an officially supported desktop embedding API, so rendering, permission dialogs and monitor compatibility require live testing.

Closing FishBowl requests normal closure of its Web browser. If it is still closing or requires confirmation, FishBowl offers staying open or explicitly leaving that browser in its own window. It does not force-terminate the browser. An orphaned dedicated profile may remain locked until its browser is closed.

## Game-only view

Play prefers identifiable renderer/game windows over larger launcher windows, removes window borders and standard native menus, and hides identifiable sibling frontend windows in the same verified session. External mode or Show emulator controls restores those windows. Unidentified or unrelated windows are not hidden. For emulators with controls drawn into their own client area, open Play > More > Game view settings and adjust the top/bottom/left/right edges. Values start at zero to preserve the full game image and are scaled for the renderer's DPI. Apply saves adjustments for the matched emulator; native games without a matched profile use session-only adjustments. Reset restores zero edges. Show emulator controls restores the uncropped view; Open in window restores native menus, styles and placement.

FishBowl remains the launcher and display host; the emulator still performs emulation. Renderer-managed menus and separate game windows vary by emulator and may need external mode or emulator launch-profile options. Verify each game-view adjustment so it does not crop the game itself.

## Closing with running games

FishBowl asks before closing games it launched or explicitly adopted into Play. Close games and exit posts normal close requests to verified process windows. Save or emulator confirmation dialogs remain available. If a game refuses to close or is still shutting down, FishBowl stays open; finish its prompt and retry. Cancel keeps playing. Keep games running is an explicit alternative that restores their own windows and exits FishBowl.

No process is force-terminated during normal app closure. Process/window ownership is checked by PID and process creation time before close requests. Unrelated emulator or personal browser processes are unaffected. Windows shutdown or task-manager closure does not show the interactive game confirmation.

Linux retains its existing external-launch UI and preserves the optional browser/view settings for round trips. See [Play validation](PLAY-VALIDATION.md) for game/controller/monitor checks. Also verify Web on the selected browser: page rendering, Go/Back/Forward/Reload, form input, permission/download dialogs, navigation away and back, fullscreen, external restoration and shutdown with multiple tabs.
