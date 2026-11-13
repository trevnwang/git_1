# -*- coding: utf-8 -*-
"""Extract readable structure from a drawio file (mxfile XML)."""
import sys
import html
import xml.etree.ElementTree as ET


def strip_html(s):
    # convert common block tags to newlines then strip remaining tags
    import re
    s = re.sub(r'<br\s*/?>', '\n', s)
    s = re.sub(r'</(div|p|li|tr)>', '\n', s)
    s = re.sub(r'<li[^>]*>', '- ', s)
    s = re.sub(r'<[^>]+>', '', s)
    s = html.unescape(html.unescape(s))
    lines = [ln.rstrip() for ln in s.split('\n')]
    return '\n'.join(lines).strip()


def main(path, out):
    tree = ET.parse(path)
    root = tree.getroot()
    out_lines = []
    for diagram in root.findall('diagram'):
        out_lines.append(f'===== DIAGRAM: {diagram.get("name")} ({diagram.get("id")}) =====')
        model = diagram.find('mxGraphModel')
        if model is None:
            out_lines.append('(no mxGraphModel / compressed?)')
            continue
        cells = {}
        # collect vertices/edges in doc order
        for cell in model.iter('mxCell'):
            cid = cell.get('id')
            val = cell.get('value') or ''
            txt = strip_html(val)
            kind = 'vertex' if cell.get('vertex') else ('edge' if cell.get('edge') else 'cell')
            src = cell.get('source')
            tgt = cell.get('target')
            parent = cell.get('parent')
            geo = cell.find('mxGeometry')
            x = y = w = h = None
            if geo is not None:
                if geo.get('x') is not None:
                    x = round(float(geo.get('x')))
                if geo.get('y') is not None:
                    y = round(float(geo.get('y')))
                w = geo.get('width', '')
                h = geo.get('height', '')
            cells[cid] = dict(kind=kind, text=txt, src=src, tgt=tgt, parent=parent,
                              x=x, y=y, w=w, h=h, style=cell.get('style') or '')
        # Build container hierarchy
        children = {}
        for cid, c in cells.items():
            children.setdefault(c.get('parent'), []).append(cid)

        def label(cid):
            c = cells.get(cid, {})
            t = c.get('text', '').replace('\n', ' / ')
            if len(t) > 60:
                t = t[:60] + '...'
            return f'{cid}[{c.get("kind")}] {t}'

        out_lines.append('--- VERTEXES (grouped by container) ---')
        for cid, c in cells.items():
            if c['kind'] != 'vertex':
                continue
            depth = 0
            p = c['parent']
            while p and p in cells and cells[p]['parent'] not in (None, '0', '1'):
                depth += 1
                p = cells[p]['parent']
            indent = '  ' * depth
            t = c['text'].replace('\n', ' | ')
            out_lines.append(f'{indent}- {cid} pos=({c["x"]},{c["y"]}) size=({c["w"]}x{c["h"]}) "{t}"')

        out_lines.append('--- EDGES ---')
        for cid, c in cells.items():
            if c['kind'] != 'edge':
                continue
            lbl = c['text']
            out_lines.append(f'- {cid}: {c["src"]} -> {c["tgt"]} label="{lbl}"')

        # dump any long vertex text fully
        out_lines.append('--- FULL LONG TEXT BLOCKS ---')
        for cid, c in cells.items():
            if c['kind'] == 'vertex' and len(c['text']) > 200:
                out_lines.append(f'### {cid}')
                out_lines.append(c['text'])
                out_lines.append('')
    with open(out, 'w', encoding='utf-8') as f:
        f.write('\n'.join(out_lines))
    print(f'wrote {out}')


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
