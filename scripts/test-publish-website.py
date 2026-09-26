#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Qualify website publication using disposable local Git remotes; never GitHub."""

import importlib.util
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch, MagicMock

ROOT = Path(__file__).resolve().parent.parent
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("publish_website", ROOT / "scripts/publish-website.py")
publisher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(publisher)


class WebsitePublicationTests(unittest.TestCase):
    def setUp(self):
        artifacts = ROOT / "artifacts/website-publication-tests"
        artifacts.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="website publish ", dir=artifacts)
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)
        self.remote = self.directory / "remote.git"
        self.repository = self.directory / "source"
        self.output = self.directory / "dist"
        self.repository.mkdir()
        self.git(self.directory, "init", "--bare", str(self.remote))
        self.git(self.repository, "init", "--initial-branch=main")
        self.git(self.repository, "config", "user.name", "Website test")
        self.git(self.repository, "config", "user.email", "website@example.invalid")
        self.git(self.repository, "config", "commit.gpgsign", "false")
        self.git(self.repository, "remote", "add", "origin", str(self.remote))
        (self.repository / "private-source.txt").write_text("Source must never enter site history.")
        self.source = self.advance_main()
        self.files = {"index.html", "404.html", "docs/index.html", "pagefind/pagefind.js", "_astro/site.css"}
        for name in self.files:
            path = self.output / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(name)

    def git(self, directory, *args):
        return publisher.git(directory, *args)

    def advance_main(self):
        self.git(self.repository, "add", "--all")
        self.git(self.repository, "commit", "--allow-empty", "-m", "Source revision")
        self.git(self.repository, "push", "origin", "main")
        return self.git(self.repository, "rev-parse", "HEAD")

    def publish(self, source=None, webhook_url=None):
        publisher.publish(self.repository, self.output, source or self.source, webhook_url)

    def site(self):
        return self.git(self.remote, "rev-parse", "refs/heads/site")

    def test_webhook_runs_only_after_successful_changed_push(self):
        url = "https://hosting.example.invalid/hook?token=test"
        def notify(received):
            self.assertEqual(url, received)
            self.assertIn(self.source, self.git(self.remote, "show", "-s", "--format=%B", "site"))
        with patch.object(publisher, "notify_host", side_effect=notify) as hook:
            self.publish(webhook_url=url)
            hook.assert_called_once_with(url)
            hook.reset_mock()
            self.source = self.advance_main()
            self.publish(webhook_url=url)
            self.advance_main()
            (self.output / "index.html").write_text("stale build")
            self.publish(webhook_url=url)
            hook.assert_not_called()

    def test_failed_push_does_not_call_webhook(self):
        original = publisher.git
        def reject(repository, *args, **kwargs):
            if args[0] == "push":
                raise RuntimeError("push failed")
            return original(repository, *args, **kwargs)
        with patch.object(publisher, "git", side_effect=reject), patch.object(publisher, "notify_host") as hook:
            with self.assertRaisesRegex(RuntimeError, "push failed"):
                self.publish(webhook_url="https://hosting.example.invalid/hook")
            hook.assert_not_called()

    def test_webhook_retries_without_disclosing_url(self):
        url = "https://hosting.example.invalid/hook?token=secret"
        response = MagicMock()
        response.__enter__.return_value.status = 204
        with patch.object(publisher.urllib.request, "build_opener") as build, patch.object(publisher.time, "sleep"):
            build.return_value.open.side_effect = [publisher.urllib.error.URLError(url), response]
            publisher.notify_host(url)
            self.assertEqual(2, build.return_value.open.call_count)
            build.return_value.open.assert_called_with(url, timeout=20)
            self.assertIsNone(build.call_args.args[0].redirect_request(None, None, 302, "", {}, url))
            build.return_value.open.side_effect = publisher.urllib.error.URLError(url)
            build.return_value.open.reset_mock()
            with self.assertRaises(RuntimeError) as failure:
                publisher.notify_host(url)
            self.assertNotIn("secret", str(failure.exception))
            self.assertEqual(3, build.return_value.open.call_count)

    def test_invalid_webhook_is_rejected_before_publication(self):
        with self.assertRaises(ValueError):
            self.publish(webhook_url="http://hosting.example.invalid/hook")
        self.assertEqual("", self.git(self.remote, "for-each-ref", "refs/heads/site"))

    def test_initial_publication_is_an_independent_root_containing_only_output(self):
        self.publish()
        self.assertEqual(1, len(self.git(self.remote, "rev-list", "--parents", "-n", "1", "site").split()))
        self.assertEqual(self.files, set(self.git(self.remote, "ls-tree", "-r", "--name-only", "site").splitlines()))
        self.assertIn(self.source, self.git(self.remote, "show", "-s", "--format=%B", "site"))
        self.assertEqual("main", self.git(self.repository, "branch", "--show-current"))
        self.assertEqual("", self.git(self.repository, "status", "--porcelain"))

    def test_updates_are_fast_forward_and_remove_obsolete_assets(self):
        self.publish()
        previous = self.site()
        (self.output / "_astro/site.css").unlink()
        (self.output / "_astro/new.css").write_text("new")
        self.source = self.advance_main()
        self.publish()
        self.assertEqual(previous, self.git(self.remote, "rev-parse", "site^"))
        files = set(self.git(self.remote, "ls-tree", "-r", "--name-only", "site").splitlines())
        self.assertNotIn("_astro/site.css", files)
        self.assertIn("_astro/new.css", files)

    def test_identical_output_does_not_add_commits(self):
        self.publish()
        previous = self.site()
        self.source = self.advance_main()
        self.publish()
        self.assertEqual(previous, self.site())

    def test_stale_source_cannot_overwrite_the_published_site(self):
        self.publish()
        previous = self.site()
        self.advance_main()
        (self.output / "index.html").write_text("old build")
        self.publish()
        self.assertEqual(previous, self.site())

    def test_main_advancing_during_preparation_prevents_publication(self):
        original = publisher.git

        def intercept(repository, *args, **kwargs):
            result = original(repository, *args, **kwargs)
            if args[0] == "write-tree":
                self.advance_main()
            return result

        with patch.object(publisher, "git", side_effect=intercept):
            self.publish()
        self.assertEqual("", self.git(self.remote, "for-each-ref", "refs/heads/site"))

    def test_incomplete_build_and_symlinks_do_not_publish(self):
        (self.output / "index.html").unlink()
        with self.assertRaisesRegex(RuntimeError, "Incomplete"):
            self.publish()
        (self.output / "index.html").write_text("restored")
        (self.output / "leaked-source").symlink_to(self.repository / "private-source.txt")
        with self.assertRaisesRegex(RuntimeError, "static deployment"):
            self.publish()
        self.assertEqual("", self.git(self.remote, "for-each-ref", "refs/heads/site"))

    def test_existing_unmanaged_branch_is_preserved(self):
        self.git(self.repository, "push", "origin", "main:site")
        with self.assertRaisesRegex(RuntimeError, "not a generated"):
            self.publish()
        self.assertEqual(self.source, self.site())

    def test_metadata_and_source_artifacts_are_rejected_before_publication(self):
        for name in ['.DS_Store', '._index.html', 'Thumbs.db', 'desktop.ini', '.git', '.github', 'node_modules', '__MACOSX']:
            for parent in [self.output, self.output / 'docs']:
                path = parent / name
                with self.subTest(path=path.relative_to(self.output)):
                    path.write_text('not a deployment asset')
                    try:
                        with self.assertRaisesRegex(RuntimeError, 'Not a static deployment asset'):
                            self.publish()
                        self.assertEqual('', self.git(self.remote, 'for-each-ref', 'refs/heads/site'))
                    finally:
                        path.unlink()

    def test_source_worktree_and_staging_area_are_preserved(self):
        (self.repository / "staged.txt").write_text("staged")
        self.git(self.repository, "add", "staged.txt")
        (self.repository / "private-source.txt").write_text("local edit")
        before = self.git(self.repository, "status", "--porcelain")
        self.publish()
        self.assertEqual(before, self.git(self.repository, "status", "--porcelain"))
        self.assertEqual("staged", self.git(self.repository, "show", ":staged.txt"))

    def test_concurrent_site_update_is_not_force_overwritten(self):
        self.publish()
        previous = self.site()
        (self.output / "index.html").write_text("new build")
        original = publisher.git
        competitor = None

        def intercept(repository, *args, **kwargs):
            nonlocal competitor
            if args[0] == "push":
                tree = original(self.repository, "rev-parse", f"{previous}^{{tree}}")
                competitor = original(self.repository, "commit-tree", tree, "-p", previous,
                                      "-m", "Concurrent website update")
                original(self.repository, "push", "origin", f"{competitor}:refs/heads/site")
            return original(repository, *args, **kwargs)

        with patch.object(publisher, "git", side_effect=intercept):
            with self.assertRaisesRegex(RuntimeError, "push failed"):
                self.publish()
        self.assertEqual(competitor, self.site())


if __name__ == "__main__":
    unittest.main()
