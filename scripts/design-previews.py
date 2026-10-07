#!/usr/bin/env python3
"""Render labelled design previews from shared Core geometry; these are not WPF screenshots.
Run setup-cloud.sh first. Optional preview tool requires PyMuPDF, not the Windows app.
"""
from pathlib import Path
import base64
import html
import json
import fitz

root = Path(__file__).resolve().parents[1]
fixture = json.loads((root / 'artifacts/export-checks/SUMAPP-design.json').read_text())
T = fixture['Theme']
logo = base64.b64encode((root / 'src/EireTodo.Windows/Assets/SumappLogo.png').read_bytes()).decode()
out = root / 'previews'
out.mkdir(exist_ok=True)
W, H = 1600, 900

def text(x, y, value, colour=None, size=17, bold=False):
    return f'<text x="{x}" y="{y}" fill="{colour or T["Ink"]}" font-size="{size}" font-family="sans-serif" font-weight="{"bold" if bold else "normal"}">{html.escape(str(value))}</text>'

def rect(x, y, w, h, fill, stroke='', radius=4):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{radius}" fill="{fill}" stroke="{stroke or "none"}" stroke-width="1"/>'

def frame(module):
    s = [f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="{W}" height="{H}">', rect(0,0,W,H,T['Surface']),rect(0,0,W,104,T['Black'],radius=0),rect(0,0,W,3,T['Yellow'],radius=0),rect(12,10,44,44,'#FFFFFF'), f'<image x="14" y="12" width="40" height="40" xlink:href="data:image/png;base64,{logo}"/>',text(68,40,'SUMAPP','#FFFFFF',22,True),rect(W-370,18,180,28,'#FFFFFF'),text(W-360,38,'Eire · logo pending',size=13)]
    for x, label in [(W-166,'−'),(W-119,'□'),(W-72,'×')]: s += [rect(x,18,36,28,T['Black'],T['DarkTeal']),text(x+10,39,label,'#FFFFFF',17)]
    s += [rect(12,64,210,32,T['DarkTeal']),text(24,86,module,'#FFFFFF',16,True),text(196,86,'⌄','#FFFFFF',16),text(W-315,85,'OFFLINE','#FFFFFF',12),text(W-200,85,'07/10/2026 14:30','#FFFFFF',13)]
    for x, label in [(12,'Home'),(90,'Insert'),(162,'Format'),(249,'View')]:
        s += [rect(x,112,72,34,T['DarkTeal'] if label=='Home' else T['Surface']),text(x+10,135,label,'#FFFFFF' if label=='Home' else T['Ink'],15,True)]
    s += [rect(12,151,W-24,102,'#FFFFFF')]
    return s

def action(s, x, y, label, primary=False, icon='plus'):
    width = len(label)*8+44
    if primary: s.append(rect(x,y-22,width,34,T['DarkTeal']))
    colour = '#FFFFFF' if primary else T['Ink']
    if icon=='plus': motif=f'M{x+12} {y-14} v16 M{x+4} {y-6} h16'
    elif icon=='trash': motif=f'M{x+4} {y-13} h16 M{x+7} {y-13} v16 h10 v-16 M{x+9} {y-17} h6'
    else: motif=f'M{x+4} {y-15} h16 v18 h-16 z M{x+8} {y-10} h8 M{x+8} {y-4} h8'
    s.append(f'<path d="{motif}" stroke="{colour}" fill="none" stroke-width="1.6"/>')
    s.append(text(x+28,y,label,colour,14))

def save(name, parts):
    parts += [text(16,H-20,'DESIGN PREVIEW · Native Windows screenshot not captured · Eire source logo pending',T['Ink'],13),'</svg>']
    svg = ''.join(parts).encode()
    (out / (name+'.svg')).write_bytes(svg)
    document=fitz.open(stream=svg,filetype='svg'); pdf=fitz.open(stream=document.convert_to_pdf(),filetype='pdf')
    pdf[0].get_pixmap(matrix=fitz.Matrix(1,1)).save(out / (name+'.png'))
    (out / (name+'.html')).write_text('<!doctype html><title>SUMAPP design preview</title><body style="margin:0">'+svg.decode()+'</body>')

s=frame('TO-DO')
action(s,26,182,'Add task',True); action(s,26,223,'Edit / notes',icon='doc'); action(s,194,182,'Delete',icon='trash'); action(s,194,223,'Projects',icon='doc')
s += [text(70,246,'TASKS',size=12),text(238,246,'PROJECTS',size=12)]
s += [rect(12,267,W-24,107,'#FFFFFF'),text(25,288,'PROJECT VIEW',size=14,bold=True),rect(25,298,200,40,'#FFFFFF',T['DarkTeal']),text(37,324,'All projects',size=17),text(241,288,'SEARCH TASK',size=14,bold=True),rect(241,298,220,40,'#FFFFFF',T['DarkTeal']),rect(480,309,22,22,'#FFFFFF',T['DarkTeal']),text(512,326,'Hide completed',size=16)]
action(s,683,324,'Clear filters',icon='doc');s.append(text(27,363,'⌄  More filters · notes, category, status and dates',size=16))
cols=[('Status',155),('Project',160),('Task',300),('Start date',135),('Finish date',135),('Category',160),('Notes',220),('Created date / time',205)]
x=12
for label,width in cols: s += [rect(x,389,width,44,T['SoftTeal'],radius=0),text(x+12,417,label,T['DarkTeal'],16,True)];x+=width
rows=[('Steel fabrication BOQ','Procurement','14/10/2026',False,True),('Guide rail','Fabrication','15/10/2026',True,False),('Chain hooks','Fabrication','05/10/2026',False,False),('Chain supports','Fabrication','17/10/2026',False,False),('Pipe supports','Fabrication','18/10/2026',False,False)]
for i,(title,category,finish,done,priority) in enumerate(rows):
    y=433+i*65; colour='#B42318' if i==2 else T['Ink'];s.append(rect(12,y,1470,65,'#FFFFFF' if i!=3 else T['SoftTeal'],radius=0)); x=12
    s += [rect(x+10,y+21,22,22,T['DarkTeal'] if done else '#FFFFFF',T['DarkTeal']),text(x+12,y+39,'✓' if done else '','#FFFFFF',17),rect(x+43,y+20,98,26,T['SoftTeal']),text(x+49,y+38,'Completed' if done else 'To do',T['DarkTeal'],12)]
    x+=155;s.append(text(x+10,y+31,'SOU001',colour)); x+=160;s.append(text(x+10,y+27,title,colour));s.append(text(x+10,y+49,'SOU001 · '+category,T['Ink'],13))
    if priority:s += [rect(x+213,y+36,76,18,T['Yellow']),text(x+217,y+49,'PRIORITY',T['Black'],11,True)]
    x+=300;s.append(text(x+10,y+31,'07/10/2026',colour));x+=135;s.append(text(x+10,y+31,finish,colour));x+=135;s.append(text(x+10,y+31,category,colour));x+=160;s.append(text(x+10,y+31,'Review drawing and…',colour));x+=220;s.append(text(x+10,y+31,'07/10/2026 09:48',colour))
    s.append(f'<path d="M12 {y+65} H1482" stroke="{T["DarkTeal"]}" stroke-opacity="0.2"/>')
s += [rect(12,772,1470,5,T['SoftTeal']),rect(12,772,1000,5,T['DarkTeal']),text(14,824,'5 tasks · 1 completed · 1 overdue',size=16)]
action(s,340,824,'Show overdue log',icon='doc');action(s,570,824,'Backup / export',icon='doc');action(s,790,824,'Restore',icon='doc');action(s,940,824,'Data folder',icon='doc')
save('SUMAPP-to-do-design',s)

s=frame('MIND MAP / WBS')
s += [rect(26,160,250,34,'#FFFFFF',T['DarkTeal']),text(37,182,'SOU001 · Mechanical items',size=15)]
action(s,26,223,'New diagram',icon='doc');action(s,326,182,'Sibling',True);action(s,326,223,'Child',icon='plus');action(s,500,182,'Edit title',icon='doc');action(s,500,223,'Node details',icon='doc');action(s,688,182,'Delete node',icon='trash');action(s,888,182,'Export',True,icon='doc');action(s,888,223,'Overdue log',icon='doc')
scene=fixture['Scene'];scale=min(1.2,(W-100)/scene['Width'],570/scene['Height']);dx=(W-scene['Width']*scale)/2;dy=285+(570-scene['Height']*scale)/2
s.append(f'<g transform="translate({dx} {dy}) scale({scale})">')
for mark in fixture['Marks']:
    d=mark['Data'];kind=mark['Kind']
    if kind=='PdfBox':s.append(rect(d['X'],d['Y'],d['Width'],d['Height'],d['Fill'],d['Stroke'],d['Radius']))
    elif kind=='PdfText':s.append(text(d['X'],d['Y'],d['Text'],d['Colour'],d['Size'],d['Bold']))
    elif kind=='PdfLine':
        points=d['Points'];p='M'+str(points[0]['X'])+' '+str(points[0]['Y'])
        if d['Curve'] and len(points)==4:p+=' C'+' '.join(str(v) for q in points[1:] for v in [q['X'],q['Y']])
        else:p+=' '+' '.join('L'+str(q['X'])+' '+str(q['Y']) for q in points[1:])
        s.append(f'<path d="{p}" fill="none" stroke="{d["Colour"]}" stroke-width="{d["Thickness"]}"/>')
s += ['</g>',text(16,855,'9 nodes · Drag space to pan · Ctrl+wheel to zoom',size=15)]
save('SUMAPP-mind-map-design',s)
print('Design previews rendered from Core geometry and tokens; not Windows screenshots:',out)
