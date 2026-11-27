# =============================================================================
# circuit.py - Circuit helper module for EESimulator Python scripting
# =============================================================================

import eesim

connect = eesim.connect
clear = eesim.clear_circuit
clear_circuit = eesim.clear_circuit
add_component = eesim.add_component
add_resistor = eesim.add_resistor
add_capacitor = eesim.add_capacitor
add_inductor = eesim.add_inductor
add_ground = eesim.add_ground
add_node = eesim.add_node
add_wire = eesim.add_wire
add_voltage_source = eesim.add_voltage_source
add_microphone = eesim.add_microphone
set_microphone_audio = eesim.set_microphone_audio
set_microphone_bandwidth = eesim.set_microphone_bandwidth
set_component_property = eesim.set_component_property
set_logic = eesim.set_logic
press_button = eesim.press_button
set_dip_switch = eesim.set_dip_switch
set_joystick = eesim.set_joystick
send_keyboard_key = eesim.send_keyboard_key
schedule_input = eesim.schedule_input
clear_scheduled_inputs = eesim.clear_scheduled_inputs
set_logical_grid_spacing = eesim.set_logical_grid_spacing
simulate = eesim.simulate
play_simulation = eesim.play_simulation
evaluate_combinational = eesim.evaluate_combinational
advance_simulation = eesim.advance_simulation
read_pin_voltage = eesim.read_pin_voltage
read_digital_output_state = eesim.read_digital_output_state
