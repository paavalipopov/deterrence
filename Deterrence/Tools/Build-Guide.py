"""Build a self-contained illustrated guide from data exported by the C# model.

Uses only the standard library for SVG/HTML authoring. PyMuPDF is optional for
rendering chart previews; it is already available in this environment.
"""
from pathlib import Path
import html
import json
import math
import re

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'docs' / 'assets'
DATA = json.loads((ASSETS / 'two-hazards.json').read_text(encoding='utf-8'))
SAMPLES = DATA['samples']
BLUE, ORANGE, NAVY = '#1670a5', '#b65b12', '#20364d'
MUTED, GRID = '#526477', '#dce4eb'


def text(x, y, label, size=18, fill=NAVY, anchor='start', extra=''):
    return f'<text x="{x:.2f}" y="{y:.2f}" font-family="Arial, sans-serif" font-size="{size}" fill="{fill}" text-anchor="{anchor}" {extra}>{html.escape(str(label))}</text>'


def figure(kind):
    width, height = 960, 560
    left, right, top, bottom = 86, 35, 95, 430
    maximum = 25000 if kind == 'credit' else 110000
    x = lambda day: left + day / 26 * (width-left-right)
    y = lambda value: bottom - value / maximum * (bottom-top)
    def line(coords, color, thickness=3, dash=''):
        # Explicit dash segments also survive SVG readers that ignore dash arrays.
        commands = []
        if not dash:
            commands = [('M' if i == 0 else 'L') + f'{xx:.2f},{yy:.2f}' for i, (xx, yy) in enumerate(coords)]
        else:
            pattern = [float(value) for value in dash.split()]
            phase, remaining = 0, pattern[0]
            for first, last in zip(coords, coords[1:]):
                xx, yy = first
                dx, dy = last[0]-xx, last[1]-yy
                length = math.hypot(dx, dy)
                if length == 0:
                    continue
                used = 0
                while used < length-1e-9:
                    step = min(remaining, length-used)
                    start = (xx+dx*used/length, yy+dy*used/length)
                    used += step
                    end = (xx+dx*used/length, yy+dy*used/length)
                    if phase % 2 == 0:
                        commands.extend([f'M{start[0]:.2f},{start[1]:.2f}', f'L{end[0]:.2f},{end[1]:.2f}'])
                    remaining -= step
                    if remaining < 1e-9:
                        phase = (phase+1) % len(pattern)
                        remaining = pattern[phase]
        return f'<path d="{" ".join(commands)}" fill="none" stroke="{color}" stroke-width="{thickness}" stroke-linecap="butt"/>'
    title = 'Two balances, one total' if kind == 'credit' else 'What the storyteller receives'
    description = ('Fallout accumulates from day 0 to 6, then fades. A hostile hive accumulates from day 3 to 11, then fades. The total is their sum at each time, peaking near day 8.7. Both have faded by day 22.'
                   if kind == 'credit' else 'Unmodified threat wealth stays at 100,000. The biome-only input stays near 77,460. With both source balances subtracted, adjusted threat wealth falls to about 59,708, then returns to the biome-only level by day 22.')
    parts = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" role="img" aria-labelledby="{kind}-title {kind}-desc">',
             f'<title id="{kind}-title">{title}</title><desc id="{kind}-desc">{description}</desc>',
             f'<rect width="{width}" height="{height}" fill="#ffffff"/>', text(left, 32, title, 25)]
    legend = [('Fallout', BLUE, ''), ('One hostile hive', ORANGE, ''), ('Total deterrence', NAVY, '')] if kind == 'credit' else [('Without mod', MUTED, '8 5'), ('Biome only', BLUE, '8 5'), ('Biome + both balances', NAVY, '')]
    for i, (label, color, dash) in enumerate(legend):
        xx = left + i*275
        parts.append(line([(xx,62),(xx+33,62)],color,4,dash))
        parts.append(text(xx+42, 68, label, 17))
    ticks = range(0, 25001, 5000) if kind == 'credit' else range(0, 100001, 20000)
    for value in ticks:
        yy = y(value)
        parts.append(f'<path d="M {left} {yy:.2f} H {width-right}" stroke="{GRID}" stroke-width="1"/>')
        parts.append(text(left-12, yy+6, f'{value/1000:g}k', 17, MUTED, 'end'))
    for day in [0, 5, 10, 15, 20, 25]:
        xx = x(day)
        parts.append(f'<path d="M {xx:.2f} {top} V {bottom}" stroke="{GRID}" stroke-width="1"/>')
        parts.append(text(xx, bottom+27, day, 17, MUTED, 'middle'))
    parts.append(f'<path d="M {left} {top} V {bottom} H {width-right}" fill="none" stroke="{MUTED}" stroke-width="1.2"/>')
    parts.append(text(22, (top+bottom)/2, 'Deterrence (silver-equivalent)' if kind == 'credit' else 'Threat wealth (silver)', 18,
                      anchor='middle', extra=f'transform="rotate(-90 22 {(top+bottom)/2})"'))
    parts.append(text((left+width-right)/2, bottom+55, 'Days since fallout begins', 18, anchor='middle'))

    def path(field, points, color, dash='', thickness=3):
        if not points:
            return
        parts.append(line([(x(p['day']),y(p[field])) for p in points],color,thickness,dash))

    if kind == 'credit':
        for field, color, start, end in [('fallout', BLUE, 0, 6), ('hive', ORANGE, 3, 11)]:
            path(field, [p for p in SAMPLES if start <= p['day'] <= end], color)
            path(field, [p for p in SAMPLES if p['day'] >= end], color, '7 5')
        path('total', SAMPLES, NAVY, thickness=4.5)
        for day, label in [(6, 'Fallout ends'), (11, 'Hive cleared')]:
            parts.append(line([(x(day),top),(x(day),bottom)],MUTED,1.3,'3 5'))
            parts.append(text(x(day)+7, top+24, label, 16))
        for yy, start, end, label, color in [(508, 0, 6, 'Fallout present', BLUE), (537, 3, 11, 'Hive present', ORANGE)]:
            parts.append(f'<path d="M {x(start):.2f} {yy} H {x(end):.2f}" stroke="{color}" stroke-width="8"/>')
            parts.append(text(x(end)+12, yy+6, label, 16, color))
    else:
        path('vanilla', SAMPLES, MUTED, '8 5', 2.5)
        path('biomeOnly', SAMPLES, BLUE, '8 5', 2.5)
        path('adjusted', SAMPLES, NAVY, thickness=4.5)
        parts.append(text(width-right-8, y(100000)-12, '100,000', 18, MUTED, 'end'))
        parts.append(text(width-right-8, y(DATA['milestones'][0]['biomeOnly'])-12, '77,460', 18, BLUE, 'end'))
        peak = max(SAMPLES, key=lambda p:p['total'])
        parts.append(text(x(peak['day']), y(peak['adjusted'])+27, 'about 59,700', 18, NAVY, 'middle'))
        parts.append(text(left, 533, 'Fixed example: 100,000 threat wealth; temperate-swamp biome weight 0.6; exponent 0.5.', 16, MUTED))
    parts.append('</svg>')
    return ''.join(parts)


credits = figure('credit')
wealth = figure('wealth')
(ASSETS/'two-hazards.svg').write_text(credits, encoding='utf-8')
(ASSETS/'wealth-effect.svg').write_text(wealth, encoding='utf-8')


def violence_figure():
    data=json.loads((ASSETS/'violence-bumps.json').read_text(encoding='utf-8'))
    left,right,top,bottom=86,925,105,410
    x=lambda day:left+day/18*(right-left)
    y=lambda value:bottom-value/4000*(bottom-top)
    purple='#8956ac'
    parts=['<svg xmlns="http://www.w3.org/2000/svg" width="960" height="520" viewBox="0 0 960 520" role="img" aria-labelledby="violence-title violence-desc">',
           '<title id="violence-title">Acts share one Reputation meter</title><desc id="violence-desc">Each act adds 2,000 to the remaining reputation. Acts on days zero, three and six each renew the fade. A single act fades earlier. No lost value is restored.</desc>',
           '<rect width="960" height="520" fill="#fff"/>',text(left,32,'Acts share one Reputation meter',25)]
    for xx,label,color in [(left,'One act only',MUTED),(430,'Three acts, one shared meter',purple)]:
        parts.append(f'<path d="M{xx},65 H{xx+34}" stroke="{color}" stroke-width="4"/>')
        parts.append(text(xx+44,71,label,17,color))
    for value in range(0,4001,1000):
        yy=y(value)
        parts.append(f'<path d="M{left},{yy} H{right}" stroke="{GRID}"/>')
        parts.append(text(left-12,yy+6,f'{value/1000:g}k',17,MUTED,'end'))
    for day in range(0,19,3):
        xx=x(day)
        parts.append(f'<path d="M{xx},{top} V{bottom}" stroke="{GRID}"/>')
        parts.append(text(xx,bottom+28,day,17,MUTED,'middle'))
    for key,color,weight in [('single',MUTED,2.5),('shared',purple,4)]:
        points=[p for p in data['samples'] if p['day']<=18]
        commands=' '.join(('M' if i==0 else 'L')+f'{x(p["day"]):.2f},{y(p[key]):.2f}' for i,p in enumerate(points))
        parts.append(f'<path d="{commands}" fill="none" stroke="{color}" stroke-width="{weight}"/>')
    for day,label in [(3,'Next act'),(6,'Next act')]:
        parts.append(text(x(day)+7,top+26,label,16,purple))
        parts.append(f'<path d="M{x(day)},{top+36} V{bottom}" stroke="{purple}" stroke-width="0.8" opacity="0.35"/>')
    parts.append(f'<path d="M{left},{top} V{bottom} H{right}" stroke="{MUTED}" fill="none"/>')
    parts.append(text(22,(top+bottom)/2,'Reputation (silver-equivalent)',18,anchor='middle',extra=f'transform="rotate(-90 22 {(top+bottom)/2})"'))
    parts.append(text((left+right)/2,468,'Days since first act',18,anchor='middle'))
    parts.append(text(left,507,'Example: 2,000 per act; default cubic fade and square-root duration.',16,MUTED))
    parts.append('</svg>')
    return ''.join(parts)

violence=violence_figure()
(ASSETS/'violence-bumps.svg').write_text(violence,encoding='utf-8')

guide = '''# Deterrence: a short illustrated guide

**Outsiders may avoid a dangerous place. Deterrence represents that danger and its lingering reputation. Being sick or otherwise weak earns no reduction.**

## Start with the formula

`Adjusted threat wealth = max(0, Game threat wealth - Deterrence) x biome factor x island factor`

This changes the wealth used to size threats. Actual assets keep their prices. Population, adaptation, event frequency, and minimum threat sizes still follow the game's rules.

Open **Formula** to toggle deterrence, the biome factor and the island factor independently. Meters continue tracking and fading when deterrence is off. Each disabled terrain factor uses 1. Hover a setting label or formula for help.

| Setting | Effect |
| --- | --- |
| Formula > Biome strength | The factor is w ^ strength. Here w is the biome's settlement selection weight, limited to 0 through 1. Strength ranges from 0 to 3. At 0 the factor is 1. The default 0.5 takes the square root. At 1 it equals the weight. At 3 a weight of 0.5 gives a factor of 0.125. |
| Formula > Island factor | A 0–1 multiplier for water-enclosed landmasses within the tile limit. The limit defaults to 180 world tiles and accepts 1–350. By default, the colony must be the island's sole settlement. The multiplier defaults to 0.2. At 0 the wealth contribution is zero and the game's other threat factors still apply. |
| Misc > Smoothed surface wealth | Multiplier for smoothed floors/walls in **Game threat wealth**, before the formula. **The default of 0 excludes their contribution. Set 1 for vanilla weighting.** |

The terrain factors apply directly. Mainland coasts and land that reaches a partial world's edge use island factor 1. The surface discount respects vanilla's half-value weighting of building wealth and refreshes hourly or when you press **Apply & save**.

## Corpse storage

Open the storage tab of a grave, sarcophagus or stockpile and expand **Corpses > Humanlike corpses**. Each branch starts on **Any**.

| Branch | What you select |
| --- | --- |
| Allowed status at death | Hostile, Neutral or Friendly according to the pawn's individual role. Older corpses use a saved estimate from their faction. |
| Allowed faction status | The faction's current diplomatic status. A peace treaty can change which bodies qualify. |
| Allowed factions | Named factions in this game, including the player faction, and Factionless. |

Choose any combination inside a branch. All three branches and the vanilla filters must allow the body. Use **None** to reject every humanlike corpse at an ordinary destination. An explicit grave assignment reserves the grave for its assigned pawn even with **None** selected. Copy and paste storage settings to apply the same rules elsewhere.

## Three meters

| Meter | How it works | When the source ends |
| --- | --- | --- |
| **Remains** | Bodies maintain a fixed amount. Kept prisoner organs maintain a fraction of their current market value. | Each removed body's amount fades separately. Organ-stock value ends on removal, without another tail. |
| **Reputation** | A qualifying death, prisoner surgery or execution, or ritual adds a one-time gain to one shared balance. | Fading starts immediately. Every positive gain adds to what remains and restarts the shared fade. |
| **Hazards** | Pollution, toxic conditions, hostile hives, and outdoor extremes accumulate with diminishing returns. | Each source type fades separately after its exposure ends. |

Open meters from the shield badge beside **World**, or use **Formula > Options > Open meters**. The pie chart shows how much Remains, Reputation, and Hazards each contribute to the current map. Hover a slice or legend entry for its amount and percentage. Expand a meter to see its sources. Body rows identify burials, and arrows locate a body, grave, organ, or container.

## Remains: bodies and organs

Bodies use the pawn kind's fixed **combat power**, not individual skills, health, gear, or corpse condition. Proper burials keep the same contribution. Exhuming and reburying the same body does not add another entry.

| Body | New-setting default per combat power |
| --- | --- |
| Hostile human | 5 |
| Hostile mechanoid | 5 |
| Hostile / manhunter animal | 1 |

Ordinary hunted animals do not qualify. Existing settings are preserved: to lower an older animal weight, use **Remains > Animal remains > Low (1) > Apply & save**. Amounts also accept typing, sliders, and minus/plus buttons. The body switch is independent of Reputation.

New natural organs harvested locally from prisoners are marked and count while kept: **market value x 0.75** by default. Adjust this from 0 to 5 under **Kept prisoner organs**. Bought or old unmarked organs do not qualify. Selling, using, destroying, or removing stock ends this portion on refresh. The harvest's separate Reputation gain keeps fading.

## Reputation: one shared memory

| Act | Default gain |
| --- | --- |
| Hostile death | Combat power x 5; humans also include original weapons/apparel value |
| Prisoner organ harvest | 1,000 |
| Failed removal surgery | 333 |
| Successful amputation without an organ | 500 |
| Prisoner execution | 2,000 |
| Selected ritual reward | 5,000 |

One surgery earns one outcome. Prisoner deaths do not also earn ordinary hostile-death gains. Human gear is captured before downing or direct death; stripping afterward cannot rewrite it.

With **Ideology**, choose **reputation boost** as a ritual's normal bonus reward. Positive outcomes then add Reputation; other rituals earn nothing automatically. Adjust the amount under **Reputation > Ritual reward**.

![Each act adds to the surviving balance and restarts one fade.](docs/assets/violence-bumps.svg)

Later acts restore no lost value. Even a small positive gain can prolong a large surviving balance; zero or disabled gains cannot renew it. The breakdown shows each act's remaining share, keeping 40 recent acts plus **Earlier reputation**. Old saves use that aggregate for unrecorded history.

## Hazards: separate sources can overlap

Each environmental type has one balance per home map. Sequential toxic fallouts resume the same surviving fallout balance and diminishing returns; fully faded sources start fresh. Pollution is separate. Duplicate copies of one condition do not multiply its rate.

![Fallout runs on days 0-6; a hostile hive runs on days 3-11. Solid lines accumulate; dashed lines fade.](docs/assets/two-hazards.svg)

Fallout ends near 15,769; the hive ends near 11,756. Their fades last about 12.6 and 10.8 days. The total can keep rising after fallout ends because the hive is still active.

![At fixed 100,000 threat wealth, the biome-only input is about 77,460; both balances lower it to about 59,708.](docs/assets/wealth-effect.svg)

Actual outdoor heat/cold counts, including temperatures caused by heat waves and cold snaps. Named events grant no separate bonus and have no settings. Their old separate balances are discarded on upgrade. Biome and temperature may both apply. Ground pollution requires Biotech.

Acid Rain and Toxic Rain are optional mod conditions. Their controls appear when a loaded mod supplies the corresponding definition. Toxic Fallout is a separate base-game event.

Illness, solar flares, eclipses, psychic drones, volcanic winter, poor soil, and swamp terrain give no deterrence.

## Tune the curves

Settings follow **Formula, Remains, Reputation, Hazards, Curves, Summary, Misc, Presets**. The bottom of Formula has **Colony size scaling**, **Faction overrides**, and **Open meters**. Colony size scaling reveals sample colonist inputs and additional Summary columns. This display preference applies to Global and every faction. Saved gameplay scaling applies while its controls are hidden.

**Curves** puts the live plot beside scrollable growth and fade controls on wide windows. On narrow windows the plot sits above them. The preview offers Hazards, Reputation, and Combined views. You can set when a hive appears and when the cluster is cleared. The preview uses midpoint reproduction times at standard speed, with slower growth as hives crowd together. **By total exposure** slows hazards as raw exposure accumulates. **By daily rate** gives stronger hazards a longer slowdown time, calibrated at 2,000 points/day. Both use one shape slider: 0 for linear growth, 0.5 for square root, and 1 for logarithmic growth.

Default fading is a smooth cubic curve. **10,000 deterrence lasts 10 days**, scaled by the square root of the ending amount: four times as much lasts twice as long. The duration exponent adjusts this scaling. Switch to **Hazard history** to use an ongoing hazard’s area in point-days. The reference area defaults to 20,000 point-days. Reputation and removed bodies use starting balance. A fade in progress keeps its saved shape and duration.

H is hazard buildup in points. The line beneath daily gain shows the long-run trend for a steady hazard. At p = 0, H = Rt. Between 0 and 1, H grows like (t/T)^(1 − p). At p = 1, it grows like ln(t/T). The graph gives the exact point totals. During growth, t counts active days and T sets the slowdown time. During fading, H₀ is the starting balance, t counts days since fading began, and T is the time to zero. The fraction elapsed is x = t/T. Each mode uses D for its own reference duration.

Existing fades keep their saved shape and duration. Newly ended sources and renewed Reputation use current controls. Press **Apply & save** to refresh the map; History plots actual wealth, Game threat wealth, Adjusted threat wealth, and deterrence.

**Summary** shows the selected scope's formula and standard examples. Examples include a 100-power body or death, original human gear worth 1,500, stock worth 1,000, one act, or one day of a hazard at the stated exposure. The default view uses five colonists. Colony size scaling reveals columns for 1, 5, 10, and 20. Click a row to edit it.

## Optional faction overrides

Check **Faction overrides** in **Formula > Options** to show **Settings for**. Select a faction type to edit its settings. Untouched values follow Global, including later changes. **Reset to Global** clears that faction's overrides. **Summary** follows the selected faction.

Faction settings affect the size of ordinary enemy raids on colony maps. The storyteller still chooses the faction using the shared Global budget. Incidents with explicit raid budgets keep their assigned points. The Global meters and history report the shared values.

If you first customize a source or curve, that faction starts with the colony's current Global balance. New gains and fades then use the faction's settings. Running fades keep their saved timing. Press **Apply & save** to apply changes.

## What changes about raids

The storyteller controls when it attempts raids. Threat wealth feeds the points budget alongside population, adaptation, and difficulty. Points can qualify or rule out some incidents.

For an ordinary enemy raid, RimWorld draws from eligible factions. The draw is weighted by the shared point budget and faction rules. A scripted event can assign a faction or budget. Once the game picks a faction in a standard raid, Deterrence can apply that faction's settings to the raid size. The game then applies its usual strategy and arrival adjustments.

## Save and restore settings

In **Presets**, type a name and select **Save new**. Pick a saved preset to load, rename, overwrite, or delete it. Presets include Global settings and faction overrides. Deterrence balances and wealth history stay with the save.

This guide describes **Deterrence 0.13.1**, formerly Antiwealth.
'''

(ROOT/'Deterrence-Guide.md').write_text(guide, encoding='utf-8')


def inline(s):
    s=html.escape(s)
    s=re.sub(r'\*\*(.+?)\*\*',r'<strong>\1</strong>',s)
    s=re.sub(r'`(.+?)`',r'<code>\1</code>',s)
    return s


def render_markdown(source):
    lines=source.splitlines(); out=[]; i=0
    while i<len(lines):
        line=lines[i]
        if not line.strip(): i+=1; continue
        if line.startswith('!['):
            match=re.match(r'!\[(.*?)\]\((.*?)\)',line)
            svg=credits if 'two-hazards' in match[2] else violence if 'violence-bumps' in match[2] else wealth
            out.append('<figure>'+svg+'<figcaption>'+html.escape(match[1])+'</figcaption></figure>'); i+=1; continue
        if line.startswith('# '):out.append('<h1>'+inline(line[2:])+'</h1>');i+=1;continue
        if line.startswith('## '):out.append('<h2>'+inline(line[3:])+'</h2>');i+=1;continue
        if line.startswith('|'):
            rows=[]
            while i<len(lines) and lines[i].startswith('|'):
                cells=[c.strip() for c in lines[i].strip('|').split('|')]
                if not all(re.fullmatch(r':?-+:?',c) for c in cells): rows.append(cells)
                i+=1
            out.append('<div class="table-wrap"><table><thead><tr>'+''.join('<th>'+inline(c)+'</th>' for c in rows[0])+'</tr></thead><tbody>')
            for row in rows[1:]:out.append('<tr>'+''.join('<td>'+inline(c)+'</td>' for c in row)+'</tr>')
            out.append('</tbody></table></div>');continue
        paragraph=[line]; i+=1
        while i<len(lines) and lines[i].strip() and not lines[i].startswith(('#','|','![')):
            paragraph.append(lines[i]); i+=1
        out.append('<p>'+inline(' '.join(paragraph))+'</p>')
    return '\n'.join(out)


document='''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Deterrence - a short illustrated guide</title><style>
*{box-sizing:border-box}html{color-scheme:light}body{margin:0;background:#f3f6f8;color:#20364d;font:18px/1.65 system-ui,-apple-system,"Segoe UI",sans-serif}main{max-width:1080px;margin:0 auto;padding:48px 52px 72px;background:#fff}h1{font-size:2.1rem;line-height:1.2;margin:0 0 24px}h2{font-size:1.4rem;line-height:1.3;margin:42px 0 14px}p{margin:14px 0}strong{font-weight:650}code{font-size:.95em;background:#edf3f7;padding:4px 7px;box-decoration-break:clone}figure{margin:26px 0}figure svg{display:block;width:100%;height:auto}figcaption{font-size:.95rem;color:#40566b;margin:10px 0}.table-wrap{overflow-x:auto;margin:20px 0}table{width:100%;border-collapse:collapse;font-size:1rem;text-align:left}th{font-weight:650;background:#edf3f7}th,td{padding:12px 13px;border-bottom:1px solid #dce4eb;vertical-align:top}a{color:#1266a0}footer{margin-top:38px;padding-top:20px;border-top:1px solid #dce4eb;color:#40566b;font-size:.95rem}@media(max-width:700px){main{padding:28px 20px}body{font-size:17px}h1{font-size:1.8rem}th,td{padding:10px 8px}}@media print{body{background:#fff;font-size:11pt}main{padding:0}figure,table{break-inside:avoid}h2{break-after:avoid}}
</style></head><body><main>'''+render_markdown(guide)+'''<footer>Editable text: Deterrence-Guide.md. Example data was exported from the actual C# calculation functions. The example lines use the actual calculation model. Ongoing sources update hourly; qualifying acts add immediate gains. <a href="docs/Antiwealth-Description-First-Draft.md">Read the unchanged first draft</a>.</footer></main></body></html>'''
(ROOT/'Deterrence-Guide.html').write_text(document, encoding='utf-8')

try:
    import pymupdf as fitz
    preview=ROOT.parent/'.deterrence-doc-review'
    preview.mkdir(exist_ok=True)
    for name in ['two-hazards','wealth-effect','violence-bumps']:
        image=fitz.open(stream=(ASSETS/(name+'.svg')).read_bytes(),filetype='svg')
        image[0].get_pixmap(matrix=fitz.Matrix(1.5,1.5),alpha=False).save(str(preview/(name+'.png')))
except ImportError:
    pass

assert abs(DATA['sourceA']['peak']-DATA['samples'][300]['fallout'])<0.000001
assert DATA['milestones'][-2]['total']==0
assert DATA['milestones'][3]['total']>DATA['milestones'][2]['total']
print(json.dumps({'guide_words':len(guide.split()),'peak':max(SAMPLES,key=lambda p:p['total']),
                  'first_fade_end':DATA['sourceA']['fadeEnd'],'second_fade_end':DATA['sourceB']['fadeEnd']},indent=2))
