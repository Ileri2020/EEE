import json

def inspect_file(fname):
    print(f"==================== {fname} ====================")
    with open(f"Resources/Raw/Examples/{fname}", "r", encoding="utf-8") as f:
        d = json.load(f)
    print(f"Name: {d.get('Name')}")
    print(f"Description: {d.get('Description')}")
    print(f"Components count: {len(d.get('Components', []))}")
    print(f"Wires count: {len(d.get('Wires', []))}")
    for c in d.get('Components', []):
        tid = c.get('StableTypeId') or c.get('TypeId')
        lbl = c.get('Label') or c.get('Designator') or c.get('Properties', {}).get('Designator') or c.get('Properties', {}).get('Label')
        pins = [p.get('PinId') or p.get('Name') or p.get('Id') for p in c.get('Pins', [])]
        props = c.get('Properties', {})
        print(f"  [{tid}] {lbl} (Id: {c.get('Id')}) pins={pins} props={props}")
    print("Wires sample (first 5):")
    for w in d.get('Wires', [])[:5]:
        print(f"  {w}")

inspect_file('three_phase_transformer.eee')
inspect_file('eight_bit_cpu.eee')
inspect_file('three_phase_controlled_rectifier.eee')
inspect_file('full_bridge_smps.eee')
inspect_file('push_pull_converter.eee')

