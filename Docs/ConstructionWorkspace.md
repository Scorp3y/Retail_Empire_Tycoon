# Construction workspace and English-only development build

## Store size and saved games

The construction kit is 10% larger than the preceding StorePolish kit. Private prefab variants live in `Assets/Art/ConstructionWorkspace`; imported source models are unchanged. Model bounds, alignment offsets, construction cell size and storey height use the same scale. The total construction scale is now 1.65 relative to the original kit, with a 0.275055 cell and a 1.65 storey.

`ShopProportions` version 2 distinguishes version-one saves (0.25005 cells) from older saves (0.1667 cells). Interior grid addresses, upper-floor addresses, stock identities, money and inventory are preserved. Outdoor asphalt and furniture are reprojected to keep the driveway at the public road. Parking is fitted to available asphalt without covering doors or neighbouring approaches. Conversion works on a detached snapshot and commits only after a successful fit. Repeating it has no effect; an impossible conversion does not overwrite the original data.

City uses the same uniform Game-to-City coordinate mapping. There is no automatic roof. Only saved, player-built floor/ceiling surfaces are copied. Interior fixtures and their actual stock are also represented, while temporary editing highlights are excluded. The store remains closed to driving through its interior; removing the visual roof does not remove the collision hull.

## Construction workflow

The hammer is the rightmost lower-right action, after the warehouse. It opens one workspace containing:

- an owned-item catalog with category filters and quantities;
- placement of shelves, structures, decorations and floor materials;
- selection, movement, rotation, confirmation, cancellation and return to storage;
- the personal-parking relocation tool;
- floor/ceiling brushes, tile removal and floor navigation.

Warehouse windows show stock but do not place furniture or offer editing/private-parking buttons. The E shortcut opens construction; U opens warehouse products. Catalog purchasing remains separate and still adds actual owned supplies. Rotation continues to use the configured R/Q bindings.

Floor navigation uses the visible down/up chevrons and the keyboard Down/Up arrows. While constructing, those keys do not pan the camera; WASD and mouse navigation remain available. Closing the workspace cancels its current preview, restores floor 1 and returns ordinary input.

Icons extend the existing normalized vector silhouette system instead of mixing raster styles. The new hammer, move, rotation, floor arrows, surface brushes, eraser and parking icons remain sharp at different UI scales.

## Upper-floor presentation

The grid uses the actual surface elevation plus a 2 mm depth bias. An unbuilt upper surface is at the wall/storey plane, not at a floating ground-floor offset. Upper floor slabs also use that plane; NPC landing heights and elevator/stair routes use the corresponding offset. Ground-floor tiles retain their existing slab offset.

Camera cutaway considers only walls on the currently edited floor. Editing floor 2 does not hide floor-1 walls. Upper-floor painting operates on independent layer data and does not remove, move or replace ground-floor objects or tiles. Visibility changes never modify physics, occupancy or saved construction.

## English-only policy

This development build always selects English, including when an older language preference is Russian. The stored preference and Russian translation tables are retained for future use. The settings language control indicates the fixed English policy. Runtime construction errors, commerce and staffing notices, driving/map/supplier UI and static scene labels are English, as are current item display names. Font selection and sizes follow the English settings.

## Verification and recovery

Authoring and native Unity checks run in `Library/CityUpdateQA/IsolatedProject`, without the player save writer. Reports and captured frames are installed in `Docs/ConstructionWorkspace`. Original code, localization scripts and affected assets are backed up in `Library/ConstructionWorkspaceUpdate/Before`; the installer additionally backs up assets immediately before replacement. No player save reset, commit or push is part of this update.

Old handmade saves with insufficient asphalt can reject conversion rather than silently delete their parking or inventory. The diagnostic explains that more free space is needed. Cars and the surrounding town are not enlarged, and passenger boarding animations are unchanged.

Final native checks passed: the full customer-car shopping cycle, actual construction buttons and floor arrows, warehouse read-only inventory, upper-floor painting with unchanged ground-floor data/renderers, aligned upper slabs, both save-schema conversions and repeat-conversion safety. English was checked on construction, upper-floor construction, catalog, warehouse, settings, driving HUD, town map and supplier assortment; an attempted legacy locale change returns to English. Static-copy migration found no remaining Russian labels in the four playable scenes. All three storey transports and the 20-car traffic fleet in five districts passed their regression checks.

The installed-asset receipt contains 101 files. Unity held a shelf asset memory-mapped during installation, so replacement used same-directory staging and atomic file replacement, retaining backups rather than closing the user's editor. Complete pre-update sources/assets are in `Library/ConstructionWorkspaceUpdate/Before`; additional installation backups are in the timestamped `InstalledBefore_*` folders and `AtomicBefore`. No player save, commit or push was performed. Restart Play Mode and reload the changed scene to load the new serialized grid and prefab references.
