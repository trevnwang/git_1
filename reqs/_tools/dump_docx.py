# -*- coding: utf-8 -*-
"""Dump a .docx into structured plain text so the requirement can be reviewed."""
import sys
from docx import Document
from docx.table import Table
from docx.text.paragraph import Paragraph
from docx.oxml.ns import qn


def iter_block_items(parent):
    from docx.document import Document as _Doc
    if isinstance(parent, _Doc):
        parent_elm = parent.element.body
    else:
        parent_elm = parent._tc
    for child in parent_elm.iterchildren():
        if child.tag == qn('w:p'):
            yield Paragraph(child, parent)
        elif child.tag == qn('w:tbl'):
            yield Table(child, parent)


def dump_para(p):
    text = p.text
    style = p.style.name if p.style is not None else ''
    num = ''
    # list numbering info
    pPr = p._p.pPr
    if pPr is not None and pPr.numPr is not None:
        num = ' [list]'
    return f'[{style}{num}] {text}'


def dump_table(t):
    lines = []
    lines.append(f'<<TABLE rows={len(t.rows)} cols={len(t.columns)}>>')
    for row in t.rows:
        cells = []
        for c in row.cells:
            cells.append(c.text.replace('\n', ' / '))
        lines.append(' | '.join(cells))
    lines.append('<<END TABLE>>')
    return '\n'.join(lines)


def main(path, out):
    doc = Document(path)
    out_lines = []
    for block in iter_block_items(doc):
        if isinstance(block, Paragraph):
            out_lines.append(dump_para(block))
        elif isinstance(block, Table):
            out_lines.append(dump_table(block))
    # core properties
    cp = doc.core_properties
    head = [
        f'== title: {cp.title}',
        f'== author: {cp.author}',
        f'== created: {cp.created}',
        f'== modified: {cp.modified}',
        f'== revision: {cp.revision}',
        '',
    ]
    with open(out, 'w', encoding='utf-8') as f:
        f.write('\n'.join(head + out_lines))
    print(f'wrote {out}: {len(out_lines)} blocks')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
