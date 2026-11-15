import json

with open("Resources/Raw/Examples/single_phase_transformer.eee", "r", encoding="utf-8") as f:
    d = json.load(f)

print("Connections[0]:", json.dumps(d["Connections"][0], indent=2))
for i, c in enumerate(d["Connections"]):
    print(f"Conn {i}: {c.get('FromComponentId') or c.get('SourceComponentId') or c.get('FromId')} : {c.get('FromPin') or c.get('SourcePinId') or c.get('FromPinId')} -> {c.get('ToComponentId') or c.get('TargetComponentId') or c.get('ToId')} : {c.get('ToPin') or c.get('TargetPinId') or c.get('ToPinId')}")

