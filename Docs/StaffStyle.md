# Employees matching the current NPC pack

The five employee models now use the same original mini-character geometry as the live shopper roster. They are not independently approximated chibi figures. Heads, authored facial polygons, hair, hands, boots, relaxed arm positions, UV shading and the fourteen-bone generic skeleton come from the existing Kenney mini-character assets already used by `BuildShopperFleet.py`.

`Scripts/Blender/BuildStoreStaffV2.py --skin-hands` builds the current revision in `ArtSource/StaffV4/<Role>/<Role>.blend`. It retains the original arm shapes, gives male employees natural hands matching their heads, and keeps rounded clothing details. Previous source collections remain preserved. `ArtSource/StaffV4/Manifest.json` records base mesh identities, source/export hashes and hand palette cells. The Unity staff atlas is an exact copy of the current shopper palette; no customer assets or global materials are changed. See `Docs/StaffHands.md` for the current appearance.

The Unity comparison places an actual current shopper on the left and the five employee prefabs alongside it at the same normalized height, camera angle and lighting. See `Docs/StaffStyle/StaffComparison.png`; working accessories and animation poses are shown in `StaffWorking.png`.

Only staff FBX files, controllers, prefabs, their palette/material, recruitment portraits and portrait assignments are replaced. Existing asset GUIDs remain stable, so scene role assignments and save IDs remain valid. Cashier, security, cleaner, stocker and driver logic, vehicle ownership, paid orders and delivery routes are unchanged.

`StaffUpdateBatch.RefreshModelsAndPlay` performs the isolated import and actual job/journey checks. The guarded installer compares source/dependency hashes and asset baselines, requires unchanged GUIDs, and atomically replaces the verified appearance files with backups. Player saves, original shopper assets, scene geometry and Git history are not modified.
