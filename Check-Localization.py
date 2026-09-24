"""Reconciles the string file against the code, by TEXT as well as by key.

A pass that only reconciles key sets proves nothing about what a player sees:
every key can be present and every string still carry the text from three
versions ago. That happened once here and shipped eighteen stale strings.

Reads every {=KEY}Fallback in the C# and the prefabs, reads sta_strings.xml,
and reports three classes: declared but never used, used but never declared,
and declared with text that no longer matches the fallback beside the key.

  python Check-Localization.py <project root> [--fix]

--fix rewrites the text attribute of every drifted string in place and adds
every missing one at the end of <strings>, leaving orphans to be removed by
hand because an orphan can be a key a prefab builds at runtime.
"""
import html
import io
import re
import sys
from pathlib import Path

# One C# string can carry two tags: an MCM group path is written as
# "{=A}Creation Menus/{=B}Gear and Finish", so a fallback runs to the next tag
# or to the end of its literal, and the slash belongs to MCM, not to the label.
TAG = re.compile(r'\{=([A-Za-z0-9_!]+)\}')
STRING = re.compile(r'<string\s+id="([^"]+)"\s+text="([^"]*)"\s*/>')

SOURCE_SUFFIXES = ('.cs', '.xml')
# Folders that hold no shipped text: build output, git internals, reference material,
# and the test project, whose strings are fixtures rather than lines a player reads.
SKIP_PARTS = {'obj', 'bin', 'out', '.git', '.REFERENCE', 'tests'}


def fallback_after(body, start):
    """Text between a tag and whatever ends it: the next tag, or the closing quote.

    Scanning the raw file rather than pairing quotes first, because a quote
    inside a doc comment shifts every pair after it and the whole file then
    reads as having no tags at all.
    """
    end = len(body)
    nxt = TAG.search(body, start)
    if nxt:
        end = nxt.start()
    i = start
    while i < end:
        if body[i] == chr(34) and body[i - 1] != chr(92):
            end = i
            break
        if body[i] == chr(10):
            end = i
            break
        i += 1
    return body[start:end]


def used(root):
    """Key to the fallback text written beside it, from code and prefabs."""
    found = {}
    for path in root.rglob('*'):
        if path.suffix not in SOURCE_SUFFIXES:
            continue
        if set(path.parts) & SKIP_PARTS:
            continue
        if path.name == 'sta_strings.xml':
            continue
        body = path.read_text(encoding='utf-8-sig', errors='replace')
        for mark in TAG.finditer(body):
            key = mark.group(1)
            if key == '!':
                continue
            text = fallback_after(body, mark.end())
            # An MCM group path is one C# string carrying two tags, and MCM
            # splits it on the slash before localizing each segment, so the
            # delimiter is structure and never part of the label. Trimmed only
            # when another tag follows, which is the only place it can occur.
            follows = TAG.search(body, mark.end())
            joined = follows is not None and follows.start() == mark.end() + len(text)
            if joined and text.endswith('/'):
                text = text[:-1]
            found.setdefault(key, []).append((text, path))
    return found


def declared(strings_file):
    body = strings_file.read_text(encoding='utf-8-sig')
    return {m.group(1): html.unescape(m.group(2)) for m in STRING.finditer(body)}, body


def main():
    root = Path(sys.argv[1])
    fix = '--fix' in sys.argv
    strings_file = root / '_Module' / 'ModuleData' / 'Languages' / 'EN' / 'sta_strings.xml'

    in_code = used(root)
    in_file, body = declared(strings_file)

    missing = sorted(k for k in in_code if k not in in_file)
    orphaned = sorted(k for k in in_file if k not in in_code)
    drifted = []
    conflicted = []
    for key, sites in sorted(in_code.items()):
        texts = {fallback for fallback, _ in sites}
        if len(texts) > 1:
            conflicted.append((key, sorted(texts)))
            continue
        fallback = sites[0][0]
        if key in in_file and in_file[key] != fallback:
            drifted.append((key, in_file[key], fallback))

    print(f'declared {len(in_file)}, used {len(in_code)}')
    print(f'missing {len(missing)}, orphaned {len(orphaned)}, '
          f'drifted {len(drifted)}, conflicting fallbacks {len(conflicted)}')

    for key, was, now in drifted:
        print(f'  DRIFT  {key}\n         file: {was}\n         code: {now}')
    for key, texts in conflicted:
        print(f'  CONFLICT {key}: ' + ' | '.join(texts))
    for key in missing:
        print(f'  MISSING {key}: {in_code[key][0][0]}')
    for key in orphaned:
        print(f'  ORPHAN  {key}: {in_file[key]}')

    if fix and (drifted or missing):
        for key, _, now in drifted:
            pattern = re.compile(r'(<string\s+id="' + re.escape(key) + r'"\s+text=")[^"]*(")')
            body = pattern.sub(lambda m: m.group(1) + html.escape(now, quote=True) + m.group(2), body, count=1)
        if missing:
            added = ''.join(
                f'        <string id="{key}" text="{html.escape(in_code[key][0][0], quote=True)}" />\n'
                for key in missing)
            body = body.replace('    </strings>', added + '    </strings>', 1)
        io.open(strings_file, 'w', encoding='utf-8-sig', newline='\r\n').write(body)
        print(f'fixed {len(drifted)} drifted, added {len(missing)} missing')

    return 1 if (missing or drifted or conflicted) and not fix else 0


if __name__ == '__main__':
    sys.exit(main())
