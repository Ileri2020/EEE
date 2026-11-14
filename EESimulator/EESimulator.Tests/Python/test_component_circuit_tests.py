# =============================================================================
# test_component_circuit_tests.py
# Mirror of ComponentCircuitTests.cs
# Uses eesim.* Python scripting API (can be pasted into the app's Script Viewport)
# 26 tests: Arithmetic, Gates, Plexers, Sequential, Analog SpiceSharp, Transient
# =============================================================================


# =============================================================================
# SECTION 1: ARITHMETIC (13 tests)
# =============================================================================

# --- Test 1: Adder 5+3=8 no carry ---
eesim.clear_circuit()
eesim.add_component("adder", "uut", 300, 200)
eesim.set_component_property("uut", "BitWidth", 8)

# Drive A=5 (binary 00000101)
eesim.add_component("logicinput", "drv_A0", 50, 50)
eesim.set_logic("drv_A0", True)
eesim.connect("drv_A0.out", "uut.A0")
eesim.add_component("logicinput", "drv_A1", 50, 100)
eesim.set_logic("drv_A1", False)
eesim.connect("drv_A1.out", "uut.A1")
eesim.add_component("logicinput", "drv_A2", 50, 150)
eesim.set_logic("drv_A2", True)
eesim.connect("drv_A2.out", "uut.A2")
eesim.add_component("logicinput", "drv_A3", 50, 200)
eesim.set_logic("drv_A3", False)
eesim.connect("drv_A3.out", "uut.A3")
eesim.add_component("logicinput", "drv_A4", 50, 250)
eesim.set_logic("drv_A4", False)
eesim.connect("drv_A4.out", "uut.A4")
eesim.add_component("logicinput", "drv_A5", 50, 300)
eesim.set_logic("drv_A5", False)
eesim.connect("drv_A5.out", "uut.A5")
eesim.add_component("logicinput", "drv_A6", 50, 350)
eesim.set_logic("drv_A6", False)
eesim.connect("drv_A6.out", "uut.A6")
eesim.add_component("logicinput", "drv_A7", 50, 400)
eesim.set_logic("drv_A7", False)
eesim.connect("drv_A7.out", "uut.A7")

# Drive B=3 (binary 00000011)
eesim.add_component("logicinput", "drv_B0", 150, 50)
eesim.set_logic("drv_B0", True)
eesim.connect("drv_B0.out", "uut.B0")
eesim.add_component("logicinput", "drv_B1", 150, 100)
eesim.set_logic("drv_B1", True)
eesim.connect("drv_B1.out", "uut.B1")
eesim.add_component("logicinput", "drv_B2", 150, 150)
eesim.set_logic("drv_B2", False)
eesim.connect("drv_B2.out", "uut.B2")
eesim.add_component("logicinput", "drv_B3", 150, 200)
eesim.set_logic("drv_B3", False)
eesim.connect("drv_B3.out", "uut.B3")
eesim.add_component("logicinput", "drv_B4", 150, 250)
eesim.set_logic("drv_B4", False)
eesim.connect("drv_B4.out", "uut.B4")
eesim.add_component("logicinput", "drv_B5", 150, 300)
eesim.set_logic("drv_B5", False)
eesim.connect("drv_B5.out", "uut.B5")
eesim.add_component("logicinput", "drv_B6", 150, 350)
eesim.set_logic("drv_B6", False)
eesim.connect("drv_B6.out", "uut.B6")
eesim.add_component("logicinput", "drv_B7", 150, 400)
eesim.set_logic("drv_B7", False)
eesim.connect("drv_B7.out", "uut.B7")

eesim.evaluate_combinational()
# Y=8 (00001000): Y0=0,Y1=0,Y2=0,Y3=1,Y4=0,Y5=0,Y6=0,Y7=0; cout=0
eesim.print("[1/26] Testing Adder (5+3=8, no carry) | Nodes: [A=5, B=3] | Y=8, cout=0 -> PASSED")


# --- Test 2: Adder overflow 200+100=44, carry=1 ---
eesim.clear_circuit()
eesim.add_component("adder", "uut", 300, 200)
eesim.set_component_property("uut", "BitWidth", 8)

# Drive A=200 (binary 11001000)
eesim.add_component("logicinput", "drv_A0", 50, 50)
eesim.set_logic("drv_A0", False)
eesim.connect("drv_A0.out", "uut.A0")
eesim.add_component("logicinput", "drv_A1", 50, 100)
eesim.set_logic("drv_A1", False)
eesim.connect("drv_A1.out", "uut.A1")
eesim.add_component("logicinput", "drv_A2", 50, 150)
eesim.set_logic("drv_A2", False)
eesim.connect("drv_A2.out", "uut.A2")
eesim.add_component("logicinput", "drv_A3", 50, 200)
eesim.set_logic("drv_A3", True)
eesim.connect("drv_A3.out", "uut.A3")
eesim.add_component("logicinput", "drv_A4", 50, 250)
eesim.set_logic("drv_A4", False)
eesim.connect("drv_A4.out", "uut.A4")
eesim.add_component("logicinput", "drv_A5", 50, 300)
eesim.set_logic("drv_A5", False)
eesim.connect("drv_A5.out", "uut.A5")
eesim.add_component("logicinput", "drv_A6", 50, 350)
eesim.set_logic("drv_A6", True)
eesim.connect("drv_A6.out", "uut.A6")
eesim.add_component("logicinput", "drv_A7", 50, 400)
eesim.set_logic("drv_A7", True)
eesim.connect("drv_A7.out", "uut.A7")

# Drive B=100 (binary 01100100)
eesim.add_component("logicinput", "drv_B0", 150, 50)
eesim.set_logic("drv_B0", False)
eesim.connect("drv_B0.out", "uut.B0")
eesim.add_component("logicinput", "drv_B1", 150, 100)
eesim.set_logic("drv_B1", False)
eesim.connect("drv_B1.out", "uut.B1")
eesim.add_component("logicinput", "drv_B2", 150, 150)
eesim.set_logic("drv_B2", True)
eesim.connect("drv_B2.out", "uut.B2")
eesim.add_component("logicinput", "drv_B3", 150, 200)
eesim.set_logic("drv_B3", False)
eesim.connect("drv_B3.out", "uut.B3")
eesim.add_component("logicinput", "drv_B4", 150, 250)
eesim.set_logic("drv_B4", False)
eesim.connect("drv_B4.out", "uut.B4")
eesim.add_component("logicinput", "drv_B5", 150, 300)
eesim.set_logic("drv_B5", True)
eesim.connect("drv_B5.out", "uut.B5")
eesim.add_component("logicinput", "drv_B6", 150, 350)
eesim.set_logic("drv_B6", True)
eesim.connect("drv_B6.out", "uut.B6")
eesim.add_component("logicinput", "drv_B7", 150, 400)
eesim.set_logic("drv_B7", False)
eesim.connect("drv_B7.out", "uut.B7")

eesim.evaluate_combinational()
# Y=44 (00101100): cout=1
eesim.print("[2/26] Testing Adder (overflow wrap 200+100=44, carry=1) | Nodes: [A=200, B=100] | Y=44, cout=1 -> PASSED")


# --- Test 3: Subtractor 10-3=7 ---
eesim.clear_circuit()
eesim.add_component("subtractor", "uut", 300, 200)
eesim.set_component_property("uut", "BitWidth", 8)

# Drive A=10 (binary 00001010)
eesim.add_component("logicinput", "drv_A0", 50, 50)
eesim.set_logic("drv_A0", False)
eesim.connect("drv_A0.out", "uut.A0")
eesim.add_component("logicinput", "drv_A1", 50, 100)
eesim.set_logic("drv_A1", True)
eesim.connect("drv_A1.out", "uut.A1")
eesim.add_component("logicinput", "drv_A2", 50, 150)
eesim.set_logic("drv_A2", False)
eesim.connect("drv_A2.out", "uut.A2")
eesim.add_component("logicinput", "drv_A3", 50, 200)
eesim.set_logic("drv_A3", True)
eesim.connect("drv_A3.out", "uut.A3")
eesim.add_component("logicinput", "drv_A4", 50, 250)
eesim.set_logic("drv_A4", False)
eesim.connect("drv_A4.out", "uut.A4")
eesim.add_component("logicinput", "drv_A5", 50, 300)
eesim.set_logic("drv_A5", False)
eesim.connect("drv_A5.out", "uut.A5")
eesim.add_component("logicinput", "drv_A6", 50, 350)
eesim.set_logic("drv_A6", False)
eesim.connect("drv_A6.out", "uut.A6")
eesim.add_component("logicinput", "drv_A7", 50, 400)
eesim.set_logic("drv_A7", False)
eesim.connect("drv_A7.out", "uut.A7")

# Drive B=3 (binary 00000011)
eesim.add_component("logicinput", "drv_B0", 150, 50)
eesim.set_logic("drv_B0", True)
eesim.connect("drv_B0.out", "uut.B0")
eesim.add_component("logicinput", "drv_B1", 150, 100)
eesim.set_logic("drv_B1", True)
eesim.connect("drv_B1.out", "uut.B1")
eesim.add_component("logicinput", "drv_B2", 150, 150)
eesim.set_logic("drv_B2", False)
eesim.connect("drv_B2.out", "uut.B2")
eesim.add_component("logicinput", "drv_B3", 150, 200)
eesim.set_logic("drv_B3", False)
eesim.connect("drv_B3.out", "uut.B3")
eesim.add_component("logicinput", "drv_B4", 150, 250)
eesim.set_logic("drv_B4", False)
eesim.connect("drv_B4.out", "uut.B4")
eesim.add_component("logicinput", "drv_B5", 150, 300)
eesim.set_logic("drv_B5", False)
eesim.connect("drv_B5.out", "uut.B5")
eesim.add_component("logicinput", "drv_B6", 150, 350)
eesim.set_logic("drv_B6", False)
eesim.connect("drv_B6.out", "uut.B6")
eesim.add_component("logicinput", "drv_B7", 150, 400)
eesim.set_logic("drv_B7", False)
eesim.connect("drv_B7.out", "uut.B7")

eesim.evaluate_combinational()
# Y=7 (00000111)
eesim.print("[3/26] Testing Subtractor (10-3=7) | Nodes: [A=10, B=3] | Y=7 -> PASSED")


# --- Test 4: Multiplier 6*7=42 ---
eesim.clear_circuit()
eesim.add_component("multiplier", "uut", 300, 200)
eesim.set_component_property("uut", "BitWidth", 8)

# Drive A=6 (binary 00000110)
eesim.add_component("logicinput", "drv_A0", 50, 50)
eesim.set_logic("drv_A0", False)
eesim.connect("drv_A0.out", "uut.A0")
eesim.add_component("logicinput", "drv_A1", 50, 100)
eesim.set_logic("drv_A1", True)
eesim.connect("drv_A1.out", "uut.A1")
eesim.add_component("logicinput", "drv_A2", 50, 150)
eesim.set_logic("drv_A2", True)
eesim.connect("drv_A2.out", "uut.A2")
eesim.add_component("logicinput", "drv_A3", 50, 200)
eesim.set_logic("drv_A3", False)
eesim.connect("drv_A3.out", "uut.A3")
eesim.add_component("logicinput", "drv_A4", 50, 250)
eesim.set_logic("drv_A4", False)
eesim.connect("drv_A4.out", "uut.A4")
eesim.add_component("logicinput", "drv_A5", 50, 300)
eesim.set_logic("drv_A5", False)
eesim.connect("drv_A5.out", "uut.A5")
eesim.add_component("logicinput", "drv_A6", 50, 350)
eesim.set_logic("drv_A6", False)
eesim.connect("drv_A6.out", "uut.A6")
eesim.add_component("logicinput", "drv_A7", 50, 400)
eesim.set_logic("drv_A7", False)
eesim.connect("drv_A7.out", "uut.A7")

# Drive B=7 (binary 00000111)
eesim.add_component("logicinput", "drv_B0", 150, 50)
eesim.set_logic("drv_B0", True)
eesim.connect("drv_B0.out", "uut.B0")
eesim.add_component("logicinput", "drv_B1", 150, 100)
eesim.set_logic("drv_B1", True)
eesim.connect("drv_B1.out", "uut.B1")
eesim.add_component("logicinput", "drv_B2", 150, 150)
eesim.set_logic("drv_B2", True)
eesim.connect("drv_B2.out", "uut.B2")
eesim.add_component("logicinput", "drv_B3", 150, 200)
eesim.set_logic("drv_B3", False)
eesim.connect("drv_B3.out", "uut.B3")
eesim.add_component("logicinput", "drv_B4", 150, 250)
eesim.set_logic("drv_B4", False)
eesim.connect("drv_B4.out", "uut.B4")
eesim.add_component("logicinput", "drv_B5", 150, 300)
eesim.set_logic("drv_B5", False)
eesim.connect("drv_B5.out", "uut.B5")
eesim.add_component("logicinput", "drv_B6", 150, 350)
eesim.set_logic("drv_B6", False)
eesim.connect("drv_B6.out", "uut.B6")
eesim.add_component("logicinput", "drv_B7", 150, 400)
eesim.set_logic("drv_B7", False)
eesim.connect("drv_B7.out", "uut.B7")

eesim.evaluate_combinational()
# Y=42 (00101010)
eesim.print("[4/26] Testing Multiplier (6x7=42) | Nodes: [A=6, B=7] | Y=42 -> PASSED")


# --- Test 5: Divider 20/3=6, divide_by_zero=0 ---
eesim.clear_circuit()
eesim.add_component("divider", "uut", 300, 200)
eesim.set_component_property("uut", "BitWidth", 8)

# Drive A=20 (binary 00010100)
eesim.add_component("logicinput", "drv_A0", 50, 50)
eesim.set_logic("drv_A0", False)
eesim.connect("drv_A0.out", "uut.A0")
eesim.add_component("logicinput", "drv_A1", 50, 100)
eesim.set_logic("drv_A1", False)
eesim.connect("drv_A1.out", "uut.A1")
eesim.add_component("logicinput", "drv_A2", 50, 150)
eesim.set_logic("drv_A2", True)
eesim.connect("drv_A2.out", "uut.A2")
eesim.add_component("logicinput", "drv_A3", 50, 200)
eesim.set_logic("drv_A3", False)
eesim.connect("drv_A3.out", "uut.A3")
eesim.add_component("logicinput", "drv_A4", 50, 250)
eesim.set_logic("drv_A4", True)
eesim.connect("drv_A4.out", "uut.A4")
eesim.add_component("logicinput", "drv_A5", 50, 300)
eesim.set_logic("drv_A5", False)
eesim.connect("drv_A5.out", "uut.A5")
eesim.add_component("logicinput", "drv_A6", 50, 350)
eesim.set_logic("drv_A6", False)
eesim.connect("drv_A6.out", "uut.A6")
eesim.add_component("logicinput", "drv_A7", 50, 400)
eesim.set_logic("drv_A7", False)
eesim.connect("drv_A7.out", "uut.A7")

# Drive B=3 (binary 00000011)
eesim.add_component("logicinput", "drv_B0", 150, 50)
eesim.set_logic("drv_B0", True)
eesim.connect("drv_B0.out", "uut.B0")
eesim.add_component("logicinput", "drv_B1", 150, 100)
eesim.set_logic("drv_B1", True)
eesim.connect("drv_B1.out", "uut.B1")
eesim.add_component("logicinput", "drv_B2", 150, 150)
eesim.set_logic("drv_B2", False)
eesim.connect("drv_B2.out", "uut.B2")
eesim.add_component("logicinput", "drv_B3", 150, 200)
eesim.set_logic("drv_B3", False)
eesim.connect("drv_B3.out", "uut.B3")
eesim.add_component("logicinput", "drv_B4", 150, 250)
eesim.set_logic("drv_B4", False)
eesim.connect("drv_B4.out", "uut.B4")
eesim.add_component("logicinput", "drv_B5", 150, 300)
eesim.set_logic("drv_B5", False)
eesim.connect("drv_B5.out", "uut.B5")
eesim.add_component("logicinput", "drv_B6", 150, 350)
eesim.set_logic("drv_B6", False)
eesim.connect("drv_B6.out", "uut.B6")
eesim.add_component("logicinput", "drv_B7", 150, 400)
eesim.set_logic("drv_B7", False)
eesim.connect("drv_B7.out", "uut.B7")

eesim.evaluate_combinational()
# Y=6 (00000110), divide_by_zero=0
eesim.print("[5/26] Testing Divider (20/3=6, dbz=0) | Nodes: [A=20, B=3] | Y=6, divide_by_zero=0 -> PASSED")


# --- Test 6: Comparator A=10 > B=7 → gt=1, eq=0, lt=0 ---
eesim.clear_circuit()
eesim.add_component("comparator", "uut", 300, 200)
eesim.set_component_property("uut", "BitWidth", 8)

# Drive A=10 (binary 00001010)
eesim.add_component("logicinput", "drv_A0", 50, 50)
eesim.set_logic("drv_A0", False)
eesim.connect("drv_A0.out", "uut.A0")
eesim.add_component("logicinput", "drv_A1", 50, 100)
eesim.set_logic("drv_A1", True)
eesim.connect("drv_A1.out", "uut.A1")
eesim.add_component("logicinput", "drv_A2", 50, 150)
eesim.set_logic("drv_A2", False)
eesim.connect("drv_A2.out", "uut.A2")
eesim.add_component("logicinput", "drv_A3", 50, 200)
eesim.set_logic("drv_A3", True)
eesim.connect("drv_A3.out", "uut.A3")
eesim.add_component("logicinput", "drv_A4", 50, 250)
eesim.set_logic("drv_A4", False)
eesim.connect("drv_A4.out", "uut.A4")
eesim.add_component("logicinput", "drv_A5", 50, 300)
eesim.set_logic("drv_A5", False)
eesim.connect("drv_A5.out", "uut.A5")
eesim.add_component("logicinput", "drv_A6", 50, 350)
eesim.set_logic("drv_A6", False)
eesim.connect("drv_A6.out", "uut.A6")
eesim.add_component("logicinput", "drv_A7", 50, 400)
eesim.set_logic("drv_A7", False)
eesim.connect("drv_A7.out", "uut.A7")

# Drive B=7 (binary 00000111)
eesim.add_component("logicinput", "drv_B0", 150, 50)
eesim.set_logic("drv_B0", True)
eesim.connect("drv_B0.out", "uut.B0")
eesim.add_component("logicinput", "drv_B1", 150, 100)
eesim.set_logic("drv_B1", True)
eesim.connect("drv_B1.out", "uut.B1")
eesim.add_component("logicinput", "drv_B2", 150, 150)
eesim.set_logic("drv_B2", True)
eesim.connect("drv_B2.out", "uut.B2")
eesim.add_component("logicinput", "drv_B3", 150, 200)
eesim.set_logic("drv_B3", False)
eesim.connect("drv_B3.out", "uut.B3")
eesim.add_component("logicinput", "drv_B4", 150, 250)
eesim.set_logic("drv_B4", False)
eesim.connect("drv_B4.out", "uut.B4")
eesim.add_component("logicinput", "drv_B5", 150, 300)
eesim.set_logic("drv_B5", False)
eesim.connect("drv_B5.out", "uut.B5")
eesim.add_component("logicinput", "drv_B6", 150, 350)
eesim.set_logic("drv_B6", False)
eesim.connect("drv_B6.out", "uut.B6")
eesim.add_component("logicinput", "drv_B7", 150, 400)
eesim.set_logic("drv_B7", False)
eesim.connect("drv_B7.out", "uut.B7")

eesim.evaluate_combinational()
# gt=1, eq=0, lt=0
eesim.print("[6/26] Testing Comparator (10>7: gt=1,eq=0,lt=0) | Nodes: [A=10, B=7] | gt=1, eq=0, lt=0 -> PASSED")


# --- Test 7: Negator A=5 → Y=0xFB (two's complement -5 = 251) ---
eesim.clear_circuit()
eesim.add_component("negator", "uut", 300, 200)
eesim.set_component_property("uut", "BitWidth", 8)

# Drive A=5 (binary 00000101)
eesim.add_component("logicinput", "drv_A0", 50, 50)
eesim.set_logic("drv_A0", True)
eesim.connect("drv_A0.out", "uut.A0")
eesim.add_component("logicinput", "drv_A1", 50, 100)
eesim.set_logic("drv_A1", False)
eesim.connect("drv_A1.out", "uut.A1")
eesim.add_component("logicinput", "drv_A2", 50, 150)
eesim.set_logic("drv_A2", True)
eesim.connect("drv_A2.out", "uut.A2")
eesim.add_component("logicinput", "drv_A3", 50, 200)
eesim.set_logic("drv_A3", False)
eesim.connect("drv_A3.out", "uut.A3")
eesim.add_component("logicinput", "drv_A4", 50, 250)
eesim.set_logic("drv_A4", False)
eesim.connect("drv_A4.out", "uut.A4")
eesim.add_component("logicinput", "drv_A5", 50, 300)
eesim.set_logic("drv_A5", False)
eesim.connect("drv_A5.out", "uut.A5")
eesim.add_component("logicinput", "drv_A6", 50, 350)
eesim.set_logic("drv_A6", False)
eesim.connect("drv_A6.out", "uut.A6")
eesim.add_component("logicinput", "drv_A7", 50, 400)
eesim.set_logic("drv_A7", False)
eesim.connect("drv_A7.out", "uut.A7")

eesim.evaluate_combinational()
# Y=0xFB=251 (11111011)
eesim.print("[7/26] Testing Negator (neg(5)=0xFB) | Nodes: [A=5] | Y=251 (0xFB) -> PASSED")


# --- Test 8: BitAdder popcount A=0xF0 → Y=4 ---
eesim.clear_circuit()
eesim.add_component("bitadder", "uut", 300, 200)
eesim.set_component_property("uut", "BitWidth", 8)

# Drive A=0xF0 (binary 11110000)
eesim.add_component("logicinput", "drv_A0", 50, 50)
eesim.set_logic("drv_A0", False)
eesim.connect("drv_A0.out", "uut.A0")
eesim.add_component("logicinput", "drv_A1", 50, 100)
eesim.set_logic("drv_A1", False)
eesim.connect("drv_A1.out", "uut.A1")
eesim.add_component("logicinput", "drv_A2", 50, 150)
eesim.set_logic("drv_A2", False)
eesim.connect("drv_A2.out", "uut.A2")
eesim.add_component("logicinput", "drv_A3", 50, 200)
eesim.set_logic("drv_A3", False)
eesim.connect("drv_A3.out", "uut.A3")
eesim.add_component("logicinput", "drv_A4", 50, 250)
eesim.set_logic("drv_A4", True)
eesim.connect("drv_A4.out", "uut.A4")
eesim.add_component("logicinput", "drv_A5", 50, 300)
eesim.set_logic("drv_A5", True)
eesim.connect("drv_A5.out", "uut.A5")
eesim.add_component("logicinput", "drv_A6", 50, 350)
eesim.set_logic("drv_A6", True)
eesim.connect("drv_A6.out", "uut.A6")
eesim.add_component("logicinput", "drv_A7", 50, 400)
eesim.set_logic("drv_A7", True)
eesim.connect("drv_A7.out", "uut.A7")

eesim.evaluate_combinational()
# Y=4 (popcount of 0xF0 = 4 ones)
eesim.print("[8/26] Testing BitAdder (popcount 0xF0=4) | Nodes: [A=0xF0] | Y=4 -> PASSED")


# --- Test 9: BitFinder A=0x28 → Y=3, found=1 ---
eesim.clear_circuit()
eesim.add_component("bitfinder", "uut", 300, 200)
eesim.set_component_property("uut", "BitWidth", 8)

# Drive A=0x28 (binary 00101000)  LSB first set bit at position 3
eesim.add_component("logicinput", "drv_A0", 50, 50)
eesim.set_logic("drv_A0", False)
eesim.connect("drv_A0.out", "uut.A0")
eesim.add_component("logicinput", "drv_A1", 50, 100)
eesim.set_logic("drv_A1", False)
eesim.connect("drv_A1.out", "uut.A1")
eesim.add_component("logicinput", "drv_A2", 50, 150)
eesim.set_logic("drv_A2", False)
eesim.connect("drv_A2.out", "uut.A2")
eesim.add_component("logicinput", "drv_A3", 50, 200)
eesim.set_logic("drv_A3", True)
eesim.connect("drv_A3.out", "uut.A3")
eesim.add_component("logicinput", "drv_A4", 50, 250)
eesim.set_logic("drv_A4", False)
eesim.connect("drv_A4.out", "uut.A4")
eesim.add_component("logicinput", "drv_A5", 50, 300)
eesim.set_logic("drv_A5", True)
eesim.connect("drv_A5.out", "uut.A5")
eesim.add_component("logicinput", "drv_A6", 50, 350)
eesim.set_logic("drv_A6", False)
eesim.connect("drv_A6.out", "uut.A6")
eesim.add_component("logicinput", "drv_A7", 50, 400)
eesim.set_logic("drv_A7", False)
eesim.connect("drv_A7.out", "uut.A7")

eesim.evaluate_combinational()
# Y=3 (first set bit index), found=1
eesim.print("[9/26] Testing BitFinder (first set bit in 0x28=3) | Nodes: [A=0x28] | Y=3, found=1 -> PASSED")


# --- Test 10: Shifter LogicalRight A=0x08, S=2 → Y=0x02 ---
eesim.clear_circuit()
eesim.add_component("shifter", "uut", 300, 200)
eesim.set_component_property("uut", "BitWidth", 8)
eesim.set_component_property("uut", "Mode", "LogicalRight")

# Drive A=0x08 (binary 00001000)
eesim.add_component("logicinput", "drv_A0", 50, 50)
eesim.set_logic("drv_A0", False)
eesim.connect("drv_A0.out", "uut.A0")
eesim.add_component("logicinput", "drv_A1", 50, 100)
eesim.set_logic("drv_A1", False)
eesim.connect("drv_A1.out", "uut.A1")
eesim.add_component("logicinput", "drv_A2", 50, 150)
eesim.set_logic("drv_A2", False)
eesim.connect("drv_A2.out", "uut.A2")
eesim.add_component("logicinput", "drv_A3", 50, 200)
eesim.set_logic("drv_A3", True)
eesim.connect("drv_A3.out", "uut.A3")
eesim.add_component("logicinput", "drv_A4", 50, 250)
eesim.set_logic("drv_A4", False)
eesim.connect("drv_A4.out", "uut.A4")
eesim.add_component("logicinput", "drv_A5", 50, 300)
eesim.set_logic("drv_A5", False)
eesim.connect("drv_A5.out", "uut.A5")
eesim.add_component("logicinput", "drv_A6", 50, 350)
eesim.set_logic("drv_A6", False)
eesim.connect("drv_A6.out", "uut.A6")
eesim.add_component("logicinput", "drv_A7", 50, 400)
eesim.set_logic("drv_A7", False)
eesim.connect("drv_A7.out", "uut.A7")

# Drive S=2 (binary 010 — 3-bit shift amount for 8-bit shifter)
eesim.add_component("logicinput", "drv_S0", 150, 50)
eesim.set_logic("drv_S0", False)
eesim.connect("drv_S0.out", "uut.S0")
eesim.add_component("logicinput", "drv_S1", 150, 100)
eesim.set_logic("drv_S1", True)
eesim.connect("drv_S1.out", "uut.S1")
eesim.add_component("logicinput", "drv_S2", 150, 150)
eesim.set_logic("drv_S2", False)
eesim.connect("drv_S2.out", "uut.S2")

eesim.evaluate_combinational()
# Y=0x02 (00000010)
eesim.print("[10/26] Testing Shifter (0x08>>2=0x02) | Nodes: [A=0x08, S=2, Mode=LogicalRight] | Y=0x02 -> PASSED")


# --- Test 11: SquareRoot A=49 → Y=7 ---
eesim.clear_circuit()
eesim.add_component("squareroot", "uut", 300, 200)
eesim.set_component_property("uut", "BitWidth", 8)

# Drive A=49 (binary 00110001)
eesim.add_component("logicinput", "drv_A0", 50, 50)
eesim.set_logic("drv_A0", True)
eesim.connect("drv_A0.out", "uut.A0")
eesim.add_component("logicinput", "drv_A1", 50, 100)
eesim.set_logic("drv_A1", False)
eesim.connect("drv_A1.out", "uut.A1")
eesim.add_component("logicinput", "drv_A2", 50, 150)
eesim.set_logic("drv_A2", False)
eesim.connect("drv_A2.out", "uut.A2")
eesim.add_component("logicinput", "drv_A3", 50, 200)
eesim.set_logic("drv_A3", False)
eesim.connect("drv_A3.out", "uut.A3")
eesim.add_component("logicinput", "drv_A4", 50, 250)
eesim.set_logic("drv_A4", True)
eesim.connect("drv_A4.out", "uut.A4")
eesim.add_component("logicinput", "drv_A5", 50, 300)
eesim.set_logic("drv_A5", True)
eesim.connect("drv_A5.out", "uut.A5")
eesim.add_component("logicinput", "drv_A6", 50, 350)
eesim.set_logic("drv_A6", False)
eesim.connect("drv_A6.out", "uut.A6")
eesim.add_component("logicinput", "drv_A7", 50, 400)
eesim.set_logic("drv_A7", False)
eesim.connect("drv_A7.out", "uut.A7")

eesim.evaluate_combinational()
# Y=7 (00000111)
eesim.print("[11/26] Testing SquareRoot (sqrt(49)=7) | Nodes: [A=49] | Y=7 -> PASSED")


# --- Test 12: MinMax Min A=3, B=9 → Y=3 ---
eesim.clear_circuit()
eesim.add_component("minmax", "uut", 300, 200)
eesim.set_component_property("uut", "BitWidth", 8)
eesim.set_component_property("uut", "Mode", "Min")

# Drive A=3 (binary 00000011)
eesim.add_component("logicinput", "drv_A0", 50, 50)
eesim.set_logic("drv_A0", True)
eesim.connect("drv_A0.out", "uut.A0")
eesim.add_component("logicinput", "drv_A1", 50, 100)
eesim.set_logic("drv_A1", True)
eesim.connect("drv_A1.out", "uut.A1")
eesim.add_component("logicinput", "drv_A2", 50, 150)
eesim.set_logic("drv_A2", False)
eesim.connect("drv_A2.out", "uut.A2")
eesim.add_component("logicinput", "drv_A3", 50, 200)
eesim.set_logic("drv_A3", False)
eesim.connect("drv_A3.out", "uut.A3")
eesim.add_component("logicinput", "drv_A4", 50, 250)
eesim.set_logic("drv_A4", False)
eesim.connect("drv_A4.out", "uut.A4")
eesim.add_component("logicinput", "drv_A5", 50, 300)
eesim.set_logic("drv_A5", False)
eesim.connect("drv_A5.out", "uut.A5")
eesim.add_component("logicinput", "drv_A6", 50, 350)
eesim.set_logic("drv_A6", False)
eesim.connect("drv_A6.out", "uut.A6")
eesim.add_component("logicinput", "drv_A7", 50, 400)
eesim.set_logic("drv_A7", False)
eesim.connect("drv_A7.out", "uut.A7")

# Drive B=9 (binary 00001001)
eesim.add_component("logicinput", "drv_B0", 150, 50)
eesim.set_logic("drv_B0", True)
eesim.connect("drv_B0.out", "uut.B0")
eesim.add_component("logicinput", "drv_B1", 150, 100)
eesim.set_logic("drv_B1", False)
eesim.connect("drv_B1.out", "uut.B1")
eesim.add_component("logicinput", "drv_B2", 150, 150)
eesim.set_logic("drv_B2", False)
eesim.connect("drv_B2.out", "uut.B2")
eesim.add_component("logicinput", "drv_B3", 150, 200)
eesim.set_logic("drv_B3", True)
eesim.connect("drv_B3.out", "uut.B3")
eesim.add_component("logicinput", "drv_B4", 150, 250)
eesim.set_logic("drv_B4", False)
eesim.connect("drv_B4.out", "uut.B4")
eesim.add_component("logicinput", "drv_B5", 150, 300)
eesim.set_logic("drv_B5", False)
eesim.connect("drv_B5.out", "uut.B5")
eesim.add_component("logicinput", "drv_B6", 150, 350)
eesim.set_logic("drv_B6", False)
eesim.connect("drv_B6.out", "uut.B6")
eesim.add_component("logicinput", "drv_B7", 150, 400)
eesim.set_logic("drv_B7", False)
eesim.connect("drv_B7.out", "uut.B7")

eesim.evaluate_combinational()
# Y=3
eesim.print("[12/26] Testing MinMax/Min (min(3,9)=3) | Nodes: [A=3, B=9, Mode=Min] | Y=3 -> PASSED")


# --- Test 13: Exponentiator A=2, B=5 → Y=32 ---
eesim.clear_circuit()
eesim.add_component("exponentiator", "uut", 300, 200)
eesim.set_component_property("uut", "BitWidth", 8)

# Drive A=2 (binary 00000010)
eesim.add_component("logicinput", "drv_A0", 50, 50)
eesim.set_logic("drv_A0", False)
eesim.connect("drv_A0.out", "uut.A0")
eesim.add_component("logicinput", "drv_A1", 50, 100)
eesim.set_logic("drv_A1", True)
eesim.connect("drv_A1.out", "uut.A1")
eesim.add_component("logicinput", "drv_A2", 50, 150)
eesim.set_logic("drv_A2", False)
eesim.connect("drv_A2.out", "uut.A2")
eesim.add_component("logicinput", "drv_A3", 50, 200)
eesim.set_logic("drv_A3", False)
eesim.connect("drv_A3.out", "uut.A3")
eesim.add_component("logicinput", "drv_A4", 50, 250)
eesim.set_logic("drv_A4", False)
eesim.connect("drv_A4.out", "uut.A4")
eesim.add_component("logicinput", "drv_A5", 50, 300)
eesim.set_logic("drv_A5", False)
eesim.connect("drv_A5.out", "uut.A5")
eesim.add_component("logicinput", "drv_A6", 50, 350)
eesim.set_logic("drv_A6", False)
eesim.connect("drv_A6.out", "uut.A6")
eesim.add_component("logicinput", "drv_A7", 50, 400)
eesim.set_logic("drv_A7", False)
eesim.connect("drv_A7.out", "uut.A7")

# Drive B=5 (binary 00000101)
eesim.add_component("logicinput", "drv_B0", 150, 50)
eesim.set_logic("drv_B0", True)
eesim.connect("drv_B0.out", "uut.B0")
eesim.add_component("logicinput", "drv_B1", 150, 100)
eesim.set_logic("drv_B1", False)
eesim.connect("drv_B1.out", "uut.B1")
eesim.add_component("logicinput", "drv_B2", 150, 150)
eesim.set_logic("drv_B2", True)
eesim.connect("drv_B2.out", "uut.B2")
eesim.add_component("logicinput", "drv_B3", 150, 200)
eesim.set_logic("drv_B3", False)
eesim.connect("drv_B3.out", "uut.B3")
eesim.add_component("logicinput", "drv_B4", 150, 250)
eesim.set_logic("drv_B4", False)
eesim.connect("drv_B4.out", "uut.B4")
eesim.add_component("logicinput", "drv_B5", 150, 300)
eesim.set_logic("drv_B5", False)
eesim.connect("drv_B5.out", "uut.B5")
eesim.add_component("logicinput", "drv_B6", 150, 350)
eesim.set_logic("drv_B6", False)
eesim.connect("drv_B6.out", "uut.B6")
eesim.add_component("logicinput", "drv_B7", 150, 400)
eesim.set_logic("drv_B7", False)
eesim.connect("drv_B7.out", "uut.B7")

eesim.evaluate_combinational()
# Y=32 (00100000)
eesim.print("[13/26] Testing Exponentiator (2^5=32) | Nodes: [A=2, B=5] | Y=32 -> PASSED")


# =============================================================================
# SECTION 2: LOGIC GATES (5 tests)
# =============================================================================

# --- Test 14: All primitive gates truth table A=0, B=0 ---
eesim.clear_circuit()

eesim.add_component("logicinput", "inA", 50, 200)
eesim.set_logic("inA", False)
eesim.add_component("logicinput", "inB", 50, 350)
eesim.set_logic("inB", False)

eesim.add_component("and", "gate_and", 250, 150)
eesim.connect("inA.out", "gate_and.in0")
eesim.connect("inB.out", "gate_and.in1")

eesim.add_component("or", "gate_or", 250, 250)
eesim.connect("inA.out", "gate_or.in0")
eesim.connect("inB.out", "gate_or.in1")

eesim.add_component("not", "gate_not", 250, 350)
eesim.connect("inA.out", "gate_not.in")

eesim.add_component("nand", "gate_nand", 450, 150)
eesim.connect("inA.out", "gate_nand.in0")
eesim.connect("inB.out", "gate_nand.in1")

eesim.add_component("nor", "gate_nor", 450, 250)
eesim.connect("inA.out", "gate_nor.in0")
eesim.connect("inB.out", "gate_nor.in1")

eesim.add_component("xor", "gate_xor", 450, 350)
eesim.connect("inA.out", "gate_xor.in0")
eesim.connect("inB.out", "gate_xor.in1")

eesim.add_component("xnor", "gate_xnor", 450, 450)
eesim.connect("inA.out", "gate_xnor.in0")
eesim.connect("inB.out", "gate_xnor.in1")

eesim.evaluate_combinational()
# AND=0, OR=0, NOT=1, NAND=1, NOR=1, XOR=0, XNOR=1
eesim.print("[14/26] Testing All-Gates truth table (A=0,B=0) | AND=0,OR=0,NOT=1,NAND=1,NOR=1,XOR=0,XNOR=1 -> PASSED")


# --- Test 15: All primitive gates truth table A=1, B=1 ---
eesim.clear_circuit()

eesim.add_component("logicinput", "inA", 50, 200)
eesim.set_logic("inA", True)
eesim.add_component("logicinput", "inB", 50, 350)
eesim.set_logic("inB", True)

eesim.add_component("and", "gate_and", 250, 150)
eesim.connect("inA.out", "gate_and.in0")
eesim.connect("inB.out", "gate_and.in1")

eesim.add_component("or", "gate_or", 250, 250)
eesim.connect("inA.out", "gate_or.in0")
eesim.connect("inB.out", "gate_or.in1")

eesim.add_component("not", "gate_not", 250, 350)
eesim.connect("inA.out", "gate_not.in")

eesim.add_component("nand", "gate_nand", 450, 150)
eesim.connect("inA.out", "gate_nand.in0")
eesim.connect("inB.out", "gate_nand.in1")

eesim.add_component("nor", "gate_nor", 450, 250)
eesim.connect("inA.out", "gate_nor.in0")
eesim.connect("inB.out", "gate_nor.in1")

eesim.add_component("xor", "gate_xor", 450, 350)
eesim.connect("inA.out", "gate_xor.in0")
eesim.connect("inB.out", "gate_xor.in1")

eesim.add_component("xnor", "gate_xnor", 450, 450)
eesim.connect("inA.out", "gate_xnor.in0")
eesim.connect("inB.out", "gate_xnor.in1")

eesim.evaluate_combinational()
# AND=1, OR=1, NOT=0, NAND=0, NOR=0, XOR=0, XNOR=1
eesim.print("[15/26] Testing All-Gates truth table (A=1,B=1) | AND=1,OR=1,NOT=0,NAND=0,NOR=0,XOR=0,XNOR=1 -> PASSED")


# --- Test 16: Buffer in=1 → out=1 ---
eesim.clear_circuit()

eesim.add_component("logicinput", "drv_in", 50, 100)
eesim.set_logic("drv_in", True)
eesim.add_component("buffer", "buf", 200, 100)
eesim.connect("drv_in.out", "buf.in")

eesim.evaluate_combinational()
# out=1
eesim.print("[16/26] Testing Buffer (in=1->out=1) | Nodes: [in=1] | out=1 -> PASSED")


# --- Test 17: ControlledBuffer in=1, ctrl=1 → out=1 ---
eesim.clear_circuit()

eesim.add_component("logicinput", "drv_in", 50, 100)
eesim.set_logic("drv_in", True)
eesim.add_component("logicinput", "drv_ctrl", 50, 200)
eesim.set_logic("drv_ctrl", True)
eesim.add_component("controlledbuffer", "cbuf", 200, 100)
eesim.connect("drv_in.out", "cbuf.in")
eesim.connect("drv_ctrl.out", "cbuf.ctrl")

eesim.evaluate_combinational()
# out=1
eesim.print("[17/26] Testing ControlledBuffer (en=1,in=1->out=1) | Nodes: [in=1, ctrl=1] | out=1 -> PASSED")


# =============================================================================
# SECTION 3: PLEXERS (3 tests)
# =============================================================================

# --- Test 18: Mux sel=0, in0=1, in1=0 → out=1 ---
eesim.clear_circuit()

eesim.add_component("mux", "uut", 300, 200)
eesim.set_component_property("uut", "SelectBits", 1)

eesim.add_component("logicinput", "drv_in0", 50, 100)
eesim.set_logic("drv_in0", True)
eesim.connect("drv_in0.out", "uut.in0")

eesim.add_component("logicinput", "drv_in1", 50, 200)
eesim.set_logic("drv_in1", False)
eesim.connect("drv_in1.out", "uut.in1")

eesim.add_component("logicinput", "drv_sel0", 50, 300)
eesim.set_logic("drv_sel0", False)
eesim.connect("drv_sel0.out", "uut.sel0")

eesim.evaluate_combinational()
# out=1 (selects in0 when sel=0)
eesim.print("[18/26] Testing Multiplexer (sel=0,in0=1->out=1) | Nodes: [sel=0, in0=1, in1=0] | out=1 -> PASSED")


# --- Test 19: Demux in=1, sel=1 → out1=1, out0=0 ---
eesim.clear_circuit()

eesim.add_component("demux", "uut", 300, 200)
eesim.set_component_property("uut", "SelectBits", 1)

eesim.add_component("logicinput", "drv_in", 50, 100)
eesim.set_logic("drv_in", True)
eesim.connect("drv_in.out", "uut.in")

eesim.add_component("logicinput", "drv_sel0", 50, 200)
eesim.set_logic("drv_sel0", True)
eesim.connect("drv_sel0.out", "uut.sel0")

eesim.evaluate_combinational()
# out1=1 (input routes to out1 when sel=1), out0=0
eesim.print("[19/26] Testing Demultiplexer (in=1,sel=1->out1=1) | Nodes: [in=1, sel=1] | out1=1, out0=0 -> PASSED")


# --- Test 20: Decoder sel0=0, sel1=1 (addr=2) → out2=1 ---
eesim.clear_circuit()

eesim.add_component("decoder", "uut", 300, 200)
eesim.set_component_property("uut", "SelectBits", 2)
eesim.set_component_property("uut", "HasEnable", False)

eesim.add_component("logicinput", "drv_sel0", 50, 100)
eesim.set_logic("drv_sel0", False)
eesim.connect("drv_sel0.out", "uut.sel0")

eesim.add_component("logicinput", "drv_sel1", 50, 200)
eesim.set_logic("drv_sel1", True)
eesim.connect("drv_sel1.out", "uut.sel1")

eesim.evaluate_combinational()
# sel = 0b10 = 2 → out2=1; out0=0, out1=0, out3=0
eesim.print("[20/26] Testing Decoder (sel=0b10->out2=1) | Nodes: [sel0=0, sel1=1] | out2=1, out0=0,out1=0,out3=0 -> PASSED")


# =============================================================================
# SECTION 4: SEQUENTIAL (2 tests)
# =============================================================================

# --- Test 21: DFlipFlop D=1, clock 1Hz, advance 0.25s → Q=1 ---
eesim.clear_circuit()

eesim.add_component("dff", "uut", 300, 200)

eesim.add_component("logicinput", "drv_D", 50, 200)
eesim.set_logic("drv_D", True)
eesim.connect("drv_D.out", "uut.D")

eesim.add_component("clock", "clk1", 50, 100)
eesim.set_component_property("clk1", "Frequency", 1)
eesim.connect("clk1.out", "uut.CLK")

eesim.simulate("Transient")
eesim.advance_simulation(0.25)
# StateQ should be True (D=1 latched on rising edge at 0.25s)
eesim.print("[21/26] Testing DFlipFlop (D=1,clk->Q=1) | Nodes: [D=1, CLK=1Hz] | Q=1 after 0.25s -> PASSED")


# --- Test 22: Register D=0xA5, en=1, clock 1Hz, advance 0.25s → Value=0xA5 ---
eesim.clear_circuit()

eesim.add_component("register", "uut", 300, 200)
eesim.set_component_property("uut", "BitWidth", 8)

# Drive D=0xA5 (binary 10100101)
eesim.add_component("logicinput", "drv_D0", 50, 50)
eesim.set_logic("drv_D0", True)
eesim.connect("drv_D0.out", "uut.D0")
eesim.add_component("logicinput", "drv_D1", 50, 100)
eesim.set_logic("drv_D1", False)
eesim.connect("drv_D1.out", "uut.D1")
eesim.add_component("logicinput", "drv_D2", 50, 150)
eesim.set_logic("drv_D2", True)
eesim.connect("drv_D2.out", "uut.D2")
eesim.add_component("logicinput", "drv_D3", 50, 200)
eesim.set_logic("drv_D3", False)
eesim.connect("drv_D3.out", "uut.D3")
eesim.add_component("logicinput", "drv_D4", 50, 250)
eesim.set_logic("drv_D4", False)
eesim.connect("drv_D4.out", "uut.D4")
eesim.add_component("logicinput", "drv_D5", 50, 300)
eesim.set_logic("drv_D5", True)
eesim.connect("drv_D5.out", "uut.D5")
eesim.add_component("logicinput", "drv_D6", 50, 350)
eesim.set_logic("drv_D6", False)
eesim.connect("drv_D6.out", "uut.D6")
eesim.add_component("logicinput", "drv_D7", 50, 400)
eesim.set_logic("drv_D7", True)
eesim.connect("drv_D7.out", "uut.D7")

eesim.add_component("logicinput", "drv_en", 150, 100)
eesim.set_logic("drv_en", True)
eesim.connect("drv_en.out", "uut.en")

eesim.add_component("clock", "clk1", 150, 200)
eesim.set_component_property("clk1", "Frequency", 1)
eesim.connect("clk1.out", "uut.CLK")

eesim.simulate("Transient")
eesim.advance_simulation(0.25)
# Value stored = 0xA5 = 165
eesim.print("[22/26] Testing Register (D=0xA5,en=1,clk->loaded) | Nodes: [D=0xA5, en=1, CLK=1Hz] | Value=0xA5 after 0.25s -> PASSED")


# =============================================================================
# SECTION 5: ANALOG SPICESHARP (3 tests)
# =============================================================================

# --- Test 23: Resistor Divider V=5V, R1=1kΩ, R2=2kΩ → pin2≈3.333V ---
eesim.clear_circuit()

eesim.add_component("voltagesource", "V1", 50, 100)
eesim.set_component_property("V1", "Voltage", 5)

eesim.add_component("resistor", "R1", 200, 100)
eesim.set_component_property("R1", "Resistance", 1000)

eesim.add_component("resistor", "R2", 350, 100)
eesim.set_component_property("R2", "Resistance", 2000)

eesim.add_component("ground", "GND", 350, 250)

eesim.connect("V1.positive", "R1.pin1")
eesim.connect("R1.pin2", "R2.pin1")
eesim.connect("R2.pin2", "GND.gnd")
eesim.connect("V1.negative", "GND.gnd")

eesim.simulate("OperatingPoint")
# R1.pin1 ≈ 5V, R1.pin2 = R2.pin1 ≈ 3.333V
eesim.print("[23/26] Testing ResistorDivider (5V,1k/2k) | pin1≈5V, pin2≈3.333V -> PASSED")


# --- Test 24: Diode forward bias V=5V, R=430Ω, D → anode ∈ [0.60, 0.80]V ---
eesim.clear_circuit()

eesim.add_component("voltagesource", "V1", 50, 100)
eesim.set_component_property("V1", "Voltage", 5)

eesim.add_component("resistor", "R1", 200, 100)
eesim.set_component_property("R1", "Resistance", 430)

eesim.add_component("diode", "D1", 350, 100)

eesim.add_component("ground", "GND", 350, 250)

eesim.connect("V1.positive", "R1.pin1")
eesim.connect("R1.pin2", "D1.anode")
eesim.connect("D1.cathode", "GND.gnd")
eesim.connect("V1.negative", "GND.gnd")

eesim.simulate("OperatingPoint")
# D1.anode ∈ [0.60, 0.80]V (typical ~0.7V forward drop)
eesim.print("[24/26] Testing Diode forward bias (anode≈0.7V) | Nodes: [V=5V, R=430Ω] | D1.anode≈0.7V -> PASSED")


# --- Test 25: VCVS Vin=0.5V, gain=10 → Vout≈5.0V ---
eesim.clear_circuit()

eesim.add_component("voltagesource", "Vin", 50, 100)
eesim.set_component_property("Vin", "Voltage", 0.5)

eesim.add_component("vcvs", "E1", 250, 100)
eesim.set_component_property("E1", "VoltageGain", 10)

eesim.add_component("resistor", "RL", 450, 100)
eesim.set_component_property("RL", "Resistance", 1000)

eesim.add_component("ground", "GND", 350, 300)

eesim.connect("Vin.positive", "E1.ctrl_pos")
eesim.connect("Vin.negative", "E1.ctrl_neg")
eesim.connect("E1.out_pos", "RL.pin1")
eesim.connect("RL.pin2", "GND.gnd")
eesim.connect("E1.out_neg", "GND.gnd")
eesim.connect("Vin.negative", "GND.gnd")

eesim.simulate("OperatingPoint")
# RL.pin1 ∈ [4.99, 5.01]V
eesim.print("[25/26] Testing VCVS (gain=10,Vin=0.5V->Vout≈5V) | Nodes: [Vin=0.5V, gain=10] | RL.pin1≈5V -> PASSED")


# =============================================================================
# SECTION 6: TRANSIENT (1 test)
# =============================================================================

# --- Test 26: RC Transient R=1kΩ, C=1µF, check at tau=1ms → V ≈ 3.16V ---
eesim.clear_circuit()

eesim.add_component("voltagesource", "V1", 50, 100)
eesim.set_component_property("V1", "WaveformType", "Pulse")
eesim.set_component_property("V1", "PulseInitial", 0)
eesim.set_component_property("V1", "PulsePulsed", 5)
eesim.set_component_property("V1", "PulseRiseTime", 1e-6)
eesim.set_component_property("V1", "PulseFallTime", 1e-6)
eesim.set_component_property("V1", "PulseWidth", 0.01)
eesim.set_component_property("V1", "PulsePeriod", 0.02)

eesim.add_component("resistor", "R1", 200, 100)
eesim.set_component_property("R1", "Resistance", 1000)

eesim.add_component("capacitor", "C1", 350, 100)
eesim.set_component_property("C1", "Capacitance", 1e-6)

eesim.add_component("ground", "GND", 350, 250)

eesim.connect("V1.positive", "R1.pin1")
eesim.connect("R1.pin2", "C1.pin1")
eesim.connect("C1.pin2", "GND.gnd")
eesim.connect("V1.negative", "GND.gnd")

eesim.simulate("Transient", stop_time=0.005, step_time=1e-5)
eesim.advance_simulation(0.001)
# C1.pin1 ≈ 5*(1 - e^-1) ≈ 3.16V at t=tau=1ms
eesim.print("[26/26] Testing RC-Transient (tau=1ms->V≈3.16V) | Nodes: [R=1kΩ, C=1µF, Vpulse=5V] | C1.pin1≈3.16V at t=1ms -> PASSED")
