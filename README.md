# MicroPatches (Warhammer 40K: Rogue Trader)

Transmechanic Microsoftenator once again fixing bugs and cleaning logs.

Tiny patches attempting to fix bugs in the game's mod support, extend tools for modders, fix other minor bugs, and silence annoying log messages.
Experimental patches require more testing and can be toggled in the mod settings (requires restart to take effect).
If you encounter any issues with an experimental patch, let me know in the issues on this repository.

**WARNING: ALL EXPERIMENTAL PATCHES ARE DISABLED BY DEFAULT FOR A REASON. THEY MAY BREAK YOUR GAME AND/OR SAVES. ACTIVATE AT YOUR OWN RISK.**

## Mod Developers: Editor Template Extension Installation

1. Add `RogueTrader.Blueprints.Editor` to the Assembly Definition References of `Mods.Editor` ![image](https://github.com/user-attachments/assets/571d3be0-c059-4e6d-97be-7c363d596c86)
2. Extract the contents of `MicroPatches-Editor-x.y.z.zip` to `Assets/Editor` ![image](https://github.com/user-attachments/assets/ce7cab93-5d8b-45e1-b37b-12340e0cb46e)

### If the editor gets stuck importing assets

1. Replace the contents of `Assets/UnitModManager` with [this zip](https://github.com/microsoftenator2022/MicroPatches/releases/download/umm-stub/UnityModManager.zip)
2. Delete `Assets/Libs/dnlib.dll`
3. **With the editor closed**, delete `Library\APIUpdater\project-dependencies.graph`
