"""Refresh the isolated city's inputs from the actual workspace, preserving owned Pinebrook outputs."""
from pathlib import Path
import os,hashlib,shutil,json
project=Path(__file__).resolve().parents[1];clone=project/'Library/CityUpdateQA/IsolatedProject'
root=Path('\\\\?\\'+str(project));target_root=Path('\\\\?\\'+str(clone));changed=[]
for scope in ('Assets','Packages','ProjectSettings'):
 for folder,dirs,files in os.walk(root/scope):
  for name in files:
   source=Path(folder)/name;relative=source.relative_to(root)
   if str(relative).replace('\\','/').startswith('Assets/Art/Pinebrook/'):continue
   target=target_root/relative
   if target.exists() and source.stat().st_size==target.stat().st_size and hashlib.sha256(source.read_bytes()).digest()==hashlib.sha256(target.read_bytes()).digest():continue
   target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target);changed.append(str(relative))
out=project/'Library/PinebrookInputSync.json';out.write_text(json.dumps(changed,indent=2));print('Refreshed',len(changed),'actual workspace inputs',flush=True)
