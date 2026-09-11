"""Generate two-times tile-skin textures without overwriting source PNGs."""

from __future__ import annotations

import argparse
from pathlib import Path

import cv2
import numpy as np


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True, help="Source skin directory")
    parser.add_argument("--output", type=Path, required=True, help="Separate output directory")
    parser.add_argument("--model", type=Path, required=True, help="FSRCNN x2 model file")
    parser.add_argument("--limit", type=int, help="Process only this many images")
    return parser.parse_args()


def upscale_image(image: np.ndarray, super_resolution: cv2.dnn_superres.DnnSuperResImpl) -> np.ndarray:
    if image.ndim == 2:
        color = cv2.cvtColor(image, cv2.COLOR_GRAY2BGR)
        alpha = None
    elif image.shape[2] == 4:
        color = image[:, :, :3]
        alpha = image[:, :, 3]
    else:
        color = image
        alpha = None

    upscaled_color = super_resolution.upsample(color)
    if alpha is None:
        return upscaled_color

    upscaled_alpha = cv2.resize(
        alpha,
        (upscaled_color.shape[1], upscaled_color.shape[0]),
        interpolation=cv2.INTER_LANCZOS4,
    )
    return np.dstack((upscaled_color, upscaled_alpha))


def main() -> None:
    args = parse_args()
    if not args.input.is_dir():
        raise SystemExit(f"Input directory does not exist: {args.input}")
    if not args.model.is_file():
        raise SystemExit(f"Model file does not exist: {args.model}")

    super_resolution = cv2.dnn_superres.DnnSuperResImpl_create()
    super_resolution.readModel(str(args.model))
    super_resolution.setModel("fsrcnn", 2)

    source_files = sorted(args.input.rglob("mj_hai_*.png"))
    if args.limit is not None:
        source_files = source_files[:args.limit]
    if not source_files:
        raise SystemExit("No tile skin PNGs found.")

    for source in source_files:
        image = cv2.imread(str(source), cv2.IMREAD_UNCHANGED)
        if image is None:
            raise SystemExit(f"Unable to read image: {source}")
        output = args.output / source.relative_to(args.input)
        output.parent.mkdir(parents=True, exist_ok=True)
        if not cv2.imwrite(str(output), upscale_image(image, super_resolution)):
            raise SystemExit(f"Unable to write image: {output}")
        print(output)


if __name__ == "__main__":
    main()