#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Check generated editor assets and adapters using disposable files and a fake CLI."""
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import types
import unittest
from unittest.mock import patch
from urllib.parse import parse_qs, urlparse, unquote
import html

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parent.parent
SUPPORT = ROOT / 'tools/editors/support'


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


if '--require-bat' in sys.argv:
    sys.argv.remove('--require-bat')
    if not (shutil.which('bat') or shutil.which('batcat')):
        raise SystemExit('bat or batcat is required for syntax-engine verification.')

GEN = load('editor_generator', ROOT / 'scripts/sync-editor-bundles.py')
DATA = json.loads((SUPPORT / 'completions.json').read_text())
sublime = types.ModuleType('sublime')
sublime.load_resource = lambda _: json.dumps(DATA)
sublime.INHIBIT_WORD_COMPLETIONS = 1
plugin = types.ModuleType('sublime_plugin')
plugin.EventListener = object
with patch.dict(sys.modules, sublime=sublime, sublime_plugin=plugin):
    COMPLETION = load('ges_completion_test', SUPPORT / 'ges_completions.py')


class EditorTests(unittest.TestCase):
    def test_generated_files_match_and_scope_captures_are_preserved(self):
        for path, text in GEN.generate().items():
            self.assertEqual(text, path.read_text(), str(path))
        value = GEN.rules([{'begin': 'a', 'end': 'b', 'name': 'outer', 'contentName': 'inner',
                            'beginCaptures': {'1': {'name': 'start'}}, 'endCaptures': {'2': {'name': 'end'}},
                            'patterns': [{'match': 'bb', 'name': 'escaped'}], 'applyEndPatternLast': True}])[0]
        self.assertEqual({1: 'start'}, value['captures'])
        self.assertEqual({'match': 'b', 'pop': True, 'captures': {2: 'end'}}, value['push'][-1])
        self.assertEqual({'meta_content_scope': 'inner'}, value['push'][1])
        with self.assertRaises(ValueError):
            GEN.rules([{'match': '.', 'unknownFutureBehavior': True}])

    def test_completion_contexts_match_in_both_editors(self):
        for line, current, expected, absent in [
            ('x as ', '', ':Number', 'emit'), ('x as :N', 'N', 'Number', ':Number'),
            ('x is :', '', 'Text', 'fold'), ('items[:f', 'f', 'fold', 'function'),
            (':Nu', 'Nu', 'Number', 'emit'), ('em', 'em', 'emit', 'Number'),
            ('"é"; x as ', '', ':Number', 'emit'),
        ]:
            with self.subTest(line=line):
                python = [w for w in COMPLETION.candidates(line) if w.lstrip(':').lower().startswith(current.lower())]
                env = dict(os.environ, TM_CURRENT_LINE=line, TM_LINE_INDEX=str(len(line.encode())), TM_CURRENT_WORD=current, TM_SCOPE='source.gameeventscript')
                perl = subprocess.check_output(['perl', str(SUPPORT / 'complete.pl')], env=env, text=True).splitlines()
                self.assertEqual(python, perl)
                self.assertIn(expected, perl)
                self.assertNotIn(absent, perl)
        result = subprocess.check_output(['perl', str(SUPPORT / 'complete.pl')], env=dict(
            os.environ, TM_CURRENT_LINE='x as :N', TM_LINE_INDEX='7',
            TM_CURRENT_WORD=':N', TM_SCOPE='source.gameeventscript'), text=True).splitlines()
        self.assertIn(':Number', result)
        self.assertNotIn('Number', result)
        for scope in ('source.gameeventscript comment.line', 'source.gameeventscript string.quoted.double'):
            result = subprocess.check_output(['perl', str(SUPPORT / 'complete.pl')], env=dict(os.environ, TM_SCOPE=scope), text=True)
            self.assertEqual('', result)
        class ExcludedView:
            def match_selector(self, point, selector):
                return False
        self.assertIsNone(COMPLETION.GesCompletions().on_query_completions(ExcludedView(), '', [1]))

    def test_textmate_commands_quote_paths_preserve_status_and_escape_output(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            fake = root / 'ges'
            fake.write_text('#!' + sys.executable + '\nimport json,sys\nprint(json.dumps(sys.argv[1:]))\nprint("<script>unsafe</script>")\nsys.exit(7 if sys.argv[1]=="run" else 0)\n')
            fake.chmod(0o755)
            source = root / 'quoted " and $(touch unexpected) ü.ges'
            source.write_text('on Main {}')
            source.with_suffix('.gesb').write_bytes(b'fixture')
            env = dict(os.environ, PATH=str(root) + os.pathsep + os.environ['PATH'], TM_FILEPATH=str(source), TM_BUNDLE_SUPPORT=str(SUPPORT))
            for action in ('check', 'compile', 'run', 'dump'):
                result = subprocess.run(['bash', str(SUPPORT / 'ges-command.sh'), action], env=env, text=True, capture_output=True)
                self.assertEqual(7 if action == 'run' else 0, result.returncode, result.stderr)
                expected = str(source.with_suffix('.gesb')) if action == 'dump' else str(source)
                self.assertIn(json.dumps([action, expected]), html.unescape(result.stdout))
                self.assertIn('&lt;script&gt;', result.stdout)
                self.assertFalse((root / 'unexpected').exists())
            source.with_suffix('.gesb').unlink()
            result = subprocess.run(['bash', str(SUPPORT / 'ges-command.sh'), 'dump'], env=env, text=True, capture_output=True)
            self.assertEqual(1, result.returncode)
            self.assertIn('Compile the source first', result.stdout)
            missing = dict(env, PATH=str(root / 'missing'))
            result = subprocess.run(['/bin/bash', str(SUPPORT / 'ges-command.sh'), 'check'], env=missing, text=True, capture_output=True)
            self.assertEqual(127, result.returncode)
            self.assertIn('CLI not found', result.stdout)

    def test_error_links_preserve_special_characters(self):
        path = '/tmp/ü # " <test>.ges'
        result = subprocess.check_output(['perl', str(SUPPORT / 'output.pl')], input=path + '(4,9): error compile.example: <bad>\n', text=True)
        url = html.unescape(re.search('href="([^"]+)"', result)[1])
        query = parse_qs(urlparse(url).query)
        self.assertEqual(path, unquote(urlparse(query['url'][0]).path))
        self.assertEqual(['4'], query['line'])
        self.assertEqual(['9'], query['column'])
        self.assertIn('&lt;bad&gt;', result)

    @unittest.skipUnless(shutil.which('bat') or shutil.which('batcat'), 'bat not installed; adapter checks still run')
    def test_bat_loads_both_grammars_and_highlights_embedded_source(self):
        bat = shutil.which('bat') or shutil.which('batcat')
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            syntax = root / 'config/syntaxes'
            syntax.mkdir(parents=True)
            for path in (ROOT / 'tools/editors/Sublime Text/GameEventScript').glob('*.sublime-syntax'):
                shutil.copy2(path, syntax / path.name)
            env = dict(os.environ, BAT_CONFIG_DIR=str(root / 'config'), BAT_CACHE_PATH=str(root / 'cache'))
            subprocess.run([bat, 'cache', '--build'], env=env, check=True, capture_output=True)
            languages = subprocess.check_output([bat, '--list-languages', '--color=never'], env=env, text=True)
            self.assertIn('GameEventScript', languages)
            self.assertIn('gesa', languages)
            examples = {
                'ges': 'module test.bot\n// comment\non Main(args) { emit ConsoleOut("Hello ""pilot""", 20%) with #ready }\n',
                'gesa': '.segment source "bot.ges"\non Main(args) { emit ConsoleOut("hello", 20%) }\n.segment code\n.source-line "bot.ges" 1 | let x be 2\n',
            }
            for extension, source in examples.items():
                path = root / ('sample.' + extension)
                path.write_text(source)
                output = subprocess.check_output([bat, '--style=plain', '--paging=never', '--color=always', '--theme=Monokai Extended', str(path)], env=env, text=True)
                ansi = r'\x1b\[[0-9;]*m'
                self.assertEqual(source, re.sub(ansi, '', output))
                self.assertGreater(len(set(re.findall(ansi, output))), 3)
                if extension == 'gesa':
                    embedded = output.splitlines()[1]
                    self.assertGreater(len(set(re.findall(ansi, embedded))), 3)


if __name__ == '__main__':
    unittest.main()
