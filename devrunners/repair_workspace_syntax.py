from pathlib import Path
p = Path(__file__).resolve().parent.parent / "ViewModels" / "EEWorkspaceViewModel.cs"
text = p.read_text(encoding="utf-8")
text = text.replace("EESimulator.Core.Components.Electrical.ClockComponent", "EESimulator.Core.Components.Electronics.ClockComponent")
old = '''            .Select(part => new ComponentPaletteItem(part, part, "TTL", "#58A6FF", "TTL", $"74-series {part}, {TtlPackageComponent.GetPackagePinCount(part)}-pin DIP package",
            .ToArray()),'''
new = '''            .Select(part => new ComponentPaletteItem(part, part, "TTL", "#58A6FF", "TTL", $"74-series {part}, {TtlPackageComponent.GetPackagePinCount(part)}-pin DIP package", $"ttl_{part}.svg"))
            .ToArray()),'''
if old in text:
    text = text.replace(old, new, 1)
p.write_text(text, encoding="utf-8")
