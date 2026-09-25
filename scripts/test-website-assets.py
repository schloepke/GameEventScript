#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Verify metadata filtering and deployment checks with disposable site assets."""

import importlib.util
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
from zipfile import ZipFile

sys.dont_write_bytecode = True
from website_assets import copy_assets, remove_metadata, validate_static_assets

ROOT = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('editor_bundles', ROOT / 'website/scripts/package-editor-bundles.py')
bundles = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bundles)


class WebsiteAssetTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / 'source'
        self.source.mkdir()

    def populate(self, directory):
        for name in ['index.html', '.well-known/security.txt', '.DS_Store', 'docs/.DS_Store',
                     'docs/._index.html', 'Thumbs.db', 'docs/desktop.ini', '__MACOSX/folder/metadata']:
            path = directory / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(name)

    def test_copy_filters_metadata_but_keeps_legitimate_hidden_assets(self):
        self.populate(self.source)
        destination = self.root / 'output'
        copy_assets(self.source, destination)
        files = {p.relative_to(destination).as_posix() for p in destination.rglob('*') if p.is_file()}
        self.assertEqual({'index.html', '.well-known/security.txt'}, files)
        validate_static_assets(destination)
        self.assertTrue((self.source / '.DS_Store').is_file(), 'Copying must not alter source directories.')

    def test_metadata_added_after_build_is_rejected(self):
        for relative in ['.DS_Store', 'docs/.DS_Store', 'docs/._index.html', 'Thumbs.db', 'docs/desktop.ini', '__MACOSX/metadata']:
            path = self.source / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('metadata')
            with self.subTest(path=relative), self.assertRaisesRegex(RuntimeError, 'Not a static deployment asset'):
                validate_static_assets(self.source)
            path.unlink()
            if relative.startswith('__MACOSX/'):
                path.parent.rmdir()

    def test_build_cleanup_preserves_assets_and_does_not_follow_symlinks(self):
        self.populate(self.source)
        outside = self.root / 'outside'
        outside.mkdir()
        (outside / 'keep.txt').write_text('keep')
        (self.source / '._outside').symlink_to(outside, target_is_directory=True)
        remove_metadata(self.source)
        validate_static_assets(self.source)
        files = {p.relative_to(self.source).as_posix() for p in self.source.rglob('*') if p.is_file()}
        self.assertEqual({'index.html', '.well-known/security.txt'}, files)
        self.assertEqual('keep', (outside / 'keep.txt').read_text())

    def test_editor_downloads_exclude_metadata(self):
        (self.source / 'LICENSE').write_text('fixture license')
        for variant, _ in bundles.VARIANTS:
            for name in ['GameEventScript.tmbundle', 'GameEventScriptAssembler.tmbundle']:
                self.populate(self.source / 'tools/editors' / variant / name)
        output = self.root / 'downloads'
        with patch.object(bundles, 'ROOT', self.source), patch.object(bundles, 'OUTPUT', output):
            bundles.package_bundles()
        for archive in output.glob('*.zip'):
            with ZipFile(archive) as package:
                self.assertEqual({'LICENSE', *[
                    f'{bundle}/{name}'
                    for bundle in ['GameEventScript.tmbundle', 'GameEventScriptAssembler.tmbundle']
                    for name in ['index.html', '.well-known/security.txt']
                ]}, set(package.namelist()))


if __name__ == '__main__':
    unittest.main()
