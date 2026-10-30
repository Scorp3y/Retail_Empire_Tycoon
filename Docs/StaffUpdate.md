# Store employees and autonomous collection

## Art and duties

Current Blender sources live under `ArtSource/StaffV4`, authored with `Scripts/Blender/BuildStoreStaffV2.py --skin-hands` from the established mini-character base. Original arm shapes are retained and male hands match their head skin tones; rounded clothing details remain. Earlier source collections are preserved. Five skinned FBX exports, Unity prefabs and recruitment portraits live under `Assets/Art/Staff` and `Assets/Art/ShopUi/Items`. Each model has fourteen rigid-weighted bones, the shopper idle/walk contract and an individual working action. Work tools are separate skinned accessories and appear during the profession's action. See `Docs/StaffHands.md` and `Docs/StaffHands/StaffLineup.png` for the current visual revision.

Cashier, security, stocker and cleaner retain their existing responsibilities and reservations. `StoreOperations.ModelFor` selects the profession's appearance. Their existing numeric save IDs remain 0–3; Driver is appended as 4. Four-slot staff saves restore with zero drivers. There is one driver per selected player vehicle, with a default hiring cost of $500 and wages of $20/minute while the store is open.

## Delivery ownership and data path

`DriverDelivery` owns the driver and one journey. `StaffRoster` owns employment, `PlayerVehicleFleet` owns exclusive use of the selected vehicle, `TownRoadTraffic` owns public-road movement, and `DeliveryOrders` retains paid orders and cargo. A delivery does not grant free stock or invent a timed off-map result.

The path is: paid order at a supplier → eligible package and available vehicle → visible employee walking to the personal parking bay → boarding → private exit → directed road lanes and existing signals → the supplier's authored loading court → package transferred from Depot to Pickup → return via the actual court exit and public roads → parked unloading → existing warehouse inventory transaction → driver exits and releases the vehicle.

The same selected model is used throughout; four- and six-wheel models retain rolling and steering visuals. While the driver owns the car, manual City departure, vehicle exchange and dismissal are rejected. The selected parking anchor is preserved. A delivery finishes even if the store closes mid-trip; a new trip requires an open store, paid staff and warehouse space.

Road routes respect lane direction. Supplier courts already define street access, entrance, intermediate approach, staging and loading poses in native town coordinates. The driver uses those definitions and reverse docking at the loading bay. The farm's shared two-way access has separate right-hand entry/exit tracks; compact city yards retain their authored central track between furniture and signs. Private exits request a gap from public traffic before entering it; existing occupants can clear, while later cars wait outside the merge. Clearance uses the actual lane and vehicle envelopes, so an opposite-lane car does not incorrectly block a safe merge.

## Tracking and save continuity

Store → Staff → Track driver opens the actual town road map, with a live vehicle position and road route. Watch vehicle follows the real 3D car/employee outside the normal construction-camera bounds. Back to store or Esc restores the prior camera pose. The map does not advance an independent delivery timer.

`GameData.driverJourney` saves activity, supplier, native position/rotation, stage and private-access progress. Paid cargo remains in the shared delivery ledger and is saved together with money and warehouse stock. Loading preserves active private legs; road legs reconnect to a nearby directed lane. No offline travel or offline wages are applied.

## Limits and checks

The driver collects already paid orders; automatic purchasing/reordering is not enabled. Capacity is checked by both weight and volume. A full warehouse leaves remaining paid cargo in the car and reports the reason until storage is freed. The existing compact supplier courts accept vehicles up to their authored maximum length (currently 6.8 m); selecting a longer truck requires switching back to a smaller vehicle for those yards. Goods are never discarded to bypass a limit.

`StaffUpdateBatch.Build` imports and binds only this update in the isolated project. `StaffUpdatePlay.Run` verifies the four working duties, old staff saves, driver hiring limits, vehicle exclusivity, visible trip stages, map/follow camera, saved journey restoration and an actual paid order round trip to each of the six suppliers. These checks use the real road/yard geometry and cargo transactions, with player-save writers removed from the sandbox.

The older broad gameplay suite stops on its unrelated shelf-product preview-scale assertion before reaching the employee checks. The task-specific duty suite exercises all four jobs directly without that legacy fixture assumption. Validation reports and screenshots are in `Docs/StaffUpdate`. Installation checks input/output hashes and baseline scene/theme hashes, then atomically replaces verified files with backups. No player save or Git history is modified.
