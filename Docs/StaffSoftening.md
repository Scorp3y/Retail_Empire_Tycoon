# Soft staff hands, sleeves and clothing details

This is the archived StaffV3 hand-shape revision. The current StaffV4 restores the original arm shape and fixes hand colour; see `Docs/StaffHands.md`. Rounded aprons and pockets are retained.

Current editable sources are `ArtSource/StaffV3/<Role>/<Role>.blend`. Generate them with `Scripts/Blender/BuildStoreStaffV2.py --project <project> --soft-edges`. Previous source collections remain preserved.

Only employee appearances change. The head, face, hair, torso, boots, palette, skeleton and role assignments retain the established mini-character style. Disconnected arm shells are replaced with closed rounded sleeves, covered elbow joints and softly bevelled mitten shapes. Hand centres and bounds come from the previous weighted forearm geometry; all fourteen original bones and the existing animation parameters remain. Explicit weight checks reject unweighted or multiply weighted vertices before export.

Thin aprons and pockets use rounded outlines in the visible plane, so their corner radius is independent of their small depth. Shirt pockets use the garment's own palette cell. Three bevel segments keep the silhouettes soft with a small polygon budget. No global customer meshes or materials are edited.

The Unity preview spaces employees apart so hands can be inspected. Actual imported models are shown in `Docs/StaffSoftening/StaffLineup.png` and `StaffWorking.png`; the comparison includes an unchanged customer as a style reference.

The isolated Unity import verifies the generic rigs, updates all five portraits and exercises the four shop duties plus the driver's boarding, road trips, delivery and save restoration. `Scripts/InstallStaffStyle.ps1 -Revision StaffSoftening` verifies fresh reports, hashes, unchanged asset GUIDs and a current appearance baseline, then installs with atomic replacements and backups. Runtime job logic, scenes and player saves are preserved.
