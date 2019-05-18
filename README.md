# SalvageOperations

BattleTech mod to revamp how 'Mech salvage is generated and how 'Mechs are assembled.

## What it does currently

### 'Mech Salvage Drops

* 'Mechs now drop from 0 pieces all the way up to the `DefaultMechPartMax.'
* Amount dropped depends on what parts were destroyed from the 'Mech during the mission.
* Currently, you get `1/2 * DefaultMechPartMax` for the CT and `1/12 * DefaultMechPartMax` for all other non-head parts.
  These values are adjustable from the Settings in the mod.json.
* Partial parts are rounded up at 0.75 and above, down otherwise. This setting is also adjustable.
* The salvaging screen now shows how many total parts between all variants you have when choosing salvage. 
* 'Mechs that are included in the Exclusion list in the Settings are marked as a (R) for Rare. 

### 'Mech Assembly

* All 'Mechs that are assembled are placed directly into Storage.
* You are not forced into assembling 'Mechs.
* An event popup happens when you can put together a 'Mech.
* Related variants of a 'Mech can be used together to create one of those variants.
* You can choose between the top three (in pieces) variants to assemble.
* 'Mechs can be excluded from this Assembly algorithm and can only be assembled using parts from their specific variant.
* The fewer pieces used to assemble a 'Mech, the longer it will take to ready from Storage.
  `Time to Assemble = (Standard Ready Time) * (Ready Mech Delay Factor) * (Parts Required to Assemble + 1 - Parts used from main Variant)`

### Hotkeys for 'Mech Assembly

* Shift-(right mouse click) a 'Mech in the Storage to attempt to assemble that variant from available parts. 
* Shift-(right mouse click) the Storage tab to assemble using all availabe parts in your inventory.

### Settings

Setting           | Description
------------------|------------
ReadyMechDelayFactor: 1 | This value adjusts how long it takes to assemble a 'Mech
CT_Value: 0.5 | Percent of 'Mech salvage from not destroying the CT.
Other_Parts_Value: 0.0833333 | Percent of 'Mech salvage from all other salvaged non-head parts.
Rounding_Cutoff: 0.75 | Partial parts are rounded up at 0.75 and above, down otherwise.
SalvageValueUsesSellPrice: true |  Does the salvage screen show the salvage value based upon how much it costs to actually sell the part?
ReplaceSalvageLogic: true | Replace the in-game salvage logic with the salvage algorithm from this mod. 
VariantExceptions: {mechdefs} | Listing of 'Mechs by mechdefs that can not be combined to salvage.

