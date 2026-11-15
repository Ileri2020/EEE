import json

with open("Resources/Raw/Examples/three_phase_transformer.eee", "r", encoding="utf-8") as f:
    d = json.load(f)

print("Root keys:", list(d.keys()))
if "Components" in d:
    print("Component 0 keys:", list(d["Components"][0].keys()))
    print("Component 0:", json.dumps(d["Components"][0], indent=2))
    print("Component 1:", json.dumps(d["Components"][1], indent=2))
    print("Component 4 (transformer):", json.dumps(d["Components"][4], indent=2))
if "Wires" in d and len(d["Wires"]) > 0:
    print("Wire 0:", d["Wires"][0])
else:
    print("Wires length is 0? Let's check keys matching wire/net/connections:", [k for k in d.keys() if 'wire' in k.lower() or 'conn' in k.lower() or 'net' in k.lower()])

