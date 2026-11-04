from pathlib import Path
p = Path(__file__).resolve().parent.parent / "ViewModels" / "EEWorkspaceViewModel.cs"
text = p.read_text(encoding="utf-8")
needle = "    private static string[] SplitArgs(string argsStr)\n"
helpers = '''    private static Dictionary<string, string> ParseNamedArgs(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string arg in args.Skip(1))
        {
            int equals = arg.IndexOf('=');
            if (equals <= 0) continue;
            string key = arg[..equals].Trim();
            string value = arg[(equals + 1)..].Trim();
            result[key] = value;
            if (key.Equals("position", StringComparison.OrdinalIgnoreCase) && value.Length >= 5 && value[0] == '(' && value[^1] == ')')
            {
                var parts = value[1..^1].Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                {
                    result["position_x"] = parts[0];
                    result["position_y"] = parts[1];
                }
            }
        }
        return result;
    }

    private static double NamedD(Dictionary<string, string> named, string key, double fallback = 0) =>
        named.TryGetValue(key, out string? value) ? ParseD(value) : fallback;

'''
if "ParseNamedArgs(string[] args)" not in text:
    if needle not in text:
        raise SystemExit("SplitArgs marker not found")
    text = text.replace(needle, helpers + needle, 1)
p.write_text(text, encoding="utf-8")
