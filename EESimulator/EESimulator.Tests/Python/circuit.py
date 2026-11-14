# =============================================================================
# circuit.py - Circuit helper module for EESimulator Python scripting
# =============================================================================

import eesim

connect = eesim.connect
clear = eesim.clear_circuit
clear_circuit = eesim.clear_circuit
add_component = eesim.add_component
set_component_property = eesim.set_component_property
simulate = eesim.simulate
evaluate_combinational = eesim.evaluate_combinational
advance_simulation = eesim.advance_simulation
read_pin_voltage = eesim.read_pin_voltage
read_digital_output_state = eesim.read_digital_output_state

