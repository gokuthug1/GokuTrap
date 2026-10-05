import glob
import os
import re
import sys

if len(sys.argv) > 1:
    directory = sys.argv[1]
elif sys.stdin.isatty():
    prompt = input("Enter project path (the one containing GokuTrap.csproj, default 'GokuTrap'): ").strip()
    directory = prompt if prompt else "GokuTrap"
else:
    directory = "GokuTrap"

if not os.path.isabs(directory):
    directory = os.path.abspath(directory)

existing = []
found = []

resx_path = os.path.join(directory, "Resources", "Strings.resx")
with open(resx_path, "r", encoding="utf-8") as file:
    existing = re.findall(r'name="([a-zA-Z0-9.]+)" xml:space="preserve"', file.read())

for filename in glob.glob(os.path.join(directory, "**", "*.*"), recursive=True):
    if any(p in filename for p in (os.sep + "bin" + os.sep, os.sep + "obj" + os.sep, os.sep + "Resources" + os.sep)):
        continue

    try:
        with open(filename, "r", encoding="utf-8", errors="ignore") as file:
            contents = file.read()

            matches = re.findall(r"Strings\.([a-zA-Z0-9_]+)", contents)
            for match in matches:
                if "_" not in match:
                    continue

                ref = match.replace("_", ".")
                if ref not in found:
                    found.append(ref)

            matches = re.findall(r'FromTranslation = "([a-zA-Z0-9.]+)"', contents)
            for match in matches:
                if match not in found:
                    found.append(match)

    except Exception:
        print(f"Could not open {filename}")
        continue

unused_count = 0
for entry in existing:
    if entry not in found and not entry.startswith("Enums."):
        print(entry)
        unused_count += 1

print(f"\nDone. Found {unused_count} potentially unused string keys out of {len(existing)}.")