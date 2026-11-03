"""Read-only check: the town prefab diff must stay inside the existing rail service."""
import hashlib,json,re
from pathlib import Path

PROJECT=Path(__file__).resolve().parents[1]
BEFORE=PROJECT/'Library/RegionalTrainV2/BeforeTown.prefab'
AFTER=PROJECT/'Library/CityUpdateQA/IsolatedProject/Assets/Prefabs/TownWorld/TownEnvironment.prefab'

def records(path):
    text=path.read_text(encoding='utf-8-sig')
    return {int(m[1]):m[0] for m in re.finditer(r'^--- !u!\d+ &(-?\d+).*?(?=^--- !u!|\Z)',text,re.M|re.S)}

def reference(text,name):
    match=re.search(r'\b'+name+r': \{fileID: (-?\d+)',text)
    return int(match[1]) if match else None

def subtree(data,root):
    included={root}
    while True:
        previous=len(included)
        game_objects={reference(data[i],'m_GameObject') for i in included if i in data}
        for key,body in data.items():
            if (reference(body,'m_Father') in included or reference(body,'m_TransformParent') in included
                or reference(body,'m_PrefabInstance') in included or reference(body,'m_GameObject') in game_objects-{None}
                or key in game_objects):
                included.add(key)
        if len(included)==previous:return included

def run():
    before,after=records(BEFORE),records(AFTER)
    guid=re.search(r'guid: (\w+)',(PROJECT/'Assets/Project/Scripts/City/TownRailTraffic.cs.meta').read_text())[1]
    old=[key for key,body in before.items() if 'guid: '+guid in body]
    assert len(old)==1,'Expected one existing rail service'
    service=old[0];assert service in after,'Rail service component ID changed'
    owner=reference(before[service],'m_GameObject')
    transform=next(key for key,body in before.items() if body.startswith('--- !u!4 ') and reference(body,'m_GameObject')==owner)
    train_before=subtree(before,transform);train_after=subtree(after,transform)
    removed=set(before)-set(after);added=set(after)-set(before)
    assert removed<=train_before, f'Unrelated objects removed: {removed-train_before}'
    assert added<=train_after, f'Unrelated objects added: {added-train_after}'
    changed={key for key in before.keys()&after.keys() if before[key]!=after[key]}
    assert changed<=(train_before|train_after),f'Unrelated objects changed: {changed-(train_before|train_after)}'
    def service_contract(body):
        body=re.sub(r'  cars:.*?(?=  metresPerSecond:)', '  cars: <replaced>\n',body,flags=re.S)
        return re.sub(r'  carSpacing:.*','  carSpacing: <replaced>',body)
    assert service_contract(before[service])==service_contract(after[service]),'Route, schedule or service contract changed'
    result=dict(status='PASS',retained_service_file_id=service,unchanged_outside_records=len(before.keys()-train_before),
                changed_records=len(changed),removed_train_records=len(removed),added_train_records=len(added),
                before_sha256=hashlib.sha256(BEFORE.read_bytes()).hexdigest(),after_sha256=hashlib.sha256(AFTER.read_bytes()).hexdigest())
    (PROJECT/'Docs/RegionalTrainV2/PrefabAudit.json').write_text(json.dumps(result,indent=2),encoding='utf8')
    print(json.dumps(result))

if __name__=='__main__':run()
