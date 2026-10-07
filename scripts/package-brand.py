#!/usr/bin/env python3
"""Install an accessible authoritative PNG unchanged; encode proportional ICO frames.
Usage: python3 scripts/package-brand.py /path/to/supplied-logo.png
Requires Pillow in the development environment, never in the portable app.
"""
from pathlib import Path
import argparse
import io
import struct
import hashlib
from PIL import Image
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('png',type=Path)
args = parser.parse_args()
original = args.png.read_bytes()
assert original.startswith(b'\x89PNG\r\n\x1a\n'), 'Supply the original PNG file.'
logo = Image.open(io.BytesIO(original)).convert('RGBA')
sizes = [16,20,24,32,40,48,64,128,256]
frames = []
for size in sizes:
    # Executable icon conversion only: transparent square frame and uniform scaling.
    frame = Image.new('RGBA',(size,size),(0,0,0,0))
    copy = logo.copy(); copy.thumbnail((size,size),Image.Resampling.LANCZOS)
    frame.alpha_composite(copy,((size-copy.width)//2,(size-copy.height)//2))
    output = io.BytesIO(); frame.save(output,format='PNG'); frames.append(output.getvalue())
header = struct.pack('<HHH',0,1,len(frames)); offset = 6 + len(frames)*16
entries = []
for size,frame in zip(sizes,frames):
    entries.append(struct.pack('<BBBBHHII',size if size < 256 else 0,size if size < 256 else 0,0,0,1,32,len(frame),offset)); offset += len(frame)
assets = Path(__file__).resolve().parents[1]/'src/EireTodo.Windows/Assets'
for name in ['SumappLogo.png','AppIcon.png']: (assets/name).write_bytes(original)
(assets/'AppIcon.ico').write_bytes(header+b''.join(entries)+b''.join(frames))
print('Original PNG preserved in both resources; nine proportional ICO frames encoded.')
print('PNG SHA256:',hashlib.sha256(original).hexdigest())
print('Rebuild with scripts/build-windows.ps1 or bash scripts/setup-cloud.sh.')
