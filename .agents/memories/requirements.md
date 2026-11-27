---
id: requirements
title: Requirement Analysis
content: Requirement Analysis memo for this mod
---

# Overview
This project aims to create a mod for the game RimWorld that utilizes the
vacuum concept introduced in the Odyssey DLC, adding an object that
depressurizes (or pressurizes) a specific enclosed room (region) to maintain a
controlled partial or full vacuum on both orbital and planetary maps.

# Required Resources

- Texture assets for the object
  - Menu icon
  - Unpowered state (with mask textures)
  - Operational state (with mask textures)
  - Inoperable state (with mask textures)
- Texture assets for command buttons
  - Adjust vacuum level
  - Adjust chamber pumping speed level
- Object definition files
- Runtime code to apply effects to the object (including planetary and orbital map support)
- Language files to describe the object:
  - English
  - Japanese
  - Traditional Chinese
- Required definition files for a RimWorld mod (such as About.xml and LoadFolders.xml)

# Non-Functional Requirements

- The object must be designed with the assumption that it can be rotated when placed.
- The object's appearance should reference RimWorld's Spacer-tier object
  designs, while remaining consistent with the theme of this mod.
- Ensure that the manufacturing material (stuff) can be selected.

# Functional Requirements

- In-game, the object is presented as a 1x1 tile, rotatable structure.
- Research is required to unlock the object.
  - Tech level: Spacer
  - Prerequisite research: Air Conditioning (with Hi-Tech Research Bench and Multi-Analyzer)
- The object consumes electricity.
  - Power consumption is 250W per pumping speed level.
  - When unpowered, the object's appearance changes to the unpowered state.
- The object cannot be placed outdoors.
  - If the room becomes outdoors or unsealed after indoor placement (e.g., due
    to a room wall or roof being destroyed), the object stops operating and
    changes its appearance to the inoperable state.
- In relation to the above, if the room's walls and roof are repaired and it
  becomes sealed indoors again, the object resumes operation and changes its
  appearance back to the operational state.
- If the unpowered state overlaps with the operational/inoperable state, the
  unpowered appearance takes priority.
- As long as the placement location is indoors and not on water, the object can
  be placed regardless of factors such as foundation presence, flooring,
  fertility, or plantability.
- Vacuum level adjustments are made via the object's command button.
  - Clicking this command button opens a dialog (featuring a target vacuum level
    text input flanked by `-` and `+` buttons) for the user to adjust.
- The game pauses while the dialog is displayed.
- The upper limit is 100%.
- The default vacuum level is 0%.
- Pumping speed levels are adjusted using the same method as the vacuum level.
  - Pumping speed is 100 m³/h per level.
  - The upper limit is 50.
  - The default pumping speed level is "1".
- Depressurization and pressurization must be translated into an in-game vacuum level change based
  on fundamental formulas from ISO 3529 vacuum technology theory, accounting for
  in-game time, room volume, and pumping/pressurization speed level.
  - When current room vacuum is below the target vacuum, depressurize until the target is reached.
  - When current room vacuum exceeds the target vacuum, pressurize until the target is reached,
    reinterpreting Pumping speed level as Pressurization speed level.
- For this calculation, the room volume is defined as `(room area) x 2.5m`
  (i.e., assuming a ceiling height of 2.5m).
- Depressurization and pressurization occur only when the object is both powered and in
  an operational state.
