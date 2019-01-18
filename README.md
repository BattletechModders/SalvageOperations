# SalvageOperations

BattleTech mod to revamp how 'Mech salvage is generated and how 'Mechs are assembled.

## What it does currently

### 'Mech Salvage Drops

* 'Mechs now drop from 0 pieces all the way up to the `DefaultMechPartMax` set by difficulty
* Amount dropped depends on the current status of each of the locations on the 'Mech
* Currently, you get `1/2 * DefaultMechPartMax` for the CT and `1/12 * DefaultMechPartMax` for all other non-head parts
* Partial parts are rounded up at .75 and above, down otherwise

### 'Mech Assembly

* You are not forced into assembling 'Mechs
* An event popup happens when you can put together a 'Mech
* Related variants of a 'Mech can be used together to create one of those variants
* You can choose between the top three (in pieces) variants to assemble

### What's not done

* Show number of related variant pieces in salvage screen
* Settings for how the thing works
* Currently 'Mechs are built into storage, not sure if that's desirable

## Future plans

* Some sort of cost for assembling 'Mechs
* Optional piece or related mod to force 'Mech missing bits to require salvaged mech pieces
* Additional event flavor
* Salvage bundles from conversation in Discord with FrostRaptor
