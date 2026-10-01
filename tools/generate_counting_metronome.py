"""Generate an original 100-measure spoken calibration track using macOS speech."""
from array import array
from pathlib import Path
import math
import subprocess
import tempfile
import wave

TEMPO = 115.03
MEASURES = 100
RATE = 44100
OFFSET = .08


def main():
    output = Path(__file__).resolve().parents[1] / "Assets/Resources/CountingMetronome.wav"
    beat = 60 / TEMPO
    with tempfile.TemporaryDirectory() as folder:
        folder = Path(folder)
        words = []
        for word in ("one", "two", "three", "four"):
            spoken = folder / (word + ".aiff")
            subprocess.run(["say", "-v", "Samantha", "-r", "240", "-o", str(spoken), word], check=True)
            raw = subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(spoken),
                                           "-ar", str(RATE), "-ac", "1", "-f", "s16le", "-"])
            samples = array("h", raw)
            active = [i for i, value in enumerate(samples) if abs(value) > 120]
            if not active:
                raise ValueError("Speech synthesis produced silence")
            samples = samples[active[0]:min(len(samples), active[-1] + int(.015 * RATE))]
            if len(samples) > int(beat * RATE * .9):
                raise ValueError(f"Spoken {word} does not fit within a beat")
            peak = max(abs(value) for value in samples)
            words.append([value / peak * .65 for value in samples])
        result = array("h", [0]) * round((OFFSET + MEASURES * 4 * beat) * RATE)
        for index in range(MEASURES * 4):
            start = round((OFFSET + index * beat) * RATE)
            for i, value in enumerate(words[index % 4]):
                result[start + i] = round(value * 32767)
            for i in range(int(.015 * RATE)):
                frequency = 1320 if index % 4 == 0 else 880
                click = .18 * math.sin(2 * math.pi * frequency * i / RATE) * math.exp(-i / (RATE * .003))
                result[start + i] += round(click * 32767)
        raw_track = folder / "counting.wav"
        with wave.open(str(raw_track), "wb") as target:
            target.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
            target.writeframes(result.tobytes())
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(raw_track),
                        "-af", "loudnorm=I=-13:TP=-1:LRA=7", "-ar", str(RATE), "-c:a", "pcm_s16le", str(output)], check=True)
        with wave.open(str(output), "rb") as track:
            assert abs(track.getnframes() / track.getframerate() - (OFFSET + 400 * beat)) < .001
        print(f"Created {output}: 400 spoken beats, {MEASURES} measures, {TEMPO} beats per minute")


if __name__ == "__main__":
    main()
