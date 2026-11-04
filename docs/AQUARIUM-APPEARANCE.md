# Aquarium appearance on Windows

Open Appearance and accessibility and choose **Use Aquarium style**. It saves the Aquarium Ocean theme, Aquarium Mint accent, rounded corners and gradient background. The normal appearance controls also offer Aquarium Midnight, Aquarium Slate, Aquarium Blue and Aquarium Coral.

Existing saved themes remain selected until this action is used. Fonts, text size, compact/roomy controls, motion preferences, game collections and emulator configuration remain independent. Theme, accent, gradients, corner radius and custom palette overrides can still be changed individually afterward. Selecting the preset turns custom palette overrides off so the named theme is visible; the stored custom color values are retained.

This is a Windows WinForms styling change. The Linux application and shared library schema are unchanged. Security is FishTank; emulation is FishBowl. This change does not embed either app in Aquarium or change update, emulation, browser or remote-play behavior.

The family design uses deep aquatic backgrounds, lighter card surfaces and readable text with Mint, Blue or Coral accents. Earlier image-search references are inspiration, not fixed design assets or pixel-exact screenshots.

Validation: build with `windows/Build.ps1`, run `windows/Run Tests.ps1 -Only ThemeTests`, and run `windows/Run Overflow Audit.ps1` before release. Check the theme at multiple scales, custom palettes and Windows high contrast, then restart to confirm persistence.

