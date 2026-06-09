# NPC Style Limiter - Project Documentation

## Project Overview
**NPC Style Limiter** is a RimWorld mod that gives players fine-grained control over how NPCs (Non-Player Characters) are generated. It allows customizing hairstyles, beards, apparel, and body types, as well as adjusting gender ratios during pawn generation.

### Key Features
- **Style Filtering:** Enable or disable specific hairstyles, beards, and apparel.
- **Weight Adjustment:** Set generation weights for different styles to make them more or less common.
- **Gender-Specific Configuration:** Optionally configure styles separately for male and female pawns.
- **Body Type Control:** Adjust the distribution of adult body types for human pawns.
- **Gender Ratio Customization:** Override the default spawn gender ratio.
- **Profile Management:** Save and load different configuration sets.
- **Modern UI:** A custom-built, high-performance in-game settings menu with search and mod-based filtering.

## Technical Architecture
- **Language:** C# 8.0
- **Framework:** .NET Framework 4.72
- **Game Version:** RimWorld 1.6
- **Hooking Engine:** [Harmony](https://github.com/pardeike/HarmonyRimWorld) 2.3.3

### Key Components
- **`CustomizerMod`**: The main entry point. Handles the UI rendering and initialization.
- **`CustomizerSettings`**: Manages data persistence (`Scribe`), profile XML serialization, and provides high-performance O(1) runtime weight lookups using a `shortHash`-indexed array.
- **`Patches`**: Contains Harmony patches for:
    - `PawnGenerator.GeneratePawn`: Tracking generation state and adjusting gender ratio.
    - `PawnStyleItemChooser.WantsToUseStyle`: Filtering hairstyles and beards.
    - `PawnStyleItemChooser.TotalStyleItemLikelihood`: Applying custom weights to styles.
    - `PawnGenerator.GetBodyTypeFor`: Overriding body type selection.
    - `PawnApparelGenerator.GenerateStartingApparelFor`: Tracking apparel generation for specific pawns.
    - `ThingStuffPair.get_Commonality`: Applying weights to apparel selection.
- **`PawnGenerationState`**: A utility to track when the game is actively generating a pawn to ensure patches only apply to NPC generation.

## Building and Running
The project uses a standard C# project file (`NPCStyleLimiter.csproj`).

### Build Prerequisites
- .NET SDK (supporting .NET Framework 4.72 targeting)
- RimWorld assemblies (referenced via `Krafs.Rimworld.Ref` NuGet package)

### Build Command
```powershell
dotnet build Source/NPCStyleLimiter.csproj
```
The build output (DLL and PDB) is automatically copied to the `1.6/Assemblies/` directory.

### Running
1. Ensure the mod folder is located in your RimWorld `Mods` directory.
2. Enable **Harmony** and **NPC Style Limiter** in the game's mod menu.
3. Access settings via `Options -> Mod Settings -> NPC Style Limiter`.

## Development Conventions
- **Licensing:** The project is licensed under **GNU General Public License v3.0**. Include the license header in all source files.
- **Bilingual Support:** Code comments and UI labels are often provided in both **English** and **Chinese**.
- **Performance:** 
    - Use `PawnGenerationState` to minimize the impact of patches during normal gameplay.
    - Use `shortHash` based indexing for fast settings lookups in hot paths.
    - Avoid memory allocations in frequently called patches (like `GetWeightedBodyTypeFor`).
- **UI:** The mod uses a custom UI theme defined in `CustomizerMod.cs`. Stick to the established `AccentColor` and modern drawing helpers for consistency.
- **Localization:** Keyed strings are stored in `Languages/[Language]/Keyed/Keys.xml`. Always use `.Translate()` for UI text.

## Directory Structure
- `1.6/`: Contains the compiled assemblies for RimWorld 1.6.
- `About/`: Mod metadata (About.xml, preview image).
- `Languages/`: Localization files for English, Simplified Chinese, and Traditional Chinese.
- `Source/`: C# source code and project file.
- `LICENSE`: Full GPL v3.0 text.
