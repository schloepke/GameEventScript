# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Keep operating-system metadata out of copied, archived and deployed site assets."""

import argparse
from pathlib import Path
import shutil


def is_metadata(path):
    return any(part in ('.DS_Store', 'Thumbs.db', 'desktop.ini', '__MACOSX')
               or part.startswith('._') for part in Path(path).parts)


def ignore_metadata(directory, names):
    return [name for name in names if is_metadata(name)]


def copy_assets(source, destination):
    shutil.copytree(source, destination, ignore=ignore_metadata)


def remove_metadata(directory):
    # Used only on generated build output, never on authored source directories.
    # Inspect names, and never follow symlinks when removing metadata directories.
    for path in sorted(Path(directory).rglob('*'), key=lambda p: len(p.parts), reverse=True):
        if is_metadata(path.name):
            if path.is_dir() and not path.is_symlink():
                shutil.rmtree(path)
            else:
                path.unlink(missing_ok=True)


def validate_static_assets(directory):
    for path in Path(directory).rglob('*'):
        relative = path.relative_to(directory)
        if path.is_symlink() or is_metadata(relative) or path.name in ('.git', '.github', 'node_modules'):
            raise RuntimeError(f'Not a static deployment asset: {relative}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    copy = commands.add_parser('copy')
    copy.add_argument('source', type=Path)
    copy.add_argument('destination', type=Path)
    check = commands.add_parser('check')
    check.add_argument('directory', type=Path)
    args = parser.parse_args()
    if args.command == 'copy':
        copy_assets(args.source, args.destination)
    else:
        validate_static_assets(args.directory)
