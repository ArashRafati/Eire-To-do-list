#!/usr/bin/env python3
"""Inspect the actual PE manifest and .NET bundle, without executing Windows code."""
import json
import struct
import sys
import xml.etree.ElementTree as ET
import zlib
from pathlib import Path

path = Path(sys.argv[1] if len(sys.argv) > 1 else 'artifacts/windows-x64/SUMAPP.exe')
b = path.read_bytes()

def u16(i): return struct.unpack_from('<H', b, i)[0]
def u32(i): return struct.unpack_from('<I', b, i)[0]
def u64(i): return struct.unpack_from('<Q', b, i)[0]

assert b[:2] == b'MZ', 'Not a Windows executable'
pe = u32(0x3c)
assert b[pe:pe+4] == b'PE\0\0'
assert u16(pe+4) == 0x8664, 'Not Windows x64'
optional = pe + 24
assert u16(optional) == 0x20b, 'Not a PE32+ executable'
assert u16(optional+68) == 2, 'Not a Windows GUI application'
sections = optional + u16(pe+20)

def file_offset(rva):
    for i in range(u16(pe+6)):
        s = sections + i * 40
        size, start, raw_size, raw = struct.unpack_from('<IIII', b, s+8)
        if start <= rva < start + max(size, raw_size): return raw + rva - start
    raise AssertionError('RVA outside PE sections')

resources = file_offset(u32(optional+112+16))

def entries(offset):
    d = resources + offset
    return [struct.unpack_from('<II', b, d+16+8*i) for i in range(u16(d+12)+u16(d+14))]

def resource_payload(resource_type, resource_id):
    type_offset = next(offset & 0x7fffffff for name, offset in entries(0) if name == resource_type)
    resource_offset = next(offset & 0x7fffffff for name, offset in entries(type_offset) if name == resource_id)
    leaf_offset = entries(resource_offset)[0][1]
    assert not leaf_offset & 0x80000000
    rva, size = struct.unpack_from('<II', b, resources + leaf_offset)
    start = file_offset(rva)
    return b[start:start + size]

# Verify that the published PE, rather than just the source project, embeds the artwork.
source_icon = Path(__file__).resolve().parents[1] / 'src/EireTodo.Windows/Assets/AppIcon.ico'
ico = source_icon.read_bytes()
reserved, icon_type, source_count = struct.unpack_from('<HHH', ico)
assert reserved == 0 and icon_type == 1
source_frames = {}
for i in range(source_count):
    width, height, colors, reserved, planes, bits, size, offset = struct.unpack_from('<BBBBHHII', ico, 6 + 16 * i)
    source_frames[(width or 256, height or 256)] = ico[offset:offset + size]
group_offset = next(offset & 0x7fffffff for name, offset in entries(0) if name == 14)
embedded_icon_sizes = None
for group_id, offset in entries(group_offset):
    if group_id & 0x80000000: continue
    group = resource_payload(14, group_id)
    reserved, icon_type, group_count = struct.unpack_from('<HHH', group)
    if group_count != source_count: continue
    matched = []
    for i in range(group_count):
        width, height, colors, reserved, planes, bits, size, icon_id = struct.unpack_from('<BBBBHHIH', group, 6 + 14 * i)
        dimensions = (width or 256, height or 256)
        payload = resource_payload(3, icon_id)
        if len(payload) != size or payload != source_frames.get(dimensions): break
        matched.append(dimensions[0])
    else:
        embedded_icon_sizes = sorted(matched)
        break
assert embedded_icon_sizes == [16, 20, 24, 32, 40, 48, 64, 128, 256], 'Published app icon does not match all source ICO frames'

manifest_dir = next(offset & 0x7fffffff for name, offset in entries(0) if name == 24)
level = entries(manifest_dir)[0][1] & 0x7fffffff
leaf = entries(level)[0][1]
assert not leaf & 0x80000000
rva, size = struct.unpack_from('<II', b, resources + leaf)
manifest = b[file_offset(rva):file_offset(rva)+size].decode('utf-8-sig').rstrip('\0')
xml = ET.fromstring(manifest)
execution = next(n for n in xml.iter() if n.tag.endswith('requestedExecutionLevel'))
assert execution.attrib == {'level': 'asInvoker', 'uiAccess': 'false'}
assert next(n for n in xml.iter() if n.tag.endswith('dpiAwareness')).text == 'PerMonitorV2'

signature = bytes.fromhex('8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae')
signature_position = b.find(signature)
assert signature_position > 8, 'Missing .NET single-file bundle marker'
p = u64(signature_position - 8)
major, minor, count = struct.unpack_from('<III', b, p)
p += 12
assert major == 6, f'Unexpected bundle format {major}.{minor}'

def read_string():
    global p
    size = 0
    for shift in range(0, 35, 7):
        c = b[p]; p += 1; size |= (c & 0x7f) << shift
        if c < 128: break
    else: raise AssertionError('Invalid string length')
    text = b[p:p+size].decode('utf-8'); p += size
    return text

bundle_id = read_string()
p += 40  # deps/config locations + header flags
files = {}
for i in range(count):
    offset, size, compressed_size = struct.unpack_from('<QQQ', b, p)
    p += 24
    kind = b[p]; p += 1
    name = read_string()
    assert offset + (compressed_size or size) <= len(b), 'Invalid bundle entry'
    files[name] = (offset, size, compressed_size, kind)
for name in ['SUMAPP.dll', 'EireTodo.Core.dll', 'System.Private.CoreLib.dll',
             'PresentationFramework.dll', 'PresentationCore.dll', 'WindowsBase.dll',
             'PresentationNative_cor3.dll', 'PdfSharp.dll']:
    assert name in files, 'Missing bundled runtime/application file: ' + name

def content(name):
    offset, size, compressed, kind = files[name]
    raw = b[offset:offset+(compressed or size)]
    return zlib.decompress(raw, -15) if compressed else raw

managed_core = content('EireTodo.Core.dll')
for font in (Path(__file__).resolve().parents[1] / 'src/EireTodo.Core/Assets').glob('*.ttf'):
    assert font.read_bytes() in managed_core, 'Offline PDF font not bundled: ' + font.name

managed_app = content('SUMAPP.dll')
# Static packaging guard: a BitmapImage constructed from an unbased relative URI
# can interpret the resource address as C:\SUMAPP;component\Assets\AppIcon.ico.
# Native decoding/startup is separately checked by --capture-previews on Windows.
pack_prefix = 'pack://application:,,,/SUMAPP;component/Assets/'
assert pack_prefix.encode('utf-16-le') in managed_app, 'Absolute bundled-image resource loader missing'
windows_source = Path(__file__).resolve().parents[1] / 'src/EireTodo.Windows'
for xaml in windows_source.glob('*.xaml'):
    for element in ET.parse(xaml).iter():
        for value in element.attrib.values():
            if ';component/Assets/' in value:
                assert value.startswith('pack://application:,,,/'), 'Relative image address in ' + xaml.name

for font in (Path(__file__).resolve().parents[1] / 'src/EireTodo.Core/Assets').glob('*.ttf'):
    assert font.read_bytes() in managed_app, 'Offline preview font not bundled: ' + font.name
assert ico in managed_app, 'Window ICO not bundled in WPF resources'
header_artwork = source_icon.with_suffix('.png').read_bytes()
assert header_artwork in managed_app, 'Header PNG not bundled in WPF resources'
sumapp_artwork = source_icon.with_name('SumappLogo.png').read_bytes()
assert sumapp_artwork in managed_app, 'SUMAPP logo not bundled in WPF resources'
eire_artwork = source_icon.with_name('EireLogo.png')
assert eire_artwork.read_bytes() in managed_app, 'Eire logo not bundled in WPF resources'

runtime = json.loads(content('SUMAPP.runtimeconfig.json'))['runtimeOptions']
assert 'frameworks' not in runtime and 'framework' not in runtime, 'Requires an installed framework'
frameworks = {f['name']: f['version'] for f in runtime['includedFrameworks']}
assert 'Microsoft.NETCore.App' in frameworks and 'Microsoft.WindowsDesktop.App' in frameworks
# .NET 10's single-file host statically includes the CLR and JIT. Verify exports.
export_table = file_offset(u32(optional + 112))
export_count = u32(export_table + 24)
export_names = file_offset(u32(export_table + 32))
exports = set()
for i in range(export_count):
    offset = file_offset(u32(export_names + 4 * i))
    exports.add(b[offset:b.index(0, offset)].decode('ascii'))
assert {'CLRJitAttachState', 'DotNetRuntimeInfo', 'g_CLREngineMetrics'} <= exports, 'Missing embedded CLR/JIT host'
print(json.dumps({'executable': str(path), 'platform': 'Windows x64 GUI',
    'execution_level': execution.attrib['level'], 'ui_access': execution.attrib['uiAccess'],
    'dpi_awareness': 'PerMonitorV2', 'compiled_absolute_pack_resource_loader': True, 'embedded_icon_sizes': embedded_icon_sizes, 'header_logos': ['SUMAPP', 'Eire'], 'bundle_files': count,
    'bundled_frameworks': frameworks, 'offline_pdf_library_and_font': True, 'requires_installed_dotnet': False}, indent=2))
