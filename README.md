# MicroPatches (Warhammer 40K: Rogue Trader)

Transmechanic Microsoftenator once again fixing bugs and cleaning logs.

Tiny patches attempting to fix bugs in the game's mod support, extend tools for modders, fix other minor bugs, and silence annoying log messages.
Experimental patches require more testing and can be toggled in the mod settings (requires restart to take effect).
If you encounter any issues with an experimental patch, let me know in the issues on this repository.

**WARNING: ALL EXPERIMENTAL PATCHES ARE DISABLED BY DEFAULT FOR A REASON. THEY MAY BREAK YOUR GAME AND/OR SAVES. ACTIVATE AT YOUR OWN RISK.**

## Mod Developers: Editor Template Extension Installation

1. Ensure you do not have the project open in the editor.
2. Replace the contents of `Assets/UnityModManager` with the contents of [this zip](https://github.com/microsoftenator2022/MicroPatches/releases/download/umm-stub/UnityModManager.zip)
3. Delete `Assets/Libs/dnlib.dll`
4. Delete `Library/APIUpdater/project-dependencies.graph`
5. Extract the contents of `MicroPatches-Editor-x.y.z.zip` to the template folder, overwriting existing files
6. Open the project in the editor select `Modifiction Tools` -> `Repair SharedString configs`
7. Optional: `MicroPatches` -> `Refresh blueprints`

### If the editor gets stuck importing assets

Close the editor and repeat step 4
