# =============================================================================
# run_all_python_tests.py
# Runs all 4 EESimulator Python component test suites and prints summary
# =============================================================================

import os
import sys
import time

script_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, script_dir)

import eesim
import circuit

if os.environ.get("EEE_SCRIPTING_BRIDGE") != "1":
    print("ERROR: Run these suites through the EESimulator .NET scripting test bridge.")
    sys.exit(1)

protocol_output = sys.stdout
eesim.attach(protocol_output, sys.stdin)
sys.stdout = sys.stderr

test_files = [
    ("test_all_116_components.py", "116 Components Full Test Suite"),
    ("test_component_circuit_tests.py", "Component Circuit Unit Tests (26 Tests)"),
    ("test_enhanced_component_tests.py", "Enhanced Component Tests (60 Tests)"),
    ("test_complex_linked_circuit_tests.py", "Complex Linked Circuit Tests (48 Checkpoints)"),
]

print("=" * 80)
print("EESIMULATOR PYTHON TEST SUITE RUNNER")
print("Target: All 4 Python Test Suites in EESimulator.Tests/Python")
print("=" * 80)

total_passed = 0
total_failed = 0
start_time = time.time()
for fname, desc in test_files:
    fpath = os.path.join(script_dir, fname)
    print("\n" + "#" * 80)
    print(f"RUNNING: {fname} - {desc}")
    print("#" * 80 + "\n")

    if not os.path.exists(fpath):
        print(f"ERROR: File not found: {fpath}")
        total_failed += 1
        continue

    try:
        eesim.clear_circuit()
        with open(fpath, "r", encoding="utf-8") as f:
            code = f.read()

        env = {
            "__file__": fpath,
            "__name__": "__main__",
            "eesim": eesim,
            "circuit": circuit,
            "true": True,
            "false": False,
            "null": None,
            "none": None,
        }
        exec(compile(code, fpath, "exec"), env)
        print(f"\n>>> [OK] {fname} completed successfully.\n")
        total_passed += 1
    except Exception as ex:
        print(f"\n>>> [FAIL] {fname} encountered error: {ex}\n")
        import traceback
        traceback.print_exc()
        total_failed += 1

elapsed = time.time() - start_time
print("=" * 80)
print("PYTHON TEST SUITES SUMMARY")
print(f"Suites Passed: {total_passed}/{len(test_files)}")
print(f"Suites Failed: {total_failed}/{len(test_files)}")
print(f"Time Taken:    {elapsed:.2f} seconds")
print("=" * 80)

if total_failed == 0:
    print("ALL PYTHON TEST SUITES COMPLETED AND VERIFIED SUCCESSFULLY.")
    sys.exit(0)
else:
    print("SOME PYTHON TEST SUITES FAILED.")
    sys.exit(1)
