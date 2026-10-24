"""Mutation controls for assertions also used on real platform-native PDFs."""
import unittest

from verify_native_feature_paths import validate_continued_row, validate_furniture_words


class NativeFeaturePathTests(unittest.TestCase):
    @staticmethod
    def words(page):
        body = [('BODY', 120.0, 110.0, 150.0, 120.0)]
        extra = [('Page', 490.0, 811.0, 511.0, 820.0),
                 (str(page), 520.0, 811.0, 525.0, 820.0),
                 ('of', 534.0, 811.0, 545.0, 820.0),
                 ('2', 554.0, 811.0, 560.0, 820.0)]
        if page == 2:
            extra += [('Business', 36.0, 16.0, 80.0, 25.0),
                      ('report', 84.0, 16.0, 114.0, 25.0),
                      ('Internal', 420.0, 16.0, 450.0, 25.0),
                      ('evaluation', 458.0, 16.0, 520.0, 25.0),
                      ('Prepared', 36.0, 811.0, 78.0, 820.0),
                      ('offline', 83.0, 811.0, 120.0, 820.0)]
        return (595.0, 842.0, body), (595.0, 842.0, body + extra)

    def test_two_native_furniture_pages_have_independent_body_and_regions(self):
        self.assertEqual(validate_furniture_words(*self.words(1), 1)['addedWords'], 4)
        self.assertEqual(validate_furniture_words(*self.words(2), 2)['addedWords'], 10)

    def test_body_move_and_missing_words_are_rejected(self):
        plain, decorated = self.words(1)
        width, height, words = decorated
        for mutated in [words[1:], [('BODY', 120, 120, 150, 130)] + words[1:]]:
            with self.subTest(mutated=mutated[0]), self.assertRaises(AssertionError):
                validate_furniture_words(plain, (width, height, mutated), 1)

    def test_wrong_totals_extra_region_and_wrong_geometry_are_rejected(self):
        plain, decorated = self.words(2)
        width, height, words = decorated
        mutants = [
            [(['3' if w[0] == '2' else w[0]] + list(w[1:])) for w in words],
            words + [('Duplicate', 50, 12, 90, 22)],
            [(w[0], 270, w[2], 290, w[4]) if w[0] == 'Page' else w for w in words],
            [(w[0], w[1], 190, w[3], 205) if w[0] == 'Internal' else w for w in words],
        ]
        for mutant in mutants:
            with self.subTest(mutant=mutant), self.assertRaises(AssertionError):
                validate_furniture_words(plain, (width, height, [tuple(w) for w in mutant]), 2)

    @staticmethod
    def row_pages():
        first = ['Description / delivery notes', 'SRV-001', '600.00'] + [f'{n:02d}. Note {n}' for n in range(1, 13)]
        second = ['Description / delivery notes'] + [f'{n:02d}. Note {n}' for n in range(13, 25)] + [
            'SRV-002', '300.00', 'TOTAL / ILLUSTRATIVE', '900.00']
        return [first, second]

    def test_continued_row_positive_and_mutations(self):
        self.assertEqual(validate_continued_row(self.row_pages())['notes'], 24)
        changes = [
            lambda p: p[0].remove('12. Note 12'),
            lambda p: p[1].append('24. Note 24'),
            lambda p: p[0].remove('Description / delivery notes'),
            lambda p: p[1].remove('SRV-002'),
            lambda p: p[1].reverse(),
        ]
        for change in changes:
            pages = self.row_pages(); change(pages)
            with self.subTest(pages=pages), self.assertRaises(AssertionError):
                validate_continued_row(pages)


if __name__ == '__main__':
    unittest.main()
