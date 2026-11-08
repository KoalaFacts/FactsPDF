"""Offline verification of pinned upstream archive download and fail-closed behavior."""
import hashlib
import io
import os
from pathlib import Path
import tempfile
import unittest
import urllib.error
import urllib.request
from unittest.mock import patch

import font_style_fonts as fonts


class PinnedFontArchiveTests(unittest.TestCase):
    def setUp(self):
        self.payload = b'original authenticated release fixture'
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.archive = Path(directory.name) / fonts.ARCHIVE
        expected_size = patch.object(fonts, 'ARCHIVE_SIZE', len(self.payload))
        expected_sha = patch.object(fonts, 'ARCHIVE_SHA', hashlib.sha256(self.payload).hexdigest())
        expected_size.start()
        expected_sha.start()
        self.addCleanup(expected_size.stop)
        self.addCleanup(expected_sha.stop)

    def test_gateway_timeout_uses_authenticated_vendor_api_with_identical_bytes(self):
        calls = []
        payload = self.payload

        class Opener:
            def open(self, request, timeout):
                calls.append((request.full_url, request.get_header('Authorization')))
                if len(calls) == 1:
                    raise urllib.error.HTTPError(request.full_url, 504, 'gateway timeout', {}, None)
                return io.BytesIO(payload)

        with patch.object(fonts.urllib.request, 'build_opener', return_value=Opener()), \
                patch.dict(os.environ, {'GITHUB_TOKEN': 'test-token'}):
            fonts.acquire_archive(self.archive)
        self.assertEqual(self.archive.read_bytes(), self.payload)
        self.assertEqual(len(calls), 2)
        self.assertTrue(calls[0][0].startswith('https://github.com/be5invis/'))
        self.assertTrue(calls[1][0].startswith('https://api.github.com/repos/be5invis/'))
        self.assertIsNone(calls[0][1])
        self.assertEqual(calls[1][1], 'Bearer test-token')
        self.assertFalse(self.archive.with_name(self.archive.name + '.partial').exists())

    def test_unexpected_archive_bytes_fail_closed_without_retrying_a_different_asset(self):
        class Opener:
            def open(self, request, timeout):
                return io.BytesIO(b'unexpected bytes')

        with patch.object(fonts.urllib.request, 'build_opener', return_value=Opener()):
            with self.assertRaises(ValueError):
                fonts.acquire_archive(self.archive)
        self.assertFalse(self.archive.exists())

    def test_repeated_gateway_timeout_preserves_no_partial_archive(self):
        class Opener:
            def open(self, request, timeout):
                raise urllib.error.HTTPError(request.full_url, 504, 'gateway timeout', {}, None)

        with patch.object(fonts.urllib.request, 'build_opener', return_value=Opener()):
            with self.assertRaisesRegex(RuntimeError, 'both official GitHub endpoints'):
                fonts.acquire_archive(self.archive)
        self.assertFalse(self.archive.exists())
        self.assertFalse(self.archive.with_name(self.archive.name + '.partial').exists())

    def test_existing_archive_checks_sha_without_network_access(self):
        self.archive.write_bytes(self.payload)
        with patch.object(fonts.urllib.request, 'build_opener', side_effect=AssertionError('network')):
            fonts.acquire_archive(self.archive)
        self.archive.write_bytes(b'tampered bytes')
        with patch.object(fonts.urllib.request, 'build_opener', side_effect=AssertionError('network')):
            with self.assertRaisesRegex(ValueError, 'hash mismatch'):
                fonts.acquire_archive(self.archive)

    def test_cross_origin_redirect_never_forwards_api_token_or_uses_http(self):
        handler = fonts._SafeGitHubRedirect()
        original = urllib.request.Request(
            'https://api.github.com/repos/x/y/releases/assets/1',
            headers={'Authorization': 'Bearer secret'})
        redirected = handler.redirect_request(original, None, 302, 'moved', {},
                                              'https://release-assets.githubusercontent.com/content')
        self.assertIsNone(redirected.get_header('Authorization'))
        self.assertEqual(original.get_header('Authorization'), 'Bearer secret')
        with self.assertRaises(ValueError):
            handler.redirect_request(original, None, 302, 'moved', {},
                                     'http://release-assets.githubusercontent.com/content')


if __name__ == '__main__':
    unittest.main()
