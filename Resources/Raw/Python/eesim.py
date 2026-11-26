"""Python facade that forwards scripting calls to EESimulator's .NET executor."""

import builtins
import json
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

        if "row" in kwargs or "col" in kwargs:
            if "row" not in kwargs or "col" not in kwargs:
                raise TypeError("Logical placement requires both row and col.")
            _call(
                f"circuit.add_component({_quoted(comp_type)}, name={_quoted(name)}, "
                f"row={_number(kwargs['row'])}, col={_number(kwargs['col'])})"
            )
        elif "position" in kwargs:
            position = kwargs["position"]
            if len(position) != 2:
                raise TypeError("position must contain x and y coordinates.")
            _call(
                f"circuit.add_component({_quoted(comp_type)}, name={_quoted(name)}, "
                f"position_x={_number(position[0])}, position_y={_number(position[1])}"
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

    def add_node(self, name, **kwargs):
        return add_node(name=name, **kwargs)

    def add_wire(self, name, direction="horizontal", **kwargs):
        return add_wire(name=name, direction=direction, **kwargs)

    def add_voltage_source(self, name, dc=5.0, **kwargs):
        return add_voltage_source(name=name, dc=dc, **kwargs)

    def add_microphone(self, name, audio_file=None, bandwidth=20000.0, amplitude=1.0, **kwargs):
        return add_microphone(name=name, audio_file=audio_file, bandwidth=bandwidth, amplitude=amplitude, **kwargs)


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


def set_logical_grid_spacing(row_spacing, column_spacing):
    _call(f"eesim.set_logical_grid_spacing({_number(row_spacing)}, {_number(column_spacing)})")


def set_custom_property(name, prop, value):
    _call(f"eesim.set_custom_property({_quoted(name)}, {_quoted(prop)}, {json.dumps(value, ensure_ascii=False)})")


def set_logic(name, value):
    if not isinstance(value, bool):
        raise TypeError("set_logic expects a bool.")
    _call(f"eesim.set_logic({_quoted(name)}, {str(value).lower()})")


def set_memory_word(name, address, value):
    _call(f"eesim.set_memory_word({_quoted(name)}, {_number(address)}, {_number(value)})")


def connect(*args, **kwargs):
    if len(args) == 2:
        endpoints = args
        resistance = None
    elif len(args) in (4, 5):
        endpoints = (f"{args[0]}.{args[1]}", f"{args[2]}.{args[3]}")
        resistance = args[4] if len(args) == 5 else None
    else:
        raise TypeError("connect expects two endpoint names or component/pin pairs.")
    suffix = f", {_number(resistance)}" if resistance is not None else ""

    extra_parts = []
    if "waypoints" in kwargs and kwargs["waypoints"] is not None:
        wp_list = kwargs["waypoints"]
        wp_str = "[" + ", ".join(f"({_number(pt[0])}, {_number(pt[1])})" for pt in wp_list) + "]"
        extra_parts.append(f"waypoints={wp_str}")
    if "exit_direction" in kwargs and kwargs["exit_direction"] is not None:
        extra_parts.append(f"exit_direction={_quoted(kwargs['exit_direction'])}")

    extra_suffix = (", " + ", ".join(extra_parts)) if extra_parts else ""
    _call(f"eesim.connect({_quoted(endpoints[0])}, {_quoted(endpoints[1])}{suffix}{extra_suffix})")


def add_resistor(name, resistance=1000.0, **kwargs):
    kwargs["param"] = resistance
    return add_component("resistor", name, **kwargs)


def add_capacitor(name, capacitance=1e-6, **kwargs):
    kwargs["param"] = capacitance
    return add_component("capacitor", name, **kwargs)


def add_inductor(name, inductance=1e-3, **kwargs):
    kwargs["param"] = inductance
    return add_component("inductor", name, **kwargs)


def add_ground(name="GND", **kwargs):
    return add_component("ground", name, **kwargs)


def add_node(name, **kwargs):
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


def start_video_recording(path=None, frame_rate=5, format="gif"):
    if isinstance(frame_rate, bool) or not isinstance(frame_rate, int):
        raise TypeError("frame_rate must be an integer.")
    argument = _quoted(path or "")
    if format.lower() not in ("gif", "mp4"):
        raise ValueError("format must be 'gif' or 'mp4'.")
    _call(f"eesim.start_video_recording({argument}, {frame_rate}, {_quoted(format)})")


def stop_video_recording():
    _call("eesim.stop_video_recording()")


def is_video_recording():
    return _call("eesim.is_video_recording()") == "True"


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
