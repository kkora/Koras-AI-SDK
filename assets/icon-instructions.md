# Package icon

`assets/icon.png` (256×256 PNG, embedded in every package as `icon.png`) is a programmatically
generated placeholder: a "K" mark on an indigo→violet gradient with rounded corners.

Before 1.0.0, replace it with the official Koras Technologies mark:

- 256×256 (NuGet minimum 128×128, max 1 MB), PNG with transparency.
- Keep the filename `assets/icon.png` — `src/Directory.Build.props` packs it automatically.
- Verify with `dotnet pack` + opening the `.nupkg` (it's a zip): the file must be at the root.
