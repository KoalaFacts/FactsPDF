import unittest
from verify_operations_report import compact


class ReadingTextTests(unittest.TestCase):
    def test_missing_english_word_separator_is_not_layout_whitespace(self):
        self.assertNotEqual(compact('next update time'), compact('nextupdatetime'))

    def test_inserted_english_word_separator_changes_text(self):
        self.assertNotEqual(compact('handover'), compact('hand over'))

    def test_line_wraps_preserve_english_words_and_chinese_text(self):
        self.assertEqual(compact('next\nupdate\ntime'), compact('next update time'))
        self.assertEqual(compact('下次\n更新'), compact('下次更新'))


if __name__ == '__main__':
    unittest.main()
