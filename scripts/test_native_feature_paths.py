"""Mutation controls for the same assertions used on actual platform-native PDFs."""
import unittest
import tempfile
from pathlib import Path

from verify_native_feature_paths import ROOT, validate_row_fixture, expected_note_texts, validate_continued_row, validate_furniture_words, inspect_list_markers, list_marker_label


class NativeFeaturePathTests(unittest.TestCase):
    def test_fixture_pin_accepts_checkout_endings_but_rejects_content_changes(self):
        original = (ROOT / "examples/business-table-long-description.html").read_bytes()
        lf = original.replace(b"\r\n", b"\n")
        with tempfile.TemporaryDirectory() as directory:
            fixture = Path(directory) / "fixture.html"
            for content in (lf, lf.replace(b"\n", b"\r\n")):
                fixture.write_bytes(content)
                self.assertEqual(validate_row_fixture(fixture),
                                 "e6dd417ec5da8193124c18110a6b48575508d034c9c91cbbc3e002ef3dc26650")
                self.assertEqual(fixture.read_bytes(), content)
                fixture.write_bytes(content.replace(b"FINAL REVIEW NOTE:", b"CHANGED REVIEW NOTE:"))
                with self.assertRaises(AssertionError):
                    validate_row_fixture(fixture)


    def test_vector_marker_pdf_evidence_rejects_offpage_and_wrong_colours(self):
        from verify_native_feature_paths import inspect_vector_marker_geometry
        # Test with 40 records of each kind across two pages and a known
        # 12pt body font. This is a checker mutation, not renderer evidence.
        def n(value):
            return f'{value:.3f}'.rstrip('0').rstrip('.') or '0'
        def circle(cx, y):
            radius, k = 1.44, 1.44 * .5522847498307936
            arcs = [
                (cx+radius,y+k,cx+k,y+radius,cx,y+radius),
                (cx-k,y+radius,cx-radius,y+k,cx-radius,y),
                (cx-radius,y-k,cx-k,y-radius,cx,y-radius),
                (cx+k,y-radius,cx+radius,y-k,cx+radius,y)]
            return ('q .157 .333 .478 RG .45 w ' + n(cx+radius) + ' ' + n(y) + ' m ' +
                    ' '.join(' '.join(map(n, arc)) + ' c' for arc in arcs) + ' S Q')
        def square(cx, y):
            return 'q .569 .271 .18 rg ' + ' '.join(
                map(n, (cx-1.8,y-1.8,3.6,3.6))) + ' re f Q'
        pages = []
        streams = []
        for page in range(2):
            words = []
            commands = []
            for idx in range(20):
                yy = 50 + idx*17
                i = page*20 + idx+1
                for prefix, maker in [('CIR',circle),('SQR',square)]:
                    # Separate kinds vertically to keep each group clear.
                    top = yy if prefix == 'CIR' else yy+360
                    words.append((f'{prefix}-{i:02d}',60,top-5,94,top+5))
                    commands.append(maker(52,841.89-top))
            pages.append((595.28,841.89,words))
            streams.append(('\n'.join(commands)+'\n').encode())
        assert inspect_vector_marker_geometry(pages,streams)['circlesStroked'] == 40
        changed = list(streams)
        changed[0] = changed[0].replace(b' RG .45 w ',b' RG .75 w ',1)
        with self.assertRaises(AssertionError):
            inspect_vector_marker_geometry(pages,changed)
        changed = list(streams)
        changed[0] = changed[0].replace(b' RG ',b' RG ',1).replace(b'746.89 m ',b'946.89 m ',1)
        with self.assertRaises(AssertionError):
            inspect_vector_marker_geometry(pages,changed)

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

    def test_overlapping_phrase_words_are_rejected(self):
        plain, decorated = self.words(2)
        words = [(word[0], 37.0, 16.0, 67.0, 25.0) if word[0] == "report" else word
                 for word in decorated[2]]
        with self.assertRaises(AssertionError):
            validate_furniture_words(plain, (decorated[0], decorated[1], words), 2)

    @staticmethod
    def row_pages():
        first = ['Item Description / delivery notes Amount', 'SRV-001'] + [f'{n:02d}. Note {n}' for n in range(1, 13)] + ['600.00']
        second = ['Item Description / delivery notes Amount',
                  'Authored brand asset, kept with its own complete image border.'] + [f'{n:02d}. Note {n}' for n in range(13, 25)] + [
            'SRV-002', '300.00', 'TOTAL / ILLUSTRATIVE', '900.00']
        return [first, second]

    def test_complete_authored_caption_on_either_boundary_page(self):
        notes, final = expected_note_texts(
            (ROOT / 'examples/business-table-long-description.html').read_text(encoding='utf-8'))
        caption = 'Authored brand asset, kept with its own complete image border.'
        first = ['Item Description / delivery notes Amount', 'SRV-001'] + notes[:12] + ['600.00']
        second = ['Item Description / delivery notes Amount', caption] + notes[12:] + [
            final, 'SRV-002', 'Document generation',
            'Prepared the agreed searchable reports after the template review.',
            '300.00', 'TOTAL / ILLUSTRATIVE', '900.00',
            'Synthetic capability sample. Amounts are illustrative and are not a product quote or commercial offer.']
        validate_continued_row([first, second], notes, final)
        first.insert(-1, caption)
        second.remove(caption)
        validate_continued_row([first, second], notes, final)

    def test_caption_missing_duplicate_and_boundary_corruption_rejected(self):
        caption = 'Authored brand asset, kept with its own complete image border.'
        mutations = [
            lambda p: p[1].remove(caption),
            lambda p: p[1].insert(1, caption),
            lambda p: p[0].insert(-1, caption),
            lambda p: p[1].__setitem__(1, caption.replace('complete', 'partial')),
            lambda p: p[1].insert(1, p[1].pop(2)),
            lambda p: p[0].insert(2, p[1].pop(1)),
            lambda p: p[1].insert(0, p[1].pop(1)),
            lambda p: p[1].insert(2, 'unexpected boundary text'),
        ]
        for mutation in mutations:
            pages = self.row_pages()
            mutation(pages)
            with self.subTest(pages=pages), self.assertRaises(AssertionError):
                validate_continued_row(pages)

    def test_continued_row_positive_and_mutations(self):
        self.assertEqual(validate_continued_row(self.row_pages())['notes'], 24)
        changes = [
            lambda p: p[0].remove('12. Note 12'),
            lambda p: p[1].append('24. Note 24'),
            lambda p: p[0].remove('Item Description / delivery notes Amount'),
            lambda p: p[1].remove('SRV-002'),
            lambda p: p[1].reverse(),
        ]
        for change in changes:
            pages = self.row_pages(); change(pages)
            with self.subTest(pages=pages), self.assertRaises(AssertionError):
                validate_continued_row(pages)

    def test_nonleading_words_and_swapped_page_numbers_are_rejected(self):
        plain, decorated = self.words(2)
        width, height, words = decorated
        for label in ('report', 'evaluation', 'offline', 'of', '2'):
            mutated = list(words)
            index = next(i for i, word in enumerate(mutated) if word[0] == label)
            token = mutated[index]
            mutated[index] = (token[0], -100.0, token[2], -60.0, token[4])
            with self.subTest(label=label), self.assertRaises(AssertionError):
                validate_furniture_words(plain, (width, height, mutated), 2)
        plain, decorated = self.words(1)
        words = list(decorated[2]); one, two = 1, 3
        original_one, original_two = words[one], words[two]
        words[one] = (original_one[0], original_two[1], original_one[2], original_two[3], original_one[4])
        words[two] = (original_two[0], original_one[1], original_two[2], original_one[3], original_two[4])
        with self.assertRaises(AssertionError):
            validate_furniture_words(plain, (decorated[0], decorated[1], words), 1)

    def test_note_body_truncation_rejected_even_when_prefix_survives(self):
        pages = self.row_pages()
        pages[0][3] = '02. TRUNCATED'
        with self.assertRaises(AssertionError):
            validate_continued_row(pages)

    def test_duplicate_wrapped_continuation_line_rejected(self):
        pages = self.row_pages()
        pages[0].insert(4, 'unwanted duplicated wrapped text')
        with self.assertRaises(AssertionError):
            validate_continued_row(pages)

    def test_repeated_header_moved_to_wrong_page_rejected(self):
        pages = self.row_pages()
        pages[0].insert(0, pages[1].pop(0))
        with self.assertRaises(AssertionError):
            validate_continued_row(pages)

    def test_following_row_and_total_before_continuation_rejected(self):
        pages = self.row_pages()
        for marker in ('SRV-002', '300.00', 'TOTAL / ILLUSTRATIVE', '900.00'):
            pages[1].remove(marker)
            pages[0].insert(1, marker)
        with self.assertRaises(AssertionError):
            validate_continued_row(pages)

    @staticmethod
    def marker_pages():
        first, second = [], []
        for prefix, count, kind, x in [('CHK', 40, 'alpha', 72.0),
                                        ('ACT', 25, 'roman', 82.0)]:
            for idx in range(1, count+1):
                page = first if prefix == 'CHK' and idx <= 20 else second
                y = 35.0 + (idx % 20) * 15
                mark = list_marker_label(kind, idx)
                page.extend([(mark, x-25, y, x-8, y+10),
                             (f'{prefix}-{idx:02d}', x, y, x+50, y+10)])
        for idx in range(1,10):
            second.append((f'NOTE-{idx:02d}', 64, 450+idx*15, 120, 460+idx*15))
        second.append(('END-REPORT', 36, 700, 100, 710))
        return [(595,842,first),(595,842,second)]

    def test_independent_list_marker_oracle_accepts_real_layout_shape(self):
        result=inspect_list_markers(self.marker_pages())
        self.assertEqual(result['records'],74)
        self.assertEqual(result['alphaMarkers'],40)
        self.assertEqual(result['romanMarkers'],25)
        self.assertEqual(list_marker_label('alpha',27),'aa.')
        self.assertEqual(list_marker_label('roman',9),'IX.')

    def test_independent_list_marker_oracle_rejects_detachment_and_duplication(self):
        for mode in ('missing','duplicate','moved'):
            pages=self.marker_pages()
            words=pages[0][2]
            if mode=='missing':
                words.pop(0)
            elif mode=='duplicate':
                words.append(words[0])
            else:
                marker=words[0]; words[0]=(marker[0],marker[1],marker[2]+40,marker[3],marker[4]+40)
            with self.subTest(mode=mode), self.assertRaises(AssertionError):
                inspect_list_markers(pages)


if __name__ == '__main__':
    unittest.main()
