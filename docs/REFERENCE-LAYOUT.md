# Platform-library reference layout

In the Windows Game Library toolbar, choose **Platforms** for the first reference-based gallery. It uses a dark sidebar, large rounded platform tiles, a search field and an accented focus border. The gallery contains only platforms found in the user's current game library; it never fabricates collections or downloads logos, games, firmware or emulator builds.

Choosing a platform filters the existing game library and selects artwork view, preserving its normal launch and editing behavior. This is an additional platform overview; it does not yet replace the full Windows shell or implement Linux parity. Existing theme and accessibility settings remain the source of colors. Full keyboard/controller navigation review and a complete embedded library-shell redesign are follow-up work.

Library menus now expose Per-game launch and controller profile, Choose local cover art, Save and configuration backups and Controller navigation guide alongside the existing Repair game paths action. These reuse existing implementations; no games or emulators are bundled.

Aquarium hosting mode marks MainForm only, selects matching DPI before startup and permits a smaller workspace. Embedded geometry does not overwrite the standalone saved window layout. Standalone launch and shared/Linux data remain unchanged. Physical emulator/controller/full-screen compatibility still requires native checks.

Workspace readiness is now published after first Shown, preventing a startup race between WinForms top-level initialization and Aquarium parenting.
