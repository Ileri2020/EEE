from pathlib import Path
import re
p = Path(__file__).resolve().parent.parent / "EESimulator" / "EESimulator.Scripting" / "PythonCodeGenerator.cs"
text = p.read_text(encoding="utf-8")
start = text.find("            // ── Power Systems, Machines, Loads & Storage")
end = text.find("            // ── Structural / Measurement / General", start)
if start < 0 or end < 0:
    raise SystemExit("Power-system switch section not found")
section = text[start:end]
section = re.sub(r'(?<!\\)"\{([^{}\n]+)\}"', lambda m: '\\"{' + m.group(1) + '}\\"', section)
text = text[:start] + section + text[end:]
p.write_text(text, encoding="utf-8")
