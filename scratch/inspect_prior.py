import json

def inspect(name):
    print(f"=== {name} ===")
    with open(f"Resources/Raw/Examples/{name}", "r", encoding="utf-8") as f:
        d = json.load(f)
    print("Title:", d.get("Title"))
    print("Description:", d.get("Description"))
    print(f"Components ({len(d.get('Components', []))}):")
    for c in d.get("Components", []):
        ref = c.get("Reference")
        tid = c.get("StableTypeId")
        ctype = c.get("ComponentType")
        print(f"  {ref} ({ctype}, {tid}): sp={c.get('StringParameters', {})} ep={c.get('ElectricalParameters', {})}")
    print(f"Connections ({len(d.get('Connections', []))}):")
    for conn in d.get("Connections", [])[:10]:
        print(f"  {conn.get('SourceComponentId')} [{conn.get('SourcePinId')}] -> {conn.get('TargetComponentId')} [{conn.get('TargetPinId')}]")

inspect("single_phase_transformer.eee")
inspect("forward_converter.eee")

