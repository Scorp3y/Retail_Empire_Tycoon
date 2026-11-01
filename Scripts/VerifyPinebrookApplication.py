"""Verify the accepted isolated district was transferred intact to the actual game."""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CLONE = ROOT / 'Library/CityUpdateQA/IsolatedProject'
QA = ROOT / 'Docs/Pinebrook/Final/QA'
PREFAB = Path('Assets/Prefabs/TownWorld/TownEnvironment.prefab')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def main():
    pipeline = ROOT / 'Library/PinebrookDistrictPipeline'
    applied = json.loads((pipeline / 'applied.json').read_text(encoding='utf-8-sig'))
    baseline = json.loads((pipeline / 'baseline.json').read_text(encoding='utf-8-sig'))
    prefab_hash = digest(ROOT / PREFAB)
    require(prefab_hash == applied['prefab'] == digest(CLONE / PREFAB), 'Main prefab differs from accepted result.')
    backup = Path(applied['backup']) / PREFAB
    require(digest(backup) == baseline['prefab'], 'Backup differs from the pre-update town.')

    owned = set()
    for stage in ('result', 'review', 'play'):
        require((QA / f'{stage}.txt').read_text(encoding='utf-8-sig').startswith('PASS:'), f'{stage} failed.')
        require((QA / f'{stage}-prefab.sha256').read_text().strip() == prefab_hash, f'{stage} is stale.')
        for line in (QA / f'{stage}-assets.sha256').read_text().splitlines():
            expected, relative = line.split(' ', 1)
            require(relative.startswith('Assets/Art/Pinebrook/') and '..' not in relative, 'Unsafe receipt path.')
            require(digest(ROOT / relative) == expected, f'Changed asset: {relative}')
            owned.add(relative)

    # Only new references need resolution here; retained references are fingerprinted in Unity.
    references = lambda path: set(re.findall(r'guid: ([a-f0-9]{32})', path.read_text(encoding='utf-8-sig')))
    new_refs = references(ROOT / PREFAB) - references(backup)
    unresolved = set(new_refs)
    for meta in (ROOT / 'Assets').rglob('*.meta'):
        match = re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text(encoding='utf-8-sig'), re.M)
        if match and match[1] in unresolved:
            require(Path(str(meta)[:-5]).exists(), f'Metadata has no asset: {meta}')
            unresolved.remove(match[1])
        if not unresolved:
            break
    require(not unresolved, f'Unresolved new asset references: {sorted(unresolved)}')

    sources = list((ROOT / 'Assets/Project/Editor').glob('Pinebrook*.cs'))
    sources += [ROOT / 'Assets/Project/Editor/TownCityCorrectionsGeometry.cs']
    sources += [ROOT / f'Assets/Project/Scripts/City/{name}.cs' for name in ('TownRoadTraffic', 'TrafficRoadNetwork', 'CityMapGraphic')]
    for source in sources:
        require(digest(source) == digest(CLONE / source.relative_to(ROOT)), f'Untested source: {source}')

    for name in ('City', 'Game'):
        scene = Path(f'Assets/Project/Scenes/{name}.unity')
        require(digest(ROOT / scene) == digest(CLONE / scene), f'{name} scene changed during testing.')
    pictures = list((ROOT / 'Docs/Pinebrook/Final').glob('*.png'))
    for picture in pictures:
        require(digest(picture) == digest(CLONE / picture.relative_to(ROOT)), f'Outdated capture: {picture}')
    play_log = (pipeline / 'Play.log').read_text(encoding='utf-8-sig')
    require(not re.search(r'\b\w*Exception:|\berror CS\d+', play_log), 'Runtime exceptions in final Play run.')
    report = (f'PASS: main town matches the three accepted Unity receipts ({prefab_hash}); '
              f'{len(owned)} owned files verified; {len(new_refs)} new asset GUIDs resolved; '
              f'{len(sources)} source files match the tested clone; {len(pictures)} captures match; '
              'City/Game scenes preserved; backup verified; no runtime exceptions in final Play run.\n')
    (QA / 'MainApplied.txt').write_text(report, encoding='utf-8')
    print(report, end='')


if __name__ == '__main__':
    main()
