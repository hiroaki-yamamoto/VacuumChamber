# Vacuum Chamber

A **RimWorld (1.7)** mod for the **Odyssey** DLC that adds a Spacer-tier, rotatable `1x1` **Vacuum Chamber** building capable of depressurizing and pressurizing enclosed indoor rooms to maintain a controlled partial or full vacuum on both orbital/space and planetary maps.

## Requirements & Compatibility

- **RimWorld**: `1.7`
- **Required DLC**: **Odyssey** (`Ludeon.RimWorld.Odyssey`)
- **Required Mod**: **Harmony** (`brrainz.harmony`)
- **Load Order**: Place after **Odyssey** and **Harmony**.
- **Package ID**: `hiroakiyamamoto.VacuumChamber`
- **Supported Languages**: English, Japanese (日本語), Traditional Chinese (繁體中文)

## Features

- **Bidirectional Pressure Control**: Set any target vacuum level from `0%` to `100%`. The chamber automatically **depressurizes** when current room vacuum is below the target, and **pressurizes** when current vacuum exceeds the target.
- **Adjustable Speed & Power Scaling**: Configure speed from **Lv 1** to **Lv 50** (`100 m³/h` and `250 W` per level, up to `5,000 m³/h` and `12,500 W`).
- **Orbital & Planetary Support**: Works seamlessly on orbital/space maps and inside sealed indoor rooms on planetary maps—complete with RimWorld's vacuum overlay toggle, tile vacuum readout, fire suppression, and vacuum exposure/burn effects.
- **Multi-Chamber Stacking**: Place multiple Vacuum Chambers in the same room to combine their pumping or pressurization speeds.
- **Automatic State & Visual Feedback**:
  - **Operational**: Powered and inside a roofed, enclosed indoor room (lit cyan/blue indicators).
  - **Unpowered**: Lacks power, switched off, or broken down (darkened indicators; takes visual priority over inoperable state).
  - **Inoperable**: Powered, but the room is outdoors or unsealed (warning indicators). Automatically resumes once walls and roofs are repaired.
- **Customizable & Relocatable**: Uses selectable **Metallic** materials (which tint the housing), can be rotated in 4 directions, and can be uninstalled and reinstalled.

## Unlocking & Building

| Property | Value |
| :--- | :--- |
| **Research Project** | `vacuum chamber` (`Spacer`, cost: `2,000`) |
| **Research Prerequisites** | `Air Conditioning` |
| **Research Facilities** | `Hi-Tech Research Bench` + `Multi-Analyzer` |
| **Build Menu Tab** | **Odyssey** |
| **Construction Skill** | `6` |
| **Build Cost** | `60` Metallic stuff (Steel, Plasteel, etc.), `3` Component, `1` Advanced Component |
| **Work to Build** | `3,500` |
| **Dimensions & Stats** | `1x1` (rotatable), `150` HP, `25` kg, `0.2` flammability, `-4` beauty, passable (`40` path cost) |

### Placement Rules

- Must be placed inside a **roofed, enclosed indoor room**.
- Cannot be placed **outdoors** or **on water**.
- Terrain flooring, foundations, soil fertility, and plantability do not restrict indoor placement.

## How to Use

Select a constructed Vacuum Chamber to access its controls and live status in the inspect pane:

1. **Set Target Vacuum (`Target vacuum: X%`)**
   - Opens a modal dialog (pausing the game while open) to choose a target vacuum between `0%` and `100%` (default: `0%`).
   - Use `-` / `+` buttons (`Shift` + click adjusts by `10%`), or enter a number directly and click **Confirm** (or press `Enter`).
2. **Set Pumping / Pressurization Speed (`Pumping speed: Lv X` / `Pressurization speed: Lv X`)**
   - Opens a dialog to choose a speed level between `1` and `50` (default: `Lv 1`).
   - Use `-` / `+` buttons (`Shift` + click adjusts by `5`), or enter a level directly and confirm.
   - When the room's current vacuum is higher than the target vacuum, the control and inspect readout automatically switch to **Pressurization speed**.
   - Confirming a new level immediately updates the chamber's power draw (`250 W × Level`).

### Inspect Pane Information

- **State**: `Operational`, `Unpowered`, or `Inoperable (outdoors or unsealed)`
- **Target vacuum**: Configured target percentage (`0%`–`100%`)
- **Room vacuum**: Current vacuum percentage and calculated room volume (`m³`), or `0.0% (unsealed)` when unsealed
- **Speed & Power**: Active mode (`Pumping speed` or `Pressurization speed`), level, flow rate (`m³/h`), and power consumption (`W`)

## Vacuum Physics (ISO 3529 Model)

Room depressurization and pressurization follow **ISO 3529-1** vacuum technology formulas rather than simple linear steps:

- **Room Volume**: Calculated from room floor area assuming a `2.5 m` ceiling height (`Room Tiles × 2.5 m³`).
- **Flow Rate**: `100 m³/h` per speed level, applied every rare tick (`250` ticks = `0.1` in-game hours).
- **Exponential Response**: Pressure approaches the target exponentially—rapidly at first and tapering as it nears full vacuum (`100%`) or atmospheric pressure (`0%`). Once within `0.01%` of the target, the room snaps cleanly to the exact target value.
- **Example**: In a `4 × 5` room (`20` tiles = `50 m³`) at `Lv 1` (`100 m³/h`, `250 W`), each rare tick changes the pressure differential by approximately `18.1%`.

## Important Notes & Caveats

- **Room Breaches**: If a wall is destroyed or a roof tile is removed on a planetary map, the room loses its seal, the chamber becomes **Inoperable**, and the room vents back to atmospheric pressure (`0%`). Rebuilding the wall or roof automatically restores operation.
- **Power Requirements**: Higher speed levels require substantial power (up to `12,500 W` at `Lv 50`). Ensure your power grid can handle the load, especially when running multiple chambers.
- **Save Compatibility**: Chamber settings (`Target vacuum` and `Speed level`) and depressurized planetary room states are persisted in your save file.

## License

This mod is licensed under the **MIT License**. See [LICENSE.md](LICENSE.md) for details.

## Credits

- **Authors**: Hiroaki Yamamoto, Gemini
- **Physics Reference**: ISO 3529-1 (Vacuum technology — Vocabulary — Part 1: General terms)
