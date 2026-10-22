"""Extract NPC speech and message samples without changing server scripts."""
import argparse
import json
import re
from pathlib import Path


def extract(directory):
    result = {}
    files = sorted(directory.glob('*.txt'))
    for path in files:
        speech = False
        for raw in path.read_text(encoding='utf-8-sig').splitlines():
            line = raw.strip()
            if not line or line.startswith('//'):
                continue
            if line.startswith('['):
                speech = line.startswith('[@')
                continue
            if line.startswith('#'):
                speech = line.upper() in ('#SAY', '#ELSESAY')
                continue
            message = re.match(r'(?:LINE|LOCAL|GLOBAL|GUILD)?MESSAGE\s+"(.*?)"', line, re.I)
            if speech or message:
                text = message[1] if message else line
                origins = result.setdefault(text, [])
                if path.name not in origins:
                    origins.append(path.name)
    return result, len(files)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path, help='NPC script directory, such as Envir/NPCs/GM')
    parser.add_argument('output', type=Path, help='Destination sample JSON')
    args = parser.parse_args()
    result, count = extract(args.directory)
    if not count:
        parser.error('No NPC scripts found')
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print(f'Extracted {len(result)} unique speech/message lines from {count} scripts.')
