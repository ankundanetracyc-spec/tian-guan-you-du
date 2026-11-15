"""Cross-agent leases for the built-in image tool; performs no API calls."""
from pathlib import Path
import argparse
import json
import os
import time
import uuid

ROOT = Path(__file__).resolve().parents[2]
FOLDER = ROOT / 'output/bai_chuan_free_world_20261104/imagegen_slots'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['acquire', 'release', 'cooldown', 'status'])
    parser.add_argument('--owner', default='')
    parser.add_argument('--token', default='')
    parser.add_argument('--seconds', type=int, default=75)
    args = parser.parse_args()
    FOLDER.mkdir(parents=True, exist_ok=True)
    now = time.time()
    cooldown = FOLDER / 'cooldown.json'
    until = json.loads(cooldown.read_text()).get('until', 0) if cooldown.exists() else 0
    if args.action == 'cooldown':
        until = max(until, now + max(1, args.seconds))
        cooldown.write_text(json.dumps({'until': until, 'owner': args.owner}))
        print(json.dumps({'ok': True, 'retry_after_seconds': int(until - now)}))
        return
    slots = [FOLDER / f'slot_{i}.json' for i in range(2)]
    if args.action == 'release':
        for path in slots:
            if path.exists() and json.loads(path.read_text()).get('token') == args.token:
                path.unlink()
                print(json.dumps({'ok': True}))
                return
        print(json.dumps({'ok': False, 'reason': 'unknown lease'}))
        return
    if args.action == 'status':
        print(json.dumps({'retry_after_seconds': max(0, int(until - now)), 'slots': [json.loads(p.read_text()) for p in slots if p.exists()]}))
        return
    if now < until:
        print(json.dumps({'ok': False, 'reason': 'cooldown', 'retry_after_seconds': int(until - now) + 1}))
        return
    for index, path in enumerate(slots):
        token = uuid.uuid4().hex
        record = {'token': token, 'owner': args.owner, 'created': now, 'slot': index}
        try:
            with path.open('x', encoding='utf-8') as file:
                json.dump(record, file)
        except FileExistsError:
            continue
        print(json.dumps({'ok': True, **record}))
        return
    print(json.dumps({'ok': False, 'reason': 'both slots busy', 'retry_after_seconds': 30}))


if __name__ == '__main__':
    main()
