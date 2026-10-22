"""Package existing trial assets and pristine Jev game data, excluding player state."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import time
import zipfile


README = """# Crystal 中文试玩资源与初始数据包

本包包含当前已下载的试玩素材与 Jev 默认游戏内容数据库，供配套 Crystal 源码使用。

- Client/Data：图库、光标、着色器、中文图片覆盖层。
- Client/Map、Client/Sound：已下载的地图和音效。
- Client/Localization：与源码同步的语言文件。
- Server/Server.MirDB：原始 Jev 游戏内容数据库，不含本机账号与角色存档。
- Server/Envir：默认 NPC、任务、掉落、路线等静态内容；已修正两处 RedDagger 掉落行缺少空格的问题。
- Server/Maps：配套默认地图。
- manifest.json：文件清单、大小与来源说明。
- Sources：默认数据库的原始许可和说明。

## 使用

1. 解压到新建的独立运行目录，例如 mir2-crystal-runtime-new；包内 Client、Server 位于目录根层。
2. 按源码仓库 README 和 docs/LOCAL-DEVELOPMENT.zh-CN.md 构建客户端与服务端，并准备相应运行依赖。
3. 将程序放入对应 Client、Server 目录，首次使用另行设置本机配置和新账号。

本包不包含游戏程序、原生依赖 DLL、本机配置、账号库 Server.MirADB、角色存档、日志、备份、个人任务追踪或商店回购物品。
Server.MirDB 是初始化用的默认内容库，已有环境请勿直接覆盖，否则会替换自己修改过的游戏内容。

## 范围与来源

这是当前已下载的试玩资源集，并非全量素材包；其他地图仍可能缺少图库或音效。
已知缺少 Snow/Object1c.Lib、Sound/22.wav、Sound/109.wav 等此前记录的素材。
客户端素材来源：https://mirfiles.co.uk/resources/mir2/crystal/patch/PList.gz
默认数据库：https://github.com/Suprcode/Crystal.Database ，Jev，版本 a19f6dca8f5e238d4ed79801820777abbf0a9ca4。
中文覆盖层与词表来自配套源码项目。Sources/Database-LICENSE.txt 仅对应默认数据库项目。
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--runtime', required=True, type=Path)
    parser.add_argument('--database-source', required=True, type=Path, help='Crystal.Database checkout root')
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    repository = Path(__file__).resolve().parents[1]
    runtime = args.runtime.resolve(strict=True)
    database = args.database_source.resolve(strict=True)
    output = args.output.resolve()
    partial = output.with_suffix(output.suffix + '.partial')
    if output.exists() or partial.exists():
        raise FileExistsError('Refusing to overwrite an existing package or partial package.')
    output.parent.mkdir(parents=True, exist_ok=True)
    entries = []

    def add_tree(base, folder, suffixes):
        source = base / folder
        if not source.is_dir():
            raise FileNotFoundError(source)
        for path in sorted(source.rglob('*')):
            relative = path.relative_to(base)
            if any(part.lower() in {'userdata', '.git', 'backups'} for part in relative.parts):
                continue
            if not path.is_file() or path.suffix.lower() not in suffixes:
                continue
            if path.is_symlink() or not path.resolve().is_relative_to(base):
                raise ValueError(f'Unexpected external file: {relative}')
            entries.append((path, relative.as_posix()))

    add_tree(runtime, 'Client/Data', {'.lib', '.cur', '.ps', '.glsl'})
    add_tree(runtime, 'Client/Map', {'.map'})
    add_tree(runtime, 'Client/Sound', {'.wav', '.lst'})
    add_tree(repository, 'Client/Localization', {'.json'})
    jev = database / 'Jev'
    for folder in ('Drops', 'Events', 'NPCs', 'Previews', 'Quests', 'Recipe', 'Routes', 'SystemScripts'):
        start = len(entries)
        add_tree(jev, 'Envir/' + folder, {'.txt', '.bmp'})
        entries[start:] = [(path, 'Server/' + name) for path, name in entries[start:]]
    start = len(entries)
    add_tree(jev, 'Maps', {'.map'})
    entries[start:] = [(path, 'Server/' + name) for path, name in entries[start:]]
    entries.append((jev / 'Server.MirDB', 'Server/Server.MirDB'))
    for name in ('Notice.txt', 'LoginNotice.txt', 'LineMessage.txt'):
        entries.append((jev / 'Envir' / name, 'Server/Envir/' + name))
    entries.extend([(database / 'LICENSE', 'Sources/Database-LICENSE.txt'),
                    (jev / 'README.md', 'Sources/Database-README.md')])
    names = [name for _, name in entries]
    if len({name.casefold() for name in names}) != len(names):
        raise ValueError('Duplicate archive paths')
    for path, name in entries:
        if not path.is_file():
            raise FileNotFoundError(path)
        if any(part in {'..', 'Goods', 'NameLists', 'Values', 'UserData', 'Configs'} for part in Path(name).parts):
            raise ValueError(f'Unexpected runtime state in package: {name}')
    count = len(entries)
    total = sum(path.stat().st_size for path, _ in entries)
    print(f'Packaging {count} files, {total / 1024**3:.3f} GiB before compression.', flush=True)
    manifest = {
        'createdUtc': datetime.now(timezone.utc).isoformat(),
        'scope': 'Existing trial assets and pristine Jev initial content; not a complete asset download.',
        'databaseCommit': 'a19f6dca8f5e238d4ed79801820777abbf0a9ca4',
        'assetIndexUrl': 'https://mirfiles.co.uk/resources/mir2/crystal/patch/PList.gz',
        'excluded': ['accounts', 'character saves', 'personal config', 'logs', 'backups', 'UserData', 'Goods', 'NameLists', 'Values', 'executables', 'native DLLs'],
        'patches': [], 'files': []}
    last_progress = time.monotonic()
    with zipfile.ZipFile(partial, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=1, allowZip64=True) as archive:
        for index, (path, name) in enumerate(entries, 1):
            size = path.stat().st_size
            if name.startswith('Server/Envir/Drops/') and path.name in {'HiGreatGhoul.txt', 'HiGreatGhoul0.txt'}:
                raw = path.read_bytes()
                fixed = raw.replace(b'1/1RedDagger Q', b'1/1 RedDagger Q')
                archive.writestr(name, fixed)
                size = len(fixed)
                if raw != fixed:
                    manifest['patches'].append(name + ': insert missing space before RedDagger')
            else:
                archive.write(path, name)
            manifest['files'].append({'path': name, 'bytes': size})
            if time.monotonic() - last_progress >= 10:
                print(f'Packed {index}/{count} files.', flush=True)
                last_progress = time.monotonic()
        archive.writestr('README.md', README.encode('utf8'))
        archive.writestr('manifest.json', json.dumps(manifest, ensure_ascii=False, indent=2).encode('utf8'))
    print('Checking ZIP entry list, sizes and CRCs...', flush=True)
    with zipfile.ZipFile(partial) as archive:
        if set(archive.namelist()) != set(names) | {'README.md', 'manifest.json'}:
            raise ValueError('Archive entry list mismatch')
        for entry in manifest['files']:
            if archive.getinfo(entry['path']).file_size != entry['bytes']:
                raise ValueError('Archive size mismatch: ' + entry['path'])
        corrupt = archive.testzip()
        if corrupt:
            raise ValueError('Archive CRC mismatch: ' + corrupt)
    digest = hashlib.sha256()
    with partial.open('rb') as stream:
        for chunk in iter(lambda: stream.read(8 * 1024 * 1024), b''):
            digest.update(chunk)
    partial.rename(output)
    sha256 = digest.hexdigest()
    output.with_suffix('.zip.sha256').write_text(f'{sha256}  {output.name}\n', encoding='ascii')
    report = {'archive': output.name, 'bytes': output.stat().st_size,
              'sha256': sha256, 'files': count + 2, 'crcVerified': True,
              'groups': dict(Counter('/'.join(name.split('/')[:2]) for name in names)),
              'patches': manifest['patches']}
    output.with_suffix('.zip.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print(json.dumps(report, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
