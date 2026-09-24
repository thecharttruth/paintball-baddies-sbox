"""Check the integrity of the original recovered competition source snapshot."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
manifest = json.loads((root / 'docs/SOURCE_MANIFEST.json').read_text(encoding='utf-8'))
failures = []
for entry in manifest['files']:
    path = (root / entry['path']).resolve()
    if not path.is_relative_to(root) or not path.is_file():
        failures.append(entry['path'] + ': missing or invalid path')
    elif hashlib.sha256(path.read_bytes()).hexdigest() != entry['sha256']:
        failures.append(entry['path'] + ': changed')
if failures:
    print('\n'.join(failures))
    raise SystemExit(1)
print(f"Verified {len(manifest['files'])} original source files from build {manifest['build']}.")
