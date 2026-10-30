# Visual review

Iteration 01: inspected empty and stocked scenes. Bakery depth rejected the long baguette at real scale; increased the usable tray depth rather than rescaling food. Freezer floor was too deep to reach/read from the camera. Two lid frame heights and lower shell joints needed separation.

Iteration 02: deeper bakery trays, raised freezer floor with 0.25 m clearance, separate lid heights/grips, inset base shell and small cabinet details. Inspected both renders. Slots, root/collider bounds and all fixture budgets pass.

Iteration 03: retained glass alpha 0.25 / roughness 0.20 and added transmission with near-air IOR so it reads as clear glass rather than a diffuse plastic film. Inspected both final renders: real baguettes fit, floor products remain visible through the closed lids, curved cabinet sides have real thickness. The final glass transmission appearance still requires renderer-specific setup at engine import.
