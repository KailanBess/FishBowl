# Play and polish validation

Automated native-window fixtures check attachment, exact process identity, launcher routing, window recovery, session sharing selection and fullscreen lifetime. They do not substitute for emulator rendering, audio, controller or streaming tests.

Use a game you own and back up saves before a live test. Do not install a package again when its title is already installed in the emulator.

| Check | Expected result |
| --- | --- |
| Launch from Library, Home and an emulator entry | Play selects the running session; existing installed titles launch without reinstalling |
| Switch Home, Emulators, Library and Play repeatedly | Game process remains alive; Play restores its window; no stray filter labels appear |
| Enter and exit fullscreen using the visible button | Game renders at the new size; Exit full screen remains available when game input has focus |
| Move and resize across monitors at different scaling | Rendering remains inside Play; no renderer DPI reset, lost window or clipped toolbar |
| Open in window, then Return to Play | The same game and process survive; original window decorations and placement return |
| Open an emulator dialog while embedded | Dialog is usable; Play does not draw over FishBowl's own modal dialogs |
| Keyboard, mouse and controller input | The focused emulator receives game input; shell navigation does not steal buttons |
| Disconnect and reconnect a controller | No stuck input; navigation resumes only when FishBowl has focus |
| Share session | Exact selected window is preselected and remains embedded; the modeless sharing dialog leaves FishBowl enabled; closing it stops sharing |
| Two-device streaming | Guest sees video and hears audio; approved controls reach only the exact foreground game; stopping releases keys |
| Close FishBowl while the game runs | Confirmation defaults to Cancel; Close games and exit requests normal shutdown, honors save/exit vetoes, and Keep games running restores the external window; session checkpoints survive restart |
| Busy, elevated or unsupported emulator | Actionable status and external-window fallback; no false detach-success message |
| Theme and text changes | Dropdown selection, empty and disabled states match the palette; all sections/dialogs fit at 100/150/200% |
| Expand a Home card, then restart | Expanded card preference is retained; every option remains accessible through current controls |
| Restore settings or a save, cancel, remove and undo | Preview/cancel leaves files intact; undo restores metadata; backups remain recoverable |

For performance comparisons, use the same game, renderer, monitor and text size. Record startup-to-first-frame time, tab-switch duration, idle CPU/memory and a short active session. Native Play placement counters verify unchanged viewports avoid repeated placement calls. Lower polling frequency alone does not prove lower total emulator CPU usage.

Use Open in window for renderer-specific problems. Session details under More explains the controls and recovery actions; attachment and detach failures are recorded in the activity log. A token service and second device are required to validate remote streaming. Linux retains external emulator launching; native embedding requires separate platform work.
