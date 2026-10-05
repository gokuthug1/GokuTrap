#!/usr/bin/env python3
"""
GokuTrap asset generator.

Maintains and synchronizes all brand-owned raster assets, multi-size .ico packages,
and showcase screenshots from the master artwork in Images/:

  GokuTrap/GokuTrap.ico                    app / window icon (16-256)
  GokuTrap/GokuTrap.png                    master logo (512x512)
  GokuTrap/Resources/IconGokuTrap.ico      bootstrapper icon (default)
  GokuTrap/Resources/IconGokuTrapAlt.ico   bootstrapper icon (alternate, ultra instinct / ice blue)
  Images/Showcase-Dark.png                 README dark showcase
  Images/Showcase-Light.png                README light showcase

Requires: pip install pillow
Usage:    python Scripts/GenerateAssets.py
"""
from __future__ import annotations

import os
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
APP = ROOT / "GokuTrap"
RES = APP / "Resources"
IMAGES = ROOT / "Images"

ICO_APP = [16, 24, 32, 48, 64, 128, 256]
ICO_BOOT = [16, 24, 32, 48, 64, 128]


# --------------------------------------------------------------------------- helpers

def save_ico(img: Image.Image, path: Path, sizes):
    path.parent.mkdir(parents=True, exist_ok=True)
    frames = [img.resize((s, s), Image.LANCZOS) for s in sizes]
    frames[-1].save(path, format="ICO", sizes=[(s, s) for s in sizes], append_images=frames[:-1])


def update_showcase(image_path: Path, logo: Image.Image, dark: bool):
    """Rebrand showcase screenshots seamlessly to GokuTrap."""
    if not image_path.exists():
        return
    im = Image.open(image_path).convert("RGBA")
    d = ImageDraw.Draw(im)

    # 1. Clean up splash badge area
    splash_col = (27, 35, 40, 255) if dark else (238, 245, 249, 255)
    d.rectangle([415, 38, 555, 160], fill=splash_col)

    # 2. Paste new tilted logo with depth of field blur
    r = logo.rotate(-21.0, resample=Image.BICUBIC, expand=True)
    r_small = r.resize((86, 86), Image.LANCZOS).filter(ImageFilter.GaussianBlur(0.85))
    bx = int(485.5 - r_small.width / 2)
    by = int(99.5 - r_small.height / 2)
    im.paste(r_small, (bx, by), r_small)

    # 3. Clean and paste mini logo on settings window (top left)
    set_bg = (34, 38, 44, 255) if dark else (240, 243, 248, 255)
    d.rectangle([22, 54, 38, 70], fill=set_bg)
    mini_logo = logo.resize((14, 14), Image.LANCZOS).filter(ImageFilter.GaussianBlur(0.4))
    im.paste(mini_logo, (24, 57), mini_logo)

    # 4. Clean up bottom-left Roblox navbar icon
    nb_col = (18, 18, 21, 255) if dark else (255, 255, 255, 255)
    d.rectangle([92, 578, 120, 603], fill=nb_col)
    bar_logo = logo.resize((18, 18), Image.LANCZOS)
    im.paste(bar_logo, (97, 582), bar_logo)

    im.save(image_path)


# --------------------------------------------------------------------------- main

def main() -> int:
    main_logo_path = IMAGES / "GokuTrap.png"
    alt_logo_path = IMAGES / "GokuTrapAlt.png"

    if not main_logo_path.exists():
        print(f"Error: {main_logo_path} missing.")
        return 1

    main_logo = Image.open(main_logo_path).convert("RGBA")
    alt_logo = Image.open(alt_logo_path).convert("RGBA") if alt_logo_path.exists() else main_logo

    # Save Windows application and bootstrapper icons
    save_ico(main_logo, APP / "GokuTrap.ico", ICO_APP)
    main_logo.resize((512, 512), Image.LANCZOS).save(APP / "GokuTrap.png")
    save_ico(main_logo, RES / "IconGokuTrap.ico", ICO_BOOT)
    save_ico(alt_logo, RES / "IconGokuTrapAlt.ico", ICO_BOOT)

    # Update showcase screenshots
    update_showcase(IMAGES / "Showcase-Dark.png", main_logo, dark=True)
    update_showcase(IMAGES / "Showcase-Light.png", main_logo, dark=False)

    print("GokuTrap assets successfully synchronized.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
