# BiosculpterDetox Features

## Core Functionality

### Detox Biosculpter Cycle
The mod adds a new "Detox" cycle to all biosculpter pods, providing a humane and effective way to cure drug addictions without the harsh withdrawal process.

#### What It Treats
- **All Vanilla Drug Addictions**: Alcohol, Smokeleaf, Psychite (Tea/Flake/Yayo), Wake-up, Go-juice, Ambrosia
- **Withdrawal Symptoms**: All associated withdrawal effects and negative moods
- **Drug Tolerances**: Clears built-up tolerances that increase addiction risk
- **Modded Drugs**: Automatically detects and treats addictions from most drug mods

#### How It Works
1. Place an addicted pawn in a biosculpter pod
2. Select the "Detox" cycle from the available options
3. Wait 12 days for the cycle to complete
4. The pawn emerges completely free from all addictions

### Requirements

#### Technology Prerequisites
- **Bioregeneration Research**: Must be completed before detox cycles become available
- **Biosculpter Construction**: Requires the same technology as other advanced cycles

#### Resources Needed
- **Power**: 200W continuous consumption during the cycle
- **Nutrition**: 5 nutrition units to feed the pawn during treatment
- **Time**: 12 days of treatment (same duration as pleasure cycles)

### User Interface

#### Smart Cycle Selection
- **Availability Check**: The detox option only appears for pawns with treatable addictions
- **Preview Information**: Shows exactly which addictions will be treated before starting
- **Real-time Updates**: Displays current treatment progress during the cycle

#### Visual Feedback
- **Cycle Icon**: Custom detox icon distinguishes it from other cycles
- **Progress Indicators**: Standard biosculpter progress bars and timers
- **Completion Notifications**: Text popup and letter notification when treatment succeeds

#### Inspection Information
- **Treatment Preview**: Hover over the detox option to see what will be cured
- **Active Cycle Display**: Shows which addictions are being treated during the cycle
- **Status Messages**: Clear feedback if no addictions are detected

## Gameplay Benefits

### Compared to Traditional Withdrawal
Traditional drug withdrawal in RimWorld involves:
- 15+ days of severe mood penalties (-20 to -35)
- Risk of mental breaks and destructive behavior
- Need to imprison or restrain addicted pawns
- Potential for relapse if drugs are available

**Detox Cycle Advantages:**
- No withdrawal symptoms during treatment
- Guaranteed success without relapse risk
- Pawn is safely contained during treatment
- Emerges with positive mood buff

### Strategic Considerations

#### When to Use Detox
- **New Recruits**: Clean up addicted prisoners or refugees
- **Colony Health**: Remove addictions before they cause problems
- **Emergency Situations**: Quick cure during drug shortages
- **Optimization**: Free skilled pawns from drug dependencies

#### Resource Investment
- **Power Planning**: Ensure stable power during the 12-day cycle
- **Nutrition Supply**: Stock adequate nutrition paste or meals
- **Timing**: Plan around other biosculpter needs
- **Opportunity Cost**: Consider if the pawn's work is more valuable than the cure

## Compatibility & Integration

### Mod Compatibility
- **Drug Mods**: Automatically detects and treats most modded drug addictions
- **Biosculpter Mods**: Compatible with mods that modify cycle durations or requirements
- **UI Mods**: Works with interface enhancement mods
- **Translation Mods**: Fully localized for international mod collections

### Supported Drug Types

#### Vanilla Drugs
| Drug Type | Addiction | Withdrawal | Tolerance |
|-----------|-----------|------------|-----------|
| Alcohol/Beer | ✓ | ✓ | ✓ |
| Smokeleaf | ✓ | ✓ | ✓ |
| Psychite Tea | ✓ | ✓ | ✓ |
| Flake | ✓ | ✓ | ✓ |
| Yayo | ✓ | ✓ | ✓ |
| Wake-up | ✓ | ✓ | ✓ |
| Go-juice | ✓ | ✓ | ✓ |
| Ambrosia | ✓ | ✓ | ✓ |

#### Modded Drug Support
The mod automatically detects addictions from other mods using pattern matching:
- Hediffs named "Addiction_[DrugName]"
- Hediffs ending in "Withdrawal" or "Addiction"
- Tolerance effects containing "Tolerance" in the name

### Limitations

#### What It Cannot Treat
- **Luciferium Addiction**: This is permanent and lethal to remove
- **Non-Drug Addictions**: Only treats chemical dependencies
- **Physical Injuries**: Does not heal wounds or missing body parts
- **Mental Conditions**: Does not cure non-addiction mental hediffs

#### Technical Limitations
- **One Pawn Per Pod**: Standard biosculpter limitations apply
- **Biotuning**: Subject to normal biotuning cooldowns between different pawns
- **Power Dependency**: Cycle fails if power is lost during treatment
- **Research Locked**: Requires advanced technology investment

## Balancing Considerations

### Designed Balance
The detox cycle is balanced to be:
- **Convenient but not overpowered**: Requires significant time and resource investment
- **Realistic alternative**: More humane than imprisonment but not magical
- **Technology-gated**: Available only after substantial research investment
- **Resource-intensive**: Meaningful power and nutrition costs

### Integration with Vanilla Mechanics
- **Research Tree**: Fits naturally into the biotechnology progression
- **Resource Economy**: Consumes nutrition and power like other cycles  
- **Time Investment**: 12-day duration provides meaningful opportunity cost
- **Faction Balance**: Available to all factions with appropriate technology

### Difficulty Scaling
The mod automatically scales with game difficulty:
- **Higher Difficulties**: Power failures and resource shortages are more impactful
- **Commitment Mode**: Failed cycles represent permanent losses
- **Faction Relations**: No impact on faction relationships (unlike organ harvesting)
- **Wealth Scaling**: Biosculpter pods increase colony wealth and raid strength