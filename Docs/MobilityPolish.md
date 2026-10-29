# Transport and showroom fixes

## Lift floor

The previous cabin floor ended at the same elevation as the painted ground floor. Two surfaces occupied the same plane, producing flickering patches through the shaft glazing. The new cabin floor is 9 mm above the painted surface. An opaque shaft base sits at +2 mm and the entrance sill at +12 mm. The safety strip stays below the passenger's existing foot offset. All geometry remains inside the lift's 5×5 placement footprint.

Floor paint data and inventories are preserved. The upper shaft opening still returns ceiling materials to storage; it cannot be painted closed while a connection occupies it.

## Narrower stairs and escalators

Existing prefab IDs and references are retained. Model width is multiplied by 0.8, including rails, steps and physical colliders. Stair occupancy changes from 5×10 to 4×10; escalator occupancy changes from 6×10 to 5×10. Length and floor height are unchanged. Saved anchors still load against the updated definitions. Previously cut openings can be wider than the new model; freed cells can be filled with flooring manually.

The existing escalator remains reversible so stores using only one escalator retain a return route. The new `escalator_down_01` is a separate, purchasable **Down escalator**. It has the same price, shared meshes and narrower dimensions, with a permanently descending belt and downward-only customer navigation. The original starter kit remains one staircase, one lift and one reversible escalator; existing saves receive no duplicate grants.

Belt cycles change from 0.22/s to 0.176/s. Customer travel on escalator route segments uses 80% of normal walking speed. `ShopCharacter` spends a time budget across route segments, so this adjustment does not change ordinary walking speed. Direction is checked when adding floor-to-floor links to the path search and when boarding the belt. Customers only choose an upstairs shelf if a return route to checkout exists.

## Vehicle catalog

Store → Vehicles now creates six separate cards: the free pickup and all five upgrades. Every card uses its own name, icon, payload, volume, price and ownership state. Buying/selecting calls the same `PlayerVehicleFleet` used by the city showroom, then refreshes the catalog. Payment, selection, parked model, cargo capacity and saves remain one shared transaction. A loaded vehicle cannot be exchanged.

## Yellow showroom marker

The riverside showroom had received the interaction checkpoint, while the older dealership retained its visible yellow stop zone. A secondary showroom entrance now serves that older stop zone at native coordinates (237, −92). Both entrances open the same vehicle catalog. The town map continues to show one showroom destination. A visible yellow zone is also present at the riverside interaction point.

`CityWorld.Interact` resolves the usable checkpoint from the current vehicle pose rather than relying on the preceding frame's cached result. Showroom proximity measures distance to the vehicle chassis, allowing a long truck's nose to reach the marker before its origin does. Only horizontal speed blocks interaction; harmless suspension motion does not keep the menu closed.

## Verification and installation

`MobilityPolishPlay.Run` uses a copy of Game with save/tutorial writers removed. It checks narrow dimensions and occupancy, the elevated lift floor, a real customer descending on the new escalator, six vehicle cards and a real store purchase, both showroom entrances, and Game → City → Game.

`MultiStoreyValidation.Run` covers existing two-way transports, ceiling refunds, protected landings, movement/rotation and save restoration. `ConstructionWorkspacePlay.RunElevator` covers a real shopper's upstairs purchase and return to the same car through the lift.

`Scripts/InstallMobilityPolish.ps1` requires fresh reports, checks dependency hashes and compares stable Unity YAML objects in the town prefab. Existing town objects are preserved; only parent child lists and the additional showroom entrance array may change. Changed assets receive backups before atomic replacement. No player save, preferences, commit or push is part of the update.
