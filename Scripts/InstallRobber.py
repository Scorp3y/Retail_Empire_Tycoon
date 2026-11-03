"""Install only the independently verified robber assets; preserve scenes and existing NPC assets."""
from pathlib import Path
import hashlib,json,re,shutil
ROOT=Path(__file__).resolve().parents[1]
CLONE=ROOT/'Library/CityUpdateQA/IsolatedProject'
DOCS=ROOT/'Docs/Robber'
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def require(value,message):
    if not value:raise RuntimeError(message)
def main():
    report=CLONE/'Library/RobberQA/result.txt'
    require(report.read_text(encoding='utf-8-sig').startswith('PASS:'),'Robber gameplay acceptance failed.')
    log=(ROOT/'Library/Robber/Unity.log').read_text(encoding='utf-8-sig')
    require(not re.search(r'^\w*Exception:|\berror CS\d+',log,re.M),'Unity compilation/runtime errors.')
    sources=['Assets/Project/Scripts/StoreOperations/StoreOperations.cs','Assets/Project/Editor/RobberModelImport.cs','Assets/Project/Editor/RobberQA.cs']
    for name in sources:require(digest(ROOT/name)==digest(CLONE/name),'Untested source: '+name)
    baseline=json.loads((ROOT/'Library/Robber/Baseline.json').read_text())
    for name,expected in baseline.items():
        require(digest(ROOT/name)==expected,'Original scene/world/roster changed: '+name)
        require(digest(CLONE/name)==expected,'Accepted copy differs from actual scene/world/roster: '+name)
    require(digest(CLONE/'Assets/Art/Robber/Robber.fbx')==digest(ROOT/'Library/Robber/Exports/Robber.fbx'),'FBX differs from Blender export.')
    names=['Assets/Art/Robber/'+name for name in ['Palette.png','Robber.fbx','Robber.mat','Robber.controller']]
    names+=['Assets/Resources/Characters/Robber.prefab']
    files=names+[name+'.meta' for name in names]+['Assets/Art/Robber.meta']
    folder=ROOT/'Assets/Resources/Characters'
    if not folder.exists():
        folder.mkdir(parents=True)
        files.append('Assets/Resources/Characters.meta')
    for name in files:
        source,target=CLONE/name,ROOT/name
        if name.endswith('.meta') and target.exists():
            require(re.search(r'^guid: (\w+)',source.read_text(),re.M).group(1)==re.search(r'^guid: (\w+)',target.read_text(),re.M).group(1),'Existing asset identity would change: '+name)
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
        require(digest(source)==digest(target),'Asset transfer failed: '+name)
    shutil.copy2(report,DOCS/'UnityChecks.txt')
    shutil.copy2(CLONE/'Docs/Robber/RobberInStore.png',DOCS/'RobberInStore.png')
    (DOCS/'AcceptedSources.json').write_text(json.dumps(dict(sources={p:digest(ROOT/p) for p in sources},assets={p:digest(ROOT/p) for p in files},protected=baseline),indent=2))
    print('PASS: dedicated robber model, controller, material and prefab installed; scenes, town and regular shopper roster unchanged; player save unchanged in acceptance.')
if __name__=='__main__':main()
