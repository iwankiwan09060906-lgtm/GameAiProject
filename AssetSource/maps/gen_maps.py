# 맵 2~4 생성기(MASTER 9.3 맵 규격). 1칸 = 1m.
# 왼쪽 야외 마당 + 건물(가운데 3칸 복도, 위·아래 방). 방마다 문 1~2개, 엄폐물 1~3개, 조명 0~2개.
# 밝기 0.5 이상 칸 비율이 30~60%가 되도록 조명 세기를 맞춘다(최종 판정은 EditMode MapValidatorTests).
# 사용: python gen_maps.py <StreamingAssets/Config/maps 폴더>
import json, math, os, sys

class Builder:
    def __init__(s, w, h):
        s.w, s.h = w, h
        s.t = [['#'] * w for _ in range(h)]   # rows[0]이 맨 위
        s.f = [['w'] * w for _ in range(h)]
        s.lights, s.enemies, s.open_doors = [], [], []

    def carve(s, x0, y0, x1, y1, floor):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                s.t[y][x] = '.'
                s.f[y][x] = floor

    def put(s, x, y, ch):
        s.t[y][x] = ch

    def door(s, x, y, vertical=False):
        for i in range(2):
            if vertical: s.t[y + i][x] = 'D'
            else: s.t[y][x + i] = 'D'

    # 행·열(row 기준) → 월드 좌표(m, y 위가 큼)
    def world(s, x, row):
        return [x + 0.5, (s.h - 1 - row) + 0.5]

    def light(s, x, row, r, i):
        s.lights.append({"pos": s.world(x, row), "radius_m": r, "intensity": i})

    def walkable(s, x, row):
        return 0 <= x < s.w and 0 <= row < s.h and s.t[row][x] in '.PTXk123456789D'

    # 밝기 추정(LightGrid와 같은 식, 시선은 0.05m 간격 표본)
    def brightness(s, ambient_in=0.15, ambient_out=0.08, scale=1.0):
        H, W = s.h, s.w
        vals = [[0.0] * W for _ in range(H)]
        def blocks(x, row):
            c = s.t[row][x]
            return c in '#CD'
        for row in range(H):
            for x in range(W):
                l = ambient_out if s.f[row][x] == 'o' else ambient_in
                cx, cy = x + 0.5, row + 0.5
                for L in s.lights:
                    lx, lrow = L["pos"][0], (H - L["pos"][1])
                    d = math.hypot(cx - lx, cy - lrow)
                    if d > L["radius_m"]: continue
                    n = max(1, int(d / 0.05))
                    ok = True
                    for k in range(1, n):
                        px = lx + (cx - lx) * k / n; py = lrow + (cy - lrow) * k / n
                        ix, iy = int(px), int(py)
                        if (ix, iy) != (x, row) and blocks(ix, iy): ok = False; break
                    if ok: l += L["intensity"] * scale * (1 - d / L["radius_m"])
                vals[row][x] = min(1.0, l)
        return vals

    def bright_ratio(s, scale=1.0):
        v = s.brightness(scale=scale)
        tot = br = 0
        for row in range(s.h):
            for x in range(s.w):
                if s.t[row][x] in '#W': continue
                tot += 1
                if v[row][x] >= 0.5: br += 1
        return br / tot

    def json(s, mid, name, mission, target_time, cans=()):
        for (x, row) in cans: s.t[row][x] = 'k'
        return {
            "version": 2, "id": mid, "name": name, "mission": mission, "target_time_s": target_time,
            "ambient": {"indoor": 0.15, "outdoor": 0.08}, "cell_m": 1,
            "rows": [''.join(r) for r in s.t], "floor_rows": [''.join(r) for r in s.f],
            "lights": s.lights, "open_doors": s.open_doors, "enemies": s.enemies,
        }


def building(b, yard_w, rooms_top, rooms_bottom, corridor_row, floors):
    """왼쪽 마당(0..yard_w-1), 건물 벽 x=yard_w, 복도 3칸(corridor_row..+2), 위·아래 방. 방 목록 반환"""
    W, H = b.w, b.h
    b.carve(1, 1, yard_w - 1, H - 2, 'o')
    x0 = yard_w + 1
    b.carve(x0, corridor_row, W - 2, corridor_row + 2, 't')
    b.door(yard_w, corridor_row, vertical=True)          # 마당 → 복도 입구
    rooms = []
    def split(count, y0, y1, door_row):
        width = (W - 1 - x0) - (count - 1)
        edges, x = [], x0
        for i in range(count):
            rw = width // count + (1 if i < width % count else 0)
            edges.append((x, x + rw - 1)); x += rw + 1
        for i, (a, c) in enumerate(edges):
            fl = floors[(len(rooms)) % len(floors)]
            b.carve(a, y0, c, y1, fl)
            mid = (a + c) // 2
            b.door(mid, door_row)
            rooms.append({"x0": a, "x1": c, "y0": y0, "y1": y1, "door": (mid, door_row), "top": door_row > y1})
        return edges
    top = split(rooms_top, 1, corridor_row - 2, corridor_row - 1)
    bottom = split(rooms_bottom, corridor_row + 4, H - 2, corridor_row + 3)
    return rooms


def covers(b, room, pattern):
    a, c, y0, y1 = room["x0"], room["x1"], room["y0"], room["y1"]
    spots = {
        "tl": [(a + 1, y0 + 1, 'C'), (a + 2, y0 + 1, 'C')],
        "br": [(c - 2, y1 - 1, 'c'), (c - 1, y1 - 1, 'c')],
        "mid": [((a + c) // 2 - 1, (y0 + y1) // 2, 'c'), ((a + c) // 2, (y0 + y1) // 2, 'c')],
        "tr": [(c - 1, y0 + 1, 'C'), (c - 1, y0 + 2, 'C')],
    }
    for key in pattern:
        for (x, y, ch) in spots[key]: b.put(x, y, ch)


def patrol_points(b, room, corridor_row, use_corridor):
    a, c, y0, y1 = room["x0"], room["x1"], room["y0"], room["y1"]
    pts = [(a + 3, y1 - 3 if room["top"] else y0 + 3), (c - 3, y0 + 3 if room["top"] else y1 - 3)]
    if (c - a) > 9: pts.append(((a + c) // 2 + 2, y0 + 2 if room["top"] else y1 - 2))
    if use_corridor: pts.append((room["door"][0], corridor_row + 1))
    out = []
    for (x, row) in pts:
        if not b.walkable(x, row): continue
        out.append(b.world(x, row))
    return out[:5]


def add_enemy(b, spawn, kind, room, corridor_row, persona, callsign, use_corridor=False):
    pts = patrol_points(b, room, corridor_row, use_corridor)
    sx, srow = room["x0"] + 4, (room["y0"] + room["y1"]) // 2 + 1
    while not b.walkable(sx, srow) or b.t[srow][sx] != '.': sx += 1
    b.put(sx, srow, str(spawn))
    b.enemies.append({"spawn": spawn, "type": kind, "patrol": pts, "persona": persona, "callsign": callsign})


def tune(b, low=0.38, high=0.52):
    # 세기는 1을 넘길 수 없으니 반경을 늘리고 줄여 맞춘다
    base = [L["radius_m"] for L in b.lights]
    lo, hi = 0.5, 2.5
    for _ in range(12):
        mid = (lo + hi) / 2
        for L, r0 in zip(b.lights, base): L["radius_m"] = round(r0 * mid, 1)
        r = b.bright_ratio()
        if r < low: lo = mid
        elif r > high: hi = mid
        else: break
    return b.bright_ratio()

def room_lights(b, r, radius, intensity):
    # 방 하나에 조명 2개(가로로 1/3, 2/3 지점)
    y = (r["y0"] + r["y1"]) // 2
    w = r["x1"] - r["x0"]
    b.light(r["x0"] + w // 3, y, radius, intensity)
    b.light(r["x0"] + 2 * w // 3, y, radius, intensity)

PERSONAS = ["veteran", "rookie", "grumpy", "cautious"]
NAMES = ["김 상병", "이 일병", "최 병장", "정 상병", "한 일병", "오 병장", "윤 상병", "장 일병", "임 병장", "서 상병"]


def map02():
    b = Builder(50, 40); cr = 18
    rooms = building(b, 9, 3, 3, cr, ['w', 'k', 't'])
    pats = [["tl", "br"], ["mid", "tr"], ["tl"], ["br", "mid"], ["tr", "tl"], ["mid", "br", "tl"]]
    for r, p in zip(rooms, pats): covers(b, r, p)
    b.put(3, 36, 'P')
    t = rooms[5]; b.put(t["x1"] - 3, (t["y0"] + t["y1"]) // 2, 'T')
    b.door(rooms[0]["x1"] + 1, 6, vertical=True)            # 위 방 1-2 사이 문
    b.open_doors.append(b.world(rooms[1]["door"][0], rooms[1]["door"][1]))
    for i, r in enumerate(rooms):
        if i in (2,): continue                              # 조명 없는 방(어둠)
        room_lights(b, r, 6.5, 0.9)
    for x in (16, 30, 44): b.light(x, cr + 1, 4.5, 0.8)
    kinds = ["guard", "rifleman", "guard", "guard", "rifleman", "guard"]
    for i, r in enumerate(rooms):
        add_enemy(b, i + 1, kinds[i], r, cr, PERSONAS[i % 4], NAMES[i], use_corridor=(i % 2 == 0))
    ratio = tune(b)
    return b.json("map_02", "맵 2 - 자료 탈취", "steal", 420, cans=[(24, cr), (40, cr + 2)]), ratio


def map03():
    b = Builder(60, 40); cr = 18
    rooms = building(b, 9, 4, 3, cr, ['t', 'w', 't', 'k'])
    pats = [["tl"], ["br", "mid"], ["tr"], ["tl", "br"], ["mid"], ["tl", "tr"], ["br", "mid", "tl"]]
    for r, p in zip(rooms, pats): covers(b, r, p)
    b.put(3, 37, 'P')
    b.put(3, 3, 'X')
    t = rooms[3]; b.put(t["x1"] - 3, (t["y0"] + t["y1"]) // 2, 'T')
    b.door(rooms[4]["x1"] + 1, cr + 10, vertical=True)
    b.open_doors.append(b.world(rooms[5]["door"][0], rooms[5]["door"][1]))
    for i, r in enumerate(rooms):
        if i in (1, 4, 6): continue                          # 야간 사무동: 조명 적음
        room_lights(b, r, 7, 0.9)
    for x in (14, 34, 52): b.light(x, cr + 1, 5, 0.8)
    kinds = ["guard", "rifleman", "guard", "rifleman", "guard", "guard", "rifleman"]
    for i, r in enumerate(rooms):
        add_enemy(b, i + 1, kinds[i], r, cr, PERSONAS[i % 4], NAMES[i], use_corridor=(i % 2 == 1))
    # 8번째 적: 복도 순찰
    b.put(30, cr + 1, '8')
    b.enemies.append({"spawn": 8, "type": "guard", "patrol": [b.world(14, cr + 1), b.world(50, cr + 1), b.world(30, cr + 2)], "persona": "cautious", "callsign": NAMES[7]})
    ratio = tune(b)
    return b.json("map_03", "맵 3 - 자료 탈취 후 탈출", "steal_exit", 480, cans=[(20, cr + 2), (45, cr)]), ratio


def map04():
    b = Builder(70, 50); cr = 23
    rooms = building(b, 10, 5, 4, cr, ['w', 'k', 't', 'w', 'k'])
    pats = [["tl", "br"], ["mid"], ["tr", "br"], ["tl"], ["mid", "tl"], ["br", "tr"], ["mid", "tl", "br"], ["tr"], ["tl", "mid"]]
    for r, p in zip(rooms, pats): covers(b, r, p)
    b.put(4, 46, 'P')
    b.door(rooms[1]["x1"] + 1, 8, vertical=True)
    b.door(rooms[6]["x1"] + 1, cr + 14, vertical=True)
    b.open_doors.append(b.world(rooms[2]["door"][0], rooms[2]["door"][1]))
    b.open_doors.append(b.world(rooms[7]["door"][0], rooms[7]["door"][1]))
    for i, r in enumerate(rooms):
        if i in (3, 7): continue
        room_lights(b, r, 7, 0.9)
    for x in (18, 34, 50, 64): b.light(x, cr + 1, 5, 0.8)
    kinds = ["guard", "rifleman", "guard", "rifleman", "guard", "rifleman", "guard", "guard", "rifleman"]
    for i, r in enumerate(rooms):
        add_enemy(b, i + 1, kinds[i], r, cr, PERSONAS[i % 4], NAMES[i], use_corridor=(i % 3 == 0))
    # 10번째 적: 스폰 번호가 한 자리라 9번 칸을 하나 더 둔다(두 번째 9 = 10번째 적)
    b.put(40, cr + 1, '9')
    b.enemies.append({"spawn": 9, "type": "guard", "patrol": [b.world(16, cr + 1), b.world(64, cr + 1), b.world(40, cr + 2)], "persona": "grumpy", "callsign": NAMES[9]})
    ratio = tune(b)
    return b.json("map_04", "맵 4 - 최종 소탕", "eliminate", 600, cans=[(26, cr), (52, cr + 2), (30, cr + 2)]), ratio


def dump(path, data):
    def pt(p): return "[" + ", ".join(("%g" % v) for v in p) + "]"
    lines = ['{']
    keys = list(data.keys())
    for i, k in enumerate(keys):
        v = data[k]; comma = ',' if i < len(keys) - 1 else ''
        if k in ("rows", "floor_rows"):
            lines.append('  "%s": [' % k)
            lines += ['    "%s"%s' % (r, ',' if j < len(v) - 1 else '') for j, r in enumerate(v)]
            lines.append('  ]' + comma)
        elif k == "lights":
            lines.append('  "lights": [')
            lines += ['    {"pos": %s, "radius_m": %g, "intensity": %g}%s' % (pt(L["pos"]), L["radius_m"], L["intensity"], ',' if j < len(v) - 1 else '') for j, L in enumerate(v)]
            lines.append('  ]' + comma)
        elif k == "open_doors":
            lines.append('  "open_doors": [%s]%s' % (", ".join(pt(p) for p in v), comma))
        elif k == "enemies":
            lines.append('  "enemies": [')
            for j, e in enumerate(v):
                lines.append('    {"spawn": %d, "type": "%s", "patrol": [%s], "persona": "%s", "callsign": "%s"}%s' % (
                    e["spawn"], e["type"], ", ".join(pt(p) for p in e["patrol"]), e["persona"], e["callsign"], ',' if j < len(v) - 1 else ''))
            lines.append('  ]' + comma)
        else:
            lines.append('  "%s": %s%s' % (k, json.dumps(v, ensure_ascii=False), comma))
    lines.append('}')
    with open(path, 'w', encoding='utf-8', newline='\n') as fp: fp.write('\n'.join(lines) + '\n')


if __name__ == "__main__":
    out = sys.argv[1]
    for fn in (map02, map03, map04):
        data, ratio = fn()
        dump(os.path.join(out, data["id"] + ".json"), data)
        print(data["id"], "적", len(data["enemies"]), "조명", len(data["lights"]), "밝은 칸 %.0f%%" % (ratio * 100))
