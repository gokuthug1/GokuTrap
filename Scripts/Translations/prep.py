import glob
import os
import re
import shutil
import sys

if len(sys.argv) > 2:
    exports = sys.argv[1]
    dest = sys.argv[2]
elif sys.stdin.isatty():
    exports = input("Path of folder of exported Crowdin files: ").strip()
    dest = input("Destination resources folder (e.g. GokuTrap/Resources): ").strip()
else:
    print("Usage: python prep.py <exported_crowdin_folder> <dest_resources_folder>")
    sys.exit(0)

icu_codes = {
    "zh-CN": "zh-Hans-CN",
    "zh-HK": "zh-Hant-HK",
    "zh-TW": "zh-Hant-TW"
}

os.makedirs(dest, exist_ok=True)

copied = 0
for filename in glob.glob(os.path.join(exports, "**", "*.*"), recursive=True):
    match = re.search(r"[\\/]([a-zA-Z\-]+)[\\/]Strings\.", filename)
    if not match:
        continue

    print(f"Copying {filename}")
    locale_code = match.group(1)
    locale_code = icu_codes.get(locale_code, locale_code)

    target = os.path.join(dest, f"Strings.{locale_code}.resx")
    shutil.copy(filename, target)
    copied += 1

print(f"Done. Processed and copied {copied} translation files.")