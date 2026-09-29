"""Generate sample-clock beat timestamps for Unity's soundtrack resources."""

import argparse
import json
from pathlib import Path

import librosa
import numpy as np


def detect_beats(audio, sample_rate, minimum_tempo, maximum_tempo, window_seconds=8, onset_max_frequency=None):
    hop = 256
    envelope = librosa.onset.onset_strength(y=audio, sr=sample_rate, hop_length=hop,
                                            n_mels=32, fmax=onset_max_frequency)
    tempogram = librosa.feature.tempogram(onset_envelope=envelope, sr=sample_rate,
                                          hop_length=hop, win_length=int(window_seconds * sample_rate / hop))
    frequencies = librosa.tempo_frequencies(len(tempogram), sr=sample_rate, hop_length=hop)
    usable = (frequencies >= minimum_tempo) & (frequencies <= maximum_tempo)
    frequencies = frequencies[usable]
    likelihood = np.maximum(tempogram[usable], 1e-6) ** 2
    likelihood /= likelihood.sum(axis=0, keepdims=True)
    # Decode a coherent tempo path so brief fills don't become false tempo changes.
    transition = .98 * np.eye(len(frequencies)) + .02 / len(frequencies)
    states = librosa.sequence.viterbi(likelihood, transition)
    tempo = frequencies[states]
    _, beats = librosa.beat.beat_track(onset_envelope=envelope, sr=sample_rate,
                                      hop_length=hop, bpm=tempo, tightness=100, trim=False, units="time")
    duration = len(audio) / sample_rate
    beats = np.asarray(beats)[np.asarray(beats) <= duration]
    if len(beats) < 2 or not np.isfinite(beats).all() or np.any(np.diff(beats) <= 0):
        raise ValueError("Audio did not yield a usable sequence of beats")
    return beats


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("audio", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--min-tempo", type=float, required=True)
    parser.add_argument("--max-tempo", type=float, required=True)
    parser.add_argument("--window-seconds", type=float, default=8)
    parser.add_argument("--onset-max-frequency", type=float)
    args = parser.parse_args()
    if not 0 < args.min_tempo < args.max_tempo:
        parser.error("Tempo limits must be positive and ordered")
    audio, rate = librosa.load(args.audio, sr=22050, mono=True)
    if args.window_seconds <= 0:
        parser.error("Analysis window must be positive")
    beats = detect_beats(audio, rate, args.min_tempo, args.max_tempo, args.window_seconds, args.onset_max_frequency)
    duration = len(audio) / rate
    result = {
        "version": 1,
        "durationSeconds": round(duration, 6),
        "generator": "librosa 0.11.0 variable-tempo beat tracker",
        "minimumTempo": args.min_tempo,
        "maximumTempo": args.max_tempo,
        "windowSeconds": args.window_seconds,
        "onsetMaxFrequency": args.onset_max_frequency,
        "beatTimes": np.round(beats, 6).tolist(),
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="ascii")
    print(f"{args.audio.name}: {len(beats)} beats across {duration:.2f} seconds")
    for start in range(0, int(duration), 30):
        intervals = np.diff(beats)[(beats[:-1] >= start) & (beats[:-1] < start + 30)]
        if len(intervals):
            print(f"  {start:3d}-{min(start + 30, duration):6.1f}s: {60 / np.median(intervals):6.2f} beats/minute")


if __name__ == "__main__":
    main()
