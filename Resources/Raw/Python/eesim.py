"""Python facade that forwards scripting calls to EESimulator's .NET executor."""

import builtins
import json
import math
import socket

_protocol_output = None
_protocol_input = None
_protocol_socket = None
_protocol_token = None
_socket_output = None
_socket_input = None


def attach(protocol_output, protocol_input):
    global _protocol_output, _protocol_input
    _protocol_output = protocol_output
    _protocol_input = protocol_input


def attach_socket(port, token):
    global _protocol_socket, _protocol_token, _socket_output, _socket_input
    _protocol_socket = socket.create_connection(("127.0.0.1", port))
    _protocol_token = token
    _socket_input = _protocol_socket.makefile("r", encoding="utf-8")
    _socket_output = _protocol_socket.makefile("w", encoding="utf-8")


def detach_socket():
    global _protocol_socket, _protocol_token, _socket_output, _socket_input
    if _socket_output is not None:
        _socket_output.close()
    if _socket_input is not None:
        _socket_input.close()
    if _protocol_socket is not None:
        _protocol_socket.close()
    _protocol_socket = None
    _protocol_token = None
    _socket_output = None
    _socket_input = None


def _call(script):
    bridge = getattr(builtins, "app_bridge", None)
    if bridge is not None:
        response = bridge.Execute(script)
        if not response["success"]:
            raise RuntimeError(response["message"])
        return response["result"]

    if _socket_output is not None and _socket_input is not None:
        _socket_output.write(json.dumps(
            {"token": _protocol_token, "script": script}, ensure_ascii=False) + "\n")
        _socket_output.flush()
        response_line = _socket_input.readline()
        if not response_line:
            raise RuntimeError("The EESimulator scripting host stopped unexpectedly.")
        response = json.loads(response_line)
        if not response["success"]:
            raise RuntimeError(response["message"])
        return response["result"]

    if _protocol_output is None or _protocol_input is None:
        raise RuntimeError("Python tests must run through the EESimulator scripting test bridge.")

    _protocol_output.write(json.dumps({"script": script}, ensure_ascii=False) + "\n")
    _protocol_output.flush()
    response_line = _protocol_input.readline()
    if not response_line:
        raise RuntimeError("The EESimulator scripting host stopped unexpectedly.")

    response = json.loads(response_line)
    if not response["success"]:
        raise RuntimeError(response["message"])
    return response["result"]


def _quoted(value):
    return json.dumps(str(value), ensure_ascii=False)


def _number(value):
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise TypeError(f"Expected a numeric scripting argument, got {type(value).__name__}.")
    return repr(value)


class ComponentRef:
    def __init__(self, name):
        self._name = name

    def __getattr__(self, pin):
        return f"{self._name}.{pin}"

    def __str__(self):
        return self._name


class CircuitRef:
    def add_component(self, comp_type, name=None, **kwargs):
        if name is None:
            raise TypeError("add_component requires a component name.")

        extra_kw = []
        for k, v in kwargs.items():
            if k in ("row", "col"): continue
            if isinstance(v, bool):
                extra_kw.append(f"{k}={'true' if v else 'false'}")
            elif isinstance(v, (int, float)):
                extra_kw.append(f"{k}={_number(v)}")
            elif isinstance(v, str):
                extra_kw.append(f"{k}={_quoted(v)}")
        extra_suffix = (", " + ", ".join(extra_kw)) if extra_kw else ""

        if "row" in kwargs or "col" in kwargs:
            if "row" not in kwargs or "col" not in kwargs:
                raise TypeError("Logical placement requires both row and col.")
            _call(
                f"circuit.add_component({_quoted(comp_type)}, name={_quoted(name)}, "
                f"row={_number(kwargs['row'])}, col={_number(kwargs['col'])}{extra_suffix})"
            )
        elif "position" in kwargs:
            position = kwargs["position"]
            if len(position) != 2:
                raise TypeError("position must contain x and y coordinates.")
            _call(
                f"circuit.add_component({_quoted(comp_type)}, name={_quoted(name)}, "
                f"position_x={_number(position[0])}, position_y={_number(position[1])}{extra_suffix}"
            )
        else:
            raise TypeError("add_component requires either position or row and col.")
        return ComponentRef(str(name))

    def connect(self, *args, **kwargs):
        return connect(*args, **kwargs)

    def add_resistor(self, name, resistance=1000.0, **kwargs):
        return add_resistor(name, resistance=resistance, **kwargs)

    def add_capacitor(self, name, capacitance=1e-6, **kwargs):
        return add_capacitor(name, capacitance=capacitance, **kwargs)

    def add_inductor(self, name, inductance=1e-3, **kwargs):
        return add_inductor(name, inductance=inductance, **kwargs)

    def add_ground(self, name="GND", **kwargs):
        return add_ground(name=name, **kwargs)

    def add_node(self, name, row=None, col=None, **kwargs):
        if row is not None: kwargs["row"] = row
        if col is not None: kwargs["col"] = col
        return add_node(name=name, **kwargs)

    def add_wire(self, name, direction="horizontal", **kwargs):
        return add_wire(name=name, direction=direction, **kwargs)

    def add_voltage_source(self, name, dc=5.0, **kwargs):
        return add_voltage_source(name=name, dc=dc, **kwargs)

    def add_microphone(self, name, audio_file=None, bandwidth=20000.0, amplitude=1.0, **kwargs):
        return add_microphone(name=name, audio_file=audio_file, bandwidth=bandwidth, amplitude=amplitude, **kwargs)

    def set_component_property(self, name, prop, value):
        return set_component_property(name, prop, value)

    def set_logic(self, name, value):
        return set_logic(name, value)

    def toggle_logic(self, name):
        return toggle_logic(name)

    def set_logic_levels(self, name, high_voltage, low_voltage):
        return set_logic_levels(name, high_voltage, low_voltage)

    def set_logic_bus(self, names, value, lsb_first=False):
        return set_logic_bus(names, value, lsb_first)

    def set_binary_inputs(self, names, value, lsb_first=False):
        return set_binary_inputs(names, value, lsb_first)

    def press_button(self, name, pressed=True):
        return press_button(name, pressed)

    def release_button(self, name):
        return release_button(name)

    def pulse_button(self, name, duration=0.1):
        return pulse_button(name, duration)

    def set_dip_switch(self, name, value):
        return set_dip_switch(name, value)

    def set_dip_switch_pole(self, name, pole_index, state):
        return set_dip_switch_pole(name, pole_index, state)

    def set_joystick(self, name, x, y, pressed=False):
        return set_joystick(name, x, y, pressed)

    def set_joystick_position(self, name, x, y):
        return set_joystick_position(name, x, y)

    def press_joystick(self, name, pressed=True):
        return press_joystick(name, pressed)

    def send_keyboard_key(self, name, character_code):
        return send_keyboard_key(name, character_code)

    def keyboard_press(self, name, key):
        return keyboard_press(name, key)

    def keyboard_type_text(self, name, text, delay=0.1):
        return keyboard_type_text(name, text, delay)

    def set_constant(self, name, value, bit_width=None, high_voltage=None, low_voltage=None):
        return set_constant(name, value, bit_width, high_voltage, low_voltage)

    def set_voltage_switch(self, name, threshold=None, hysteresis=None, ron=None, roff=None):
        return set_voltage_switch(name, threshold, hysteresis, ron, roff)

    def set_current_switch(self, name, threshold=None, hysteresis=None, ron=None, roff=None, controlling_source=None):
        return set_current_switch(name, threshold, hysteresis, ron, roff, controlling_source)

    def set_relay(self, name, pull_in=None, dropout=None, coil_r=None, contact_r=None, open_r=None):
        return set_relay(name, pull_in, dropout, coil_r, contact_r, open_r)

    def set_microphone_audio(self, name, file_path, amplitude=None, bandwidth=None, offset=None):
        return set_microphone_audio(name, file_path, amplitude, bandwidth, offset)

    def set_dc_signal(self, name, voltage):
        return set_dc_signal(name, voltage)

    def set_sine_signal(self, name, amplitude, frequency, offset=0.0, phase=0.0, delay=0.0, theta=0.0):
        return set_sine_signal(name, amplitude, frequency, offset, phase, delay, theta)

    def set_pulse_signal(self, name, v_low, v_high, period, width, delay=0.0, rise_time=1e-6, fall_time=1e-6):
        return set_pulse_signal(name, v_low, v_high, period, width, delay, rise_time, fall_time)

    def set_step_signal(self, name, v_initial, v_step, delay=0.0):
        return set_step_signal(name, v_initial, v_step, delay)

    def set_ramp_signal(self, name, v_start, v_peak, duration, delay=0.0, period=None):
        return set_ramp_signal(name, v_start, v_peak, duration, delay, period)

    def set_triangle_signal(self, name, v_low, v_high, period, delay=0.0):
        return set_triangle_signal(name, v_low, v_high, period, delay)

    def set_pwl_signal(self, name, points):
        return set_pwl_signal(name, points)

    def set_ac_source(self, name, voltage, frequency, waveform="Sine", offset=0.0, phase=0.0):
        return set_ac_source(name, voltage, frequency, waveform, offset, phase)

    def set_clock_signal(self, name, frequency, duty_cycle=0.5, high_voltage=5.0, low_voltage=0.0):
        return set_clock_signal(name, frequency, duty_cycle, high_voltage, low_voltage)

    def schedule_input(self, delay_seconds, name, prop, value):
        return schedule_input(delay_seconds, name, prop, value)

    def clear_scheduled_inputs(self):
        return clear_scheduled_inputs()

    def play_simulation(self): return play_simulation()
    def pause_simulation(self): return pause_simulation()
    def stop_simulation(self): return stop_simulation()
    def toggle_simulation(self): return toggle_simulation()
    def step_simulation(self): return step_simulation()
    def close_sidebars(self): return close_sidebars()
    def open_sidebars(self): return open_sidebars()
    def recenter_viewport(self): return recenter_viewport()
    def fit_to_screen(self): return fit_to_screen()
    def apply_layout(self): return apply_layout()
    def auto_layout(self): return auto_layout()
    def open_graph_drawer(self): return open_graph_drawer()
    def close_graph_drawer(self): return close_graph_drawer()
    def toggle_graph_drawer(self): return toggle_graph_drawer()
    def start_video_recording(self, *args, **kwargs): return start_video_recording(*args, **kwargs)
    def stop_video_recording(self): return stop_video_recording()
    def start_gif_recording(self, *args, **kwargs): return start_gif_recording(*args, **kwargs)
    def stop_gif_recording(self): return stop_gif_recording()
    def set_subtitle(self, text): return set_subtitle(text)
    def edit_subtitle(self, text): return edit_subtitle(text)
    def clear_subtitle(self): return clear_subtitle()
    def get_subtitle(self): return get_subtitle()
    def wait(self, seconds): return wait(seconds)
    def sleep(self, seconds): return sleep(seconds)
    def set_workspace_setting(self, name, value): return set_workspace_setting(name, value)
    def get_workspace_setting(self, name): return get_workspace_setting(name)
    def reset_workspace_settings(self): return reset_workspace_settings()
    def set_solver_step(self, step): return set_solver_step(step)
    def set_simulation_duration(self, duration): return set_simulation_duration(duration)
    def set_simulation_start_time(self, start): return set_simulation_start_time(start)


def clear_circuit():
    _call("eesim.clear_circuit()")


def new_circuit(name="Untitled Circuit"):
    _call(f"eesim.new_circuit({_quoted(name)})")
    return CircuitRef()


def add_component(comp_type, name, x=0, y=0, param=0, **kwargs):
    if kwargs:
        return CircuitRef().add_component(comp_type, name=name, **kwargs)
    _call(f"eesim.add_component({_quoted(comp_type)}, {_quoted(name)}, {_number(x)}, {_number(y)}, {_number(param)})")
    return ComponentRef(str(name))


def set_component_property(name, prop, value):
    _call(f"eesim.set_component_property({_quoted(name)}, {_quoted(prop)}, {json.dumps(value, ensure_ascii=False)})")


def set_workspace_setting(name, value):
    """Set a saved Workspace setting.

    Names include auto_run_simulation, default_palette_category,
    viewport_refresh_hz, graph_refresh_hz, interaction_loop_hz,
    ui_snapshot_delay_ms, voltage_delta_threshold,
    current_delta_threshold, reactive_oscillation_threshold,
    hcvp_api_base_url, enable_time_scaling, simulation_time_amount,
    simulation_time_unit, device_time_amount, device_time_unit,
    solver_step, automatic_time_step_selection, start_time, and duration.

    For a 1 kHz transient, for example:
        set_workspace_setting("solver_step", 0.00001)
        set_workspace_setting("duration", 0.01)
    """
    if not isinstance(name, str) or not name.strip():
        raise TypeError("set_workspace_setting expects a non-empty setting name.")
    if not isinstance(value, (bool, int, float, str)):
        raise TypeError("Workspace setting values must be booleans, numbers, or strings.")
    _call(
        f"eesim.set_workspace_setting({_quoted(name)}, "
        f"{json.dumps(value, ensure_ascii=False, allow_nan=False)})"
    )


def set_logical_grid_spacing(row_spacing, column_spacing):
    _call(f"eesim.set_logical_grid_spacing({_number(row_spacing)}, {_number(column_spacing)})")


def set_custom_property(name, prop, value):
    _call(f"eesim.set_custom_property({_quoted(name)}, {_quoted(prop)}, {json.dumps(value, ensure_ascii=False)})")


def set_logic(name, value):
    if not isinstance(value, bool):
        raise TypeError("set_logic expects a bool.")
    _call(f"eesim.set_logic({_quoted(name)}, {str(value).lower()})")


def toggle_logic(name):
    """Toggle digital logic level between 0 and 1."""
    _call(f"eesim.toggle_logic({_quoted(name)})")


def set_logic_levels(name, high_voltage, low_voltage):
    """Set custom voltage thresholds for Logic 1 and Logic 0."""
    _call(f"eesim.set_logic_levels({_quoted(name)}, {_number(high_voltage)}, {_number(low_voltage)})")


def set_logic_bus(names, value, lsb_first=False):
    """Set an integer value across multiple digital logic inputs (e.g. for adders, buses).
    
    Example:
        set_logic_bus(['A3', 'A2', 'A1', 'A0'], 2)  # A=2 (0010)
        set_logic_bus(['A3', 'A2', 'A1', 'A0'], 5)  # A=5 (0101)
    """
    val = int(value)
    name_list = list(names) if lsb_first else list(reversed(names))
    for bit, bit_name in enumerate(name_list):
        set_logic(bit_name, bool((val >> bit) & 1))


def set_binary_inputs(names, value, lsb_first=False):
    """Alias for set_logic_bus."""
    set_logic_bus(names, value, lsb_first)


def press_button(name, pressed=True):
    if not isinstance(pressed, bool):
        raise TypeError("press_button expects a bool for pressed.")
    _call(f"eesim.press_button({_quoted(name)}, {str(pressed).lower()})")


def release_button(name):
    """Release a momentary push button."""
    press_button(name, False)


def pulse_button(name, duration=0.1):
    """Momentarily press a push button and release after duration seconds."""
    press_button(name, True)
    wait(duration)
    press_button(name, False)


def set_dip_switch(name, value):
    if isinstance(value, bool) or not isinstance(value, int):
        raise TypeError("set_dip_switch expects an integer bitmask.")
    _call(f"eesim.set_dip_switch({_quoted(name)}, {_number(value)})")


def set_dip_switch_pole(name, pole_index, state):
    """Set a specific pole (bit index 0-based) on a DIP switch bank."""
    _call(f"eesim.set_dip_switch_pole({_quoted(name)}, {_number(pole_index)}, {str(bool(state)).lower()})")


def set_joystick(name, x, y, pressed=False):
    if any(isinstance(value, bool) or not isinstance(value, int) for value in (x, y)):
        raise TypeError("set_joystick expects integer x and y values.")
    if not isinstance(pressed, bool):
        raise TypeError("set_joystick expects a bool for pressed.")
    _call(
        f"eesim.set_joystick({_quoted(name)}, {_number(x)}, {_number(y)}, "
        f"{str(pressed).lower()})"
    )


def set_joystick_position(name, x, y):
    """Set 2-axis joystick X and Y coordinates (0 to 255, center 128)."""
    set_joystick(name, x, y, pressed=False)


def press_joystick(name, pressed=True):
    """Press or release joystick center push button."""
    _call(f"eesim.set_component_property({_quoted(name)}, 'Pressed', {str(bool(pressed)).lower()})")


def send_keyboard_key(name, character_code):
    if isinstance(character_code, bool) or not isinstance(character_code, int):
        raise TypeError("send_keyboard_key expects an integer character code.")
    _call(f"eesim.send_keyboard_key({_quoted(name)}, {_number(character_code)})")


def keyboard_press(name, key):
    """Send an ASCII character (e.g. 'A') or integer code (e.g. 65) to keyboard buffer."""
    code = ord(key) if isinstance(key, str) and len(key) == 1 else int(key)
    send_keyboard_key(name, code)


def keyboard_type_text(name, text, delay=0.1):
    """Type a string of characters into the ASCII keyboard with inter-character delay."""
    for ch in str(text):
        keyboard_press(name, ch)
        if delay > 0:
            wait(delay)


def set_constant(name, value, bit_width=None, high_voltage=None, low_voltage=None):
    """Configure a wired Logic Constant source."""
    named = {}
    if bit_width is not None: named["bit_width"] = int(bit_width)
    if high_voltage is not None: named["high_voltage"] = float(high_voltage)
    if low_voltage is not None: named["low_voltage"] = float(low_voltage)
    extra = ", " + ", ".join(f"{k}={v}" for k, v in named.items()) if named else ""
    _call(f"eesim.set_constant({_quoted(name)}, {_number(value)}{extra})")


def set_voltage_switch(name, threshold=None, hysteresis=None, ron=None, roff=None):
    """Configure a Voltage-Controlled Switch (S)."""
    named = {}
    if threshold is not None: named["threshold"] = float(threshold)
    if hysteresis is not None: named["hysteresis"] = float(hysteresis)
    if ron is not None: named["ron"] = float(ron)
    if roff is not None: named["roff"] = float(roff)
    extra = ", " + ", ".join(f"{k}={v}" for k, v in named.items()) if named else ""
    _call(f"eesim.set_voltage_switch({_quoted(name)}{extra})")


def set_current_switch(name, threshold=None, hysteresis=None, ron=None, roff=None, controlling_source=None):
    """Configure a Current-Controlled Switch (W)."""
    named = {}
    if threshold is not None: named["threshold"] = float(threshold)
    if hysteresis is not None: named["hysteresis"] = float(hysteresis)
    if ron is not None: named["ron"] = float(ron)
    if roff is not None: named["roff"] = float(roff)
    if controlling_source is not None: named["controlling_source"] = _quoted(controlling_source)
    extra = ", " + ", ".join(f"{k}={v}" for k, v in named.items()) if named else ""
    _call(f"eesim.set_current_switch({_quoted(name)}{extra})")


def set_relay(name, pull_in=None, dropout=None, coil_r=None, contact_r=None, open_r=None):
    """Configure an Electromechanical Relay."""
    named = {}
    if pull_in is not None: named["pull_in_voltage"] = float(pull_in)
    if dropout is not None: named["dropout_voltage"] = float(dropout)
    if coil_r is not None: named["coil_resistance"] = float(coil_r)
    if contact_r is not None: named["contact_resistance"] = float(contact_r)
    if open_r is not None: named["open_resistance"] = float(open_r)
    extra = ", " + ", ".join(f"{k}={v}" for k, v in named.items()) if named else ""
    _call(f"eesim.set_relay({_quoted(name)}{extra})")


def set_microphone_audio(name, file_path, amplitude=None, bandwidth=None, offset=None):
    """Load an MP3 or WAV audio track into a Microphone input transducer."""
    named = {}
    if amplitude is not None: named["amplitude"] = float(amplitude)
    if bandwidth is not None: named["bandwidth"] = float(bandwidth)
    if offset is not None: named["offset"] = float(offset)
    extra = ", " + ", ".join(f"{k}={v}" for k, v in named.items()) if named else ""
    _call(f"eesim.set_microphone_audio({_quoted(name)}, {_quoted(file_path)}{extra})")


# ============================================================
# Dynamic Signal Generation APIs (VoltageSource & ACSource / Clock)
# ============================================================
def set_dc_signal(name, voltage):
    """Set voltage source to steady DC voltage."""
    _call(f"eesim.set_dc_signal({_quoted(name)}, {_number(voltage)})")


def set_sine_signal(name, amplitude, frequency, offset=0.0, phase=0.0, delay=0.0, theta=0.0):
    """Set voltage source to Sinusoidal AC wave."""
    _call(
        f"eesim.set_sine_signal({_quoted(name)}, {_number(amplitude)}, {_number(frequency)}, "
        f"offset={_number(offset)}, phase={_number(phase)}, delay={_number(delay)}, theta={_number(theta)})"
    )


def set_pulse_signal(name, v_low, v_high, period, width, delay=0.0, rise_time=1e-6, fall_time=1e-6):
    """Set voltage source to periodic Pulse / Square / Clock train."""
    _call(
        f"eesim.set_pulse_signal({_quoted(name)}, {_number(v_low)}, {_number(v_high)}, "
        f"{_number(period)}, {_number(width)}, delay={_number(delay)}, "
        f"rise_time={_number(rise_time)}, fall_time={_number(fall_time)})"
    )


def set_step_signal(name, v_initial, v_step, delay=0.0):
    """Generate a Unit Step voltage transition at delay seconds."""
    _call(f"eesim.set_step_signal({_quoted(name)}, {_number(v_initial)}, {_number(v_step)}, {_number(delay)})")


def set_ramp_signal(name, v_start, v_peak, duration, delay=0.0, period=None):
    """Generate a continuous linear Ramp / Sawtooth signal rising over duration seconds."""
    per_arg = f", {_number(period)}" if period is not None else ""
    _call(f"eesim.set_ramp_signal({_quoted(name)}, {_number(v_start)}, {_number(v_peak)}, {_number(duration)}, {_number(delay)}{per_arg})")


def set_triangle_signal(name, v_low, v_high, period, delay=0.0):
    """Generate a symmetric Triangular waveform."""
    _call(f"eesim.set_triangle_signal({_quoted(name)}, {_number(v_low)}, {_number(v_high)}, {_number(period)}, {_number(delay)})")


def set_pwl_signal(name, points):
    """Set an arbitrary Piecewise Linear (PWL) waveform.
    
    points can be a string formatted as 't1,v1; t2,v2' or a list of (time, voltage) tuples:
    Example: [(0, 0), (0.001, 5), (0.002, 0), (0.003, 5)]
    """
    if isinstance(points, (list, tuple)):
        pwl_str = "; ".join(f"{pt[0]},{pt[1]}" for pt in points)
    else:
        pwl_str = str(points)
    _call(f"eesim.set_pwl_signal({_quoted(name)}, {_quoted(pwl_str)})")


def set_ac_source(name, voltage, frequency, waveform="Sine", offset=0.0, phase=0.0):
    """Configure Dedicated AC Voltage Source."""
    _call(
        f"eesim.set_ac_source({_quoted(name)}, {_number(voltage)}, {_number(frequency)}, "
        f"waveform={_quoted(waveform)}, offset={_number(offset)}, phase={_number(phase)})"
    )


def set_clock_signal(name, frequency, duty_cycle=0.5, high_voltage=5.0, low_voltage=0.0):
    """Configure digital Clock Generator pulses."""
    _call(
        f"eesim.set_clock_signal({_quoted(name)}, {_number(frequency)}, "
        f"duty_cycle={_number(duty_cycle)}, high_voltage={_number(high_voltage)}, low_voltage={_number(low_voltage)})"
    )


def schedule_input(delay_seconds, name, prop, value):
    """Set an input property after delay_seconds of simulation playback.

    Schedule calls made before playback starts are relative to its configured start
    time; calls made during playback are relative to the current simulation time.

    Example:
        schedule_input(2, "A", "Value", 4)
        schedule_input(9, "A", "Value", 7)
        play_simulation()
    """
    if isinstance(delay_seconds, bool) or not isinstance(delay_seconds, (int, float)):
        raise TypeError("schedule_input expects a numeric delay in seconds.")
    if not math.isfinite(delay_seconds) or delay_seconds < 0:
        raise ValueError("schedule_input delay_seconds must be finite and non-negative.")
    if not isinstance(name, str) or not name.strip():
        raise TypeError("schedule_input expects a component name.")
    if not isinstance(prop, str) or not prop.strip():
        raise TypeError("schedule_input expects a property name.")
    if isinstance(value, (dict, list, tuple)) or value is None:
        raise TypeError("Scheduled input values must be booleans, numbers, or strings.")
    try:
        serialized_value = json.dumps(value, ensure_ascii=False, allow_nan=False)
    except (TypeError, ValueError) as exc:
        raise TypeError("Scheduled input values must be booleans, finite numbers, or strings.") from exc
    _call(
        f"eesim.schedule_input({_number(delay_seconds)}, {_quoted(name)}, "
        f"{_quoted(prop)}, {serialized_value})"
    )


def clear_scheduled_inputs():
    """Cancel all input changes that have not yet been applied."""
    _call("eesim.clear_scheduled_inputs()")


def set_memory_word(name, address, value):
    _call(f"eesim.set_memory_word({_quoted(name)}, {_number(address)}, {_number(value)})")


def connect(*args, **kwargs):
    """Connect two component pins.

    Two-arg form:  connect("VCC.positive", "R1.pin1")
    Four-arg form: connect("VCC", "positive", "R1", "pin1")
    Both accept:   waypoints=[(row, col), ...], resistance=float
    """
    if len(args) == 2:
        # Two-arg dot notation: "Comp.pin" strings
        from_str = str(args[0]).strip('"').strip("'")
        to_str = str(args[1]).strip('"').strip("'")
        from_parts = from_str.split(".", 1)
        to_parts = to_str.split(".", 1)
        from_comp = from_parts[0]
        from_pin = from_parts[1] if len(from_parts) > 1 else "pin1"
        to_comp = to_parts[0]
        to_pin = to_parts[1] if len(to_parts) > 1 else "pin1"
        resistance = kwargs.get("resistance")
    elif len(args) == 3 and isinstance(args[2], (int, float)):
        from_str = str(args[0]).strip('"').strip("'")
        to_str = str(args[1]).strip('"').strip("'")
        from_parts = from_str.split(".", 1)
        to_parts = to_str.split(".", 1)
        from_comp = from_parts[0]
        from_pin = from_parts[1] if len(from_parts) > 1 else "pin1"
        to_comp = to_parts[0]
        to_pin = to_parts[1] if len(to_parts) > 1 else "pin1"
        resistance = args[2]
    elif len(args) in (4, 5):
        from_comp, from_pin, to_comp, to_pin = str(args[0]), str(args[1]), str(args[2]), str(args[3])
        resistance = args[4] if len(args) == 5 else kwargs.get("resistance")
    else:
        raise TypeError("connect expects two endpoint strings or four component/pin args.")

    suffix = f", {_number(resistance)}" if resistance is not None else ""

    extra_parts = []
    if "waypoints" in kwargs and kwargs["waypoints"] is not None:
        wp_list = kwargs["waypoints"]
        wp_str = "[" + ", ".join(f"({_number(pt[0])}, {_number(pt[1])})" for pt in wp_list) + "]"
        extra_parts.append(f"waypoints={wp_str}")
    if "exit_direction" in kwargs and kwargs["exit_direction"] is not None:
        extra_parts.append(f"exit_direction={_quoted(kwargs['exit_direction'])}")

    extra_suffix = (", " + ", ".join(extra_parts)) if extra_parts else ""
    # Always send four separate quoted args so the parser never confuses
    # component names that happen to contain dots.
    _call(f"eesim.connect({_quoted(from_comp)}, {_quoted(from_pin)}, {_quoted(to_comp)}, {_quoted(to_pin)}{suffix}{extra_suffix})")


def add_resistor(name, resistance=1000.0, **kwargs):
    kwargs["param"] = resistance
    kwargs["resistance"] = resistance
    return add_component("resistor", name, **kwargs)


def add_capacitor(name, capacitance=1e-6, **kwargs):
    kwargs["param"] = capacitance
    kwargs["capacitance"] = capacitance
    return add_component("capacitor", name, **kwargs)


def add_inductor(name, inductance=1e-3, **kwargs):
    kwargs["param"] = inductance
    kwargs["inductance"] = inductance
    return add_component("inductor", name, **kwargs)


def add_ground(name="GND", **kwargs):
    return add_component("ground", name, **kwargs)


def add_node(name, row=None, col=None, **kwargs):
    if row is not None: kwargs["row"] = row
    if col is not None: kwargs["col"] = col
    return add_component("general.node", name, **kwargs)


def add_wire(name, direction="horizontal", length=40.0, **kwargs):
    comp = add_component("general.wire", name, **kwargs)
    set_component_property(name, "Direction", direction)
    return comp


def add_voltage_source(name, dc=5.0, **kwargs):
    kwargs["param"] = dc
    return add_component("voltagesource", name, **kwargs)


def add_microphone(name, audio_file=None, bandwidth=20000.0, amplitude=1.0, **kwargs):
    comp = add_component("Microphone", name, **kwargs)
    set_component_property(name, "SignalBandwidth", bandwidth)
    set_component_property(name, "Amplitude", amplitude)
    if audio_file:
        set_custom_property(name, "AudioFilePath", audio_file)
    return comp


def set_microphone_audio(name, audio_file):
    set_custom_property(name, "AudioFilePath", audio_file)


def set_microphone_bandwidth(name, bandwidth):
    set_component_property(name, "SignalBandwidth", bandwidth)


def remove_connection(from_component, from_pin, to_component, to_pin):
    _call(
        f"eesim.remove_connection({_quoted(from_component)}, {_quoted(from_pin)}, "
        f"{_quoted(to_component)}, {_quoted(to_pin)})"
    )


def evaluate_combinational():
    _call("eesim.evaluate_combinational()")


def advance_simulation(elapsed_seconds):
    _call(f"eesim.advance_simulation({_number(elapsed_seconds)})")


def simulate(analysis="OperatingPoint", stop_time=None, step_time=None):
    arguments = [_quoted(analysis)]
    if stop_time is not None:
        arguments.append(f"stop_time={_number(stop_time)}")
    if step_time is not None:
        arguments.append(f"step_time={_number(step_time)}")
    _call(f"eesim.simulate({', '.join(arguments)})")


def save_circuit_image(path=None):
    argument = "" if path is None else _quoted(path)
    _call(f"eesim.save_circuit_image({argument})")


def start_video_recording(path=None, frame_rate=10, format="mp4", view="recentre", duration=None):
    """Start video recording of the schematic viewport.

    Args:
        path: Optional output file path (e.g. "circuit_sim.mp4").
        frame_rate: Recording frame rate (1-15 fps, default 10).
        format: "mp4" or "gif" (default "mp4").
        view: "recentre" to auto-fit circuit or "screen" for exact current camera.
        duration: Optional max recording duration in seconds. Recording stops
                  automatically when reached and shows elapsed time in the terminal.
    """
    if isinstance(frame_rate, bool) or not isinstance(frame_rate, int):
        raise TypeError("frame_rate must be an integer.")
    if format.lower() not in ("gif", "mp4"):
        raise ValueError("format must be 'gif' or 'mp4'.")
    path_arg = "null" if path is None else _quoted(path)
    dur_arg = f", duration={_number(float(duration))}" if duration is not None else ""
    _call(f"eesim.start_video_recording({path_arg}, {frame_rate}, {_quoted(format)}, {_quoted(view)}{dur_arg})")


def stop_video_recording():
    """Stop active video or GIF recording and save the file."""
    _call("eesim.stop_video_recording()")


def start_gif_recording(path=None, frame_rate=10, view="recentre", duration=None):
    """Start GIF recording of the schematic viewport."""
    start_video_recording(path=path, frame_rate=frame_rate, format="gif", view=view, duration=duration)


def stop_gif_recording():
    """Stop active GIF recording."""
    stop_video_recording()


def is_video_recording():
    return _call("eesim.is_video_recording()") == "True"


# ============================================================
# Simulation Playback & Control
# ============================================================
def play_simulation():
    """Start or resume continuous simulation playback."""
    _call("eesim.play_simulation()")

def start_simulation():
    play_simulation()

def play():
    play_simulation()

def pause_simulation():
    """Pause continuous simulation playback."""
    _call("eesim.pause_simulation()")

def pause():
    pause_simulation()

def stop_simulation():
    """Stop continuous simulation playback."""
    _call("eesim.stop_simulation()")

def stop():
    stop_simulation()

def toggle_simulation():
    """Toggle continuous simulation playback between playing and paused."""
    _call("eesim.toggle_simulation()")

def step_simulation():
    """Advance the simulation by one step."""
    _call("eesim.step_simulation()")


# ============================================================
# Viewport & Sidebar Controls (Max Viewport)
# ============================================================
def close_sidebars():
    """Close both left and right sidebars to maximize schematic viewport."""
    _call("eesim.close_sidebars()")

def hide_sidebars():
    close_sidebars()

def open_sidebars():
    """Open standard left and right sidebars."""
    _call("eesim.open_sidebars()")

def recenter_viewport():
    """Center the live schematic viewport on the circuit without changing zoom."""
    _call("eesim.recenter_viewport()")

def fit_to_screen():
    """Fit the complete circuit inside the live schematic viewport."""
    _call("eesim.fit_to_screen()")

def apply_layout():
    """Apply logical grid placement and wire routing to the circuit and fit to viewport."""
    _call("eesim.apply_layout()")

def auto_layout():
    """Alias for apply_layout."""
    apply_layout()

def show_sidebars():
    open_sidebars()

def set_left_sidebar(visible=True):
    """Set visibility of the left toolbar/sidebar."""
    _call(f"eesim.set_left_sidebar({str(bool(visible)).lower()})")

def set_right_sidebar(visible=True):
    """Set visibility of the right component/description sidebar."""
    _call(f"eesim.set_right_sidebar({str(bool(visible)).lower()})")

def toggle_left_sidebar():
    """Toggle visibility of the left toolbar."""
    _call("eesim.toggle_left_sidebar()")

def toggle_right_sidebar():
    """Toggle visibility of the right component sidebar."""
    _call("eesim.toggle_right_sidebar()")

def open_left_sidebar():
    set_left_sidebar(True)

def close_left_sidebar():
    set_left_sidebar(False)

def open_right_sidebar():
    set_right_sidebar(True)

def close_right_sidebar():
    set_right_sidebar(False)


# ============================================================
# Waveform / Graph Drawer
# ============================================================
def open_graph_drawer():
    """Open the bottom waveform / graph drawer for probe inspection."""
    _call("eesim.open_graph_drawer()")

def close_graph_drawer():
    """Close the bottom waveform / graph drawer."""
    _call("eesim.close_graph_drawer()")

def toggle_graph_drawer():
    """Toggle the bottom waveform / graph drawer."""
    _call("eesim.toggle_graph_drawer()")

def set_graph_drawer(visible=True):
    """Set visibility of the bottom waveform / graph drawer."""
    _call(f"eesim.set_graph_drawer({str(bool(visible)).lower()})")

open_waveform_drawer = open_graph_drawer
close_waveform_drawer = close_graph_drawer
toggle_waveform_drawer = toggle_graph_drawer
set_waveform_drawer = set_graph_drawer


# ============================================================
# Timed Subtitles (Circuit Subtitles for Video)
# ============================================================
def set_subtitle(text):
    """Display an on-screen subtitle pill under the circuit (recorded in video)."""
    _call(f"eesim.set_subtitle({_quoted(text)})")

def edit_subtitle(text):
    """Edit current subtitle text while simulation or recording is running."""
    set_subtitle(text)

def subtitle(text):
    set_subtitle(text)

def clear_subtitle():
    """Remove the on-screen subtitle pill."""
    _call("eesim.clear_subtitle()")

def get_subtitle():
    """Get the current subtitle text."""
    return _call("eesim.get_subtitle()")

def wait(seconds):
    """Wait for specified seconds (allows simulation & video recording to advance)."""
    import time
    time.sleep(float(seconds))
    _call(f"eesim.wait({_number(seconds)})")

def sleep(seconds):
    wait(seconds)


# ============================================================
# Settings Page - Workspace & Workflow Settings API
# ============================================================
def get_workspace_setting(name):
    """Get the current value of a Workspace setting."""
    return _call(f"eesim.get_workspace_setting({_quoted(name)})")

def reset_workspace_settings():
    """Reset all Workspace settings to factory defaults."""
    _call("eesim.reset_workspace_settings()")

def reset_settings_to_defaults():
    reset_workspace_settings()

def set_solver_step(step_seconds):
    """Set the numerical simulation solver step in seconds (e.g. 0.00001 for 10µs)."""
    set_workspace_setting("solver_step", float(step_seconds))

def set_simulation_duration(duration_seconds):
    """Set total transient simulation duration in seconds (e.g. 0.01 for 10ms)."""
    set_workspace_setting("duration", float(duration_seconds))

def set_simulation_start_time(start_seconds):
    """Set transient simulation start time in seconds."""
    set_workspace_setting("start_time", float(start_seconds))

def set_time_scaling(enabled=True, sim_amount=None, sim_unit=None, dev_amount=None, dev_unit=None):
    """Configure simulation time scaling."""
    set_workspace_setting("enable_time_scaling", bool(enabled))
    if sim_amount is not None: set_workspace_setting("simulation_time", float(sim_amount))
    if sim_unit is not None: set_workspace_setting("simulation_time_unit", str(sim_unit))
    if dev_amount is not None: set_workspace_setting("device_time", float(dev_amount))
    if dev_unit is not None: set_workspace_setting("device_time_unit", str(dev_unit))

def set_auto_run_simulation(enabled=True):
    set_workspace_setting("auto_run_simulation", bool(enabled))

def set_automatic_time_step(enabled=True):
    set_workspace_setting("automatic_time_step_selection", bool(enabled))


def read_pin_voltage(name, pin):
    return _call(f"eesim.read_pin_voltage({_quoted(name)}, {_quoted(pin)})")


def read_digital_output_state(name, pin):
    return _call(f"eesim.read_digital_output_state({_quoted(name)}, {_quoted(pin)})")


def set_description(name, description):
    _call(f"eesim.set_description({_quoted(name)}, {_quoted(description)})")


def rotate_component(name, degrees):
    _call(f"eesim.rotate_component({_quoted(name)}, {_number(degrees)})")


def stretch_component(name, scale_x, scale_y):
    _call(f"eesim.stretch_component({_quoted(name)}, {_number(scale_x)}, {_number(scale_y)})")


def print(*args, **kwargs):
    builtins.print(*args, **kwargs)
