"""Check installed Unity GUID references and catalogue identity without touching saves."""
from pathlib import Path
import json
import os
import re

root = Path(__file__).resolve().parents[1]
report = root / 'Library/StoreExpansionIntegration'
guids = {}
duplicates = []
for directory in (root / 'Assets', root / 'Library/PackageCache'):
    for folder, _, names in os.walk(directory):
        for name in names:
            if not name.endswith('.meta'):
                continue
            path = Path(folder) / name
            try:
                content = path.read_text(encoding='utf-8-sig')
            except FileNotFoundError:
                # Windows legacy path limits affect unrelated imported icon names.
                continue
            match = re.search(r'^guid: (\w+)', content, re.M)
            if match:
                key = match[1]
                if key in guids and path != guids[key]:
                    duplicates.append(key)
                guids[key] = path
pack = (root / 'Assets/Resources/StoreAssortment/Expansion.asset').read_text(encoding='utf-8')
products, fixtures = pack.split('  products:')[1].split('  fixtures:')
counts = {}
for label, section, expected in [('products', products, 144), ('fixtures', fixtures, 20)]:
    refs = re.findall(r'guid: (\w+)', section)
    assert len(refs) == expected, (label, len(refs))
    ids = []
    for guid in refs:
        assert guid in guids, guid
        text = guids[guid].with_suffix('').read_text(encoding='utf-8-sig')
        match = re.search(r'^  id: (.+)$', text, re.M)
        assert match, str(guids[guid])
        ids.append(match[1])
    assert len(set(ids)) == expected, label + ' duplicate IDs'
    counts[label] = expected
missing = []
checked = 0
for line in (report / 'installed_files.txt').read_text().splitlines():
    path = root / line
    if path.suffix not in ('.asset', '.prefab', '.mat', '.meta'):
        continue
    checked += 1
    for guid in re.findall(r'guid: ([a-f0-9]{32})', path.read_text(encoding='utf-8-sig')):
        if guid.startswith('0000000000000000'):
            continue
        if guid not in guids:
            missing.append((line, guid))
assert not missing, missing[:20]
result = dict(status='PASS', catalogue=counts, checked_files=checked,
              missing_references=missing, duplicate_guids_in_installed_assets=[
                  g for g in set(duplicates) if any(g in (root / p).read_text(encoding='utf-8-sig')
                  for p in ['Assets/Resources/StoreAssortment/Expansion.asset'])])
assert not result['duplicate_guids_in_installed_assets'], result
(report / 'references.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result))
