import json, os

files = [
    'three_phase_transformer.eee',
    'eight_bit_cpu.eee',
    'three_phase_controlled_rectifier.eee',
    'full_bridge_smps.eee',
    'push_pull_converter.eee'
]

for fname in files:
    path = os.path.join('Resources', 'Raw', 'Examples', fname)
    if not os.path.exists(path):
        print(f"NOT FOUND: {path}")
        continue
    try:
        with open(path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        comps = data.get('Components', [])
        wires = data.get('Wires', [])
        types = set(c.get('StableTypeId', c.get('TypeId')) for c in comps)
        labels = [c.get('Label') or c.get('Designator') for c in comps]
        print(f"=== {fname} ===")
        print(f"Components: {len(comps)}, Wires: {len(wires)}")
        print(f"Types: {types}")
        print(f"Labels: {labels[:15]}...")
    except Exception as e:
        print(f"=== {fname} ERROR: {e} ===")

