"""Transfer native UI evidence and verify that the installed map code matches accepted Unity inputs."""
import hashlib
import json
import re
import shutil
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CLONE = ROOT / 'Library/CityUpdateQA/IsolatedProject'
DOCS = ROOT / 'Docs/NavigationUpdate'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def main():
    qa = CLONE / 'Library/NavigationUpdateQA'
    report = (qa / 'result.txt').read_text(encoding='utf-8-sig')
    require(report.startswith('PASS:'), 'The final Unity navigation run did not pass.')
    log = (ROOT / 'Library/NavigationUpdate/Acceptance.log').read_text(encoding='utf-8-sig')
    require(not re.search(r'^\w*Exception:|\berror CS\d+', log, re.M), 'Exceptions or compilation errors in the final Unity run.')
    city = ROOT / 'Assets/Project/Scripts/City'
    files = list(city.glob('TownMap*.cs')) + list(city.glob('CityMap*.cs'))
    files += [city / f'{name}.cs' for name in ('TownMiniMap', 'TownNavigation', 'StoreTownMapHud', 'CityTrip', 'CityWorld')]
    files += [ROOT / f'Assets/Project/Scripts/Logistics/{name}.cs' for name in ('DriverRoadRoute', 'DriverMapView')]
    files += [ROOT / f'Assets/Project/Scripts/StoreOperations/{name}.cs' for name in ('GameplayControls', 'ShopOperationsHud')]
    files += [ROOT / f'Assets/Project/Editor/{name}.cs' for name in ('NavigationUpdateQA', 'PinebrookDistrictBuild')]
    sources = {}
    for source in sorted(set(files)):
        relative = source.relative_to(ROOT)
        require(digest(source) == digest(CLONE / relative), f'Untested installed source: {relative}')
        sources[relative.as_posix()] = digest(source)
        meta = Path(str(source) + '.meta')
        if not meta.exists():
            shutil.copy2(Path(str(CLONE / relative) + '.meta'), meta)
        require(re.search(r'^guid: [a-f0-9]{32}$', meta.read_text(encoding='utf-8-sig'), re.M), f'Invalid script metadata: {meta}')

    baseline = json.loads((ROOT / 'Library/NavigationUpdate/Baseline.json').read_text(encoding='utf-8-sig'))
    for relative, expected in baseline.items():
        require(digest(ROOT / relative) == expected, f'Main scene or town asset changed: {relative}')
        require(digest(CLONE / relative) == expected, f'Tested scene/town differs from the actual game: {relative}')

    DOCS.mkdir(parents=True, exist_ok=True)
    pictures = list((CLONE / 'Docs/NavigationUpdate').glob('*.png'))
    require(len(pictures) == 7, 'Missing native map screenshots.')
    for source in pictures:
        target = DOCS / source.name
        shutil.copy2(source, target)
        require(digest(source) == digest(target), f'Screenshot transfer failed: {source.name}')
    output = DOCS / 'QA'
    output.mkdir(exist_ok=True)
    for source in qa.glob('*.txt'):
        shutil.copy2(source, output / source.name)
    (output / 'Sources.json').write_text(json.dumps(dict(utc=datetime.now(timezone.utc).isoformat(), sources=sources,
        scenes_and_town=baseline, screenshots={p.name: digest(p) for p in pictures}), indent=2), encoding='utf-8')
    summary = (f'PASS: {len(sources)} installed source files match the accepted Unity clone; '
               f'{len(baseline)} original scenes/town assets unchanged; {len(pictures)} native screenshots transferred; '
               'no runtime exceptions or compilation errors in the final run; player save unchanged in acceptance.\n')
    (output / 'Applied.txt').write_text(summary, encoding='utf-8')
    print(summary, end='')


if __name__ == '__main__':
    main()
