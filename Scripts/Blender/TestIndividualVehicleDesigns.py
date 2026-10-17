"""Pure design-contract tests; no Blender scene or user file is modified."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from IndividualVehicleDesigns import DESIGNS


def inside_polygon(point, polygon):
    y,z=point
    inside=False
    for a,b in zip(polygon,polygon[1:]+polygon[:1]):
        ya,za=a;yb,zb=b
        if (za>z)!=(zb>z) and y<(yb-ya)*(z-za)/(zb-za)+ya:
            inside=not inside
    return inside


class IndividualDesignTests(unittest.TestCase):
    def test_complete_individual_registry(self):
        self.assertEqual([d.identity[:2] for d in DESIGNS], [f'{i:02}' for i in range(2,21)])
        self.assertEqual(len({(d.cabin,d.panes) for d in DESIGNS}),19)

    def test_windows_are_inside_their_own_body_profile(self):
        for design in DESIGNS:
            with self.subTest(vehicle=design.identity):
                design.validate()
                for pane in design.panes:
                    for point in pane:
                        self.assertTrue(inside_polygon(point,design.cabin),(design.identity,point))

    def test_dimensions_and_accessories_are_physically_usable(self):
        for d in DESIGNS:
            with self.subTest(vehicle=d.identity):
                self.assertTrue(all(0<=channel<=1 for channel in d.paint))
                self.assertGreater(d.front-d.front_axle,d.radius+.05)
                self.assertGreater(d.rear_axle-d.rear,d.radius+.05)
                self.assertGreater(d.front_axle-d.rear_axle,4*d.radius)
                self.assertTrue(all(front>rear for front,rear in d.doors))
                if 'open_bed' in d.features:
                    self.assertGreater(d.cabin[-1][0]-d.rear,1.0)
                if 'cargo_box' in d.features:
                    self.assertGreater(d.cabin[-1][0]-d.rear,2.5)


if __name__=='__main__':
    unittest.main(argv=[__file__],verbosity=2)
