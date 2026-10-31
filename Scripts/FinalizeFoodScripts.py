"""Make each delivered one-click script reproduce its selected final revision."""
from pathlib import Path
import json
import re
root=Path(__file__).resolve().parents[1]
folders=[p for p in (root/'ArtSource').glob('Food_*') if (p/'Validation.json').is_file()]
assert len(folders)==16
for folder in folders:
    iteration=json.loads((folder/'Validation.json').read_text())['iteration']
    scripts=list(folder.glob('Build*.py'));assert len(scripts)==1
    path=scripts[0];source=path.read_text(encoding='utf-8-sig')
    updated,count=re.subn(r'^ITERATION\s*=\s*\d+\s*$',f'ITERATION={iteration}',source,flags=re.M)
    assert count>0,path
    compile(updated,str(path),'exec')
    path.write_text(updated,encoding='utf-8')
print('PASS: 16 standalone scripts default to their final exported revision.')
