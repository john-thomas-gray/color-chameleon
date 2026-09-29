import unittest

import librosa
import numpy as np

from generate_beat_map import detect_beats


class BeatMapGenerationTests(unittest.TestCase):
    def test_changing_tempo_click_track(self):
        expected = []
        time = .2
        while time < 44:
            expected.append(time)
            tempo = 120 if time < 14 else 90 if time < 28 else 150
            time += 60 / tempo
        audio = librosa.clicks(times=np.array(expected), sr=22050, length=45 * 22050)
        beats = detect_beats(audio, 22050, 80, 160)
        for start, end, tempo in [(3, 11, 120), (18, 25, 90), (32, 41, 150)]:
            segment = beats[(beats > start) & (beats < end)]
            self.assertAlmostEqual(60 / np.median(np.diff(segment)), tempo, delta=4)
            errors = [min(abs(np.array(expected) - beat)) for beat in segment]
            self.assertLess(np.median(errors), .05)
        self.assertTrue(np.all(np.diff(beats) > 0))


if __name__ == "__main__":
    unittest.main()
