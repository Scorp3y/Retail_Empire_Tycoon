# Starter capital, narrow escalators and invisible unloading

## New games and stock

`GameData.StartingCapital` is 300000. The saved Game scene and default wallets use this amount. More importantly, `SaveManager` explicitly applies it in its no-save branch, so an older serialized wallet cannot silently produce a 50000 start. Existing saves restore their own balance.

The new-game construction stock is 53 units: five random shelves, twenty floor tiles, twenty walls, one entrance door, one unloading gate, two parking bays, one staircase, one lift, one reversible escalator and one down escalator. All four transitions are available in the building shop. Loading a save does not grant another starter kit.

## Escalator width

Both escalators have another 0.8 width multiplier: their total model scale on X is now 0.64 relative to the original, about 1.044 m in store coordinates. Each occupies 4×10 cells. Rails, steps and collision geometry narrow together; height, length, direction and the previous reduced speed remain unchanged. Stair and lift dimensions are unchanged in this update.

Existing saves retain their anchors and IDs. Any strip left over from a previously wider ceiling opening can be filled manually; player-built ceilings are not silently replaced.

## Frontage and icon

The left end of the starter entrance promenade previously extended past the building corner. Exactly two original promenade triangles are replaced with the same pavement beginning at the starter building's left edge. The rest of the walkway, lamp, planter, bench, roads and neighbouring buildings remain. The shared town prefab supplies this geometry in both worlds.

The construction hammer remains a native vector icon. Its broad striking face, thick handle and short claw use convex pieces, which the existing UI mesh renderer can triangulate correctly at small HUD sizes.

## Invisible unloading

The yellow pickup-stop prefab is removed from the City unloading checkpoint. No visible or raised replacement platform is created.

When entering City, the facade reads the currently installed ground-floor unloading gates. Every active gate gets an invisible interaction zone aligned with its actual position and normal. Its outward side is chosen relative to the store envelope. The zone places the vehicle centre beyond half the gate depth, half the vehicle length and 0.65 m clearance. Selecting a longer vehicle updates the zone immediately. Re-entering City after moving or rotating a gate reads the new pose.

The data path is: saved/placed gate → facade mapping → `UnloadingPose` → invisible `DeliveryCheckpoint` → stopped vehicle with correct rear-docking heading → existing transactional `DeliveryOrders.Unload` → warehouse inventory. Both proximity and heading use the current Rigidbody pose rather than the interpolated display transform. Without an active gate the unloading interaction is disabled. Normal construction still protects the last mandatory unloading gate from deletion.

## Checks and installation

`StarterAccessRefreshPlay.Run` tests the real no-file/load branches of `SaveManager` against a uniquely named JSON file inside the isolated project's Library directory. It verifies 300000 starting money, the 53-unit kit, existing-balance restoration without duplicate gifts, shop/storage availability, new widths, all four docking orientations, hidden geometry, adjustment for the heavy truck, wrong-heading rejection, actual cargo delivery and the no-active-gate case.

`MultiStoreyValidation.Run` checks placement, movement, protected openings, ceiling refunds, passenger movement and save restoration with the narrower geometry. `ConstructionWorkspacePlay.RunElevator` verifies the four-item starter stock and a real shopper's upstairs purchase/return lifecycle.

Generated outputs are installed only after input hash checks and comparison against the task baseline. Replaced assets receive backups and atomic file replacement. The player's actual save, preferences and Git history are not modified.
