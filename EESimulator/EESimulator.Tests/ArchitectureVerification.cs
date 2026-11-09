using System;
using System.Linq;
using EESimulator.Commands;
using EESimulator.Commands.Commands;
using EESimulator.Core.Components;
using EESimulator.Core.Electrical;
using EESimulator.Core.Models;
using EESimulator.Engine;
using EESimulator.Engine.Simulation;
using EESimulator.Formats;
using EESimulator.Scripting;
using EESimulator.Scripting.API;
using EESimulator.Scripting.Execution;

namespace EESimulator.Tests;

/// <summary>
/// End-to-end integration test validating the foundational vertical slice of EESimulator:
/// 1. Hierarchy and RootNode integrity (no circular parenting, reparenting).
/// 2. ComponentRegistry dynamic instantiation.
/// 3. Command execution through CommandManager (undo/redo).
/// 4. Electrical topology resolution and netlist generation via NetlistBuilder.
/// 5. Python two-way synchronization: Schematic -> Python -> Schematic.
/// 6. Simulation via SpiceSharpAdapter.
/// 7. Serialization and import/export (.eesim, .cir, .kicad_sch).
/// </summary>
public static class ArchitectureVerification
{
    public static void RunAllTests()
    {
        Console.WriteLine("=== [START] EESimulator Architectural Verification ===");

        TestRootNodeHierarchy();
        TestComponentRegistry();
        TestElectricalTopology();
        TestCommandAndUndoRedo();
        TestPythonTwoWaySync();
        TestLogicalGridLayout();
        TestSimulationEngine();
        TestSerializationAndImporters();

        Console.WriteLine("=== [PASS] All EESimulator Architectural Tests Succeeded! ===");
    }

    private static void TestRootNodeHierarchy()
    {
        Console.Write("Testing RootNode hierarchy and circular prevention... ");
        var root = new CircuitNode { Title = "TestRoot" };
        var group = new GroupNode("MainGroup");
        var resistor = new ResistorNode { Name = "R1", Resistance = 500 };

        root.AddChild(group);
        group.AddChild(resistor);

        Assert(resistor.Parent == group, "Resistor parent should be group");
        Assert(group.Parent == root, "Group parent should be root");
        Assert(resistor.IsDescendantOf(root), "Resistor should be descendant of root");

        // Attempt circular parenting
        bool threw = false;
        try
        {
            resistor.AddChild(root);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Assert(threw, "Circular parenting must throw InvalidOperationException");
        Console.WriteLine("OK");
    }

    private static void TestComponentRegistry()
    {
        Console.Write("Testing ComponentRegistry and factory... ");
        var defs = ComponentRegistry.GetAllDefinitions().ToList();
        Assert(defs.Count >= 5, "Should have at least 5 default components registered");

        var comp = ComponentRegistry.CreateInstance("Resistor", "R_Test", 150, 250);
        Assert(comp is ResistorNode, "Created component should be ResistorNode");
        Assert(comp.X == 150 && comp.Y == 250, "Position should be set correctly");
        Console.WriteLine("OK");
    }

    private static void TestElectricalTopology()
    {
        Console.Write("Testing NetlistBuilder union-find topology... ");
        var circuit = new CircuitNode();
        var v1 = (VoltageSourceNode)ComponentRegistry.CreateInstance("VoltageSource", "V1");
        var r1 = (ResistorNode)ComponentRegistry.CreateInstance("Resistor", "R1");
        var gnd = (GroundNode)ComponentRegistry.CreateInstance("Ground", "GND");

        circuit.AddChild(v1);
        circuit.AddChild(r1);
        circuit.AddChild(gnd);

        // V1(+) -> R1(pin1), R1(pin2) -> GND, V1(-) -> GND
        circuit.AddChild(new ConnectionNode(v1.Id, "positive", r1.Id, "pin1"));
        circuit.AddChild(new ConnectionNode(r1.Id, "pin2", gnd.Id, "gnd"));
        circuit.AddChild(new ConnectionNode(v1.Id, "negative", gnd.Id, "gnd"));

        var topology = NetlistBuilder.BuildTopology(circuit);
        Assert(topology.Nets.Count == 2, $"Expected 2 electrical nets (Node 0 and Node 1), got {topology.Nets.Count}");
        Assert(topology.Nets.Any(n => n.IsGround && n.NetName == "0"), "Should contain ground Net 0");
        Console.WriteLine("OK");
    }

    private static void TestCommandAndUndoRedo()
    {
        Console.Write("Testing CommandManager and undo/redo... ");
        var circuit = new CircuitNode();
        var cmdManager = new CommandManager();

        var addCmd = new AddComponentCommand(circuit, "Resistor", "R1", 100, 100, 2200);
        cmdManager.ExecuteCommand(addCmd);

        Assert(circuit.Components.Count() == 1, "Should have 1 component after add command");
        Assert(((ResistorNode)circuit.Components.First()).Resistance == 2200, "Resistance should be 2200");

        cmdManager.Undo();
        Assert(!circuit.Components.Any(), "Should have 0 components after undo");

        cmdManager.Redo();
        Assert(circuit.Components.Count() == 1, "Should have 1 component after redo");
        Console.WriteLine("OK");
    }

    private static void TestPythonTwoWaySync()
    {
        Console.Write("Testing Python two-way synchronization... ");
        var circuit = new CircuitNode();
        var cmdManager = new CommandManager();
        var api = new PythonApi(cmdManager, circuit);
        var synchronizer = new ScriptSynchronizer(cmdManager, circuit);
        var executor = new PythonCommandExecutor(api);

        // 1. Add component via schematic command -> should generate Python script
        var addCmd = new AddComponentCommand(circuit, "Resistor", "R1", 200, 300, 1000);
        cmdManager.ExecuteCommand(addCmd);

        string generatedScript = synchronizer.CurrentScript;
        Assert(generatedScript.Contains("add_component(\"Resistor\", name=\"R1\""), "Generated script must contain component add");

        // 2. Execute Python statement in script editor -> should update schematic
        string pythonStatement = "r2 = circuit.add_component(\"Capacitor\", name=\"C1\", position=(400, 500), capacitance=0.00001)";
        bool ok = executor.ExecuteScript(pythonStatement, out string error);
        Assert(ok, $"Python execution failed: {error}");
        Assert(circuit.FindChild<CapacitorNode>("C1") != null, "Capacitor C1 should exist in schematic");
        Console.WriteLine("OK");
    }

    private static void TestSimulationEngine()
    {
        Console.Write("Testing SimulationManager with SpiceSharpAdapter... ");
        var circuit = new CircuitNode();
        var v1 = (VoltageSourceNode)ComponentRegistry.CreateInstance("VoltageSource", "V1");
        v1.Voltage = 12.0;
        var r1 = (ResistorNode)ComponentRegistry.CreateInstance("Resistor", "R1");
        r1.Resistance = 1000.0;
        var r2 = (ResistorNode)ComponentRegistry.CreateInstance("Resistor", "R2");
        r2.Resistance = 2000.0;

        circuit.AddChild(v1);
        circuit.AddChild(r1);
        circuit.AddChild(r2);

        var simManager = new SimulationManager(new SpiceSharpAdapter());
        var results = simManager.Run(circuit);

        Assert(results.Success, $"Simulation failed: {results.Message}");
        Assert(results.OperatingPointCurrents.ContainsKey("I_total"), "Results must contain I_total");
        double iExpected = 12.0 / 3000.0;
        Assert(Math.Abs(results.OperatingPointCurrents["I_total"] - iExpected) < 1e-6, "Total current matches Ohm's law");
        Console.WriteLine("OK");
    }

    private static void TestLogicalGridLayout()
    {
        Console.Write("Testing logical grid placement and legacy positions... ");
        var circuit = new CircuitNode();
        var commands = new CommandManager();
        var api = new PythonApi(commands, circuit);
        var executor = new PythonCommandExecutor(api);
        const string gridScript = "r1 = circuit.add_component(\"Resistor\", name=\"R1\", row=2, col=3, resistance=2200)\n" +
                                  "r2 = circuit.add_component(\"Resistor\", name=\"R2\", row=2, col=3)\n" +
                                  "ram = circuit.add_component(\"RAM\", name=\"RAM1\", row=3, col=4)";

        Assert(executor.ExecuteScript(gridScript, out string gridError), $"Grid script failed: {gridError}");
        var first = circuit.FindChild<ComponentNode>("R1")!;
        var second = circuit.FindChild<ComponentNode>("R2")!;
        var memory = circuit.FindChild<ComponentNode>("RAM1")!;
        Assert(first.LogicalRow == 2 && first.LogicalColumn == 3, "Grid row/column should be stored on the component");
        Assert(first.ElectricalParameters.TryGetValue("Resistance", out double resistance) && resistance == 2200,
            "Named electrical properties should survive logical placement");
        Assert(memory.LogicalRow == 3 && memory.LogicalColumn == 4, "Grid placement should support large multi-pin components");

        circuit.AddChild(new ConnectionNode(first.Id, "pin1", second.Id, "pin2"));
        var topologyBefore = NetlistBuilder.BuildTopology(circuit);
        var layout = LogicalGridSchematicLayout.Calculate(circuit);
        Assert(layout.Placements.Count == 3, "All logically placed components should receive physical positions");
        Assert(layout.UnresolvedCollisions.Count == 0, "Adaptive row/column tracks should prevent component collisions");
        Assert(layout.Placements.Select(placement => (placement.X, placement.Y)).Distinct().Count() == 3,
            "Components sharing a logical cell should be physically separated");

        foreach (var placement in layout.Placements)
        {
            placement.Component.X = placement.X;
            placement.Component.Y = placement.Y;
        }
        var topologyAfter = NetlistBuilder.BuildTopology(circuit);
        Assert(topologyBefore.Nets.Count == topologyAfter.Nets.Count, "Physical layout must not alter electrical topology");
        Assert(circuit.Connections.Count() == 1, "Physical layout must preserve component connections");

        var roundTrip = EeePlatformSerializer.LoadFromEeeJson(EeePlatformSerializer.ExportToEee(circuit));
        var restored = roundTrip.FindChild<ComponentNode>("R1")!;
        Assert(restored.LogicalRow == 2 && restored.LogicalColumn == 3, "Native save/load should preserve logical placement");
        Assert(Math.Abs(restored.X - first.X) < 0.001 && Math.Abs(restored.Y - first.Y) < 0.001,
            "Native save/load should preserve computed physical coordinates");
        string generatedGridScript = PythonCodeGenerator.GenerateFullScript(circuit);
        Assert(generatedGridScript.Contains("row=2, col=3") && !generatedGridScript.Contains("position=("),
            "Generated scripts should expose logical placement without physical coordinates");

        var legacyCircuit = new CircuitNode();
        var legacyExecutor = new PythonCommandExecutor(new PythonApi(new CommandManager(), legacyCircuit));
        Assert(legacyExecutor.ExecuteScript(
            "r3 = circuit.add_component(\"Resistor\", name=\"R3\", position=(123, 234))", out string legacyError),
            $"Legacy position script failed: {legacyError}");
        var legacy = legacyCircuit.FindChild<ComponentNode>("R3")!;
        Assert(legacy.X == 123 && legacy.Y == 234, "Legacy pixel positions must remain unchanged");
        Assert(!legacy.HasLogicalPlacement, "Explicit positions must not be reinterpreted as grid cells");
        Assert(PythonCodeGenerator.GenerateFullScript(legacyCircuit).Contains("position=(123, 234)"),
            "Generated scripts should retain manual pixel positions");
        Console.WriteLine("OK");
    }

    private static void TestSerializationAndImporters()
    {
        Console.Write("Testing .eesim, SPICE, and KiCad import/export... ");
        var circuit = new CircuitNode { Title = "ExportTest" };
        var r1 = (ResistorNode)ComponentRegistry.CreateInstance("Resistor", "R1", 100, 200);
        circuit.AddChild(r1);

        // 1. .eesim JSON
        string json = EesimSerializer.SerializeToJson(circuit);
        Assert(json.Contains("ExportTest") && json.Contains("R1"), "JSON must serialize title and components");

        // 2. SPICE export/import
        string spice = SpiceNetlistExporter.ExportToSpice(circuit);
        Assert(spice.Contains("R_R1"), "SPICE netlist must contain R_R1");
        var reimportedSpice = SpiceNetlistImporter.ImportFromSpice(spice, "Reimported");
        Assert(reimportedSpice.Components.Any(c => c.Name.Contains("R1")), "SPICE netlist re-imported component");

        // 3. KiCad export/import
        string kicad = KiCadExporter.ExportToKiCadSch(circuit);
        Assert(kicad.Contains("kicad_sch") && kicad.Contains("R1"), "KiCad export must contain S-expressions and R1");
        var reimportedKiCad = KiCadImporter.ImportFromKiCadSch(kicad);
        Assert(reimportedKiCad.Components.Any(c => c.Name == "R1"), "KiCad importer must reconstruct R1");
        Console.WriteLine("OK");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception($"[TEST FAILURE] {message}");
    }
}
