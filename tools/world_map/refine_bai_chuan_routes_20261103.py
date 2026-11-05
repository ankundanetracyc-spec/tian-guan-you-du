"""Relax crowded transit nodes and round roads without changing graph identities.

Writes a candidate, never the live layout. Geometry and terrain are checked for
each accepted change; run the independent audit before promoting the candidate.
"""
from pathlib import Path
import argparse
import copy
import json
import math
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, 'E:/tmp/CodexData/world_map_python_libs')
from shapely.geometry import LineString, Point
from shapely.strtree import STRtree

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'textures/world/bai_chuan/acceptance_20261030'
TARGET = 85.0  # Art spacing in world pixels, not a gameplay distance.


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf8')


def unit(v):
    return v / max(1e-9, np.linalg.norm(v))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', type=Path, default=BASE/'display_layout_v3.json')
    parser.add_argument('--output', type=Path,
                        default=ROOT/'output/bai_chuan_route_refine_20261103/candidate.json')
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    original = read(args.input)
    data = copy.deepcopy(original)
    edges = data['displayGraph']['edges']
    keys = list(edges)
    ids = list(data['locations']) + list(data['transitPositions'])
    index = {n: i for i, n in enumerate(ids)}
    pos = np.array([data['locations'][n]['drawPos'] if n in data['locations']
                    else data['transitPositions'][n] for n in ids], float)
    initial = pos.copy()
    coords = {k: np.array(v, float) for k, v in data['displayEdges'].items()}
    incident = {n: [] for n in ids}
    for k, e in edges.items():
        incident[e['from']].append(k)
        incident[e['to']].append(k)
    rgb = np.array(Image.open(BASE/'master_3762.png').convert('RGB')).astype(float)
    water = (rgb[:, :, 2] > rgb[:, :, 0]*1.15) & (rgb[:, :, 2] > rgb[:, :, 1]*1.04)
    rock = (np.ptp(rgb, axis=2) < 44) & (rgb.mean(2) < 160) & ~water

    def terrain(points):
        lengths = np.r_[0, np.linalg.norm(np.diff(points, axis=0), axis=1).cumsum()]
        samples = np.linspace(0, lengths[-1], max(2, math.ceil(lengths[-1]/2)+1))
        xy = np.rint(np.column_stack([np.interp(samples, lengths, points[:, a]) for a in range(2)])).astype(int)
        xy = np.clip(xy, 0, 3761)
        return np.array([water[xy[:, 1], xy[:, 0]].mean(), rock[xy[:, 1], xy[:, 0]].mean()])*lengths[-1]

    baseline_terrain = {k: terrain(v) for k, v in coords.items()}

    def rebuild():
        nonlocal lines, tree
        lines = [LineString(coords[k]) for k in keys]
        tree = STRtree(lines)

    lines, tree = [], None
    rebuild()

    def safe(changes, moved=None, point=None):
        local = {k: LineString(v) for k, v in changes.items()}
        positions = pos.copy()
        if moved is not None:
            positions[index[moved]] = point
        # Checks use the final geometry for every simultaneously modified edge.
        for k, line in local.items():
            if not line.is_simple or line.length < 4:
                return False
            q = changes[k]
            if (q < 0).any() or (q > 3762).any():
                return False
            if np.any(terrain(q) > baseline_terrain[k] + [3.0, 8.0]):
                return False
            e = edges[k]
            lo = q.min(0)-14
            hi = q.max(0)+14
            near = np.flatnonzero(((positions >= lo) & (positions <= hi)).all(1))
            for i in near:
                if ids[i] in (e['from'], e['to']):
                    continue
                p = Point(positions[i])
                dist, along = line.distance(p), line.project(p)
                if dist < 5 or (dist < 14 and along > 18 and line.length-along > 18):
                    return False
            candidates = {keys[j] for j in tree.query(line.buffer(4.1))} | set(local)
            for other_key in candidates:
                if other_key == k:
                    continue
                other = local.get(other_key, lines[keys.index(other_key)])
                oe = edges[other_key]
                shared = {e['from'], e['to']} & {oe['from'], oe['to']}
                hit = line.intersection(other)
                a, b = line, other
                for n in shared:
                    p = Point(positions[index[n]])
                    hit = hit.difference(p.buffer(.02))
                    disk = p.buffer(8)
                    a, b = a.difference(disk), b.difference(disk)
                if not hit.is_empty:
                    return False
                if not a.is_empty and not b.is_empty and a.distance(b) < 4.001:
                    return False
        if moved is not None:
            p = Point(point)
            for j in tree.query(p.buffer(14)):
                k = keys[j]
                if k in changes:
                    continue
                line = lines[j]
                dist, along = line.distance(p), line.project(p)
                if dist < 5 or (dist < 14 and along > 18 and line.length-along > 18):
                    return False
        return True

    def local_coords(n, p):
        result = {}
        for k in incident[n]:
            q = coords[k].copy()
            reverse = edges[k]['from'] != n
            if reverse:
                q = q[::-1].copy()
            along = np.r_[0, np.linalg.norm(np.diff(q, axis=0), axis=1).cumsum()]
            # Carry nearby control points with the moved junction; moving only
            # the endpoint can fold an existing short approach back on itself.
            weight = np.maximum(0, 1-along/min(120., along[-1]))**2
            q += weight[:, None]*(p-q[0])
            q[0] = p
            if reverse:
                q = q[::-1]
            result[k] = q
        return result

    def score(n, p):
        i = index[n]
        distances = np.linalg.norm(pos-p, axis=1)
        distances[i] = TARGET
        spacing = np.maximum(0, TARGET-distances)
        result = float(spacing@spacing)*4 + float(np.linalg.norm(p-initial[i]))**2*.025
        if len(incident[n]) == 2:
            neighbors = []
            for k in incident[n]:
                q = coords[k]
                neighbors.append(q[1] if edges[k]['from'] == n else q[-2])
            result += 1600*(1+float(unit(neighbors[0]-p)@unit(neighbors[1]-p)))
        return result

    moves = []
    for iteration in range(5):
        accepted = 0
        distances = np.linalg.norm(pos[:, None]-pos[None, :], axis=2)
        np.fill_diagonal(distances, np.inf)
        order = sorted(data['transitPositions'], key=lambda n: float(distances[index[n]].min()))
        for n in order:
            i = index[n]
            old = pos[i].copy()
            old_score = score(n, old)
            candidates = []
            for radius in [10, 20, 35, 50, 70]:
                for angle in np.linspace(0, math.tau, 16, endpoint=False):
                    p = old + radius*np.array([math.cos(angle), math.sin(angle)])
                    if (p < 35).any() or (p > 3727).any() or np.linalg.norm(p-initial[i]) > 110:
                        continue
                    x, y = np.rint(p).astype(int)
                    if water[y, x] or rock[y, x]:
                        continue
                    dd = np.linalg.norm(pos-p, axis=1)
                    dd[i] = np.inf
                    if dd.min() < min(60, np.linalg.norm(pos-old, axis=1)[np.arange(len(ids)) != i].min()):
                        continue
                    value = score(n, p)
                    if value < old_score-15:
                        candidates.append((value, p))
            for value, p in sorted(candidates, key=lambda row: row[0]):
                changes = local_coords(n, p)
                if safe(changes, n, p):
                    coords.update(changes)
                    pos[i] = p
                    rebuild()
                    accepted += 1
                    moves.append(n)
                    break
        print('relaxation', iteration, 'accepted', accepted, flush=True)
        if not accepted:
            break

    # Cubic controls round only a small corridor near existing bends. At a
    # degree-two node, both incident roads receive the same through tangent.
    def round_line(q, handles=None, amount=1.):
        handles = handles or {}
        result = [q[0]]
        for j in range(len(q)-1):
            a, b = q[j], q[j+1]
            length = np.linalg.norm(b-a)
            handle = min(25., length*.25)*amount
            ta = unit(b-a) if j == 0 else unit(unit(a-q[j-1])+unit(b-a))
            tb = unit(b-a) if j+1 == len(q)-1 else unit(unit(b-a)+unit(q[j+2]-b))
            c = a + ta*handle
            d = b - tb*handle
            if j == 0 and 'start' in handles:
                c = a + handles['start']*amount
            if j+1 == len(q)-1 and 'end' in handles:
                d = b + handles['end']*amount
            for t in np.linspace(0, 1, max(4, math.ceil(length/5))+1)[1:]:
                result.append((1-t)**3*a+3*(1-t)**2*t*c+3*(1-t)*t*t*d+t**3*b)
        return np.array(result)

    # Remove small zigzags inside the existing terrain corridor before rounding.
    simplified = 0
    for k in keys:
        if len(coords[k]) <= 2:
            continue
        for tolerance in [10., 5., 2.]:
            q = np.array(LineString(coords[k]).simplify(tolerance).coords)
            if len(q) < len(coords[k]) and safe({k: q}):
                coords[k] = q
                rebuild()
                simplified += 1
                break
    # Internal bends first. Keep endpoints exact.
    # A short approach with old control points can pin a junction during the
    # first relaxation. Retry the remaining tight points after simplification.
    for n in data['transitPositions']:
        i = index[n]
        dd = np.linalg.norm(pos-pos[i], axis=1)
        dd[i] = np.inf
        if dd.min() >= 60:
            continue
        old = pos[i].copy()
        candidates = []
        for radius in [8, 16, 24, 32, 45, 60]:
            for angle in np.linspace(0, math.tau, 48, endpoint=False):
                p = old+radius*np.array([math.cos(angle), math.sin(angle)])
                if (p < 35).any() or (p > 3727).any() or np.linalg.norm(p-initial[i]) > 110:
                    continue
                x, y = np.rint(p).astype(int)
                if water[y, x] or rock[y, x]:
                    continue
                dd = np.linalg.norm(pos-p, axis=1)
                dd[i] = np.inf
                if dd.min() >= 60:
                    candidates.append((score(n, p), p))
        for _, p in sorted(candidates, key=lambda row: row[0]):
            changes = local_coords(n, p)
            if safe(changes, n, p):
                coords.update(changes)
                pos[i] = p
                moves.append(n)
                rebuild()
                break
    rounded = 0
    for k in keys:
        if len(coords[k]) <= 2:
            continue
        for amount in [1., .5, .25]:
            q = round_line(coords[k], amount=amount)
            if safe({k: q}):
                coords[k] = q
                rebuild()
                rounded += 1
                break
    # Use original segment directions to avoid repeatedly subdividing baked curves.
    reference = {k: v.copy() for k, v in coords.items()}
    smoothed_nodes = 0
    for n in data['transitPositions']:
        if len(incident[n]) < 2:
            continue
        # At junctions, round the most nearly straight through pair while the
        # other branches still join the exact same real node.
        connection = incident[n]
        if len(connection) > 2:
            directions = []
            for k in connection:
                e = edges[k]
                other = e['to'] if e['from'] == n else e['from']
                directions.append(unit(pos[index[other]]-pos[index[n]]))
            pairs = [(float(directions[a]@directions[b]), a, b)
                     for a in range(len(connection)) for b in range(a+1, len(connection))]
            dot, a, b = min(pairs)
            if dot > -.4:
                continue
            connection = [connection[a], connection[b]]
        outward, spans = [], []
        for k in connection:
            q = reference[k]
            if edges[k]['from'] != n:
                q = q[::-1]
            lengths = np.r_[0, np.linalg.norm(np.diff(q, axis=0), axis=1).cumsum()]
            j = min(len(q)-1, max(1, int(np.searchsorted(lengths, 35))))
            outward.append(unit(q[j]-q[0]))
            spans.append(lengths[-1])
        direction = unit(outward[0]-outward[1])
        if np.linalg.norm(direction) < .9:
            continue
        # Insert a short cubic only in the first/last 35px of each road.
        for amount in [1., .6, .3]:
            changes = {}
            for order, k in enumerate(connection):
                reverse = edges[k]['from'] != n
                q = coords[k][::-1].copy() if reverse else coords[k].copy()
                lengths = np.r_[0, np.linalg.norm(np.diff(q, axis=0), axis=1).cumsum()]
                span = min(40., lengths[-1]*.38)
                end = np.array([np.interp(span, lengths, q[:, a]) for a in range(2)])
                j = min(len(q)-1, int(np.searchsorted(lengths, span)))
                tail = unit(q[j]-q[max(0, j-1)])
                p = q[0]
                c = p + direction*(1 if order == 0 else -1)*span*.65*amount
                d = end-tail*span*.32
                ts = np.linspace(0, 1, 13)
                curve = np.array([(1-t)**3*p+3*(1-t)**2*t*c+3*(1-t)*t*t*d+t**3*end for t in ts])
                remaining = q[lengths > span+1e-6]
                q = np.vstack([curve, remaining])
                changes[k] = q[::-1] if reverse else q
            if safe(changes):
                coords.update(changes)
                rebuild()
                smoothed_nodes += 1
                break

    for n in data['transitPositions']:
        data['transitPositions'][n] = pos[index[n]].tolist()
    data['displayEdges'] = {k: q.tolist() for k, q in coords.items()}
    data['note'] = '2026-11-03：拉开拥挤途经点并柔化道路折转；具名地点、节点身份与连接关系保持，行旅按新曲线实际长度计时。'
    assert data['locations'] == original['locations']
    assert data['displayGraph'] == original['displayGraph']
    assert data['sourceEdgeWalks'] == original['sourceEdgeWalks']
    write(args.output, data)

    def metrics(points):
        dd = np.linalg.norm(points[:, None]-points[None, :], axis=2)
        np.fill_diagonal(dd, np.inf)
        return {'minimumSpacing': float(dd.min()), 'pairsBelow60': int((dd < 60).sum()/2),
                'pairsBelow80': int((dd < 80).sum()/2)}

    report = {'before': metrics(initial), 'after': metrics(pos), 'movedTransitNodes': len(set(moves)),
              'simplifiedZigzagRoads': simplified,
              'roundedInternalRoads': rounded, 'smoothedThroughNodes': smoothed_nodes,
              'maximumTransitDisplacement': float(np.linalg.norm(pos-initial, axis=1).max()),
              'totalRoadPixelsBefore': sum(LineString(v).length for v in original['displayEdges'].values()),
              'totalRoadPixelsAfter': sum(line.length for line in lines)}
    write(args.output.with_suffix('.report.json'), report)
    print(json.dumps(report), flush=True)


if __name__ == '__main__':
    main()
