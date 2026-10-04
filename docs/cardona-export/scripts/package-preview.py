"""Package the lightweight preview, cached dependency notices, and exact source provenance."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
parser.add_argument('--tag', required=True)
args = parser.parse_args()
repo = Path(__file__).resolve().parents[3]
output = Path(args.output).resolve()
if any(c not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-' for c in args.tag):
    raise ValueError('Use a simple release tag.')
commit = subprocess.check_output(['git', '-C', str(repo), 'rev-parse', 'HEAD'], text=True).strip()
if subprocess.check_output(['git', '-C', str(repo), 'status', '--porcelain'], text=True).strip():
    raise RuntimeError('Commit and review the source before packaging.')
build = repo / 'src/Greenshot/bin/Debug-Light/net480'
stage = output / args.tag
stage.mkdir(parents=True, exist_ok=False)
app = stage / 'app'
app.mkdir()
allowed = {'.dll', '.exe', '.config', '.xml', '.ttf'}
for path in build.iterdir():
    if path.is_file() and path.suffix.lower() in allowed:
        shutil.copy2(path, app / path.name)
shutil.copytree(build / 'Languages', app / 'Languages')
if not (app / 'Greenshot.exe').is_file():
    raise RuntimeError('Build the preview first.')
for name in ['Greenshot.Base.dll', 'Greenshot.Editor.dll']:
    tested = repo / 'src/Greenshot.Tests/bin/Debug/net480' / name
    if hashlib.sha256(tested.read_bytes()).digest() != hashlib.sha256((app / name).read_bytes()).digest():
        raise RuntimeError('Packaged library differs from the tested library: ' + name)
docs = repo / 'docs/cardona-export'
shutil.copy2(docs / 'scripts/Start-Preview.ps1', stage / 'Start-Preview.ps1')
for name in ['PREVIEW-README.md', 'ACCEPTANCE-CHECKLIST.md', 'CODE-REVIEW.md']:
    shutil.copy2(docs / name, stage / name)
shutil.copy2(repo / 'LICENSE', stage / 'LICENSE')
shutil.copy2(docs / 'SETTINGS-GUIDE.md', stage / 'SETTINGS-GUIDE.md')
shutil.copytree(docs / 'whitepaper/images', stage / 'whitepaper/images')
license_dir = stage / 'licenses'
license_dir.mkdir()
assets = json.loads((repo / 'src/Greenshot/obj/project.assets.json').read_text(encoding='utf-8'))
package_roots = [Path(p) for p in assets['packageFolders']]
runtime_names = {p.name.lower() for p in app.glob('*.dll') if not p.name.startswith('Greenshot.')}
covered = set()
notices = ['# Third-party notices', '', 'Packages identified by exact runtime DLL hashes against the cached build graph.', '']
expressions = set()
for key, lib in assets['libraries'].items():
    if lib['type'] != 'package':
        continue
    package = next((p / lib['path'] for p in package_roots if (p / lib['path']).is_dir()), None)
    if package is None:
        continue
    matches = []
    for filename in lib['files']:
        candidate = package / filename
        if candidate.name.lower() in runtime_names and candidate.is_file() and hashlib.sha256(candidate.read_bytes()).digest() == hashlib.sha256((app / candidate.name).read_bytes()).digest():
            matches.append(candidate.name)
    if not matches:
        continue
    covered.update(n.lower() for n in matches)
    nuspec = next(package.glob('*.nuspec'))
    metadata = ET.parse(nuspec).getroot().find('{*}metadata')
    fields = {e.tag.split('}')[-1]: e.text or '' for e in metadata}
    notices += ['## ' + key, 'DLLs: ' + ', '.join(sorted(set(matches))), 'Authors: ' + fields.get('authors', ''), fields.get('copyright', ''), 'License: ' + fields.get('license', fields.get('licenseUrl', '')), 'Project: ' + fields.get('projectUrl', ''), '']
    license_node = metadata.find('{*}license')
    if license_node is not None and license_node.get('type') == 'expression':
        expressions.add(license_node.text)
    target = license_dir / key.replace('/', '-')
    target.mkdir()
    for filename in lib['files']:
        candidate = package / filename
        if candidate.is_file() and any(term in candidate.name.lower() for term in ['license', 'notice', 'copyright']) and candidate.suffix.lower() in {'.txt', '.md', '.html', ''}:
            shutil.copy2(candidate, target / candidate.name)
    shutil.copy2(nuspec, target / nuspec.name)
if runtime_names - covered:
    raise RuntimeError('Unidentified runtime dependency: ' + ', '.join(sorted(runtime_names - covered)))
for expression in sorted(expressions):
    if any(c not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-' for c in expression):
        raise RuntimeError('Review compound license expression: ' + expression)
    with urllib.request.urlopen('https://licenses.nuget.org/' + expression, timeout=30) as response:
        (license_dir / (expression + '.html')).write_bytes(response.read())
with urllib.request.urlopen('https://raw.githubusercontent.com/mozilla/twemoji-colr/master/LICENSE.md', timeout=30) as response:
    (license_dir / 'Twemoji-Mozilla-LICENSE.md').write_bytes(response.read())
notices += ['## Twemoji Mozilla', 'Unmodified font copied from the Greenshot source tree.', 'Mozilla Foundation and Twemoji contributors. Font construction: Apache-2.0; emoji artwork: CC-BY-4.0.', 'https://github.com/mozilla/twemoji-colr', 'See licenses/Twemoji-Mozilla-LICENSE.md.', '']
(stage / 'THIRD-PARTY-NOTICES.md').write_text('\n'.join(notices), encoding='utf-8')
manifest = {'tag': args.tag, 'sourceCommit': commit, 'sourceUrl': 'https://github.com/jasonCGI/greenshot/tree/' + commit,
            'buildConfiguration': 'DebugLight', 'targetFramework': 'net480', 'nativeAcceptance': 'pending',
            'files': {p.relative_to(stage).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(stage.rglob('*')) if p.is_file()}}
(stage / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
archive = output / (args.tag + '-windows-net48.zip')
with zipfile.ZipFile(archive, 'x', compression=zipfile.ZIP_DEFLATED) as zipped:
    for path in sorted(stage.rglob('*')):
        if path.is_file():
            zipped.write(path, args.tag + '/' + path.relative_to(stage).as_posix())
checksum = hashlib.sha256(archive.read_bytes()).hexdigest()
(output / 'SHA256SUMS.txt').write_text(checksum + '  ' + archive.name + '\n', encoding='ascii')
print(json.dumps({'archive': str(archive), 'sha256': checksum, 'sourceCommit': commit}))
