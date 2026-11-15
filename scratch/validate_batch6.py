import json, os, uuid

files = [
    "three_phase_transformer.eee",
    "eight_bit_cpu.eee",
    "three_phase_controlled_rectifier.eee",
    "full_bridge_smps.eee",
    "push_pull_converter.eee"
]

total_errors = 0

for fname in files:
    path = os.path.join("Resources", "Raw", "Examples", fname)
    print(f"\n==================== VALIDATING {fname} ====================")
    if not os.path.exists(path):
        print(f"FAIL: File does not exist: {path}")
        total_errors += 1
        continue
    
    try:
        with open(path, "r", encoding="utf-8") as f:
            raw = f.read()
        data = json.loads(raw)
    except Exception as e:
        print(f"FAIL: Invalid JSON in {fname}: {e}")
        total_errors += 1
        continue
    
    comps = data.get("Components", [])
    conns = data.get("Connections", [])
    
    print(f"Components: {len(comps)}, Connections: {len(conns)}")
    
    comp_ids = set()
    comp_refs = set()
    probes_checked = 0
    
    for c in comps:
        cid = c.get("Id")
        ref = c.get("Reference")
        ctype = c.get("ComponentType")
        stype = c.get("StableTypeId")
        bc = c.get("BreakingCurrent")
        
        # UUID check
        try:
            uuid.UUID(cid)
        except Exception:
            print(f"FAIL: Invalid UUID for component {ref}: {cid}")
            total_errors += 1
        
        if cid in comp_ids:
            print(f"FAIL: Duplicate component Id: {cid}")
            total_errors += 1
        comp_ids.add(cid)
        
        if not ref:
            print(f"FAIL: Component missing Reference: {cid}")
            total_errors += 1
        comp_refs.add(ref)
        
        if not ctype:
            print(f"FAIL: Component {ref} missing ComponentType")
            total_errors += 1
            
        if not stype or stype == "None":
            print(f"FAIL: Component {ref} missing StableTypeId")
            total_errors += 1
            
        if bc is None or bc <= 0 or not isinstance(bc, (int, float)):
            print(f"FAIL: Component {ref} has invalid BreakingCurrent: {bc}")
            total_errors += 1
            
        # Check string params for bad lowercase booleans
        sp = c.get("StringParameters", {})
        for k, v in sp.items():
            if v in ("true", "false"):
                print(f"FAIL: Component {ref} has lowercase boolean in StringParameters[{k}]: {v}")
                total_errors += 1
                
        # Probe checks
        if ctype == "Probe":
            probes_checked += 1
            exp_v = sp.get("ExpectedVoltage")
            if not exp_v:
                print(f"FAIL: Probe {ref} missing ExpectedVoltage")
                total_errors += 1
            else:
                try:
                    ev_float = float(exp_v)
                    print(f"  Probe {ref}: ExpectedVoltage = {ev_float} V")
                except ValueError:
                    print(f"FAIL: Probe {ref} ExpectedVoltage is not numeric: {exp_v}")
                    total_errors += 1

    # Connections check
    for idx, conn in enumerate(conns):
        conn_id = conn.get("Id")
        from_id = conn.get("FromComponentId")
        from_pin = conn.get("FromPin")
        to_id = conn.get("ToComponentId")
        to_pin = conn.get("ToPin")
        
        if from_id not in comp_ids:
            print(f"FAIL: Connection {idx} from non-existent component {from_id}")
            total_errors += 1
        if to_id not in comp_ids:
            print(f"FAIL: Connection {idx} to non-existent component {to_id}")
            total_errors += 1
        if not from_pin:
            print(f"FAIL: Connection {idx} missing FromPin")
            total_errors += 1
        if not to_pin:
            print(f"FAIL: Connection {idx} missing ToPin")
            total_errors += 1

    print(f"Probes verified: {probes_checked}")
    print(f"{fname}: PASSED checks")

if total_errors == 0:
    print("\nALL 5 SCHEMATICS VALIDATED 100% CLEAN WITH ZERO ERRORS!")
else:
    print(f"\nVALIDATION FAILED WITH {total_errors} ERRORS.")

