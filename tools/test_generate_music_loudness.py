import unittest

from generate_music_loudness import common_target


class MusicLoudnessTests(unittest.TestCase):
    def test_default_target_attenuates_loud_music(self):
        self.assertEqual(common_target([(-7, 1), (-9, .5)]), -14)

    def test_quiet_track_lowers_every_tracks_target(self):
        self.assertEqual(common_target([(-20, -3), (-8, 0)]), -20)

    def test_peaks_lower_common_target_without_compression(self):
        self.assertEqual(common_target([(-18, 2), (-10, 0)]), -21)

    def test_invalid_measurements_rejected(self):
        for values in ([], [(float("nan"), 0)], [(-10, float("inf"))]):
            with self.assertRaises(ValueError):
                common_target(values)

    def test_invalid_target_or_ceiling_rejected(self):
        for requested, ceiling in ((float("nan"), -1), (-14, float("inf")), (-14, 1)):
            with self.assertRaises(ValueError):
                common_target([(-8, 0)], requested, ceiling)


if __name__ == "__main__":
    unittest.main()
