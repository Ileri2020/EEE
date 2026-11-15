import json

def inspect_sample(fname):
    print(f"=== {fname} ===")
    with open(f"Resources/Raw/Examples/{fname}", "r", encoding="utf-8") as f:
        d = json.load(f)
    for c in d.get("Components", []):
        ref = c.get("Reference")
        tid = c.get("StableTypeId")
        ctype = c.get("ComponentType")
        print(f"  {ref} ({ctype}, {tid})")
    print("Sample connections:")
    for conn in d.get("Connections", [])[:8]:
        print(f"  {conn}")

inspect_sample("active_clamp_flyback_converter.eee")
inspect_sample("sepic_converter.eee")

