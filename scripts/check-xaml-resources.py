#!/usr/bin/env python3
"""Catch duplicate explicit/implicit WPF resource keys before Windows startup."""
from pathlib import Path
import xml.etree.ElementTree as ET

X = '{http://schemas.microsoft.com/winfx/2006/xaml}'
ROOT = Path(__file__).resolve().parents[1]

def validate(path):
    document = ET.parse(path)
    count = 0
    for dictionary in document.iter():
        tag = dictionary.tag.rsplit('}', 1)[-1]
        if not (tag == 'ResourceDictionary' or tag.endswith('.Resources')):
            continue
        keys = set()
        for item in dictionary:
            key = item.get(X + 'Key')
            implicit = item.tag.rsplit('}', 1)[-1] == 'Style' and key is None
            if implicit:
                target = item.get('TargetType')
                if target: key = '{x:Type ' + target.removeprefix('{x:Type ').removesuffix('}') + '}'
            if key is None: continue
            if key in keys: raise AssertionError(f'{path.name}: duplicate resource key {key}')
            keys.add(key); count += 1
    return count

if __name__ == '__main__':
    # Regression: the two implicit styles that broke 3.7 must be rejected.
    import tempfile
    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / 'duplicate.xaml'
        path.write_text('<Application xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><Application.Resources><Style TargetType="ListBox"/><Style TargetType="ListBox"/></Application.Resources></Application>')
        try: validate(path)
        except AssertionError: pass
        else: raise AssertionError('Duplicate implicit style was not detected')
    paths = sorted((ROOT / 'src/EireTodo.Windows').glob('*.xaml'))
    total = sum(validate(p) for p in paths)
    print(f'PASS XAML resource uniqueness: {len(paths)} files, {total} keys; duplicate-style regression rejected')
