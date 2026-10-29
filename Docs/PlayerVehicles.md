# Player vehicle upgrades

The approved reference is [Concept-v1.png](../ArtSource/PlayerVehicles/Concept-v1.png). Each upgrade has a separate visual prefab, native mesh assets, catalog definition and catalog preview. The original delivery pickup remains the free starting vehicle.

| Vehicle | Price | Payload | Cargo volume | Axles |
| --- | ---: | ---: | ---: | ---: |
| Delivery pickup | Free | 350 kg | 2 m³ | 2 |
| Compact van | $8,000 | 600 kg | 4 m³ | 2 |
| High-roof van | $16,000 | 1,000 kg | 8 m³ | 2 |
| Light box truck | $30,000 | 1,800 kg | 14 m³ | 2 |
| Medium cargo truck | $55,000 | 3,500 kg | 24 m³ | 2 |
| Heavy box truck | $90,000 | 6,000 kg | 40 m³ | 3 |

Prices are initial balancing values. The body colors and silhouettes follow the concept: ivory compact van, teal high-roof van, terracotta cab/ivory box, blue cab/grey curtain with buckles, green cab/long ivory box with tandem rear axles. Windows are opaque. Bevels, inset hubs, separate wheels, mirrors, lamps, cargo door locks and truck side guards provide detail without textures that require large downloads.

## Purchase and ownership

Drive to the existing city showroom, stop at its public interaction point and press **E**. Buy an upgrade or select a previously owned vehicle. The fleet retains every purchase; selection is not a trade-in. The store's Vehicles tab shows the active vehicle and directs the player to the showroom.

The input path is: showroom button → `PlayerVehicleFleet` validates cargo/ownership/balance → debit and selection → actual `DeliveryOrders` limits → existing atomic save → change event updates the city driving rig and the parked shop model. `GameData.vehicles` stores owned IDs and selected ID. Missing fleet data in an older save keeps the free pickup. A failed persistence callback restores payment, ownership, selection and capacities.

Vehicles can only be exchanged with an empty cargo hold. Paid supplier orders and warehouse stock do not block exchange. The same vehicle remains selected between store and city and after restoring a save. This avoids silently transferring paid cargo into a smaller hold.

## Driving and deliveries

The existing `PickupDrive` controller remains the single driving implementation. Selecting a model builds four or six WheelCollider contacts from its authored pivots, applies its mass, chassis envelope, suspension and speed/acceleration values, and uses the same steering, braking and terrain contact logic. The front wheels steer and all tires rotate. All dimensions use native town metres; the parked store display uses the existing 1:2 world mapping.

Actual order loading checks both selected payload and cubic volume. Enclosed vehicles hide the starting pickup's exposed cargo crates. The warehouse limit remains independent of truck size: unloading accepts only units that fit and preserves the rest in the vehicle. A full-warehouse retry cannot duplicate cargo.

## Dealership placement

The shared town prefab contains five instances under `Riverside car showroom/Player vehicle collection`: compact van on the front platform, high-roof van/light truck/medium truck inside the hall, heavy truck in the rear court. The five earlier exhibition sedans are removed from their baked visual batch through an exact triangle match. The unrelated parked customer sedan and showroom building/furnishings remain.

The authoring batch preserves all serialized components outside this showroom and writes a trimmed copy of the showroom visual mesh. It does not overwrite shared town meshes or regenerate streets, suppliers, train, traffic routes or residential models.

## Review and checks

`PlayerVehicleModelBatch.Build` authors the five separate prefabs and catalog assets, installs the display and captures native Unity renders. `PlayerVehicleValidation.Run` uses the copied Game scene with save and tutorial writers removed, then exercises the actual Game → City → Game journey, showroom purchase buttons, ownership JSON, load limits and native wheel physics.

Reports and renders are installed into `Docs/PlayerVehicles/`. Source scripts, asset GUIDs and tested dependency hashes are checked before generated assets are installed. Installation backs up the replaced town prefab. No player save, preferences, Git commit or push is part of this operation.

## Current scope

One vehicle is active at a time. There is no vehicle resale, interior/cab camera or walk-in showroom gameplay. Cargo capacity is a gameplay limit rather than a simulation of individual boxes packed in the freight body. The starting private parking paint is retained; the largest truck has a small bumper overhang, so custom parking should have clear space at both ends. This update does not introduce truck trailers or AI freight drivers.
