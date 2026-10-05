import os
import re

gokutrap_dir = os.path.abspath("GokuTrap")

def fix_file(filepath):
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()

    orig = content

    # 1. Fix pack URIs
    # Replace pack://application:,,,/Resources/ with pack://application:,,,/GokuTrap;component/Resources/
    content = content.replace("pack://application:,,,/Resources/", "pack://application:,,,/GokuTrap;component/Resources/")
    # Replace pack://application:,,,/GokuTrap.ico with pack://application:,,,/GokuTrap;component/GokuTrap.ico
    content = content.replace("pack://application:,,,/GokuTrap.ico", "pack://application:,,,/GokuTrap;component/GokuTrap.ico")

    # 2. Fix Window transparency and styles for MainWindow.xaml files
    if filepath.endswith("MainWindow.xaml"):
        # Remove WindowStyle="None"
        content = re.sub(r'\s*WindowStyle="None"', '', content)
        # Change AllowsTransparency="True" to AllowsTransparency="False"
        content = re.sub(r'AllowsTransparency="True"', 'AllowsTransparency="False"', content)

    # 3. For RegionalSettingsDialog.xaml
    if filepath.endswith("RegionalSettingsDialog.xaml"):
        if 'ShowInTaskbar="True"' not in content:
            content = content.replace('ExtendsContentIntoTitleBar="True"', 'ExtendsContentIntoTitleBar="True"\n        ShowInTaskbar="True"')

    if content != orig:
        with open(filepath, "w", encoding="utf-8", newline="") as f:
            f.write(content)
        print(f"Updated: {filepath}")

for root, dirs, files in os.walk(gokutrap_dir):
    for file in files:
        if file.endswith((".xaml", ".cs")):
            fix_file(os.path.join(root, file))

print("Done scanning and updating GokuTrap files.")
