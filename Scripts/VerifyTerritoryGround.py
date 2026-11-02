"""Install accepted parcel ground assets, allowing only a mesh reference and appended plot children."""
from pathlib import Path
import hashlib
import json
import re
import shutil

ROOT=Path(__file__).resolve().parents[1]
CLONE=ROOT/'Library/CityUpdateQA/IsolatedProject'
DOCS=ROOT/'Docs/TerritoryGroundUpdate'
PREFAB='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
ART='Assets/Art/TownForgottenYard/OwnershipGround'

def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def require(value,message):
    if not value:
        raise RuntimeError(message)

def records(p):
    return {m[1]:(m[0],m[2]) for m in re.findall(r'(?ms)^--- !u!(\d+) &(\d+)\r?\n(.*?)(?=^--- !u!|\Z)',p.read_text(encoding='utf-8-sig'))}

def main():
    report=CLONE/'Library/TerritoryPurchaseQA/result.txt'
    require(report.read_text(encoding='utf-8-sig').startswith('PASS:'),'Unity acceptance failed.')
    log=(ROOT/'Library/TerritoryPurchaseUpdate/Acceptance.log').read_text(encoding='utf-8-sig')
    require(not re.search(r'^\w*Exception:|\berror CS\d+',log,re.M),'Unity compiler/runtime error.')
    sources=json.loads((ROOT/'Library/TerritoryPurchaseUpdate/Sources.json').read_text())
    sources+=['Assets/Project/Editor/TerritoryGroundOwnershipBake.cs','Assets/Project/Editor/TownYardStoryPlacement.cs']
    for name in sources:
        require(digest(ROOT/name)==digest(CLONE/name),'Untested source: '+name)
    baseline=json.loads((ROOT/'Library/TerritoryPurchaseUpdate/Baseline.json').read_text())
    for name,expected in baseline.items():
        if name==PREFAB:continue
        require(digest(ROOT/name)==expected,'Protected scene/layout changed: '+name)
    old,new=records(ROOT/PREFAB),records(CLONE/PREFAB)
    require(not set(old)-set(new),'Existing prefab records were removed.')
    mesh_changes=0;child_changes=0
    added=set(new)-set(old)
    for key,(kind,before) in old.items():
        require(new[key][0]==kind,'Prefab component changed type.')
        after=new[key][1]
        if before==after:continue
        if kind=='33':
            pattern=r'm_Mesh: \{[^}]+\}'
            require(re.sub(pattern,'MESH',before)==re.sub(pattern,'MESH',after),'Mesh filter changed beyond mesh reference.')
            mesh_changes+=1
        elif kind=='4':
            pattern=r'(?ms)^  m_Children:.*?(?=^  m_Father:)'
            require(re.sub(pattern,'CHILDREN\n',before)==re.sub(pattern,'CHILDREN\n',after),'Existing transform moved.')
            previous=re.search(pattern,before).group();current=re.search(pattern,after).group()
            original_ids=re.findall(r'fileID: (\d+)',previous);current_ids=re.findall(r'fileID: (\d+)',current)
            require(current_ids[:len(original_ids)]==original_ids and set(current_ids[len(original_ids):])<=added,'Existing children changed.')
            child_changes+=1
        else:
            raise RuntimeError('Unexpected changed prefab record: '+kind+' / '+key)
    require((mesh_changes,child_changes,len(added)) in [(1,24,96),(0,0,0)],'Unexpected scope of ground ownership changes.')
    require(len(list((CLONE/ART/'Meshes').glob('ParcelGround_*.asset')))==24,'Missing parcel pieces.')
    require(digest(ROOT/(PREFAB+'.meta'))==digest(CLONE/(PREFAB+'.meta')),'Prefab GUID changed.')
    DOCS.mkdir(parents=True,exist_ok=True)
    if not (DOCS/'BeforeProtected.json').exists():
        (DOCS/'BeforeProtected.json').write_text(json.dumps(baseline,indent=2))
    for p in (CLONE/ART).rglob('*'):
        if p.is_file():
            target=ROOT/p.relative_to(CLONE);target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target)
    shutil.copy2(CLONE/(ART+'.meta'),ROOT/(ART+'.meta'))
    shutil.copy2(CLONE/PREFAB,ROOT/PREFAB)
    for name in ['NormalPlay.png','AfterPurchase.png','AllLandCleared.png']:
        shutil.copy2(CLONE/'Docs/TerritoryPurchaseUpdate'/name,DOCS/name)
    shutil.copy2(report,DOCS/'result.txt')
    for name in ['BakeInitial.txt','Bake.txt']:
        shutil.copy2(CLONE/'Library/TerritoryGroundOwnership'/name,DOCS/name)
    audit=dict(sources={p:digest(ROOT/p) for p in sources},prefab=digest(ROOT/PREFAB),
               protected={p:digest(ROOT/p) for p in baseline},mesh_references_changed=mesh_changes,
               plots_with_appended_ground=child_changes,new_prefab_records=len(added),
               assets={str(p.relative_to(ROOT)):digest(p) for p in (ROOT/ART).rglob('*') if p.is_file()})
    (DOCS/'AcceptedSources.json').write_text(json.dumps(audit,indent=2),encoding='utf-8')
    print('PASS: 24 owned ground pieces installed; only one permanent mesh reference and 24 child lists changed; original scenes/layout and player save unchanged.')

if __name__=='__main__':main()
