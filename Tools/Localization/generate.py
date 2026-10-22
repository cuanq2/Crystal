"""Generate client Chinese terms from versioned sources; never touch a game DB."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
DATA = Path(__file__).resolve().parent / 'Data'


def read(path):
    def unique_pairs(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f'Duplicate JSON key in {path}: {key}')
            result[key] = value
        return result
    return json.loads(path.read_text(encoding='utf-8-sig'), object_pairs_hook=unique_pairs)


def build(records_path):
    records = read(records_path)
    items = read(DATA / 'ItemNames.zh-CN.json')
    terms = read(DATA / 'Terms.zh-CN.json')
    folded = {key.casefold() for key in terms}
    if len(folded) != len(terms):
        raise ValueError('Case-insensitive duplicate term keys')
    aliases = {}
    for row in records:
        name = row['friendlyName']
        if name not in items:
            raise ValueError(f'Missing item translation: {row["index"]} {name}')
        value = items[name]
        if re.search(r'[A-Za-z\uac00-\ud7af\ufffd]', value) or not re.search(r'[\u3400-\u9fff]', value):
            raise ValueError(f'Incomplete item translation: {name} = {value}')
        for key in (name, row['name']):
            if key.casefold() in folded:
                raise ValueError(f'Item alias also defined in general terms: {key}')
            if key.casefold() in aliases and aliases[key.casefold()][1] != value:
                raise ValueError(f'Conflicting item aliases: {key}')
            aliases[key.casefold()] = (key, value)
    terms.update(dict(aliases.values()))

    phrases = read(ROOT / 'Client/Localization/Chinese.Quests.json')
    token_pattern = r'/[^<>{}\r\n]+(?=[>}])|<\$[^>]*>'
    for source, translation in phrases.items():
        if Counter(re.findall(token_pattern, source)) != Counter(re.findall(token_pattern, translation)):
            raise ValueError(f'Quest action, color or variable changed: {source}')
    return terms, len(records), len({row['friendlyName'] for row in records}), len(aliases)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--write', action='store_true', help='Update Client/Localization/Chinese.Trial.json only')
    mode.add_argument('--check', action='store_true', help='Verify generated terms are current (default)')
    parser.add_argument('--records', type=Path, default=ROOT / 'Checks/Fixtures/item-name-audit.json', help='Item audit JSON; every item type is required')
    args = parser.parse_args()
    terms, records, names, aliases = build(args.records)
    output = ROOT / 'Client/Localization/Chinese.Trial.json'
    generated = json.dumps(terms, ensure_ascii=False, indent=2, sort_keys=True) + '\n'
    if args.write:
        output.write_text(generated, encoding='utf8', newline='\n')
    elif output.read_text(encoding='utf-8-sig') != generated:
        print('Generated terms are stale. Run: python Tools/Localization/generate.py --write', file=sys.stderr)
        return 1
    print(f'Chinese terms verified: {records} item records, {names} names, {aliases} aliases, {len(terms)} terms; all item types covered.')
    return 0


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except (ValueError, OSError, KeyError) as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
