using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Components.General;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Registry;

/// <summary>
/// Central dynamic registry and factory for all electrical and electronic components.
/// Supports stable type identifiers (e.g. 'electrical.resistor'), categorized browsing,
/// and dynamic instantiation for UI palettes and Python scripting.
/// </summary>
public static class ComponentRegistry
{
    private static readonly ConcurrentDictionary<string, ComponentDefinition> _registry = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<ComponentDefinition> _allDefinitions = new();
    private static readonly object _syncLock = new();

    static ComponentRegistry()
    {
        RegisterDefaultComponents();
    }

    public static void Register<T>(string stableTypeId, string typeName, string category, string referencePrefix, Action<ComponentDefinition>? configure = null) where T : ComponentNode, new()
    {
        var def = new ComponentDefinition
        {
            StableTypeId = stableTypeId,
            TypeName = typeName,
            DisplayName = typeName,
            Category = category,
            ReferencePrefix = referencePrefix,
            SymbolKey = typeName,
            NodeType = typeof(T),
            FactoryMethod = () => new T()
        };

        configure?.Invoke(def);

        lock (_syncLock)
        {
            _registry[stableTypeId] = def;
            _registry[typeName] = def;
            _registry[typeof(T).Name] = def;

            if (!_allDefinitions.Any(d => d.StableTypeId == stableTypeId))
            {
                _allDefinitions.Add(def);
            }
        }
    }

    public static bool TryGetDefinition(string typeIdOrName, out ComponentDefinition definition)
    {
        return _registry.TryGetValue(typeIdOrName, out definition!);
    }

    public static ComponentDefinition GetDefinition(string typeIdOrName)
    {
        if (_registry.TryGetValue(typeIdOrName, out var def))
            return def;

        throw new KeyNotFoundException($"No component registered for type or ID '{typeIdOrName}'.");
    }

    public static IEnumerable<ComponentDefinition> GetAllDefinitions()
    {
        lock (_syncLock)
        {
            return _allDefinitions.ToList();
        }
    }

    public static IEnumerable<ComponentDefinition> GetByCategory(string category)
    {
        lock (_syncLock)
        {
            return _allDefinitions.Where(d => d.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    public static IEnumerable<string> GetCategories()
    {
        lock (_syncLock)
        {
            return _allDefinitions.Select(d => d.Category).Distinct().ToList();
        }
    }

    public static ComponentNode CreateInstance(string typeIdOrName, string name = "", double x = 0, double y = 0)
    {
        var def = GetDefinition(typeIdOrName);
        var instance = (ComponentNode)def.FactoryMethod();
        instance.Name = string.IsNullOrEmpty(name) ? $"{def.ReferencePrefix}_{Guid.NewGuid().ToString("N")[..4]}" : name;
        instance.Reference = instance.Name;
        instance.X = x;
        instance.Y = y;
        return instance;
    }

    private static void RegisterDefaultComponents()
    {
        // ── Electrical (Passive & Sources) ───────────────────────────────────
        Register<ResistorNode>("electrical.resistor", "Resistor", "Electrical", "R", d =>
        {
            d.Description = "Linear two-terminal electrical resistor";
            d.DefaultParameters["Resistance"] = 1000.0;
        });

        Register<CapacitorNode>("electrical.capacitor", "Capacitor", "Electrical", "C", d =>
        {
            d.Description = "Linear two-terminal electrical capacitor";
            d.DefaultParameters["Capacitance"] = 1e-6;
        });

        Register<InductorNode>("electrical.inductor", "Inductor", "Electrical", "L", d =>
        {
            d.Description = "Linear two-terminal electrical inductor";
            d.DefaultParameters["Inductance"] = 1e-3;
        });

        Register<VoltageSourceNode>("electrical.voltagesource", "VoltageSource", "Electrical", "V", d =>
        {
            d.Description = "Independent DC/AC voltage source";
            d.DefaultParameters["Voltage"] = 5.0;
        });

        Register<CurrentSourceNode>("electrical.currentsource", "CurrentSource", "Electrical", "I", d =>
        {
            d.Description = "Independent DC/AC current source";
            d.DefaultParameters["Current"] = 0.001;
        });

        Register<GroundNode>("electrical.ground", "Ground", "Electrical", "GND", d =>
        {
            d.Description = "Circuit ground reference (Node 0)";
        });

        Register<DiodeComponent>("electrical.diode", "Diode", "Electrical", "D", d =>
        {
            d.Description = "Semiconductor P-N junction diode";
            d.DefaultParameters["Is"] = 1e-14;
        });

        Register<MutualInductanceNode>("electrical.mutualinductance", "MutualInductance", "Electrical", "K", d =>
        {
            d.Description = "Magnetic mutual coupling between two inductors (K)";
            d.DefaultParameters["Coupling"] = 0.99;
        });

        // ── Electronics (Active & Semiconductors) ────────────────────────────
        Register<LEDComponent>("electronics.led", "LED", "Electronics", "LED", d =>
        {
            d.Description = "Light Emitting Diode";
            d.DefaultParameters["Vf"] = 2.0;
        });

        Register<BJTComponent>("electronics.bjt", "BJT", "Electronics", "Q", d =>
        {
            d.Description = "Bipolar Junction Transistor (NPN/PNP)";
            d.DefaultParameters["Bf"] = 100.0;
        });

        Register<MOSFETComponent>("electronics.mosfet", "MOSFET", "Electronics", "M", d =>
        {
            d.Description = "MOSFET Field-Effect Transistor (N-Ch/P-Ch)";
            d.DefaultParameters["Vto"] = 2.0;
        });

        Register<OpAmpComponent>("electronics.opamp", "OpAmp", "Electronics", "U", d =>
        {
            d.Description = "Operational Amplifier";
            d.DefaultParameters["Aol"] = 100000.0;
        });

        Register<LogicGateComponent>("electronics.logicgate", "LogicGate", "Electronics", "U", d =>
        {
            d.Description = "Generic Digital Logic Gate";
        });

        Register<AndGateComponent>("electronics.and", "AND", "Electronics", "U", d =>
        {
            d.Description = "2-Input AND Gate";
            d.Icon = "and.svg";
        });

        Register<OrGateComponent>("electronics.or", "OR", "Electronics", "U", d =>
        {
            d.Description = "2-Input OR Gate";
            d.Icon = "or.svg";
        });

        Register<NotGateComponent>("electronics.not", "NOT", "Electronics", "U", d =>
        {
            d.Description = "Inverter / NOT Gate";
            d.Icon = "not.svg";
        });

        Register<NandGateComponent>("electronics.nand", "NAND", "Electronics", "U", d =>
        {
            d.Description = "2-Input NAND Gate";
            d.Icon = "nand.svg";
        });

        Register<NorGateComponent>("electronics.nor", "NOR", "Electronics", "U", d =>
        {
            d.Description = "2-Input NOR Gate";
            d.Icon = "nor.svg";
        });

        Register<XorGateComponent>("electronics.xor", "XOR", "Electronics", "U", d =>
        {
            d.Description = "2-Input XOR Gate";
            d.Icon = "xor.svg";
        });

        Register<XnorGateComponent>("electronics.xnor", "XNOR", "Electronics", "U", d =>
        {
            d.Description = "2-Input XNOR Gate";
            d.Icon = "xnor.svg";
        });

        Register<ICComponent>("electronics.ic", "IC", "Electronics", "U", d =>
        {
            d.Description = "Integrated Circuit DIP package";
        });

        // ── General (Structural & Schematic) ─────────────────────────────────
        Register<WireComponent>("general.wire", "Wire", "General", "W", d =>
        {
            d.Description = "Schematic wire segment";
            d.IsSimulatable = false;
        });

        Register<JunctionComponent>("general.junction", "Junction", "General", "J", d =>
        {
            d.Description = "Electrical wire junction";
            d.IsSimulatable = false;
        });

        Register<LabelComponent>("general.label", "Label", "General", "LBL", d =>
        {
            d.Description = "Net connectivity label";
            d.IsSimulatable = false;
        });

        Register<TextAnnotationComponent>("general.textannotation", "TextAnnotation", "General", "TXT", d =>
        {
            d.Description = "Free-floating text annotation";
            d.IsSimulatable = false;
        });

        Register<ConnectorComponent>("general.connector", "Connector", "General", "J", d =>
        {
            d.Description = "Multi-pin external header connector";
            d.IsSimulatable = false;
        });
    }
}
