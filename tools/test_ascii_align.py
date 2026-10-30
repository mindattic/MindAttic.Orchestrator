"""Tests for ascii_align.py (M.ORCH-T1305). Run with: python tools/test_ascii_align.py"""
from __future__ import annotations

import unittest

from ascii_align import align_text


class AlignTextTests(unittest.TestCase):
    def test_pads_short_content_rows_to_the_longest_row_in_the_block(self):
        text = "\n".join([
            "+----------------+",
            "| short          |",
            "| a much longer line |",
            "+----------------+",
        ])
        aligned = align_text(text)
        lines = aligned.split("\n")
        widths = {len(line) for line in lines}
        self.assertEqual(len(widths), 1, aligned)
        for line in lines:
            self.assertTrue(line.endswith("|") or line.endswith("+"), line)

    def test_extends_a_rule_line_instead_of_inserting_a_gap(self):
        text = "\n".join([
            "+-- Stats --+",
            "| a much longer row of content |",
        ])
        aligned = align_text(text).split("\n")
        self.assertEqual(len(aligned[0]), len(aligned[1]))
        # The rule's own fill run lengthens; its "Stats" title and the run right after it are
        # untouched, and nothing but that fill character and the closing "+" was appended.
        self.assertTrue(aligned[0].startswith("+-- Stats --"), aligned[0])
        self.assertTrue(aligned[0].endswith("-+"), aligned[0])
        added = aligned[0][len("+-- Stats --"):-1]
        self.assertTrue(set(added) <= {"-"}, aligned[0])

    def test_box_drawing_characters(self):
        text = "\n".join([
            "┌─────┐",
            "│ hi              │",
            "└─────┘",
        ])
        aligned = align_text(text).split("\n")
        widths = {len(line) for line in aligned}
        self.assertEqual(len(widths), 1, aligned)
        self.assertTrue(aligned[0].endswith("─┐"))
        self.assertTrue(aligned[2].endswith("─┘"))

    def test_blank_lines_separate_independent_blocks(self):
        text = "\n".join([
            "+-----+",
            "| one |",
            "+-----+",
            "",
            "+---------------+",
            "| two           |",
            "+---------------+",
        ])
        aligned = align_text(text)
        self.assertEqual(aligned, text, "already-aligned blocks pass through unchanged")

    def test_already_aligned_text_is_unchanged(self):
        text = "+-----+\n| hi  |\n+-----+\n"
        self.assertEqual(align_text(text), text)

    def test_preserves_trailing_newline_presence(self):
        with_newline = "| a   |\n| bb  |\n"
        without_newline = "| a   |\n| bb  |"
        self.assertTrue(align_text(with_newline).endswith("\n"))
        self.assertFalse(align_text(without_newline).endswith("\n"))

    def test_plain_text_without_borders_just_gets_right_padded(self):
        text = "short\na much longer line"
        aligned = align_text(text).split("\n")
        self.assertEqual(len(aligned[0]), len(aligned[1]))
        self.assertEqual(aligned[0].rstrip(), "short")


if __name__ == "__main__":
    unittest.main()
