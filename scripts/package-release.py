#!/usr/bin/env python3
"""Package the portable executable and source without embedding old release ZIPs."""
from pathlib import Path
import hashlib
import zipfile
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
artifacts = root / 'artifacts'
artifacts.mkdir(exist_ok=True)
publish = artifacts / 'windows-folder'
exe = publish / 'SUMAPP.exe'
version = ET.parse(root / 'src/EireTodo.Windows/EireTodo.Windows.csproj').findtext('.//Version')
assert version and all(c.isdigit() or c == '.' for c in version)
app_name = 'SUMAPP-' + version
windows_zip = app_name + '-Windows-x64.zip'
source_zip = app_name + '-Source.zip'
source_root = app_name + '-Source/'
def portable_name(file):
    relative = file.relative_to(publish).as_posix()
    return app_name + '/' + (app_name + '.exe' if relative == 'SUMAPP.exe' else relative)
assert exe.is_file(), 'Publish the Windows executable first.'
with zipfile.ZipFile(artifacts / windows_zip, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for file in sorted(publish.rglob('*')):
        if file.is_file(): z.write(file, portable_name(file))
    for file in [root / 'QUICKSTART.txt', root / 'BRANDING.md', root / 'WINDOWS-VERIFICATION.md', root / 'PLANNING-EXPORTS.md', root / 'THIRD-PARTY-NOTICES.txt']:
        z.write(file, app_name + '/' + file.name)
    for file in (root / 'previews').glob('*.png'):
        z.write(file, app_name + '/previews/' + file.name)
source_files = [root / p for p in ['.gitignore', 'global.json', 'README.md', 'CHANGELOG.md', 'BRANDING.md', 'QUICKSTART.txt', 'VERIFICATION.md', 'WINDOWS-VERIFICATION.md', 'PLANNING-EXPORTS.md', 'THIRD-PARTY-NOTICES.txt']]
for directory in ['assets', 'src', 'tests', 'scripts', 'previews']:
    source_files.extend(p for p in (root / directory).rglob('*')
        if p.is_file() and not any(part in {'bin', 'obj', '__pycache__'} for part in p.relative_to(root).parts))
with zipfile.ZipFile(artifacts / source_zip, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for file in sorted(source_files):
        z.write(file, source_root + file.relative_to(root).as_posix())
checks = []
for name in [windows_zip, source_zip]:
    p = artifacts / name
    with zipfile.ZipFile(p) as z:
        assert z.testzip() is None, 'Archive integrity failure: ' + name
    checks.append(hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + name)
    print(name, p.stat().st_size, 'bytes; integrity OK')
(artifacts / (app_name + '-SHA256SUMS.txt')).write_text('\n'.join(checks) + '\n')
with zipfile.ZipFile(artifacts / windows_zip) as z:
    assert z.read(app_name + '/' + app_name + '.exe') == exe.read_bytes()
with zipfile.ZipFile(artifacts / windows_zip) as z:
    for file in publish.rglob('*'):
        if file.is_file(): assert z.read(portable_name(file)) == file.read_bytes()
print('Portable ZIP matches every current application/runtime file.')
