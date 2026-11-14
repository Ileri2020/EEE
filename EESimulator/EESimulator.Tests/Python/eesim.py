"""Python facade that forwards scripting calls to EESimulator's .NET executor."""

import builtins
import json

_protocol_output = None
_protocol_input = None


def attach(protocol_output, protocol_input):
    global _protocol_output, _protocol_input
    _protocol_output = protocol_output
    _protocol_input = protocol_input


def _call(script):
    bridge = getattr(builtins, "app_bridge", None)
    if bridge is not None:
        response = bridge.Execute(script)
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


def clear_circuit():
    _call("eesim.clear_circuit()")


def add_component(comp_type, name, x=0, y=0, param=0, **kwargs):
    if kwargs:
        raise TypeError("add_component accepts positional arguments only in the app scripting interface.")
    _call(
        f"eesim.add_component({_quoted(comp_type)}, {_quoted(name)}, "
        f"{_number(x)}, {_number(y)}, {_number(param)})"
    )
    return ComponentRef(str(name))


def set_component_property(name, prop, value):
    _call(f"eesim.set_component_property({_quoted(name)}, {_quoted(prop)}, {json.dumps(value, ensure_ascii=False)})")


def set_custom_property(name, prop, value):
    _call(f"eesim.set_custom_property({_quoted(name)}, {_quoted(prop)}, {json.dumps(value, ensure_ascii=False)})")


def set_logic(name, value):
    if not isinstance(value, bool):
        raise TypeError("set_logic expects a bool.")
    _call(f"eesim.set_logic({_quoted(name)}, {str(value).lower()})")


def set_memory_word(name, address, value):
    _call(f"eesim.set_memory_word({_quoted(name)}, {_number(address)}, {_number(value)})")


def connect(*args):
    if len(args) == 2:
        endpoints = args
    elif len(args) == 4:
        endpoints = (f"{args[0]}.{args[1]}", f"{args[2]}.{args[3]}")
    else:
        raise TypeError("connect expects two endpoint names or two component/pin pairs.")
    _call(f"eesim.connect({_quoted(endpoints[0])}, {_quoted(endpoints[1])})")


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


def read_pin_voltage(name, pin):
    return _call(f"eesim.read_pin_voltage({_quoted(name)}, {_quoted(pin)})")


def read_digital_output_state(name, pin):
    return _call(f"eesim.read_digital_output_state({_quoted(name)}, {_quoted(pin)})")


def print(*args, **kwargs):
    builtins.print(*args, **kwargs)
