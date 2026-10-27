"""Integration checks for local-only inputs. Run in the project Docker container."""
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'offline'))
sys.path.insert(0, str(ROOT / 'godot290'))
from local_sources import legacy106_catalog, canonical, CATALOG_SHA
from text_sources import supplemental_text
from recover_rules import recover, normalized, CONTRACTS
from prepare import SOURCE, EXPECTED

class LocalSourceTests(unittest.TestCase):
    def test_catalog_matches_previous_migration_contract(self):
        catalog = legacy106_catalog()
        self.assertEqual(hashlib.sha256(canonical(catalog)).hexdigest(), CATALOG_SHA)
        self.assertEqual((len(catalog['characters']), len(catalog['quests'])), (522, 371))

    def test_text_payload_and_order_are_unchanged(self):
        text = supplemental_text(ROOT / 'offline/v400/playable/text-fallback.json', 'v400')
        payload = json.dumps(text, ensure_ascii=True, separators=(',', ':')).encode()
        self.assertEqual(hashlib.sha256(payload).hexdigest(),
                         '6388a5c96fcdf7eb864667a25c222e23c3977748fd94a1b381f3f75aef45c67f')
        self.assertTrue(all(isinstance(v, str) for v in text.values()))

    def test_missing_and_wrong_inputs_fail_explicitly(self):
        with tempfile.TemporaryDirectory(prefix='dg-source-negative-') as directory:
            p = Path(directory) / 'missing.apk'
            with self.assertRaises(FileNotFoundError):
                legacy106_catalog(p)
            with self.assertRaises(FileNotFoundError):
                recover(p, EXPECTED, Path(directory) / 'project')
            p.write_bytes(b'wrong version')
            with self.assertRaises(ValueError):
                legacy106_catalog(p)
            with self.assertRaises(ValueError):
                recover(p, EXPECTED, Path(directory) / 'project')
            with self.assertRaises(ValueError):
                supplemental_text(ROOT / 'offline/v400/playable/text-fallback.json', 'v400', p)
            self.assertFalse((Path(directory) / 'project').exists())

    def test_clean_recovery_matches_previous_rule_bodies(self):
        with tempfile.TemporaryDirectory(prefix='dg-rule-test-') as directory:
            project = Path(directory)
            output = recover(SOURCE, EXPECTED, project)
            for name, contract in CONTRACTS.items():
                text = (output / name).read_text()
                self.assertEqual(hashlib.sha256(normalized(text).encode()).hexdigest(), contract[3])
                self.assertFalse((project / name).exists())
            manifest = json.loads((output / 'provenance.json').read_text())
            self.assertEqual(manifest['source_apk_sha256'], EXPECTED)
            # A previous generated-project layout is upgraded without duplicate classes.
            for name in CONTRACTS:
                (project / name).write_bytes((output / name).read_bytes())
            recover(SOURCE, EXPECTED, project)
            self.assertTrue(all(not (project / name).exists() for name in CONTRACTS))

if __name__ == '__main__':
    unittest.main()
