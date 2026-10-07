#!/usr/bin/env python3
"""Optional independent export check: pip install mpxj==16.10.0 JPype1==1.7.1.
Requires a Java runtime and Poppler pdftotext. Neither is needed by the app.
Generate fixtures with the functional runner's --exports-dir option first.
"""
from pathlib import Path
import csv
import io
import json
import subprocess
import sys

folder = Path(sys.argv[1] if len(sys.argv) > 1 else 'artifacts/export-checks').resolve()
fixture = json.loads((folder / 'fixture.json').read_text())
nodes = fixture['Nodes']
by_id = {n['Id']: n for n in nodes}
review = next(n for n in nodes if n['Title'].startswith('Review'))

for name in ['HierarchyCsv', 'ProjectCsv', 'PrimaveraCsv']:
    rows = list(csv.DictReader(io.StringIO((folder / (name + '.csv')).read_text(encoding='utf-8-sig'))))
    expected = len(nodes) + (1 if name == 'PrimaveraCsv' else 0)
    assert len(rows) == expected, (name, len(rows), expected)
    row = next(r for r in rows if r['Node ID'] == review['Id'])
    note_column = 'Notebook' if name == 'PrimaveraCsv' else 'Notes'
    assert row[note_column] == review['Notes']
    assert row['Parent Node ID'] == review['ParentId']
    print('PASS independent CSV parser:', name, expected, 'data rows, full quoted multiline notes')

import jpype
import mpxj
jpype.startJVM()
try:
    from org.mpxj.reader import UniversalProjectReader
    from org.mpxj import TimeUnit
    for path in sorted(folder.glob('*.xml')):
        project = UniversalProjectReader().read(str(path))
        tasks = list(project.getTasks())
        assert len(tasks) == len(nodes) + 1, (path.name, len(tasks))
        native = {str(t.getGUID()): t for t in tasks if t.getGUID() is not None}
        for node in nodes:
            task = native[node['Id']]
            assert str(task.getName()) == node['Title']
            if node['ParentId'] is not None:
                assert str(task.getParentTask().getGUID()) == node['ParentId']
            else:
                assert task.getParentTask() is not None
        task = native[review['Id']]
        assert str(task.getStart()) == '2026-10-07T08:00'
        assert str(task.getFinish()) == '2026-10-09T17:00'
        assert task.getDuration().convertUnits(TimeUnit.HOURS, project.getProjectProperties()).getDuration() == 24
        if path.name.startswith('Primavera'):
            assert str(task.getActivityID()) == f"A{review['Number']:05}"
            assert float(task.getPhysicalPercentComplete().doubleValue()) == 100
        else:
            assert int(task.getUniqueID().intValue()) == review['Number']
            assert float(task.getPercentageComplete().doubleValue()) == 100
        assert 'café Ω' in str(task.getNotes()) and 'Second line' in str(task.getNotes())
        print('PASS independent MPXJ reader:', path.name, 'hierarchy, IDs, dates, 24h duration, 100% completion, notes')
finally:
    jpype.shutdownJVM()

result = subprocess.run(['pdftotext', str(folder / 'Pdf.pdf'), '-'], check=True, capture_output=True, text=True)
assert review['Title'] in result.stdout and review['Notes'] in result.stdout
for node in nodes:
    assert node['Title'] in result.stdout
print('PASS independent PDF text extraction: full labels, multiline notes and Unicode')
