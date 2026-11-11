"""Bake unchanged river SDF, fine water cells and independently rendered bridges.

This is a deterministic technical-data bake. Native water / bridge artwork and
all legacy resources are read-only. Candidate crossings come only from the old
575 roads. Outside-world padding is never used as a land connection.
"""
from __future__ import annotations
from collections import Counter
from pathlib import Path
import hashlib
import json
import math
import sys

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi
from scipy.spatial import cKDTree

ROOT = Path(__file__).resolve().parents[2]
SURFACE = ROOT / "textures/world/bai_chuan/expanded3x_20261106/surface"
RESOURCE = ROOT / "resources/world/bai_chuan/expanded_water_20261106.json"
FEATURE = ROOT / "textures/world/bai_chuan/layered_20261106/feature_data.png"
OLD_LAYOUT = ROOT / "resources/world/bai_chuan/free_layout_mountains_20261106.json"
WORLD = 22118.4
COLUMNS = 1413
CELL = 15.678468899521532
SOURCE_PIXELS = 3762
PAD_PIXELS = 6
SOURCE_SCALE = WORLD / SOURCE_PIXELS
SDF_RANGE = 12.0
PREFIX = "res://textures/world/bai_chuan/expanded3x_20261106/surface/"
CROSS = ndi.generate_binary_structure(2, 1)
EIGHT = np.ones((3, 3), dtype=bool)
MAX_BRIDGE_WIDTH = 900.0
NOMINAL_ZOOM = 0.8
SHORE_SMOOTH_SIGMA = 1.4

# Measured after viewing the actual atlas; bottom-right crosses the 2x2 seam.
# Bounds are x0,y0,x1,y1 in the unchanged native atlas. Decks exclude footings.
BRIDGE_SPECS = [
    ("wood_crossbeam", [43, 197, 812, 339], [85, 225, 765, 307]),
    ("stone_slab", [924, 197, 1736, 339], [980, 223, 1686, 310]),
    ("timber_short", [143, 608, 657, 765], [211, 631, 592, 712]),
    ("timber_long", [795, 608, 1718, 762], [855, 631, 1655, 704]),
]


def read(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def write(path: Path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def cell_at(point):
    x, y = np.floor(np.asarray(point) / CELL).astype(int)
    return int(x), int(y)


def center_at(x, y):
    return [(x + 0.5) * CELL, (y + 0.5) * CELL]


def native_bridge_catalog(alpha):
    catalog = {}
    reports = []
    for ident, measured, deck in BRIDGE_SPECS:
        x0, y0, x1, y1 = measured
        region = [x0 - 4, y0 - 4, x1 - x0 + 8, y1 - y0 + 8]
        dx0, dy0, dx1, dy1 = deck
        deck_alpha = alpha[dy0:dy1, dx0:dx1]
        assert int(deck_alpha.min()) >= 240
        pivot_abs = np.array([(dx0 + dx1) / 2, (dy0 + dy1) / 2])
        catalog[ident] = {
            "atlas": PREFIX + "bridges_native.png",
            "region": region,
            "native_size": region[2:],
            "pivot": ((pivot_abs - region[:2]) / region[2:]).tolist(),
            "deck_rect": [dx0 - region[0], dy0 - region[1], dx1 - dx0, dy1 - dy0],
            "deck_rect_atlas": deck,
            "native_pivot_px": pivot_abs.tolist(),
            "deck_alpha_min": int(deck_alpha.min()),
            "layer": "ground_decals",
            "z": 28,
        }
        reports.append({
            "asset": ident, "region": region, "pivot": catalog[ident]["pivot"],
            "deck_rect_atlas": deck, "deck_alpha_min": int(deck_alpha.min()),
            "all_original_alpha_preserved": True,
        })
    return catalog, reports


def topology_counts(water):
    return {
        "water_4":int(ndi.label(water,structure=CROSS)[1]),
        "water_8":int(ndi.label(water,structure=EIGHT)[1]),
        "land_4":int(ndi.label(~water,structure=CROSS)[1]),
        "land_8":int(ndi.label(~water,structure=EIGHT)[1]),
    }


def zero_crossings(field):
    """Subpixel zero intersections on horizontal/vertical source-pixel edges."""
    mask = field < 0
    y,x = np.nonzero(mask[:,:-1] != mask[:,1:])
    a,b = field[y,x],field[y,x+1]
    horizontal = np.column_stack([x + a/(a-b) + 0.5,y + 0.5])
    y,x = np.nonzero(mask[:-1,:] != mask[1:,:])
    a,b = field[y,x],field[y+1,x]
    vertical = np.column_stack([x + 0.5,y + a/(a-b) + 0.5])
    return np.vstack([horizontal,vertical])


def bake_smooth_sdf(signed,source_water):
    """Smooth render data while protecting every digital land/water component."""
    smooth = cv2.GaussianBlur(signed,(0,0),SHORE_SMOOTH_SIGMA,borderType=cv2.BORDER_REPLICATE)
    proposed = smooth < 0
    current = source_water.copy()
    points = np.column_stack(np.nonzero(proposed != source_water))
    strengths = np.abs(smooth[points[:,0],points[:,1]])
    accepted = 0
    # Only simple point changes are permitted: local component counts remain
    # identical for BOTH 4 and 8 connectivity, for water AND complementary land.
    # This keeps tiny islands that a plain Gaussian would otherwise consume.
    for index in np.argsort(-strengths):
        y,x = points[index]
        y0,x0 = max(0,y-1),max(0,x-1)
        local = current[y0:y+2,x0:x+2].copy()
        before = topology_counts(local)
        local[y-y0,x-x0] = not local[y-y0,x-x0]
        if topology_counts(local) == before:
            current[y,x] = proposed[y,x]
            accepted += 1
    protected = proposed != current
    # R8's closest nonzero distances are +/-0.0471 source pixels. These 58
    # topology-critical samples stay near zero, preserving the smooth neighbours.
    smooth[protected] = np.where(source_water[protected],-0.055,0.055)
    encoded = np.rint((np.clip(smooth,-SDF_RANGE,SDF_RANGE)+SDF_RANGE)*(255/(2*SDF_RANGE))).astype(np.uint8)
    Image.fromarray(encoded).save(SURFACE/"water_sdf_smooth.png")
    readback = np.asarray(Image.open(SURFACE/"water_sdf_smooth.png"))
    final_field = readback.astype(np.float32)*(2*SDF_RANGE/255)-SDF_RANGE
    final_water = final_field < 0
    assert np.array_equal(final_water,current)
    before,after = topology_counts(source_water),topology_counts(final_water)
    assert before == after
    exact_crossings = zero_crossings(signed)
    smooth_crossings = zero_crossings(final_field)
    exact_to_smooth = cKDTree(smooth_crossings).query(exact_crossings)[0]
    smooth_to_exact = cKDTree(exact_crossings).query(smooth_crossings)[0]
    maximum = float(max(exact_to_smooth.max(),smooth_to_exact.max()))
    changed = final_water != source_water
    report = {
        "method":"Gaussian signed distance plus topology-preserving local simple-point guard",
        "sigma_source_pixels":SHORE_SMOOTH_SIGMA,"native_size":[SOURCE_PIXELS,SOURCE_PIXELS],
        "proposed_zero_sign_changes":len(points),"accepted_zero_sign_changes":accepted,
        "topology_guarded_pixels":int(protected.sum()),
        "final_zero_sign_changed_pixels":int(changed.sum()),
        "changed_water_pixels_to_land":int((changed&source_water).sum()),
        "changed_land_pixels_to_water":int((changed&~source_water).sum()),
        "topology_before":before,"topology_after":after,"topology_unchanged":before==after,
        "maximum_shore_displacement_source_pixels":maximum,
        "shore_displacement_measurement":"Bidirectional nearest subpixel zero intersections on horizontal/vertical source-pixel edges (sampled contour distance)",
        "exact_to_smooth_max_source_pixels":float(exact_to_smooth.max()),
        "smooth_to_exact_max_source_pixels":float(smooth_to_exact.max()),
        "shore_displacement_p99_source_pixels":float(max(np.quantile(exact_to_smooth,.99),np.quantile(smooth_to_exact,.99))),
        "max_original_absolute_sdf_at_changed_pixel_centers":float(np.abs(signed[changed]).max(initial=0)),
        "source_pixel_world_size":SOURCE_SCALE,
        "exact_source_and_navigation_unchanged":True,
        "retreat_samples_over_2_source_pixels":[
            {"source_position":point.tolist(),"distance_source_pixels":float(distance)}
            for point,distance in zip(exact_crossings,exact_to_smooth) if distance>2.0
        ],
    }
    return final_field,report


def audit_smooth_navigation(smooth_sdf,exact_r8,source_water,water,opened,domain,positions,shore_report):
    """Report shoreline display differences; never modify frozen navigation."""
    centers=(np.arange(COLUMNS,dtype=np.float32)+0.5)*CELL/SOURCE_SCALE-0.5
    map_x,map_y=np.meshgrid(centers,centers)
    smooth_center=ndi.map_coordinates(smooth_sdf,[map_y,map_x],order=1,mode="nearest")
    exact_field=exact_r8.astype(np.float32)*(2*SDF_RANGE/255)-SDF_RANGE
    exact_center=ndi.map_coordinates(exact_field,[map_y,map_x],order=1,mode="nearest")
    flooded=(smooth_center<0)&~water&~opened&domain
    center_reviews=[]
    for y,x in np.column_stack(np.nonzero(flooded)):
        center_reviews.append({"grid":[int(x),int(y)],"position":center_at(int(x),int(y)),
                               "smooth_signed_distance_source_pixels":float(smooth_center[y,x]),
                               "exact_signed_distance_source_pixels":float(exact_center[y,x]),
                               "newly_flooded":bool(exact_center[y,x]>=0),"bridge_open":False})
    changed_land=np.column_stack(np.nonzero((smooth_sdf<0)&~source_water))
    touched={}
    for sy,sx in changed_land:
        x0=int(sx*SOURCE_SCALE/CELL);x1=int(((sx+1)*SOURCE_SCALE-1e-8)/CELL)
        y0=int(sy*SOURCE_SCALE/CELL);y1=int(((sy+1)*SOURCE_SCALE-1e-8)/CELL)
        for y in range(y0,y1+1):
            for x in range(x0,x1+1):
                if 0<=x<COLUMNS and 0<=y<COLUMNS and domain[y,x] and not water[y,x] and not opened[y,x]:
                    touched[(x,y)]=touched.get((x,y),0)+1
    tip_reviews=[]
    for tip in shore_report["retreat_samples_over_2_source_pixels"]:
        point=np.array(tip["source_position"])*SOURCE_SCALE
        x,y=cell_at(point)
        tip_reviews.append({**tip,"world_position":point.tolist(),"grid":[x,y],
                            "old_conservative_water_cell":bool(water[y,x]),"bridge_open":bool(opened[y,x])})
    entry_proofs=[]
    for ident,point in positions.items():
        if not ident.startswith("bc_place_"):
            continue
        source_point=np.asarray(point)/SOURCE_SCALE-0.5
        distance=float(ndi.map_coordinates(smooth_sdf,[[source_point[1]],[source_point[0]]],order=1,mode="nearest")[0])
        assert distance>0, "Smooth shore flooded original location entry: "+ident
        entry_proofs.append({"id":ident,"position":np.asarray(point).tolist(),
                             "smooth_signed_distance_source_pixels":distance,"on_rendered_land":True})
    result={
        "navigation_and_bridge_grids_modified":False,
        "retreat_samples_over_2_source_pixels":tip_reviews,
        "all_retreat_samples_already_blocked_by_water_grid":all(q["old_conservative_water_cell"] for q in tip_reviews),
        "render_water_at_old_land_cell_centers":center_reviews,
        "newly_flooded_cell_centers_count":sum(q["newly_flooded"] for q in center_reviews),
        "old_land_cells_partly_touched_by_new_visual_water":[
            {"grid":[x,y],"position":center_at(x,y),"changed_source_pixels":count,
             "cell_center_smooth_signed_distance":float(smooth_center[y,x]),
             "cell_center_exact_signed_distance":float(exact_center[y,x]),"bridge_open":False}
            for (x,y),count in sorted(touched.items(),key=lambda q:(q[0][1],q[0][0]))
        ],
        "all_123_original_location_entries_on_rendered_land":all(q["on_rendered_land"] for q in entry_proofs),
        "location_entry_proofs":entry_proofs,
        "root_action":"Keep bridge-open cells. Optional shoreline blockers are the listed old-land cell centres actually inside rendered water; partial-touch list is a conservative review list.",
    }
    write(SURFACE/"water_sdf_navigation_review.json",result)
    return result


def corners_for_cells(points):
    return np.concatenate([
        points + [sx * CELL * 0.5, sy * CELL * 0.5]
        for sx, sy in [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    ])


def fit_bridge(candidate_id, water_points, bank_cells, bank_ids, catalog, land):
    """Fit one unchanged-aspect native deck over complete candidate water cells."""
    covariance = np.cov(water_points.T) if len(water_points) > 1 else np.eye(2)
    _, eigenvectors = np.linalg.eigh(covariance)
    axis = eigenvectors[:, -1]
    if axis[0] < 0 or (abs(axis[0]) < 1e-8 and axis[1] < 0):
        axis = -axis
    perpendicular = np.array([-axis[1], axis[0]])
    rotation_matrix = np.column_stack([axis, perpendicular])
    projections = water_points @ rotation_matrix
    preferred_v = (projections[:, 1].min() + projections[:, 1].max()) * 0.5
    bank_points = []
    bank_details = []
    for bank_id in bank_ids:
        cells = bank_cells[bank_id]
        points = (cells[:, ::-1] + 0.5) * CELL
        uv = points @ rotation_matrix
        # One real 4-neighbour shore contact per bank, near the bridge centreline.
        score = np.abs(uv[:, 1] - preferred_v)
        index = int(score.argmin())
        point = points[index]
        bank_points.append(point)
        bank_details.append({
            "land_component": int(bank_id),
            "grid": [int(cells[index, 1]), int(cells[index, 0])],
            "position": point.tolist(),
        })
    all_corners = corners_for_cells(np.vstack([water_points, bank_points]))
    uv = all_corners @ rotation_matrix
    low = uv.min(axis=0)
    high = uv.max(axis=0)
    # Additional bank overlap and a small numerical tolerance remain inside deck.
    required_extent = high - low + [CELL, 1.0]
    center_uv = (low + high) * 0.5
    position = center_uv @ rotation_matrix.T
    theta = math.atan2(float(axis[1]), float(axis[0]))
    # Select the shortest native deck that can cover every opened cell, has
    # reachable dry end footings and stays native-sized at the .8 close camera.
    choices = []
    rejected = []
    for asset_id, asset in catalog.items():
        d = asset["deck_rect_atlas"]
        deck_size = np.array([d[2] - d[0], d[3] - d[1]], dtype=float)
        scale = float(np.max(required_extent / deck_size))
        width, height = np.asarray(asset["native_size"]) * scale
        if width > MAX_BRIDGE_WIDTH:
            rejected.append({"asset":asset_id,"reason":"world_width_over_900","world_width":float(width)})
            continue
        if scale * NOMINAL_ZOOM > 1.0 + 1e-6:
            rejected.append({"asset":asset_id,"reason":"native_pixels_would_be_enlarged","source_to_world_scale":scale})
            continue
        region = np.array(asset["region"], dtype=float)
        pivot = np.array(asset["native_pivot_px"])
        # End supports are near the full image's left/right limits, on deck axis.
        end_native = np.array([
            [region[0] + region[2] * 0.06, pivot[1]],
            [region[0] + region[2] * 0.94, pivot[1]],
        ])
        end_world = (end_native - pivot) * scale @ rotation_matrix.T + position
        end_labels = []
        valid_ends = True
        for point in end_world:
            gx, gy = cell_at(point)
            if not (0 <= gx < COLUMNS and 0 <= gy < COLUMNS):
                valid_ends = False
                end_labels.append(0)
            else:
                label = int(land[gy, gx])
                end_labels.append(label)
                if label <= 0:
                    valid_ends = False
        if not valid_ends or set(end_labels) != set(bank_ids):
            rejected.append({"asset":asset_id,"reason":"end_footings_not_on_both_banks","end_components":end_labels})
            continue
        full_corners = np.array([
            [region[0], region[1]], [region[0] + region[2], region[1]],
            [region[0] + region[2], region[1] + region[3]], [region[0], region[1] + region[3]],
        ])
        world_corners = (full_corners - pivot) * scale @ rotation_matrix.T + position
        bounds_min = world_corners.min(axis=0)
        bounds_max = world_corners.max(axis=0)
        choices.append({
            "asset": asset_id,
            "position": position.tolist(), "x": float(position[0]), "y": float(position[1]),
            "z":28,"layer":"ground_decals","rotation":theta,
            "world_width":float(width),"world_height":float(height),
            "bounds":[*bounds_min.tolist(), *(bounds_max-bounds_min).tolist()],
            "rect":[*bounds_min.tolist(), *bounds_max.tolist()],
            "source_to_world_scale":scale,
            "bank_contacts":bank_details,
            "end_footings":[
                {"position":p.tolist(),"land_component":int(c)}
                for p,c in zip(end_world,end_labels)
            ],
            "water_span_world":float(np.ptp(projections[:,0]) + CELL),
            "connected_land_components":[int(q) for q in bank_ids],
            "candidate_id":candidate_id,
            "deck_world_extent":(deck_size*scale).tolist(),
        })
    return (min(choices, key=lambda q:q["world_width"]) if choices else None), rejected


def packed_rows(mask, true_char):
    data = np.where(mask, ord(true_char), ord("0")).astype(np.uint8)
    return [row.tobytes().decode("ascii") for row in data]


def open_runs(points_yx):
    runs = []
    for y in sorted(set(int(p[0]) for p in points_yx)):
        xs = sorted(int(p[1]) for p in points_yx if int(p[0]) == y)
        start = previous = xs[0]
        for x in xs[1:]:
            if x > previous + 1:
                runs.append([y,start,previous+1])
                start = x
            previous = x
        runs.append([y,start,previous+1])
    return runs


class UnionFind:
    def __init__(self, count):
        self.parent = list(range(count + 1))
    def find(self, value):
        if self.parent[value] != value:
            self.parent[value] = self.find(self.parent[value])
        return self.parent[value]
    def join(self, a, b):
        a,b = self.find(a),self.find(b)
        if a == b:
            return False
        self.parent[b] = a
        return True


def main():
    SURFACE.mkdir(parents=True, exist_ok=True)
    previous = read(RESOURCE) if RESOURCE.exists() else None
    protected = [FEATURE, OLD_LAYOUT, SURFACE/"water_native.png", SURFACE/"bridges_native.png"]
    hashes_before = {str(p.relative_to(ROOT)): sha(p) for p in protected}
    features = np.asarray(Image.open(FEATURE))
    source_water = features[:,:,0] >= 128
    assert source_water.shape == (SOURCE_PIXELS,SOURCE_PIXELS)
    del features

    # Native source-pixel SDF: no bridge rasterization or waterline modification.
    land_distance = cv2.distanceTransform((~source_water).astype(np.uint8),cv2.DIST_L2,cv2.DIST_MASK_PRECISE)
    water_distance = cv2.distanceTransform(source_water.astype(np.uint8),cv2.DIST_L2,cv2.DIST_MASK_PRECISE)
    signed = land_distance - water_distance
    assert np.all(signed[~source_water] > 0) and np.all(signed[source_water] < 0)
    encoded = np.rint((np.clip(signed,-SDF_RANGE,SDF_RANGE)+SDF_RANGE)*(255/(2*SDF_RANGE))).astype(np.uint8)
    Image.fromarray(encoded).save(SURFACE/"water_sdf.png")
    sdf_readback = np.asarray(Image.open(SURFACE/"water_sdf.png"))
    sdf_sign_mismatches = int(((sdf_readback < 128) != source_water).sum())
    assert sdf_readback.shape == source_water.shape and sdf_sign_mismatches == 0
    smooth_sdf,shore_report = bake_smooth_sdf(signed,source_water)
    del land_distance,water_distance,signed,encoded

    coverage = cv2.resize(np.pad(source_water.astype(np.float32),((0,PAD_PIXELS),(0,PAD_PIXELS))),
                          (COLUMNS,COLUMNS),interpolation=cv2.INTER_AREA)
    water = coverage >= 0.12
    active_axis = (np.arange(COLUMNS)+0.5)*CELL < WORLD
    domain = active_axis[:,None] & active_axis[None,:]
    land, land_count = ndi.label((~water)&domain,structure=CROSS)
    land_sizes = np.bincount(land.ravel())
    layout = read(OLD_LAYOUT)
    assert len(layout["displayEdges"]) == 575
    positions = {key:np.asarray(row["drawPos"],float)*3 for key,row in layout["locations"].items()}
    positions.update({key:np.asarray(point,float)*3 for key,point in layout["transitPositions"].items()})
    assert len(positions) == 444
    nearest_land = ndi.distance_transform_edt(land == 0,return_distances=False,return_indices=True)
    required_anchors = {}
    wet_anchors = []
    for ident,point in positions.items():
        gx,gy = cell_at(point)
        component = int(land[gy,gx])
        if component == 0:
            ny,nx = nearest_land[:,gy,gx]
            replacement = center_at(int(nx),int(ny))
            component = int(land[ny,nx])
            wet_anchors.append({
                "id":ident,"position":point.tolist(),"grid":[gx,gy],
                "suggested_land_grid":[int(nx),int(ny)],
                "suggested_land_position":replacement,
                "distance":float(np.linalg.norm(point-replacement)),
                "required_component":component,
                "position_was_not_modified":True,
            })
        required_anchors.setdefault(component,[]).append(ident)
    required = set(required_anchors)

    road = np.zeros((COLUMNS,COLUMNS),np.uint8)
    for points in layout["displayEdges"].values():
        xy = np.floor(np.asarray(points)*3/CELL).astype(np.int32)
        cv2.polylines(road,[xy],False,1,1,cv2.LINE_8)
    road_three = ndi.binary_dilation(road != 0,structure=EIGHT)
    candidates_grid, candidate_count = ndi.label(road_three&water&domain,structure=EIGHT)
    candidate_slices = ndi.find_objects(candidates_grid)
    atlas_image = Image.open(SURFACE/"bridges_native.png").convert("RGBA")
    atlas_alpha = np.asarray(atlas_image)[:,:,3]
    bridge_catalog, alpha_regions = native_bridge_catalog(atlas_alpha)
    candidates = []
    candidate_cells = {}
    for candidate_id,box in enumerate(candidate_slices,1):
        sy,sx = box
        y0,y1=max(0,sy.start-2),min(COLUMNS,sy.stop+2)
        x0,x1=max(0,sx.start-2),min(COLUMNS,sx.stop+2)
        local_slice = np.s_[y0:y1,x0:x1]
        mask = candidates_grid[local_slice] == candidate_id
        ys,xs = np.nonzero(mask)
        global_cells = np.column_stack([ys+y0,xs+x0])
        points = (global_cells[:,::-1]+0.5)*CELL
        shore = ndi.binary_dilation(mask,structure=CROSS) & (land[local_slice] > 0)
        bank_ids = [int(q) for q in np.unique(land[local_slice][shore])]
        record = {
            "id":candidate_id,"water_cells":int(mask.sum()),
            "grid_bounds":[int(sx.start),int(sy.start),int(sx.stop),int(sy.stop)],
            "position":points.mean(axis=0).tolist(),
            "bank_components":bank_ids,"selected":False,
        }
        candidate_cells[candidate_id] = global_cells
        if len(bank_ids) != 2:
            record["rejection_reason"] = "no_two_distinct_real_land_banks"
        else:
            bank_cells = {}
            for bank_id in bank_ids:
                by,bx = np.nonzero(shore & (land[local_slice] == bank_id))
                bank_cells[bank_id] = np.column_stack([by+y0,bx+x0])
            bridge,rejections = fit_bridge(candidate_id,points,bank_cells,bank_ids,bridge_catalog,land)
            record["native_fit_rejections"] = rejections
            if bridge is None:
                record["rejection_reason"] = "no_native_deck_with_dry_reachable_footings"
            else:
                record["bridge"] = bridge
                record["cost"] = bridge["water_span_world"]
        candidates.append(record)

    # A minimum spanning forest, then prune all non-required island leaves.
    # In the present map every eligible two-bank candidate joins required land.
    eligible = [q for q in candidates if "bridge" in q]
    uf = UnionFind(land_count)
    selected = []
    for candidate in sorted(eligible,key=lambda q:(q["cost"],q["bridge"]["world_width"],q["id"])):
        a,b = candidate["bank_components"]
        if uf.join(a,b):
            selected.append(candidate)
    changed = True
    while changed:
        changed = False
        degree = Counter(q for c in selected for q in c["bank_components"])
        removable = {q for q,n in degree.items() if n == 1 and q not in required}
        if removable:
            selected = [c for c in selected if not removable.intersection(c["bank_components"])]
            changed = True

    opened = np.zeros_like(water)
    bridges = []
    alpha_proofs = []
    for serial,candidate in enumerate(sorted(selected,key=lambda q:q["id"]),1):
        candidate["selected"] = True
        bridge = candidate["bridge"].copy()
        bridge["id"] = f"bc_bridge_{serial:03d}"
        cells = candidate_cells[candidate["id"]]
        bridge["open_water_cells"] = len(cells)
        bridge["open_runs"] = open_runs(cells)
        opened[cells[:,0],cells[:,1]] = True
        asset = bridge_catalog[bridge["asset"]]
        theta = bridge["rotation"]
        rotation_matrix = np.array([[math.cos(theta),-math.sin(theta)],[math.sin(theta),math.cos(theta)]])
        corners = corners_for_cells((cells[:,::-1]+0.5)*CELL)
        native = ((corners-np.array(bridge["position"])) @ rotation_matrix) / bridge["source_to_world_scale"] + asset["native_pivot_px"]
        ix,iy = np.floor(native).astype(int).T
        in_atlas = (ix>=0)&(iy>=0)&(ix<atlas_alpha.shape[1])&(iy<atlas_alpha.shape[0])
        assert np.all(in_atlas)
        sampled = atlas_alpha[iy,ix]
        assert int(sampled.min()) >= 240
        d=asset["deck_rect_atlas"]
        in_deck = (native[:,0]>=d[0])&(native[:,0]<d[2])&(native[:,1]>=d[1])&(native[:,1]<d[3])
        assert np.all(in_deck)
        alpha_proofs.append({
            "bridge":bridge["id"],"candidate_id":candidate["id"],
            "opened_cells":len(cells),"all_cell_corners_inside_native_deck":True,
            "minimum_corner_alpha":int(sampled.min()),"native_corner_samples":len(corners),
            "both_end_footings_dry":all(q["land_component"]>0 for q in bridge["end_footings"]),
        })
        bridges.append(bridge)

    assert not np.any(opened & ~water)
    bridge_end_proofs = []
    for bridge in bridges:
        for index,foot in enumerate(bridge["end_footings"]):
            source_point = np.array(foot["position"])/SOURCE_SCALE - 0.5
            distance = float(ndi.map_coordinates(smooth_sdf,[[source_point[1]],[source_point[0]]],order=1,mode="nearest")[0])
            assert distance > 0, f"Smooth shore flooded bridge footing: {bridge['id']}"
            bridge_end_proofs.append({"bridge":bridge["id"],"end":index,
                                      "position":foot["position"],"smooth_signed_distance_source_pixels":distance,
                                      "on_rendered_land":True})
    shore_report["bridge_end_footings"] = bridge_end_proofs
    shore_report["all_bridge_ends_on_rendered_land"] = all(q["on_rendered_land"] for q in bridge_end_proofs)
    navigation_review = audit_smooth_navigation(smooth_sdf,sdf_readback,source_water,water,opened,domain,positions,shore_report)
    shore_report["navigation_review"] = PREFIX+"water_sdf_navigation_review.json"
    shore_report["newly_flooded_old_land_cell_centers"] = navigation_review["newly_flooded_cell_centers_count"]
    shore_report["all_123_original_location_entries_on_rendered_land"] = navigation_review["all_123_original_location_entries_on_rendered_land"]
    walkable = ((~water)|opened)&domain
    walk_components,walk_count = ndi.label(walkable,structure=CROSS)
    required_reachable = {}
    for component,anchor_ids in required_anchors.items():
        yy,xx = np.nonzero(land == component)
        required_reachable[component] = int(walk_components[yy[0],xx[0]])
    all_required_connected = len(set(required_reachable.values())) == 1
    hashes_after = {str(p.relative_to(ROOT)):sha(p) for p in protected}
    assert hashes_before == hashes_after

    result = {
        "version":"expanded_water_native_bridges_20261106_v1",
        "world_size":WORLD,"world_km":8000.0,"columns":COLUMNS,"cell_size":CELL,
        "grid_extent_world":COLUMNS*CELL,"active_cell_centers_per_axis":int(active_axis.sum()),
        "outside_world_padding_is_not_traversable":True,
        "source_size":[SOURCE_PIXELS,SOURCE_PIXELS],"source_pad_right_bottom":PAD_PIXELS,
        "source_pixel_world_size":SOURCE_SCALE,
        "water_grid_method":"R>=128, bottom/right pad6, INTER_AREA 1413x1413, water coverage>=0.12",
        "water_texture":PREFIX+"water_native.png",
        "water_sdf":PREFIX+"water_sdf_smooth.png",
        "water_sdf_exact":PREFIX+"water_sdf.png",
        "water_sdf_smoothing_report":PREFIX+"water_sdf_smoothing_report.json",
        "water_sdf_navigation_review":PREFIX+"water_sdf_navigation_review.json",
        "water_sdf_encoding":{"channel":"R","native_size":[SOURCE_PIXELS,SOURCE_PIXELS],
                              "signed_distance_source_pixels":"R/255*24-12",
                              "range_source_pixels":SDF_RANGE,"positive":"land","negative":"water",
                              "bridges_baked_into_sdf":False,"source_sign_mismatches":sdf_sign_mismatches},
        "water_rows":packed_rows(water,"2"),"bridge_open_rows":packed_rows(opened,"1"),
        "bridge_catalog":bridge_catalog,"bridges":bridges,
        "bridge_bounds_format":"[x,y,width,height]; rect is [x0,y0,x1,y1]",
        "all_required_land_connected":all_required_connected,
        "source_sha256":hashes_before,
        "reports":{"candidates":PREFIX+"bridge_candidates.json",
                   "connectivity":PREFIX+"bridge_connectivity_report.json",
                   "alpha":PREFIX+"bridge_alpha_report.json"},
    }
    if previous is not None:
        unchanged_fields = ["water_rows","bridge_open_rows","bridge_catalog","bridges"]
        for field in unchanged_fields:
            assert result[field] == previous[field], "Existing navigation / bridges changed: " + field
        shore_report["navigation_and_bridge_fields_unchanged"] = unchanged_fields
    write(SURFACE/"water_sdf_smoothing_report.json",shore_report)
    write(RESOURCE,result)
    write(SURFACE/"bridge_candidates.json",{
        "source_roads":575,"route_width_cells":3,"candidate_count":candidate_count,
        "two_bank_candidates":sum(len(q["bank_components"])==2 for q in candidates),
        "native_eligible_candidates":len(eligible),"selected_count":len(bridges),
        "candidates":candidates,
    })
    connectivity = {
        "all_required_land_connected":all_required_connected,
        "initial_land_components":land_count,"after_bridge_walk_components":walk_count,
        "required_component_count":len(required),
        "minimum_bridge_count_lower_bound":len(required)-1,
        "selected_bridges":len(bridges),
        "minimum_count_achieved":all_required_connected and len(bridges)==len(required)-1,
        "required_components":[
            {"id":int(k),"land_cells":int(land_sizes[k]),"anchors":v,
             "post_bridge_component":required_reachable[k]}
            for k,v in required_anchors.items()
        ],
        "wet_anchors_for_root_to_reposition":wet_anchors,
        "all_123_location_anchors_initially_dry":not any(q["id"].startswith("bc_place_") for q in wet_anchors),
        "world_size":WORLD,"grid_extent_world":COLUMNS*CELL,
        "excluded_world_padding_cells":int((~domain).sum()),
        "opened_water_cells":int(opened.sum()),"water_cells":int(water.sum()),
        "source_water_pixels":int(source_water.sum()),
        "native_sdf_water_sign_mismatches":sdf_sign_mismatches,
        "other_islands_left_unconnected":land_count-len(required),
        "unreachable_required_groups":(
            {str(k):[int(q) for q,v in required_reachable.items() if v==k]
             for k in sorted(set(required_reachable.values()))} if not all_required_connected else {}
        ),
        "over_900_world_bridge_sprites":sum(q["world_width"]>MAX_BRIDGE_WIDTH for q in bridges),
        "multi_segment_bridges":0,
        "native_assets_unchanged":hashes_before==hashes_after,
    }
    write(SURFACE/"bridge_connectivity_report.json",connectivity)
    write(SURFACE/"bridge_alpha_report.json",{
        "atlas_native_size":list(atlas_image.size),
        "native_alpha_preserved":True,"source_sha256":hashes_before,
        "catalog_regions":alpha_regions,"opened_cell_coverage":alpha_proofs,
        "every_opened_water_cell_covered":sum(q["opened_cells"] for q in alpha_proofs)==int(opened.sum()),
        "minimum_corner_alpha":min((q["minimum_corner_alpha"] for q in alpha_proofs),default=255),
    })

    # Technical QA overview only; generated art itself is never altered.
    colors=np.empty((COLUMNS,COLUMNS,3),np.uint8);colors[:]=[209,190,147]
    colors[water]=[111,145,150];colors[road_three&~water]=[181,164,122]
    colors[opened]=[107,70,42]
    preview=Image.fromarray(colors).resize((1884,1884),Image.Resampling.NEAREST)
    draw=ImageDraw.Draw(preview)
    font=ImageFont.truetype("C:/Windows/Fonts/arial.ttf",15)
    for candidate in candidates:
        x,y=np.array(candidate["position"])/CELL/COLUMNS*1884
        if candidate["selected"]:
            draw.ellipse((x-5,y-5,x+5,y+5),outline=(90,32,17),width=2)
            label=next(b["id"] for b in bridges if b["candidate_id"]==candidate["id"])
            draw.text((x+8,y-8),label.replace("bc_bridge_",""),font=font,fill=(64,30,14))
        elif len(candidate["bank_components"])==2:
            draw.ellipse((x-2,y-2,x+2,y+2),outline=(113,89,56),width=1)
    preview.save(SURFACE/"bridge_connectivity_preview.png")
    summary={k:connectivity[k] for k in [
        "all_required_land_connected","initial_land_components","required_component_count",
        "selected_bridges","minimum_count_achieved","water_cells","opened_water_cells",
        "native_assets_unchanged"]}
    summary["resource"]=str(RESOURCE.relative_to(ROOT))
    summary["wet_anchors"]=len(wet_anchors)
    summary["native_assets_used"]=dict(Counter(b["asset"] for b in bridges))
    print(json.dumps(summary,ensure_ascii=False,indent=2))
    return 0 if all_required_connected else 2


if __name__ == "__main__":
    if hasattr(sys.stdout,"reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    raise SystemExit(main())

