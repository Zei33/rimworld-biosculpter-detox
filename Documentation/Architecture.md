# BiosculpterDetox Mod Architecture

## Overview

BiosculpterDetox is a RimWorld mod that adds a new cycle to biosculpter pods, allowing pawns to cure drug addictions and withdrawal symptoms. The mod integrates seamlessly with RimWorld's existing biosculpter system while maintaining compatibility with other mods.

## Project Structure

```
rimworld-biosculpter-detox/
├── 1.6/
│   ├── ModEntry.cs                     # Main mod entry point
│   ├── Core/                           # Core functionality
│   │   ├── BiosculpterDetoxDefOf.cs   # Definition references
│   │   └── DetoxCycle.cs              # Detox cycle implementation
│   ├── Defs/                          # XML definitions
│   │   └── BiosculpterCycleDefs/      # Biosculpter cycle definitions
│   │       └── Cycles_Detox.xml
│   ├── Patches/                       # Harmony patches
│   │   └── BiosculpterPatches.cs      # Patches for biosculpter functionality
│   ├── Languages/                     # Localization (9 languages)
│   ├── Textures/                      # UI graphics
│   │   └── UI/Commands/Detox.png      # Detox cycle icon
│   └── Libraries/
│       └── 0Harmony.dll               # Harmony patching library
├── Documentation/
│   ├── Architecture.md                # This file
│   └── Features.md                    # Feature documentation
└── README.md                          # Main documentation
```

## Key Components

### ModEntry
- Entry point for the mod
- Initializes Harmony patches using the ID "com.zei33.biosculpterdetox"
- Logs successful initialization

### Core System

#### BiosculpterDetoxDefOf
- Static class providing compile-time safe references to XML definitions
- Ensures proper initialization of mod definitions

#### DetoxCycle
- Core logic for the detox functionality
- Handles identification and removal of drug addictions and withdrawal effects
- Supports both vanilla and modded drugs through pattern matching
- Provides UI feedback and success notifications

### Harmony Integration

#### BiosculpterPatches
The mod uses several Harmony patches to integrate with the biosculpter system:

1. **CompBiosculpter_CycleCompleted_Patch**
   - Postfix patch that triggers detox logic when a detox cycle completes
   - Handles success notifications and letter sending

2. **CompBiosculpter_GetGizmos_Patch** 
   - Modifies biosculpter UI to show detox-specific information
   - Disables detox option if no addictions are present
   - Shows preview of what will be treated

3. **CompBiosculpter_CompInspectStringExtra_Patch**
   - Adds detox information to biosculpter inspection text
   - Shows what conditions are being treated during cycle

### XML Definitions

#### BiosculpterCycle_Detox
- Defines the detox cycle with appropriate duration, power, and nutrition requirements
- Duration: 12 days (same as pleasure cycle)
- Power: 200W consumption
- Nutrition: 5 units required
- Research requirement: Bioregeneration technology

## Technical Implementation

### Addiction Detection
The mod identifies detoxifiable conditions through multiple methods:

1. **Hardcoded Lists**: Vanilla drug addictions and withdrawals
2. **Pattern Matching**: Detects modded drugs using naming conventions:
   - Hediffs starting with "Addiction_"
   - Hediffs ending with "Withdrawal" or "Addiction"
   - Hediffs containing "Tolerance"

### Detox Process
When a detox cycle completes:

1. Scan pawn's health for detoxifiable conditions
2. Remove all identified addiction and withdrawal hediffs
3. Clear drug tolerance hediffs
4. Apply positive mood buff (using existing cathartic meditation hediff)
5. Show visual feedback and send notification letter

### Mod Compatibility
The mod is designed for maximum compatibility:

- Uses only postfix patches to avoid conflicts
- Leverages existing RimWorld systems (hediffs, notifications, etc.)
- Supports modded drugs through pattern matching
- Does not modify core game files

### Localization Support
Complete localization support for 9 languages:
- English
- Chinese Simplified
- French  
- German
- Japanese
- Polish
- Portuguese Brazilian
- Russian
- Spanish

Each language includes all UI strings, notifications, and descriptions.

## Extension Points

### Adding New Detoxifiable Conditions
To extend the mod for new types of addictions:

1. Add the hediff def name to appropriate lists in `DetoxCycle.cs`
2. Ensure pattern matching covers the naming convention
3. Test compatibility with the new conditions

### Customizing Cycle Parameters
The cycle definition in `Cycles_Detox.xml` can be modified to adjust:
- Duration (durationDays)
- Power consumption (powerConsumption)  
- Nutrition requirements (nutritionRequired)
- Research prerequisites (requiredResearch)

### UI Customization
The Harmony patches in `BiosculpterPatches.cs` can be extended to add:
- Additional UI information
- Custom warnings or requirements
- Enhanced visual feedback