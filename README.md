# MicroPatches (Warhammer 40K: Rogue Trader)

Transmechanic Microsoftenator once again fixing bugs and cleaning logs.

Tiny patches attempting to fix minor bugs and silence annoying log messages.
Experimental patches require more testing and can be toggled in the mod settings (requires restart to take effect).
If you encounter any issues with an experimental patch, let me know in the issues on this repository.

**WARNING: ALL EXPERIMENTAL PATCHES ARE DISABLED BY DEFAULT FOR A REASON. THEY MAY BREAK YOUR GAME AND/OR SAVES. ACTIVATE AT YOUR OWN RISK.**

## Current patches as of 1.2:

### Ported from WrathPatches

- Fixed NRE in Element.AssetGuid and Element.AssetGuidShort
- SharedStringConverter.ReadJson now correctly uses ScriptableObject.CreateInstance not the class constructor. Fixes harmless but noisy template mod localization errors
- Silenced "Bind: no binding named X" messages

### Experimental

These have all recieved basic testing, but are not guaranteed to be free of issues:

- Swap calculations for CasterNamedProperty and TargetNamedProperty. Fix for: Some effects that should be using the caster's stats incorrectly use the target's state and vice-versa.
- Fixed AchievementsManager initialization
- Ignore Steam Achievements with Null ID
- Lowered the severity of missing save file warnings ("DLC has no status in the DLCCache. Defaulting to unavailable") no longer prints a stacktrace
- Silenced keybinding conflict messages
- Skip LoadAssigneeAsync - prevents internal QA error message
