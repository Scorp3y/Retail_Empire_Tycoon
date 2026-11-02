"""Verify accepted Unity inputs, transfer price-only changes and native territory screenshots."""
from pathlib import Path
import hashlib
import json
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]
CLONE = ROOT / 'Library/CityUpdateQA/IsolatedProject'
RUN = ROOT / 'Library/TerritoryPurchaseUpdate'
DOCS = ROOT / 'Docs/TerritoryPurchaseUpdate'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def main():
    report = CLONE / 'Library/TerritoryPurchaseQA/result.txt'
    require(report.read_text(encoding='utf-8-sig').startswith('PASS:'), 'Unity acceptance did not pass.')
    log = (RUN / 'Acceptance.log').read_text(encoding='utf-8-sig')
    require(not re.search(r'^\w*Exception:|\berror CS\d+', log, re.M), 'Unity compiler or runtime error.')
    sources = json.loads((RUN / 'Sources.json').read_text())
    for relative in sources:
        require(digest(ROOT / relative) == digest(CLONE / relative), f'Untested source: {relative}')
    baseline = json.loads((RUN / 'Baseline.json').read_text())
    for relative, expected in baseline.items():
        require(digest(ROOT / relative) == expected, f'Original scene or town changed: {relative}')
    relative = 'Assets/Art/TownForgottenYard/Land24.asset'
    source, target = CLONE / relative, ROOT / relative
    without_prices = lambda p: re.sub(r'^    price: \d+\s*$', '', p.read_text(encoding='utf-8-sig'), flags=re.M).split()
    require(without_prices(source) == without_prices(target), 'Parcel identities or geometry changed.')
    prices = [int(v) for v in re.findall(r'^    price: (\d+)', source.read_text(), re.M)]
    require(len(prices) == 24 and sum(prices) == 735500, 'Unexpected land catalogue.')
    shutil.copy2(source, target)
    DOCS.mkdir(parents=True, exist_ok=True)
    for source in (CLONE / 'Docs/TerritoryPurchaseUpdate').glob('*.png'):
        shutil.copy2(source, DOCS / source.name)
    for name in ['result.txt', 'Prices.csv']:
        shutil.copy2(CLONE / 'Library/TerritoryPurchaseQA' / name, DOCS / name)
    audit = dict(sources={name: digest(ROOT / name) for name in sources}, scenes=baseline,
                 catalogue=digest(target), total_price=sum(prices))
    (DOCS / 'AcceptedSources.json').write_text(json.dumps(audit, indent=2), encoding='utf-8')
    print(f'PASS: {len(sources)} accepted source files; 24 price-only parcel changes; original scenes/town unchanged; player save unchanged in Unity acceptance.')


if __name__ == '__main__':
    main()
