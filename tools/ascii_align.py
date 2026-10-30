#!/usr/bin/env python3
"""Pad ASCII box/table diagrams so every line in a block ends at the same column (M.ORCH-T1305).

LLMs reliably miscount characters when hand-aligning ASCII art, so a box's right-hand border
drifts a column or two partway down. Prompt instructions don't fix this reliably: the model still
has to count characters correctly while generating, and that's the part it's bad at. This fixes it
mechanically instead: find the widest line in each blank-line-separated block, then extend every
other line in that block out to that width — by lengthening its own trailing rule/fill run (so a
"-----+" or "-- Stats ---------+" stays a clean rule) or, for a plain content row, by inserting
spaces just before its own trailing wall character ("| text |" stays "| text   |").

Usage:
    python tools/ascii_align.py < diagram.txt      # aligned result to stdout
    python tools/ascii_align.py diagram.txt
    python tools/ascii_align.py --check diagram.txt   # validate only; exit 1 if misaligned
    python tools/ascii_align.py --write diagram.txt   # rewrite the file in place

Also usable as a library: `from ascii_align import align_text`.
"""
from __future__ import annotations

import argparse
import sys

# Characters a horizontal rule/fill run is built from: extending one of these just lengthens it.
RULE_CHARS = set("-=_.~─━═╌╍┄┅┈┉·")

# Characters that close off the right side of a box row (a wall, corner or junction): padding goes
# in just before one of these rather than after it.
RIGHT_EDGE = set("|│┃｜+┐┘┤╮╯╢┫║╗╝╣╬>")


def _pad_line(line: str, width: int) -> str:
    """Extends ``line`` (already known to be no wider than ``width``) out to ``width``."""
    body = line.rstrip("\n")
    stripped = body.rstrip()
    if len(stripped) >= width:
        return body
    need = width - len(stripped)
    trailing = ""
    if stripped and stripped[-1] in RIGHT_EDGE:
        trailing = stripped[-1]
        stripped = stripped[:-1]
    if stripped and stripped[-1] in RULE_CHARS:
        stripped += stripped[-1] * need
    else:
        stripped += " " * need
    return stripped + trailing


def align_block(lines: list[str]) -> list[str]:
    """Pads every line in one blank-line-free block out to its own widest line."""
    width = max((len(line.rstrip()) for line in lines), default=0)
    return [_pad_line(line, width) for line in lines]


def align_text(text: str) -> str:
    """Aligns every blank-line-separated block of ``text`` independently; blank lines pass through."""
    out: list[str] = []
    block: list[str] = []
    had_trailing_newline = text.endswith("\n")
    for line in text.split("\n"):
        if line.strip() == "":
            if block:
                out.extend(align_block(block))
                block = []
            out.append(line)
        else:
            block.append(line)
    if block:
        out.extend(align_block(block))
    result = "\n".join(out)
    if had_trailing_newline and not result.endswith("\n"):
        result += "\n"
    return result


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("path", nargs="?", help="file to read (default: stdin)")
    parser.add_argument("--check", action="store_true", help="validate only; exit 1 if any line would change")
    parser.add_argument("--write", action="store_true", help="rewrite the input file in place instead of printing")
    args = parser.parse_args(argv)

    if args.write and not args.path:
        parser.error("--write requires a file path, not stdin")

    if args.path:
        with open(args.path, "r", encoding="utf-8") as f:
            text = f.read()
    else:
        text = sys.stdin.read()

    aligned = align_text(text)

    if args.check:
        if aligned != text:
            sys.stderr.write("ascii_align: misaligned — run without --check to see the fixed version\n")
            return 1
        return 0

    if args.write:
        if aligned != text:
            with open(args.path, "w", encoding="utf-8") as f:
                f.write(aligned)
        return 0

    sys.stdout.write(aligned)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
