import json

def inspect_pins_and_conns(fname):
    print(f"\n==================== {fname} ====================")
    with open(f"Resources/Raw/Examples/{fname}", "r", encoding="utf-8") as f:
        d = json.load(f)
    print("Components:")
    comp_map = {}
    for c in d.get("Components", []):
        cid = c.get("Id")
        ref = c.get("Reference") or c.get("Name")
        comp_map[cid] = ref
        print(f"  {ref} ({c.get('ComponentType')}, {c.get('StableTypeId')}, Id: {cid})")
    print(f"Connections ({len(d.get('Connections', []))}):")
    for conn in d.get("Connections", []):
        from_ref = comp_map.get(conn.get("FromComponentId"), conn.get("FromComponentId"))
        to_ref = comp_map.get(conn.get("ToComponentId"), conn.get("ToComponentId"))
        print(f"  {from_ref}:{conn.get('FromPin')} -> {to_ref}:{conn.get('ToPin')}")

inspect_pins_and_conns("three_phase_transformer.eee")
inspect_pins_and_conns("eight_bit_cpu.eee")
inspect_pins_and_conns("three_phase_controlled_rectifier.eee")
inspect_pins_and_conns("full_bridge_smps.eee")
inspect_pins_and_conns("push_pull_converter.eee")

