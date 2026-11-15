import json

try:
    with open("Resources/Raw/Examples/forward_converter.eee", "r", encoding="utf-8") as f:
        content = f.read()
    d = json.loads(content)
    print("forward_converter.eee is VALID JSON")
except Exception as e:
    print("forward_converter.eee JSON ERROR:", e)
    # let's find the trailing comma and fix it
    import re
    # Remove trailing commas before } or ]
    fixed = re.sub(r',\s*([}\]])', r'\1', content)
    try:
        json.loads(fixed)
        with open("Resources/Raw/Examples/forward_converter.eee", "w", encoding="utf-8") as f:
            f.write(fixed)
        print("Fixed forward_converter.eee trailing comma successfully!")
    except Exception as e2:
        print("Failed to auto-fix:", e2)

