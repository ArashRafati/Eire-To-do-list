#!/usr/bin/env python3
"""Package the portable executable and source without embedding old release ZIPs."""
from pathlib import Path
import hashlib
import zipfile

root = Path(__file__).resolve().parents[1]
artifacts = root / 'artifacts'
artifacts.mkdir(exist_ok=True)
exe = artifacts / 'windows-x64/SUMAPP.exe'
assert exe.is_file(), 'Publish the Windows executable first.'
with zipfile.ZipFile(artifacts / 'SUMAPP-Windows-x64.zip', 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for file in [exe, root / 'QUICKSTART.txt', root / 'BRANDING.md', root / 'WINDOWS-VERIFICATION.md', root / 'PLANNING-EXPORTS.md', root / 'THIRD-PARTY-NOTICES.txt']:
        z.write(file, 'SUMAPP/' + file.name)
    for file in (root / 'previews').glob('*.png'):
        z.write(file, 'SUMAPP/previews/' + file.name)
source_files = [root / p for p in ['.gitignore', 'global.json', 'README.md', 'CHANGELOG.md', 'BRANDING.md', 'QUICKSTART.txt', 'VERIFICATION.md', 'WINDOWS-VERIFICATION.md', 'PLANNING-EXPORTS.md', 'THIRD-PARTY-NOTICES.txt']]
for directory in ['assets', 'src', 'tests', 'scripts', 'previews']:
    source_files.extend(p for p in (root / directory).rglob('*')
        if p.is_file() and not any(part in {'bin', 'obj', '__pycache__'} for part in p.relative_to(root).parts))
with zipfile.ZipFile(artifacts / 'SUMAPP-Source.zip', 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for file in sorted(source_files):
        z.write(file, 'SUMAPP-Source/' + file.relative_to(root).as_posix())
checks = []
for name in ['SUMAPP-Windows-x64.zip', 'SUMAPP-Source.zip']:
    p = artifacts / name
    with zipfile.ZipFile(p) as z:
        assert z.testzip() is None, 'Archive integrity failure: ' + name
    checks.append(hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + name)
    print(name, p.stat().st_size, 'bytes; integrity OK')
(artifacts / 'SHA256SUMS.txt').write_text('\n'.join(checks) + '\n')
with zipfile.ZipFile(artifacts / 'SUMAPP-Windows-x64.zip') as z:
    assert z.read('SUMAPP/SUMAPP.exe') == exe.read_bytes()
print('Portable ZIP matches the current published executable.')
