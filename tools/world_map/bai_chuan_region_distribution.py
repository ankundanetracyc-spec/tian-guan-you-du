"""Original seeded regional composition inspired by documented center/base/edge rules.

Not a port of Guigubahuang's native algorithm or its seed-to-map mapping.
"""
from __future__ import annotations
import collections
import math
import random
import re


def compose(seed, density, cfg, catalog, anchors, scene_text):
    rng = random.Random(seed)
    art_rng = random.Random(f'{seed}:art:v2')
    policy = cfg['regional_distribution']
    tile = cfg['tile_size']
    width, height = cfg['columns'], cfg['rows']
    reserved, occupied, records = set(), set(), []
    families = collections.defaultdict(list)
    for item in catalog['assets']:
        families[item['family']].append(item)

    def rectangle(x, y, w, h):
        return {(xx, yy) for yy in range(y, y+h) for xx in range(x, x+w)}

    points = []
    for anchor in anchors:
        x, y = anchor['grid_origin']
        w, h = anchor.get('footprint', [1, 1])
        margin = cfg.get('anchor_margin', 1)
        reserved.update(rectangle(x-margin, y-margin, w+2*margin, h+2*margin))
        points.append((x+w/2, y+h/2))
    # Meandering clear routes avoid the former long, straight cuts through woods.
    linked = [points[0]] if points else []
    for x, y in points[1:]:
        tx, ty = min(linked, key=lambda p: math.dist(p, (x, y)))
        dx, dy = tx-x, ty-y
        length = max(1, math.hypot(dx, dy))
        bend = rng.uniform(-4, 4)
        for n in range(math.ceil(length*2)+1):
            t = n / math.ceil(length*2)
            wave = math.sin(t*math.pi)*bend
            reserved.add((int(x+dx*t-dy/length*wave), int(y+dy*t+dx/length*wave)))
        linked.append((x, y))
    for block in re.split(r'(?=^\[node )', scene_text, flags=re.M):
        if 'parent="NatureDecorations/LandmarkProps"' not in block.split('\n', 1)[0]:
            continue
        match = re.search(r'metadata/cell_footprint = Rect2i\((\d+), (\d+), (\d+), (\d+)\)', block)
        if match:
            x, y, w, h = map(int, match.groups())
            reserved.update(rectangle(x-1, y-1, w+2, h+2))

    def add(family, x, y, group, role=None, center=False):
        x, y = round(x), round(y)
        role = role or ('tree' if family in cfg['wood_families'] else
                        'mountain' if family in ('peak', 'hill', 'cliff') else
                        'water_detail' if family in ('bank', 'pond', 'stream', 'lotus', 'spring') else 'rock_detail')
        # Try all variants when a large footprint fails, rather than dropping the slot.
        pool = families[family][:]
        rng.shuffle(pool)
        pool.sort(key=lambda a: -math.log(max(rng.random(), 1e-12))/
                  (a.get('weight', 1)*(math.prod(a['footprint'])**1.5 if center else 1)))
        for item in pool:
            w, h = item['footprint']
            foot = rectangle(x, y, w, h)
            if any(not (1 <= xx < width-1 and 1 <= yy < height-1) for xx, yy in foot):
                continue
            if foot & (reserved | occupied):
                continue
            occupied.update(foot)
            l, t, r, b = item['bounds']; iw, ih = item['size']
            ratio = art_rng.uniform(.68, .88) if role == 'tree' else art_rng.uniform(.86, .96)
            scale = min(w*tile*ratio/(r-l), h*tile*ratio/(b-t))
            # Visible alpha bounds stay inside the footprint even at the largest size.
            mx = max(0, (w*tile-(r-l)*scale)/2-4)
            my = max(0, (h*tile-(b-t)*scale)/2-4)
            ox = art_rng.uniform(-min(31, mx), min(31, mx)) if role == 'tree' else 0
            oy = art_rng.uniform(-min(27, my), min(27, my)) if role == 'tree' else 0
            root = 'ForestPatches' if role == 'tree' else 'MountainRanges' if role == 'mountain' else 'SmallScenery'
            records.append(dict(asset=item['path'], family=family, role=role,
                                parent=f'{root}/{group}',
                                position=[round((x+w/2)*tile+ox, 3), round((y+h/2)*tile+oy, 3)],
                                offset=[iw/2-(l+r)/2, ih/2-(t+b)/2], scale=scale,
                                footprint=[x, y, w, h], cell=[x, y]))
            return True
        return False

    # Smooth, stable fields control composition independently of sprite selection.
    def lattice(x, y, salt):
        n = (x*374761393+y*668265263+seed*1442695041+salt*2246822519) & 0xffffffff
        n = ((n ^ (n >> 13))*1274126177) & 0xffffffff
        return ((n ^ (n >> 16)) & 0xffffffff)/4294967295

    def noise(x, y, scale, salt):
        x, y = x/scale, y/scale
        ix, iy = math.floor(x), math.floor(y)
        fx, fy = x-ix, y-iy
        fx, fy = fx*fx*(3-2*fx), fy*fy*(3-2*fy)
        a = lattice(ix, iy, salt)*(1-fx)+lattice(ix+1, iy, salt)*fx
        b = lattice(ix, iy+1, salt)*(1-fx)+lattice(ix+1, iy+1, salt)*fx
        return a*(1-fy)+b*fy

    def make_paths(entries):
        output = []
        for entry in entries:
            controls = [(x+rng.uniform(-6, 6), y+rng.uniform(-6, 6)) for x, y in entry['points']]
            path = []
            for a, b in zip(controls, controls[1:]):
                steps = max(1, math.ceil(math.dist(a, b)))
                for n in range(steps):
                    t = n/steps
                    path.append((a[0]*(1-t)+b[0]*t, a[1]*(1-t)+b[1]*t))
            path.append(controls[-1])
            output.append(dict(id=entry['id'], points=path, width=entry['width'],
                               family=entry.get('family', 'pine')))
        return output

    ridges = make_paths(policy['ridges'])
    woods = make_paths(policy['woodlands'])

    def influence(x, y, paths, salt):
        wx = x+(noise(x, y, 13, salt)-.5)*9
        wy = y+(noise(x, y, 13, salt+1)-.5)*9
        best, best_path = 0, paths[0]
        for path in paths:
            distance = min((wx-px)**2+(wy-py)**2 for px, py in path['points'])
            value = math.exp(-distance/(2*path['width']**2))
            if value > best:
                best, best_path = value, path
        return best, best_path

    # Cached fields: geographic areas first, individual decorations second.
    forest_field, ridge_field = {}, {}
    for y in range(2, height-2):
        for x in range(2, width-2):
            forest_field[x, y] = influence(x, y, woods, 20)
            ridge_field[x, y] = influence(x, y, ridges, 40)[0]

    # Center + low surrounding peaks, based on the source configuration's structure.
    mountain_centers = []
    for path in ridges:
        candidates = path['points'][:]
        rng.shuffle(candidates)
        count = max(2, round(len(candidates)*policy['mountain_centers_per_path_cell']*density))
        accepted = 0
        for cx, cy in candidates:
            cx, cy = round(cx), round(cy)
            if accepted >= count:
                break
            if any(math.dist((cx, cy), p) < rng.uniform(5, 8) for p in mountain_centers):
                continue
            group = f'Ridge_{path["id"]}_{accepted:02d}'
            if not add('peak', cx, cy, group, center=True):
                continue
            mountain_centers.append((cx, cy)); accepted += 1
            ring = [(dx, dy) for dy in range(-3, 4) for dx in range(-3, 4) if 1.4 <= math.hypot(dx, dy) <= 3.2]
            rng.shuffle(ring)
            filled = 0; surround_count = rng.randint(4, 6)
            for dx, dy in ring:
                if filled >= surround_count:
                    break
                family = rng.choices(['hill', 'tree_rock', 'terrace', 'peak'], [60, 20, 12, 8])[0]
                filled += int(add(family, cx+dx, cy+dy, group))
            # Outer transition is sparse, with no perfect closed ring.
            for _ in range(rng.randint(2, 4)):
                dx, dy = rng.choice(ring)
                family = rng.choices(['pine', 'boulder', 'shrub', 'hill'], [43, 22, 20, 15])[0]
                add(family, cx+dx*1.6, cy+dy*1.6, group+'_Edge')

    # Reserve the cave accents before surrounding scenery occupies their slots.
    for i, (x, y) in enumerate(mountain_centers):
        if i % 3:
            continue
        for dx, dy in [(2, 4), (-3, 4), (4, 2), (-4, -2), (0, 5)]:
            if add('cave', x+dx, y+dy, f'RidgeCave_{i:02d}'):
                break

    # The woodland area is porous: core, open pockets, lower scenery, then sparse edge.
    cells = list(forest_field)
    rng.shuffle(cells)
    for x, y in cells:
        strength, area = forest_field[x, y]
        if strength < .09 or (x, y) in occupied or (x, y) in reserved:
            continue
        patch = .48+.75*noise(x, y, 6, 64)
        probability = min(.83, strength*patch*policy['woodland_fill']*density)
        if lattice(x, y, 71) > probability:
            continue
        main = area['family']
        if strength > .48:
            palette = [main, 'pine' if main != 'pine' else 'broadleaf', 'shrub', 'tree_rock', 'hill', 'boulder', 'deadwood']
            weights = [70, 6, 6, 5, 5, 5, 3]
        else:
            palette = [main, 'shrub', 'tree_rock', 'hill', 'boulder', 'deadwood']
            weights = [46, 12, 10, 12, 14, 6]
        family = rng.choices(palette, weights)[0]
        add(family, x, y, 'Woodland_'+area['id'])

    # Core ridges join centers with smaller mountains; foothills fade outwards.
    rng.shuffle(cells)
    for x, y in cells:
        field = ridge_field[x, y]
        if field >= .78 and lattice(x, y, 80) < policy['ridge_core_fill']*density:
            add(rng.choices(['peak', 'hill', 'tree_rock', 'terrace'], [35, 42, 17, 6])[0], x, y, 'RidgeConnectingSlopes')
    for x, y in cells:
        field = ridge_field[x, y]
        if .12 < field < .78 and lattice(x, y, 81) < policy['foothill_fill']*field*density:
            add(rng.choices(['hill', 'tree_rock', 'boulder', 'pine', 'cliff'], [42, 20, 16, 18, 4])[0], x, y, 'FoothillTransition')

    # Water details form local pond/spring groups, not an evenly spaced map-wide layer.
    garden_sites = []
    for x, y in cells:
        strength, area = forest_field[x, y]
        if not (.17 < strength < .7) or (x, y) in occupied or (x, y) in reserved:
            continue
        if any(math.dist((x, y), p) < 15 for p in garden_sites):
            continue
        if len(garden_sites) >= round(policy['water_garden_count']*density):
            break
        group = f'Waterside_{len(garden_sites):02d}'
        if not add(rng.choice(['pond', 'spring', 'bank']), x, y, group):
            continue
        garden_sites.append((x, y))
        offsets = [(dx, dy) for dy in range(-3, 4) for dx in range(-3, 4) if 2 <= math.hypot(dx, dy) <= 3.5]
        rng.shuffle(offsets)
        added = 0
        for dx, dy in offsets:
            if added >= 4:
                break
            added += int(add(rng.choices(['willow', 'bamboo', 'bank', 'stream', 'lotus', 'boulder'], [28, 15, 12, 14, 12, 19])[0], x+dx, y+dy, group+'_Edge'))

    # Small 3–6-cell groves supplement the regional woods, with actual neighboring trees.
    groves = []
    for x, y in cells:
        if len(groves) >= round(cfg['small_grove_count']*density):
            break
        if forest_field[x, y][0] > .25 or (x, y) in occupied or (x, y) in reserved:
            continue
        if any(math.dist((x, y), p) < 9 for p in groves):
            continue
        group = f'SmallGrove_{len(groves):02d}'
        family = rng.choice(['broadleaf', 'pine', 'bamboo'])
        if not add(family, x, y, group):
            continue
        groves.append((x, y)); count = rng.randint(*cfg['small_grove_size'])
        frontier = [(x+1, y), (x-1, y), (x, y+1), (x, y-1)]
        seen = {(x, y)}; placed = 1
        while frontier and placed < count:
            p = frontier.pop(rng.randrange(len(frontier)))
            if p in seen:
                continue
            seen.add(p)
            if add(family, *p, group):
                placed += 1
                px, py = p
                frontier.extend([(px+1, py), (px-1, py), (px, py+1), (px, py-1)])

    # Plain accents are allowed near regional scenery and at a few noise hot spots.
    for x, y in cells:
        if forest_field[x, y][0] > .4 or ridge_field[x, y] > .55:
            continue
        probability = policy['plain_accent_fill']*density*(.35+noise(x, y, 19, 92))
        if lattice(x, y, 93) < probability:
            add(rng.choices(['broadleaf', 'pine', 'boulder', 'deadwood', 'terrace', 'tree_rock', 'shrub'], [15, 10, 21, 10, 10, 18, 16])[0], x, y, 'OpenCountry')
    counts = dict(collections.Counter(r['role'] for r in records))
    return dict(version=2, algorithm='regional_center_surround_edge_v2', seed=seed, density=density,
                static_scene_nodes=True, water_logic_cells=0, counts=counts,
                asset_types_used=len({r['asset'] for r in records}),
                composition=dict(mountain_centers=len(mountain_centers), woodlands=len(woods),
                                 small_groves=len(groves), water_gardens=len(garden_sites)), records=records)
