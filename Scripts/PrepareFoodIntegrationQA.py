"""Synchronize the existing isolated Unity project without copying or using player saves."""
from pathlib import Path
import hashlib
import shutil

ROOT=Path(__file__).resolve().parents[1]
QA=ROOT/'Library/CityUpdateQA/IsolatedProject'
assert (QA/'ProjectSettings/ProjectVersion.txt').is_file(),'Existing isolated project is required.'
SCOPES=['Assets/Project','Assets/Prefabs','Assets/Resources/StoreAssortment',
        'Assets/Art/FoodExpansion','Assets/Art/Wave2','Assets/Art/ShopUi/Items',
        'ProjectSettings','Packages/manifest.json','Packages/packages-lock.json']
count=0
for scope in SCOPES:
    source=ROOT/scope
    paths=[source] if source.is_file() else list(source.rglob('*'))
    if source.with_suffix(source.suffix+'.meta').is_file():paths.append(source.with_suffix(source.suffix+'.meta'))
    for path in paths:
        if not path.is_file():continue
        target=QA/path.relative_to(ROOT)
        if target.is_file() and hashlib.sha256(path.read_bytes()).digest()==hashlib.sha256(target.read_bytes()).digest():continue
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,target);count+=1
print('Synchronized',count,'changed project files into the existing isolated Unity editor.')
