from pathlib import Path
p = Path(__file__).resolve().parent.parent / "EESimulator" / "EESimulator.Formats" / "EeePlatformSerializer.cs"
text = p.read_text(encoding="utf-8")
text = text.replace('    public Guid Id { get; set; }\n    public string ComponentType { get; set; } = string.Empty;\n', '    public Guid Id { get; set; }\n    public string StableTypeId { get; set; } = string.Empty;\n    public string ComponentType { get; set; } = string.Empty;\n', 1)
text = text.replace('                Id = comp.Id,\n                ComponentType = comp.ComponentType,\n', '                Id = comp.Id,\n                StableTypeId = comp is EESimulator.Core.Components.Base.ComponentBase baseComponent ? baseComponent.StableTypeId : string.Empty,\n                ComponentType = comp.ComponentType,\n', 1)
old = '''                comp = ComponentRegistry.CreateInstance(cDto.ComponentType, cDto.Name, cDto.X, cDto.Y);'''
new = '''                string registryKey = !string.IsNullOrWhiteSpace(cDto.StableTypeId) ? cDto.StableTypeId : cDto.ComponentType;
                comp = ComponentRegistry.CreateInstance(registryKey, cDto.Name, cDto.X, cDto.Y);'''
text = text.replace(old, new, 1)
p.write_text(text, encoding="utf-8")
