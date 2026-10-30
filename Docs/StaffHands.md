# Natural hand colour with the original arm shape

Current editable sources are `ArtSource/StaffV4/<Role>/<Role>.blend`, built with `Scripts/Blender/BuildStoreStaffV2.py --skin-hands`.

The employee arm geometry is restored from the original mini-character base. The source builder verifies that hand recolouring does not change any base vertex position. The extra rounded arm reconstruction from StaffV3 is not used. Rounded apron and pocket details remain.

Security, stocker and driver hands use the same skin palette cell as their respective heads. Sleeves retain the profession's clothing colour. Whole hand/wrist faces receive a single skin-cell UV coordinate: recolouring only selected corners of a triangle would interpolate unrelated colours across the atlas. Cashier and cleaner retain their original skin colours.

Skeletons, bone weights, role/save IDs and runtime duties remain unchanged. The isolated import updates existing prefabs and portraits without changing their GUIDs. `Scripts/InstallStaffStyle.ps1 -Revision StaffHands` installs verified outputs with baseline checks and backups. Current Unity views and validation reports are in `Docs/StaffHands`.
