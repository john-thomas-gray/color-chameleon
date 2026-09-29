"""Measure music loudness and generate attenuation-only playback gains.

LUFS (Loudness Units relative to Full Scale) measures integrated perceived loudness.
dBTP (decibels true peak) measures peak headroom.
Original recordings are never modified; FFmpeg measures the gain-adjusted output.
"""

import argparse
import hashlib
import json
import math
from pathlib import Path
import subprocess


def measure(path, gain_db=0):
    result = subprocess.run(
        ["ffmpeg", "-hide_banner", "-nostdin", "-nostats", "-i", str(path),
         "-map", "0:a:0", "-af", f"volume={gain_db}dB,loudnorm=print_format=json",
         "-f", "null", "-"], capture_output=True, text=True, check=True)
    start = result.stderr.rfind("{")
    if start < 0:
        raise ValueError(f"No loudness measurement returned for {path}")
    data, _ = json.JSONDecoder().raw_decode(result.stderr[start:])
    loudness, peak = float(data["input_i"]), float(data["input_tp"])
    if not all(math.isfinite(value) for value in (loudness, peak)):
        raise ValueError(f"Silent or invalid music recording: {path}")
    return loudness, peak


def common_target(measurements, requested=-14, ceiling=-1):
    if not measurements or not math.isfinite(requested) or not math.isfinite(ceiling) or ceiling > 0:
        raise ValueError("Finite loudness measurements and a nonpositive peak ceiling are required")
    limits = [requested]
    for loudness, peak in measurements:
        if not math.isfinite(loudness) or not math.isfinite(peak):
            raise ValueError("Loudness measurements must be finite")
        limits.extend((loudness, loudness + ceiling - peak))
    # Lower the common target when necessary; never boost or compress a recording.
    return math.floor(min(limits) * 100) / 100


def generate(resources, requested=-14, ceiling=-1):
    paths = sorted(path for path in resources.rglob("*")
                   if path.suffix.lower() in {".wav", ".mp3", ".ogg", ".flac", ".aiff", ".aif", ".m4a"})
    if len({path.stem for path in paths}) != len(paths):
        raise ValueError("Music resource names must be unique")
    measurements = [measure(path) for path in paths]
    target = common_target(measurements, requested, ceiling)
    tracks = []
    for path, (loudness, peak) in zip(paths, measurements):
        gain = round(target - loudness, 2)
        actual_loudness, actual_peak = measure(path, gain)
        if abs(actual_loudness - target) > .1 or actual_peak > ceiling + .05:
            raise ValueError(f"Normalized output failed verification: {path.name}")
        tracks.append({"resourceName": path.stem,
                       "sourceSha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                       "integratedLufs": loudness, "truePeakDbtp": peak, "gainDb": gain})
        print(f"{path.name}: {loudness:.2f} -> {actual_loudness:.2f} loudness; "
              f"gain {gain:.2f} decibels; true peak {actual_peak:.2f}", flush=True)
    return {"version": 1, "targetLufs": target, "truePeakCeilingDbtp": ceiling, "tracks": tracks}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("resources", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--target", type=float, default=-14)
    parser.add_argument("--peak-ceiling", type=float, default=-1)
    parser.add_argument("--check", action="store_true", help="Remeasure and verify an existing profile without writing")
    args = parser.parse_args()
    result = generate(args.resources, args.target, args.peak_ceiling)
    if args.check:
        if json.loads(args.output.read_text(encoding="ascii")) != result:
            raise ValueError("Music loudness profile is stale; regenerate it")
    else:
        args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="ascii")


if __name__ == "__main__":
    main()
