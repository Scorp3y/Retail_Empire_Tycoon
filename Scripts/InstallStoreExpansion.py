"""Install only verified generated assets; preserve existing GUIDs and legacy SKU data."""
from pathlib import Path
import hashlib
import json
import re
import shutil

root=Path(__file__).resolve().parents[1]
qa=root/'Library/CityUpdateQA/IsolatedProject'
report=root/'Library/StoreExpansionIntegration';report.mkdir(exist_ok=True)
pack=Path('Assets/Resources/StoreAssortment/Expansion.asset')
assert hashlib.sha256((root/pack).read_bytes()).hexdigest()=='7fc849496557ea30380f23217163e3927ca64218c4c26a615207988ae4cc0af1','Main catalogue changed during validation'
assert (qa/'Library/AssortmentQA/play.txt').read_text().startswith('PASS 144 ')
assert 'PASS 346 compatible' in (qa/'Library/AssortmentQA/details.txt').read_text()
backup=report/'Before'
def save_before(path):
    if path.is_file():
        dest=backup/path.relative_to(root);dest.parent.mkdir(parents=True,exist_ok=True)
        if not dest.exists():shutil.copy2(path,dest)
def guid(path):
    return re.search(r'^guid: (\w+)',path.read_text(encoding='utf-8'),re.M).group(1)
files=[]
for folder in ['Assets/Art/Wave2','Assets/Resources/StoreAssortment/Wave2']:
    source=qa/folder;files += [p for p in source.rglob('*') if p.is_file()]
    if source.with_suffix('.meta').is_file():files.append(source.with_suffix('.meta'))
for source in (root/'Assets/Art/Wave2/Source').glob('*.json'):
    data=json.loads(source.read_text());key=('shelf_' if data.get('fixture') else 'product_')+source.stem
    for suffix in ('.png','.png.meta'):files.append(qa/('Assets/Art/ShopUi/Items/'+key+suffix))
mapping={}
for source in files:
    dest=root/source.relative_to(qa)
    if source.suffix=='.meta' and dest.is_file():mapping[guid(source)]=guid(dest)
shader=Path('Assets/Project/Shaders/ProductVertexColor.shader.meta')
mapping[guid(qa/shader)]=guid(root/shader)
def remap(text):return re.sub(r'guid: ([a-f0-9]{32})',lambda m:'guid: '+mapping.get(m[1],m[1]),text)
installed=[]
for source in files:
    dest=root/source.relative_to(qa);dest.parent.mkdir(parents=True,exist_ok=True)
    if source.suffix in ('.meta','.asset','.mat','.prefab'):data=remap(source.read_text(encoding='utf-8')).encode('utf-8')
    else:data=source.read_bytes()
    if dest.exists() and dest.read_bytes()!=data:save_before(dest)
    dest.write_bytes(data);installed.append(str(dest.relative_to(root)))
# Legacy SKUs may already be present in base catalogues: change only their visual/icon fields.
guid_paths={guid(p):p.with_suffix('') for p in (qa/'Assets').rglob('*.asset.meta')}
text=(qa/pack).read_text(encoding='utf-8');product_section=text.split('  products:')[1].split('  fixtures:')[0]
for g in re.findall(r'guid: (\w+)',product_section):
    source=guid_paths[g];relative=source.relative_to(qa)
    if str(relative).replace('\\','/').startswith('Assets/Resources/StoreAssortment/Wave2/'):continue
    dest=root/relative
    assert dest.is_file() and guid(dest.with_suffix(dest.suffix+'.meta'))==g
    original=dest.read_text(encoding='utf-8');incoming=remap(source.read_text(encoding='utf-8'));updated=original
    for field in ('shelfDisplayPrefab','icon'):
        match=re.search(r'^  '+field+r': .+$',incoming,re.M)
        if match:updated=re.sub(r'^  '+field+r': .+$',lambda _:match[0],updated,flags=re.M)
    if original!=updated:save_before(dest);dest.write_text(updated,encoding='utf-8');installed.append(str(relative))
save_before(root/pack);(root/pack).write_text(remap(text),encoding='utf-8');installed.append(str(pack))
for name in ('play.txt','details.txt'):shutil.copy2(qa/'Library/AssortmentQA'/name,report/name)
(report/'installed_files.txt').write_text('\n'.join(installed))
print('Installed',len(installed),'verified assets; existing GUIDs, legacy SKU settings and player scenes preserved')
