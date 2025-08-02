# Biosculpter Detox

A RimWorld mod that adds a detox cycle to biosculpter pods, allowing pawns to cure drug addictions and withdrawal symptoms through advanced medical technology.

## Features

### 🧬 Advanced Detox Treatment
- **Complete Addiction Cure**: Removes all drug addictions and withdrawal effects
- **Safe Process**: No withdrawal symptoms during the 12-day treatment cycle  
- **Comprehensive Coverage**: Treats all vanilla drugs plus most modded addictions
- **Tolerance Reset**: Clears drug tolerances that increase addiction risk

### 💊 Supported Addictions
- **Vanilla Drugs**: Alcohol, Smokeleaf, Psychite (Tea/Flake/Yayo), Wake-up, Go-juice, Ambrosia
- **Modded Drugs**: Automatically detects addictions from most drug mods
- **Withdrawal Effects**: Removes all associated withdrawal symptoms and mood penalties
- **Smart Detection**: Uses pattern matching to identify new addiction types

### 🔬 Technology Integration  
- **Research Requirement**: Unlocked after completing Bioregeneration research
- **Balanced Resources**: Requires 200W power and 5 nutrition units
- **Cycle Duration**: 12 days (same as pleasure cycles)
- **Visual Integration**: Custom detox icon and UI elements

## Installation

### Requirements
- **RimWorld Version**: 1.6 (compatible)
- **DLC Required**: Ideology (for biosculpter pods)
- **Dependencies**: Harmony (included)

### Installation Steps
1. Subscribe to the mod on Steam Workshop (when published)
2. Ensure you have the Ideology DLC
3. Start or load your save game
4. Research Bioregeneration technology if not already completed

## Usage

### How to Detox a Pawn
1. **Build a Biosculpter Pod** (requires advanced technology)
2. **Research Bioregeneration** if not already completed
3. **Select an addicted pawn** and right-click the biosculpter pod
4. **Choose "Enter biosculpter pod"** from the context menu
5. **Select the Detox cycle** from the available options
6. **Wait 12 days** for the treatment to complete
7. **Enjoy addiction-free pawn** with positive mood buff!

### Smart UI Features
- **Availability Check**: Detox option only appears for pawns with treatable addictions
- **Treatment Preview**: Shows exactly which addictions will be cured
- **Progress Tracking**: Standard biosculpter progress indicators
- **Real-time Updates**: Inspection text shows current treatment status

### When to Use Detox
- **New Recruits**: Clean up addicted prisoners or refugees  
- **Resource Shortages**: Cure addictions when drugs are unavailable
- **Colony Optimization**: Free skilled pawns from drug dependencies
- **Emergency Treatment**: Avoid withdrawal mental breaks

## Balancing

### Resource Requirements
- **Power**: 200W continuous consumption (plan your power grid)
- **Nutrition**: 5 nutrition units (stock up on meals/paste)
- **Time**: 12 days of treatment (significant opportunity cost)
- **Technology**: Requires substantial research investment

### Gameplay Balance
- **Not Overpowered**: Significant time and resource investment required
- **Realistic Alternative**: More humane than imprisonment, less magical than instant cures
- **Strategic Choice**: Meaningful decision between detox time vs. pawn productivity
- **Vanilla Integration**: Fits naturally into existing game mechanics

## Compatibility

### Mod Compatibility
- ✅ **Drug Mods**: Automatically supports most modded drugs
- ✅ **Biosculpter Mods**: Compatible with cycle modification mods
- ✅ **UI Enhancement Mods**: Works with interface improvements
- ✅ **Translation Mods**: Fully localized for international play

### Known Limitations
- ❌ **Luciferium**: Cannot cure luciferium addiction (it's permanent)
- ❌ **Non-Drug Dependencies**: Only treats chemical addictions
- ❌ **Physical Healing**: Does not heal injuries or replace body parts

## Localization

The mod includes complete translations for:
- 🇺🇸 English
- 🇨🇳 Chinese Simplified  
- 🇫🇷 French
- 🇩🇪 German
- 🇯🇵 Japanese
- 🇵🇱 Polish
- 🇧🇷 Portuguese Brazilian
- 🇷🇺 Russian
- 🇪🇸 Spanish

## Technical Details

### Architecture
- **Harmony Patches**: Non-intrusive integration with biosculpter system
- **XML Definitions**: Standard RimWorld cycle definition system
- **Modular Design**: Clean separation of concerns for maintainability
- **Error Handling**: Robust error handling and logging

### For Modders
- **Extension Points**: Easy to add support for new addiction types
- **Pattern Matching**: Automatic detection of modded drugs
- **API Compatibility**: Does not break other mod integrations
- **Open Source**: Learn from the implementation

## Troubleshooting

### Common Issues
**Q: The detox option doesn't appear**
A: Make sure the pawn has addictions and you've researched Bioregeneration

**Q: The cycle failed/stopped working**  
A: Check that you have stable power and sufficient nutrition supply

**Q: Modded drug not detected**
A: Report the specific drug mod - we can add explicit support

**Q: Pawn still has withdrawal after detox**
A: This should not happen - please report with a save file

### Reporting Bugs
When reporting issues, please include:
- List of other mods you're using
- Steps to reproduce the problem  
- Save file if possible
- Any error messages from the dev console

## Credits

### Development
- **Author**: Zei33
- **Based on**: Simple Improve mod architecture
- **Inspired by**: Community requests for humane addiction treatment

### Special Thanks
- RimWorld modding community for guidance and feedback
- Ludeon Studios for creating an amazing and moddable game
- Translators who contributed localization support

## License

This mod is released under the MIT License. Feel free to learn from, modify, or extend the code while respecting the original authorship.

---

**Enjoy building a more humane colony with advanced medical technology!** 🏥✨