using System;
using System.Linq;
using System.Numerics;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electrical;

/// <summary>Shared dictionary-backed storage for persisted power-system parameters.</summary>
public abstract class PowerSystemComponent : ElectricalComponent
{
    protected const string DeviceTimeKey = "DeviceTimeSeconds";
    protected double Number(string key, double fallback) => ElectricalParameters.TryGetValue(key, out var value) ? value : fallback;
    protected void SetNumber(string key, double value, double minimum = double.NegativeInfinity)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(key, "Value must be finite.");
        ElectricalParameters[key] = Math.Max(value, minimum);
        NotifyModified(key);
    }
    protected string Text(string key, string fallback) => CustomProperties.TryGetValue(key, out var value) ? value?.ToString() ?? fallback : fallback;
    protected void SetText(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value cannot be empty.", key);
        CustomProperties[key] = value.Trim();
        NotifyModified(key);
    }
    protected bool Flag(string key, bool fallback) => CustomProperties.TryGetValue(key, out var value) && bool.TryParse(value?.ToString(), out var parsed) ? parsed : fallback;
    protected void SetFlag(string key, bool value) { CustomProperties[key] = value; NotifyModified(key); }
    public static bool IsScheduleActive(string schedule, double deviceTimeSeconds)
    {
        if (schedule.Equals("AlwaysOff", StringComparison.OrdinalIgnoreCase)) return false;
        if (schedule.Equals("AlwaysOn", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(schedule)) return true;
        var range = schedule.Split('-', StringSplitOptions.TrimEntries);
        if (range.Length != 2 || !TimeSpan.TryParse(range[0], out var start) || !TimeSpan.TryParse(range[1], out var end)) return true;
        double daySeconds = ((deviceTimeSeconds % 86400) + 86400) % 86400;
        var time = TimeSpan.FromSeconds(daySeconds);
        return start <= end ? time >= start && time < end : time >= start || time < end;
    }
    public virtual void ValidateConfiguration() { }
}

/// <summary>Single-phase AC line using short, nominal-pi medium, or long-line ABCD calculations.</summary>
public sealed class SinglePhaseTransmissionLineComponent : PowerSystemComponent
{
    public override string ComponentType => "SinglePhaseTransmissionLine";
    public override string StableTypeId => "powerline.single-phase";
    public override string SpicePrefix => "TL1";
    public override string Description => "Single-phase line with short/medium/long-line RLGC calculations";
    public double RatedVoltage { get => Number(nameof(RatedVoltage), 230); set => SetNumber(nameof(RatedVoltage), value, 1); }
    public double Frequency { get => Number(nameof(Frequency), 50); set => SetNumber(nameof(Frequency), value, 0.01); }
    public double LengthKm { get => Number(nameof(LengthKm), 1); set => SetNumber(nameof(LengthKm), value, 0.001); }
    public double ResistancePerKm { get => Number(nameof(ResistancePerKm), 0.2); set => SetNumber(nameof(ResistancePerKm), value, 0); }
    public double InductancePerKm { get => Number(nameof(InductancePerKm), 1e-3); set => SetNumber(nameof(InductancePerKm), value, 0); }
    public double CapacitancePerKm { get => Number(nameof(CapacitancePerKm), 10e-9); set => SetNumber(nameof(CapacitancePerKm), value, 0); }
    public double ConductancePerKm { get => Number(nameof(ConductancePerKm), 0); set => SetNumber(nameof(ConductancePerKm), value, 0); }
    public int NumberOfConductors { get => (int)Number(nameof(NumberOfConductors), 2); set => SetNumber(nameof(NumberOfConductors), Math.Clamp(value, 2, 4)); }
    public double PowerFactor { get => Number(nameof(PowerFactor), 0.9); set => SetNumber(nameof(PowerFactor), Math.Clamp(value, -1, 1)); }
    public double MaximumCurrent { get => Number(nameof(MaximumCurrent), 100); set => SetNumber(nameof(MaximumCurrent), value, 0.01); }
    public double LineTemperature { get => Number(nameof(LineTemperature), 20); set => SetNumber(nameof(LineTemperature), value, -273.15); }
    public string ConductorType { get => Text(nameof(ConductorType), "Copper"); set => SetText(nameof(ConductorType), value); }
    public string ModelType { get => Text(nameof(ModelType), "Short Line"); set { if (value is not ("Short Line" or "Medium Line" or "Long Line")) throw new ArgumentException("Choose Short Line, Medium Line, or Long Line."); SetText(nameof(ModelType), value); } }
    public string SupplyMode { get => Text(nameof(SupplyMode), "ExternalGenerators"); set { if (value is not ("ExternalGenerators" or "FixedVoltageSource")) throw new ArgumentException("Supply mode must be ExternalGenerators or FixedVoltageSource."); SetText(nameof(SupplyMode), value); } }
    public double FixedVoltageRms { get => Number(nameof(FixedVoltageRms), RatedVoltage); set => SetNumber(nameof(FixedVoltageRms), value, 0); }
    public double InitialVoltageMagnitude { get => Number(nameof(InitialVoltageMagnitude), 230); set => SetNumber(nameof(InitialVoltageMagnitude), value, 0); }
    public double InitialPhaseAngle { get => Number(nameof(InitialPhaseAngle), 0); set => SetNumber(nameof(InitialPhaseAngle), value); }
    public double TotalResistance => ResistancePerKm * LengthKm * Math.Max(0, 1 + ConductorTemperatureCoefficient * (LineTemperature - 20));
    private double ConductorTemperatureCoefficient => ConductorType.Equals("Aluminum", StringComparison.OrdinalIgnoreCase) ? 0.00403 : 0.00393;
    public double TotalInductiveReactance => 2 * Math.PI * Frequency * InductancePerKm * LengthKm;
    public double TotalCapacitiveReactance => CapacitancePerKm <= 0 ? double.PositiveInfinity : 1 / (2 * Math.PI * Frequency * CapacitancePerKm * LengthKm);
    public double SeriesImpedanceMagnitude => Math.Sqrt(TotalResistance * TotalResistance + TotalInductiveReactance * TotalInductiveReactance);
    public double ShuntAdmittance => Math.Sqrt(Math.Pow(ConductancePerKm * LengthKm, 2) + Math.Pow(2 * Math.PI * Frequency * CapacitancePerKm * LengthKm, 2));
    public Complex SeriesImpedance => new(TotalResistance, TotalInductiveReactance);
    public Complex ShuntAdmittancePhasor => new(ConductancePerKm * LengthKm, 2 * Math.PI * Frequency * CapacitancePerKm * LengthKm);
    private Complex AbcdA
    {
        get
        {
            Complex z = SeriesImpedance;
            Complex y = ShuntAdmittancePhasor;
            return ModelType switch
            {
                "Medium Line" => Complex.One + y * z / 2,
                "Long Line" when y.Magnitude > 1e-18 => Complex.Cosh(Complex.Sqrt(z * y)),
                _ => Complex.One
            };
        }
    }
    private Complex AbcdB
    {
        get
        {
            Complex z = SeriesImpedance;
            Complex y = ShuntAdmittancePhasor;
            return ModelType switch
            {
                "Medium Line" => z * (Complex.One + y * z / 4),
                "Long Line" when y.Magnitude > 1e-18 => Complex.Sqrt(z / y) * Complex.Sinh(Complex.Sqrt(z * y)),
                _ => z
            };
        }
    }
    private Complex SendingEndPhasor
    {
        get
        {
            double angle = InitialPhaseAngle * Math.PI / 180;
            Complex receivingVoltage = Complex.FromPolarCoordinates(Math.Max(InitialVoltageMagnitude, 0), angle);
            double currentAngle = PowerFactor < 0 ? Math.Acos(Math.Abs(PowerFactor)) : -Math.Acos(Math.Clamp(PowerFactor, -1, 1));
            Complex receivingCurrent = Complex.FromPolarCoordinates(MaximumCurrent, angle + currentAngle);
            return AbcdA * receivingVoltage + AbcdB * receivingCurrent;
        }
    }
    public double VoltageDrop => Math.Max(0, SendingEndPhasor.Magnitude - InitialVoltageMagnitude);
    public double ReceivingEndVoltage => Math.Max(0, InitialVoltageMagnitude);
    public double SendingEndVoltage => SendingEndPhasor.Magnitude;
    public double VoltageRegulationPercent => ReceivingEndVoltage <= 0 ? 0 : (SendingEndVoltage - ReceivingEndVoltage) / ReceivingEndVoltage * 100;
    public double TransmissionLosses => MaximumCurrent * MaximumCurrent * TotalResistance;
    public double EfficiencyPercent => TransmissionLosses + RatedVoltage * MaximumCurrent * Math.Abs(PowerFactor) <= 0 ? 0 : 100 * (1 - TransmissionLosses / (TransmissionLosses + RatedVoltage * MaximumCurrent * Math.Abs(PowerFactor)));
    public SinglePhaseTransmissionLineComponent() { Pins.Add(new("from", "Sending end", -20, 0)); Pins.Add(new("to", "Receiving end", 20, 0)); }
    public override void ValidateConfiguration() { if (NumberOfConductors is < 2 or > 4) throw new InvalidOperationException("A single-phase line requires 2 to 4 conductors."); }
}

/// <summary>Two-phase line; TrueTwoPhase has independent phase returns while SplitPhaseThreeWire has a center neutral.</summary>
public sealed class TwoPhaseTransmissionLineComponent : PowerSystemComponent
{
    public override string ComponentType => "TwoPhaseTransmissionLine";
    public override string StableTypeId => "powerline.two-phase";
    public override string SpicePrefix => "TL2";
    public override string Description => "True two-phase or split-phase three-wire RLGC line";
    public double RatedLineVoltage { get => Number(nameof(RatedLineVoltage), 240); set => SetNumber(nameof(RatedLineVoltage), value, 1); }
    public double Frequency { get => Number(nameof(Frequency), 60); set => SetNumber(nameof(Frequency), value, 0.01); }
    public double LengthKm { get => Number(nameof(LengthKm), 1); set => SetNumber(nameof(LengthKm), value, 0.001); }
    public double ResistancePerKm { get => Number(nameof(ResistancePerKm), 0.2); set => SetNumber(nameof(ResistancePerKm), value, 0); }
    public double InductancePerKm { get => Number(nameof(InductancePerKm), 1e-3); set => SetNumber(nameof(InductancePerKm), value, 0); }
    public double CapacitancePerKm { get => Number(nameof(CapacitancePerKm), 10e-9); set => SetNumber(nameof(CapacitancePerKm), value, 0); }
    public double ConductancePerKm { get => Number(nameof(ConductancePerKm), 0); set => SetNumber(nameof(ConductancePerKm), value, 0); }
    public double PhaseDisplacementDegrees { get => Number(nameof(PhaseDisplacementDegrees), 90); set => SetNumber(nameof(PhaseDisplacementDegrees), value); }
    public double MaximumCurrent { get => Number(nameof(MaximumCurrent), 100); set => SetNumber(nameof(MaximumCurrent), value, 0.01); }
    public string ConductorType { get => Text(nameof(ConductorType), "Copper"); set => SetText(nameof(ConductorType), value); }
    public string ConductorConfiguration { get => Text(nameof(ConductorConfiguration), "TrueTwoPhase"); set { if (value is not ("TrueTwoPhase" or "SplitPhaseThreeWire")) throw new ArgumentException("Choose TrueTwoPhase or SplitPhaseThreeWire; these have different wiring models."); SetText(nameof(ConductorConfiguration), value); } }
    public string SupplyMode { get => Text(nameof(SupplyMode), "ExternalGenerators"); set { if (value is not ("ExternalGenerators" or "FixedVoltageSource")) throw new ArgumentException("Supply mode must be ExternalGenerators or FixedVoltageSource."); SetText(nameof(SupplyMode), value); } }
    public double FixedVoltageRms { get => Number(nameof(FixedVoltageRms), PhaseVoltage); set => SetNumber(nameof(FixedVoltageRms), value, 0); }
    public double PowerFactor { get => Number(nameof(PowerFactor), 0.9); set => SetNumber(nameof(PowerFactor), Math.Clamp(value, -1, 1)); }
    public double LineTemperature { get => Number(nameof(LineTemperature), 20); set => SetNumber(nameof(LineTemperature), value, -273.15); }
    private double TemperatureAdjustedResistance => ResistancePerKm * Math.Max(0, 1 + (ConductorType.Equals("Aluminum", StringComparison.OrdinalIgnoreCase) ? 0.00403 : 0.00393) * (LineTemperature - 20));
    public double PhaseVoltage => ConductorConfiguration == "SplitPhaseThreeWire" ? RatedLineVoltage / 2 : RatedLineVoltage;
    public double PhaseBVoltageReal => PhaseVoltage * Math.Cos(PhaseDisplacementDegrees * Math.PI / 180);
    public double PhaseBVoltageImaginary => PhaseVoltage * Math.Sin(PhaseDisplacementDegrees * Math.PI / 180);
    public double VoltageDrop => MaximumCurrent * TemperatureAdjustedResistance * LengthKm * Math.Abs(PowerFactor);
    public double LineLosses => 2 * MaximumCurrent * MaximumCurrent * TemperatureAdjustedResistance * LengthKm;
    public TwoPhaseTransmissionLineComponent()
    {
        Pins.Add(new("a_from", "Phase A send", -20, -14)); Pins.Add(new("a_to", "Phase A receive", 20, -14));
        Pins.Add(new("b_from", "Phase B send", -20, 14)); Pins.Add(new("b_to", "Phase B receive", 20, 14));
        Pins.Add(new("neutral_from", "Neutral send (split-phase only)", -20, 24));
        Pins.Add(new("neutral_to", "Neutral receive (split-phase only)", 20, 24));
    }
    public override void ValidateConfiguration() { if (ConductorConfiguration == "TrueTwoPhase" && Math.Abs(Math.Abs(PhaseDisplacementDegrees) - 90) > 1e-6) throw new InvalidOperationException("A true two-phase system requires 90° phase displacement."); }
}

/// <summary>Three-phase per-phase RLGC line with explicit phase and optional neutral terminals.</summary>
public sealed class ThreePhaseTransmissionLineComponent : PowerSystemComponent
{
    public override string ComponentType => "ThreePhaseTransmissionLine";
    public override string StableTypeId => "powerline.three-phase";
    public override string SpicePrefix => "TL3";
    public override string Description => "Three-phase RLGC line; phase quantities are distinct from line-to-line quantities";
    public double RatedLineVoltage { get => Number(nameof(RatedLineVoltage), 11000); set => SetNumber(nameof(RatedLineVoltage), value, 1); }
    public double Frequency { get => Number(nameof(Frequency), 50); set => SetNumber(nameof(Frequency), value, 0.01); }
    public double LengthKm { get => Number(nameof(LengthKm), 1); set => SetNumber(nameof(LengthKm), value, 0.001); }
    public double ResistancePerKmPerPhase { get => Number(nameof(ResistancePerKmPerPhase), 0.2); set => SetNumber(nameof(ResistancePerKmPerPhase), value, 0); }
    public double InductancePerKmPerPhase { get => Number(nameof(InductancePerKmPerPhase), 1e-3); set => SetNumber(nameof(InductancePerKmPerPhase), value, 0); }
    public double CapacitancePerKmPerPhase { get => Number(nameof(CapacitancePerKmPerPhase), 10e-9); set => SetNumber(nameof(CapacitancePerKmPerPhase), value, 0); }
    public double ConductancePerKmPerPhase { get => Number(nameof(ConductancePerKmPerPhase), 0); set => SetNumber(nameof(ConductancePerKmPerPhase), value, 0); }
    public string PhaseSequence { get => Text(nameof(PhaseSequence), "ABC"); set { if (value is not ("ABC" or "ACB")) throw new ArgumentException("Phase sequence must be ABC or ACB."); SetText(nameof(PhaseSequence), value); } }
    public string Connection { get => Text(nameof(Connection), "Three-wire"); set { if (value is not ("Three-wire" or "Four-wire")) throw new ArgumentException("Connection must be Three-wire or Four-wire."); SetText(nameof(Connection), value); } }
    public string ConductorType { get => Text(nameof(ConductorType), "Copper"); set => SetText(nameof(ConductorType), value); }
    public double MaximumCurrent { get => Number(nameof(MaximumCurrent), 100); set => SetNumber(nameof(MaximumCurrent), value, 0.01); }
    public double PowerFactor { get => Number(nameof(PowerFactor), 0.9); set => SetNumber(nameof(PowerFactor), Math.Clamp(value, -1, 1)); }
    public double LineTemperature { get => Number(nameof(LineTemperature), 20); set => SetNumber(nameof(LineTemperature), value, -273.15); }
    public double NeutralImpedance { get => Number(nameof(NeutralImpedance), 0); set => SetNumber(nameof(NeutralImpedance), value, 0); }
    public string ModelType { get => Text(nameof(ModelType), "Short"); set { if (value is not ("Short" or "Medium" or "Long")) throw new ArgumentException("Model type must be Short, Medium, or Long."); SetText(nameof(ModelType), value); } }
    public string SupplyMode { get => Text(nameof(SupplyMode), "ExternalGenerators"); set { if (value is not ("ExternalGenerators" or "FixedVoltageSource")) throw new ArgumentException("Supply mode must be ExternalGenerators or FixedVoltageSource."); SetText(nameof(SupplyMode), value); } }
    public double FixedPhaseVoltageRms { get => Number(nameof(FixedPhaseVoltageRms), PhaseVoltage); set => SetNumber(nameof(FixedPhaseVoltageRms), value, 0); }
    public double PhaseVoltage => RatedLineVoltage / Math.Sqrt(3);
    public double LineVoltage => RatedLineVoltage;
    public double PhaseCurrent => MaximumCurrent;
    public double ActivePower => Math.Sqrt(3) * RatedLineVoltage * PhaseCurrent * Math.Abs(PowerFactor);
    public double ApparentPower => Math.Sqrt(3) * RatedLineVoltage * PhaseCurrent;
    public double ReactivePower => ApparentPower * Math.Sqrt(Math.Max(0, 1 - PowerFactor * PowerFactor));
    private double TemperatureAdjustedResistance => ResistancePerKmPerPhase * Math.Max(0, 1 + (ConductorType.Equals("Aluminum", StringComparison.OrdinalIgnoreCase) ? 0.00403 : 0.00393) * (LineTemperature - 20));
    public double VoltageDrop => Math.Sqrt(3) * PhaseCurrent * TemperatureAdjustedResistance * LengthKm * Math.Abs(PowerFactor);
    public double VoltageRegulationPercent => RatedLineVoltage <= VoltageDrop ? 0 : VoltageDrop / (RatedLineVoltage - VoltageDrop) * 100;
    public double LineLosses => 3 * PhaseCurrent * PhaseCurrent * TemperatureAdjustedResistance * LengthKm;
    public double EfficiencyPercent => ActivePower + LineLosses <= 0 ? 0 : 100 * ActivePower / (ActivePower + LineLosses);
    public double PhaseImbalancePercent { get => Number(nameof(PhaseImbalancePercent), 0); set => SetNumber(nameof(PhaseImbalancePercent), Math.Clamp(value, 0, 100)); }
    public ThreePhaseTransmissionLineComponent()
    {
        string[] names = { "A_send", "B_send", "C_send", "A_receive", "B_receive", "C_receive", "N_send", "N_receive" };
        for (int i = 0; i < names.Length; i++) Pins.Add(new ComponentPin(names[i], names[i].Replace('_', ' '), i < 3 || i == 6 ? -20 : 20, (i % 3 - 1) * 14));
    }
}

/// <summary>Multi-winding-capable transformer with a persisted steady-state equivalent-circuit parameter set.</summary>
public sealed class ACTransformerComponent : PowerSystemComponent
{
    public override string ComponentType => "ACTransformer";
    public override string StableTypeId => "machine.ac-transformer";
    public override string SpicePrefix => "XTR";
    public override string Description => "Single-/three-phase transformer equivalent circuit";
    public double RatedPrimaryVoltage { get => Number(nameof(RatedPrimaryVoltage), 230); set => SetNumber(nameof(RatedPrimaryVoltage), value, 0.1); }
    public double RatedSecondaryVoltage { get => Number(nameof(RatedSecondaryVoltage), 115); set => SetNumber(nameof(RatedSecondaryVoltage), value, 0.1); }
    public double RatedApparentPower { get => Number(nameof(RatedApparentPower), 1000); set => SetNumber(nameof(RatedApparentPower), value, 0.1); }
    public int PrimaryTurns { get => (int)Number(nameof(PrimaryTurns), 1000); set => SetNumber(nameof(PrimaryTurns), value, 1); }
    public int SecondaryTurns { get => (int)Number(nameof(SecondaryTurns), 500); set => SetNumber(nameof(SecondaryTurns), value, 1); }
    public double Frequency { get => Number(nameof(Frequency), 50); set => SetNumber(nameof(Frequency), value, 0.01); }
    public double PrimaryResistance { get => Number(nameof(PrimaryResistance), 0.1); set => SetNumber(nameof(PrimaryResistance), value, 0); }
    public double SecondaryResistance { get => Number(nameof(SecondaryResistance), 0.05); set => SetNumber(nameof(SecondaryResistance), value, 0); }
    public double PrimaryLeakageInductance { get => Number(nameof(PrimaryLeakageInductance), 1e-3); set => SetNumber(nameof(PrimaryLeakageInductance), value, 0); }
    public double SecondaryLeakageInductance { get => Number(nameof(SecondaryLeakageInductance), 0.5e-3); set => SetNumber(nameof(SecondaryLeakageInductance), value, 0); }
    public double MagnetizingInductance { get => Number(nameof(MagnetizingInductance), 1); set => SetNumber(nameof(MagnetizingInductance), value, 1e-9); }
    public double CoreLossResistance { get => Number(nameof(CoreLossResistance), 10000); set => SetNumber(nameof(CoreLossResistance), value, 1e-6); }
    public string CoreType { get => Text(nameof(CoreType), "Laminated steel"); set => SetText(nameof(CoreType), value); }
    public double EfficiencyPercent { get => Number(nameof(EfficiencyPercent), 95); set => SetNumber(nameof(EfficiencyPercent), Math.Clamp(value, 0, 100)); }
    public bool DotsAligned { get => Flag(nameof(DotsAligned), true); set => SetFlag(nameof(DotsAligned), value); }
    public int NumberOfWindings { get => (int)Number(nameof(NumberOfWindings), 2); set => SetNumber(nameof(NumberOfWindings), Math.Clamp(value, 2, 8)); }
    public int NumberOfPhases { get => (int)Number(nameof(NumberOfPhases), 1); set { if (value is not (1 or 3)) throw new ArgumentOutOfRangeException(nameof(value), "Transformer phase count must be one or three."); SetNumber(nameof(NumberOfPhases), value); RebuildPins(); } }
    public string WindingConnection
    {
        get => Text(nameof(WindingConnection), "Single-phase");
        set
        {
            if (value is not ("Single-phase" or "Y-Y" or "Delta-Delta" or "Y-Delta" or "Delta-Y"))
                throw new ArgumentException("Winding connection must be Single-phase, Y-Y, Delta-Delta, Y-Delta, or Delta-Y.");
            SetText(nameof(WindingConnection), value);
            NumberOfPhases = value == "Single-phase" ? 1 : 3;
        }
    }
    public double SaturationFlux { get => Number(nameof(SaturationFlux), 1.5); set => SetNumber(nameof(SaturationFlux), value, 0.01); }
    public string TransformerType
    {
        get => Text(nameof(TransformerType), "TwoWinding");
        set
        {
            if (value is not ("StepUp" or "StepDown" or "Isolation" or "Autotransformer" or "SinglePhase" or "ThreePhase" or "MultiWinding" or "TwoWinding"))
                throw new ArgumentException("Unsupported transformer type.");
            SetText(nameof(TransformerType), value);
            if (value == "ThreePhase")
            {
                NumberOfPhases = 3;
                if (WindingConnection == "Single-phase") WindingConnection = "Y-Y";
            }
        }
    }
    public string VectorGroup { get => Text(nameof(VectorGroup), "Yy0"); set => SetText(nameof(VectorGroup), value); }
    public double PhaseDisplacementDegrees { get => Number(nameof(PhaseDisplacementDegrees), 0); set => SetNumber(nameof(PhaseDisplacementDegrees), value); }
    public double TurnsRatio => (double)PrimaryTurns / SecondaryTurns;
    public double PrimaryCurrent => RatedPrimaryVoltage > 0 ? RatedApparentPower / RatedPrimaryVoltage : 0;
    public double SecondaryCurrent => RatedSecondaryVoltage > 0 ? RatedApparentPower / RatedSecondaryVoltage : 0;
    public double CopperLosses => PrimaryCurrent * PrimaryCurrent * PrimaryResistance + SecondaryCurrent * SecondaryCurrent * SecondaryResistance;
    public double CoreLosses => RatedPrimaryVoltage * RatedPrimaryVoltage / CoreLossResistance;
    public double TotalLosses => CopperLosses + CoreLosses;
    public double VoltageRegulationPercent => RatedSecondaryVoltage <= 0 ? 0 : (1 - EfficiencyPercent / 100) * 100;
    public ACTransformerComponent()
    {
        RebuildPins();
    }
    private void RebuildPins()
    {
        Pins.Clear();
        if (NumberOfPhases == 1)
        {
            Pins.Add(new("primary_dot", "Primary •", -20, -10)); Pins.Add(new("primary", "Primary", -20, 10));
            Pins.Add(new("secondary_dot", "Secondary •", 20, -10)); Pins.Add(new("secondary", "Secondary", 20, 10));
            return;
        }
        string[] phases = { "A", "B", "C" };
        foreach (var phase in phases) Pins.Add(new ComponentPin($"primary_{phase}", $"Primary {phase}", -20, (Array.IndexOf(phases, phase) - 1) * 12));
        foreach (var phase in phases) Pins.Add(new ComponentPin($"secondary_{phase}", $"Secondary {phase}", 20, (Array.IndexOf(phases, phase) - 1) * 12));
        Pins.Add(new ComponentPin("primary_neutral", "Primary N", -20, 24));
        Pins.Add(new ComponentPin("secondary_neutral", "Secondary N", 20, 24));
    }
    public override void ValidateConfiguration()
    {
        if (NumberOfWindings > 2 && TransformerType != "MultiWinding") throw new InvalidOperationException("Select MultiWinding to use more than two windings.");
        if (NumberOfPhases == 3 && WindingConnection == "Single-phase") throw new InvalidOperationException("Choose a three-phase winding connection.");
        RebuildPins();
    }
}

/// <summary>Steady-state synchronous source represented by phase terminals and machine nameplate parameters.</summary>
public sealed class SynchronousGeneratorComponent : PowerSystemComponent
{
    public override string ComponentType => "SynchronousGenerator";
    public override string StableTypeId => "machine.synchronous-generator";
    public override string SpicePrefix => "GEN";
    public override string Description => "Steady-state synchronous generator source";
    public double RatedApparentPower { get => Number(nameof(RatedApparentPower), 10000); set => SetNumber(nameof(RatedApparentPower), value, 1); }
    public double RatedVoltage { get => Number(nameof(RatedVoltage), 400); set => SetNumber(nameof(RatedVoltage), value, 1); }
    public double RatedFrequency { get => Number(nameof(RatedFrequency), 50); set => SetNumber(nameof(RatedFrequency), value, 0.01); }
    public int NumberOfPhases { get => (int)Number(nameof(NumberOfPhases), 3); set => SetNumber(nameof(NumberOfPhases), Math.Clamp(value, 1, 3)); }
    public int NumberOfPoles { get => (int)Number(nameof(NumberOfPoles), 4); set => SetNumber(nameof(NumberOfPoles), Math.Max(value, 2)); }
    public double RotationalSpeedRpm { get => Number(nameof(RotationalSpeedRpm), 1500); set => SetNumber(nameof(RotationalSpeedRpm), value, 0); }
    public double PowerFactor { get => Number(nameof(PowerFactor), 0.8); set => SetNumber(nameof(PowerFactor), Math.Clamp(value, -1, 1)); }
    public double ArmatureResistance { get => Number(nameof(ArmatureResistance), 0.2); set => SetNumber(nameof(ArmatureResistance), value, 0); }
    public double Xd { get => Number(nameof(Xd), 1.5); set => SetNumber(nameof(Xd), value, 0); }
    public double Xq { get => Number(nameof(Xq), 1.0); set => SetNumber(nameof(Xq), value, 0); }
    public double XdTransient { get => Number(nameof(XdTransient), 0.3); set => SetNumber(nameof(XdTransient), value, 0); }
    public double XqTransient { get => Number(nameof(XqTransient), 0.5); set => SetNumber(nameof(XqTransient), value, 0); }
    public double XdSubtransient { get => Number(nameof(XdSubtransient), 0.2); set => SetNumber(nameof(XdSubtransient), value, 0); }
    public double XqSubtransient { get => Number(nameof(XqSubtransient), 0.2); set => SetNumber(nameof(XqSubtransient), value, 0); }
    public double InertiaConstant { get => Number(nameof(InertiaConstant), 3); set => SetNumber(nameof(InertiaConstant), value, 0); }
    public double DampingCoefficient { get => Number(nameof(DampingCoefficient), 0); set => SetNumber(nameof(DampingCoefficient), value, 0); }
    public double FieldExcitation { get => Number(nameof(FieldExcitation), 1); set => SetNumber(nameof(FieldExcitation), value, 0); }
    public double InitialRotorAngle { get => Number(nameof(InitialRotorAngle), 0); set => SetNumber(nameof(InitialRotorAngle), value); }
    public double MechanicalInputPower { get => Number(nameof(MechanicalInputPower), 8000); set => SetNumber(nameof(MechanicalInputPower), value, 0); }
    public string ConnectionType { get => Text(nameof(ConnectionType), "Star"); set => SetText(nameof(ConnectionType), value); }
    public string GroundingConfiguration { get => Text(nameof(GroundingConfiguration), "Grounded neutral"); set => SetText(nameof(GroundingConfiguration), value); }
    public string PhaseSequence { get => Text(nameof(PhaseSequence), "ABC"); set { if (value is not ("ABC" or "ACB")) throw new ArgumentException("Phase sequence must be ABC or ACB."); SetText(nameof(PhaseSequence), value); } }
    public double GeneratedEmf => RatedVoltage / Math.Sqrt(NumberOfPhases == 3 ? 3 : 1) * FieldExcitation;
    public double SynchronousSpeedRpm => 120 * RatedFrequency / NumberOfPoles;
    public double ActivePower => RatedApparentPower * Math.Abs(PowerFactor);
    public double ReactivePower => RatedApparentPower * Math.Sqrt(Math.Max(0, 1 - PowerFactor * PowerFactor));
    public double OutputCurrent => RatedVoltage > 0 ? RatedApparentPower / (NumberOfPhases == 3 ? Math.Sqrt(3) * RatedVoltage : RatedVoltage) : 0;
    public double PowerAngleDegrees => InitialRotorAngle;
    public double RotorSpeedRpm => RotationalSpeedRpm;
    public double FrequencyDeviation => SynchronousSpeedRpm <= 0 ? 0 : (RotationalSpeedRpm - SynchronousSpeedRpm) * NumberOfPoles / 120;
    public double ElectricalLosses => OutputCurrent * OutputCurrent * ArmatureResistance * NumberOfPhases;
    public double EfficiencyPercent => MechanicalInputPower <= 0 ? 0 : 100 * Math.Clamp(ActivePower / MechanicalInputPower, 0, 1);
    public SynchronousGeneratorComponent()
    {
        Pins.Add(new("A", "Phase A", -20, -14)); Pins.Add(new("B", "Phase B", -20, 0)); Pins.Add(new("C", "Phase C", -20, 14)); Pins.Add(new("N", "Neutral", 20, 0));
    }
}

/// <summary>Steady-state induction motor using nameplate equivalent impedance and torque/slip calculations.</summary>
public sealed class InductionMotorComponent : PowerSystemComponent
{
    public override string ComponentType => "InductionMotor";
    public override string StableTypeId => "machine.induction-motor";
    public override string SpicePrefix => "MTR";
    public override string Description => "Steady-state induction motor equivalent load";
    public double RatedVoltage { get => Number(nameof(RatedVoltage), 400); set => SetNumber(nameof(RatedVoltage), value, 1); }
    public double RatedPower { get => Number(nameof(RatedPower), 1500); set => SetNumber(nameof(RatedPower), value, 1); }
    public double Frequency { get => Number(nameof(Frequency), 50); set => SetNumber(nameof(Frequency), value, 0.01); }
    public int NumberOfPoles { get => (int)Number(nameof(NumberOfPoles), 4); set => SetNumber(nameof(NumberOfPoles), Math.Max(value, 2)); }
    public double RatedSpeedRpm { get => Number(nameof(RatedSpeedRpm), 1440); set => SetNumber(nameof(RatedSpeedRpm), value, 0); }
    public double StatorResistance { get => Number(nameof(StatorResistance), 0.5); set => SetNumber(nameof(StatorResistance), value, 0); }
    public double RotorResistance { get => Number(nameof(RotorResistance), 0.3); set => SetNumber(nameof(RotorResistance), value, 0); }
    public double StatorLeakageReactance { get => Number(nameof(StatorLeakageReactance), 1); set => SetNumber(nameof(StatorLeakageReactance), value, 0); }
    public double RotorLeakageReactance { get => Number(nameof(RotorLeakageReactance), 1); set => SetNumber(nameof(RotorLeakageReactance), value, 0); }
    public double MagnetizingReactance { get => Number(nameof(MagnetizingReactance), 25); set => SetNumber(nameof(MagnetizingReactance), value, 0.001); }
    public double Slip { get => Number(nameof(Slip), 0.04); set => SetNumber(nameof(Slip), Math.Clamp(value, 0, 1)); }
    public double PowerFactor { get => Number(nameof(PowerFactor), 0.82); set => SetNumber(nameof(PowerFactor), Math.Clamp(value, 0, 1)); }
    public double EfficiencyPercent { get => Number(nameof(EfficiencyPercent), 85); set => SetNumber(nameof(EfficiencyPercent), Math.Clamp(value, 0, 100)); }
    public double StartingCurrentMultiple { get => Number(nameof(StartingCurrentMultiple), 6); set => SetNumber(nameof(StartingCurrentMultiple), value, 1); }
    public double RatedTorque { get => Number(nameof(RatedTorque), 9.95); set => SetNumber(nameof(RatedTorque), value, 0); }
    public double LoadTorque { get => Number(nameof(LoadTorque), 9.95); set => SetNumber(nameof(LoadTorque), value, 0); }
    public double Inertia { get => Number(nameof(Inertia), 0.02); set => SetNumber(nameof(Inertia), value, 0); }
    public string ConnectionType { get => Text(nameof(ConnectionType), "Star"); set => SetText(nameof(ConnectionType), value); }
    public double SynchronousSpeedRpm => 120 * Frequency / NumberOfPoles;
    public double CalculatedSlip => SynchronousSpeedRpm <= 0 ? 0 : Math.Clamp((SynchronousSpeedRpm - RatedSpeedRpm) / SynchronousSpeedRpm, 0, 1);
    private Complex MagnetizingImpedance => new(0, MagnetizingReactance);
    private Complex RotorBranchImpedance => new(RotorResistance / Math.Max(Slip, 1e-4), RotorLeakageReactance);
    private Complex StatorImpedance => new(StatorResistance, StatorLeakageReactance);
    private Complex ParallelAirGapImpedance => MagnetizingImpedance * RotorBranchImpedance /
        (MagnetizingImpedance + RotorBranchImpedance);
    public Complex PerPhaseInputImpedance => StatorImpedance + ParallelAirGapImpedance;
    private double PhaseVoltage => RatedVoltage / Math.Sqrt(3);
    public double InputCurrent => PerPhaseInputImpedance.Magnitude <= 1e-12 ? 0 : PhaseVoltage / PerPhaseInputImpedance.Magnitude;
    public double CalculatedPowerFactor => PerPhaseInputImpedance.Magnitude <= 1e-12 ? 0 : Math.Clamp(Math.Cos(PerPhaseInputImpedance.Phase), 0, 1);
    public double InputPower => 3 * PhaseVoltage * InputCurrent * CalculatedPowerFactor;
    public double RotorCurrent
    {
        get
        {
            Complex impedance = PerPhaseInputImpedance;
            if (impedance.Magnitude <= 1e-12) return 0;
            Complex input = PhaseVoltage / impedance;
            Complex airGap = input * ParallelAirGapImpedance;
            return (airGap / RotorBranchImpedance).Magnitude;
        }
    }
    public double StartingCurrent => InputCurrent * StartingCurrentMultiple;
    public double MechanicalOutputPower => Math.Max(0, 3 * RotorCurrent * RotorCurrent * RotorResistance / Math.Max(Slip, 1e-4) * (1 - Slip));
    public double Losses => Math.Max(0, InputPower - MechanicalOutputPower);
    public double CalculatedTorque => SynchronousSpeedRpm <= 0 ? 0 : MechanicalOutputPower / Math.Max(2 * Math.PI * SynchronousSpeedRpm / 60 * (1 - Slip), 1e-6);
    public InductionMotorComponent() { Pins.Add(new("A", "Phase A", -20, -14)); Pins.Add(new("B", "Phase B", -20, 0)); Pins.Add(new("C", "Phase C", -20, 14)); Pins.Add(new("N", "Neutral", 20, 0)); }
}

/// <summary>Household AC bulb modeled as a switched resistive load with optional dimming.</summary>
public sealed class ACBulbComponent : PowerSystemComponent
{
    public override string ComponentType => "ACBulb";
    public override string StableTypeId => "load.ac-bulb";
    public override string SpicePrefix => "LAC";
    public override string Description => "Household AC resistive bulb load";
    public double RatedVoltage { get => Number(nameof(RatedVoltage), 230); set => SetNumber(nameof(RatedVoltage), value, 1); }
    public double RatedPower { get => Number(nameof(RatedPower), 60); set => SetNumber(nameof(RatedPower), value, 0.1); }
    public double Frequency { get => Number(nameof(Frequency), 50); set => SetNumber(nameof(Frequency), value, 0.01); }
    public double PowerFactor { get => Number(nameof(PowerFactor), 1); set => SetNumber(nameof(PowerFactor), Math.Clamp(value, 0, 1)); }
    public string LoadType { get => Text(nameof(LoadType), "Resistive"); set { if (value is not ("Resistive" or "Configurable")) throw new ArgumentException("Load type must be Resistive or Configurable."); SetText(nameof(LoadType), value); } }
    public int NumberOfBulbs { get => (int)Number(nameof(NumberOfBulbs), 1); set => SetNumber(nameof(NumberOfBulbs), Math.Max(value, 1)); }
    public bool InitiallyOn { get => Flag(nameof(InitiallyOn), true); set => SetFlag(nameof(InitiallyOn), value); }
    public double DimmingPercent { get => Number(nameof(DimmingPercent), 100); set => SetNumber(nameof(DimmingPercent), Math.Clamp(value, 0, 100)); }
    public string OperatingSchedule { get => Text(nameof(OperatingSchedule), "AlwaysOn"); set => SetText(nameof(OperatingSchedule), value); }
    public double EquivalentResistance => RatedPower <= 0 ? double.PositiveInfinity : RatedVoltage * RatedVoltage / (RatedPower * NumberOfBulbs * Math.Max(DimmingPercent / 100, 0.01));
    public double RmsCurrent => RatedVoltage <= 0 ? 0 : ActivePower / (RatedVoltage * Math.Max(PowerFactor, 0.01));
    public double EnergyConsumptionWh => Number(nameof(EnergyConsumptionWh), 0);
    public bool ScheduleActive => IsScheduleActive(OperatingSchedule, Number(DeviceTimeKey, 0));
    public double ActivePower => InitiallyOn && ScheduleActive ? RatedPower * NumberOfBulbs * DimmingPercent / 100 : 0;
    public double ApparentPower => PowerFactor <= 0 ? 0 : ActivePower / PowerFactor;
    public double ReactivePower => Math.Sqrt(Math.Max(0, ApparentPower * ApparentPower - ActivePower * ActivePower));
    public void AdvanceDeviceTime(double elapsedSeconds, double timeScale)
    {
        if (elapsedSeconds <= 0) return;
        double deviceTime = Number(DeviceTimeKey, 0) + elapsedSeconds * timeScale;
        ElectricalParameters[DeviceTimeKey] = deviceTime;
        ElectricalParameters[nameof(EnergyConsumptionWh)] = EnergyConsumptionWh + ActivePower * elapsedSeconds * timeScale / 3600;
    }
    public ACBulbComponent() { Pins.Add(new("L", "Line", -20, 0)); Pins.Add(new("N", "Neutral", 20, 0)); }
}

/// <summary>Household AC fan represented by a rated electrical load and motor efficiency metadata.</summary>
public sealed class ACFanComponent : PowerSystemComponent
{
    public override string ComponentType => "ACFan";
    public override string StableTypeId => "load.ac-fan";
    public override string SpicePrefix => "FAN";
    public override string Description => "Household AC motor fan load";
    public double RatedVoltage { get => Number(nameof(RatedVoltage), 230); set => SetNumber(nameof(RatedVoltage), value, 1); }
    public double RatedPower { get => Number(nameof(RatedPower), 75); set => SetNumber(nameof(RatedPower), value, 0.1); }
    public double Frequency { get => Number(nameof(Frequency), 50); set => SetNumber(nameof(Frequency), value, 0.01); }
    public double RatedCurrent { get => Number(nameof(RatedCurrent), 0.5); set => SetNumber(nameof(RatedCurrent), value, 0); }
    public double PowerFactor { get => Number(nameof(PowerFactor), 0.8); set => SetNumber(nameof(PowerFactor), Math.Clamp(value, 0, 1)); }
    public string MotorType { get => Text(nameof(MotorType), "Induction"); set => SetText(nameof(MotorType), value); }
    public int NumberOfSpeedSettings { get => (int)Number(nameof(NumberOfSpeedSettings), 3); set => SetNumber(nameof(NumberOfSpeedSettings), Math.Clamp(value, 1, 10)); }
    public int SelectedSpeed { get => (int)Number(nameof(SelectedSpeed), 3); set => SetNumber(nameof(SelectedSpeed), Math.Clamp(value, 0, NumberOfSpeedSettings)); }
    public double MotorEfficiencyPercent { get => Number(nameof(MotorEfficiencyPercent), 75); set => SetNumber(nameof(MotorEfficiencyPercent), Math.Clamp(value, 1, 100)); }
    public bool InitiallyOn { get => Flag(nameof(InitiallyOn), true); set => SetFlag(nameof(InitiallyOn), value); }
    public double MechanicalLoad { get => Number(nameof(MechanicalLoad), 1); set => SetNumber(nameof(MechanicalLoad), Math.Clamp(value, 0, 2)); }
    public string OperatingSchedule { get => Text(nameof(OperatingSchedule), "AlwaysOn"); set => SetText(nameof(OperatingSchedule), value); }
    public double EnergyConsumptionWh => Number(nameof(EnergyConsumptionWh), 0);
    public bool ScheduleActive => IsScheduleActive(OperatingSchedule, Number(DeviceTimeKey, 0));
    public double ElectricalInputPower => InitiallyOn && ScheduleActive ? RatedPower * SelectedSpeed / NumberOfSpeedSettings : 0;
    public double CalculatedCurrent => RatedVoltage <= 0 ? 0 : ElectricalInputPower / (RatedVoltage * Math.Max(PowerFactor, 0.01));
    public double MechanicalOutputPower => ElectricalInputPower * MotorEfficiencyPercent / 100 * MechanicalLoad;
    public double MotorLosses => Math.Max(0, ElectricalInputPower - MechanicalOutputPower);
    public void AdvanceDeviceTime(double elapsedSeconds, double timeScale)
    {
        if (elapsedSeconds <= 0) return;
        double deviceTime = Number(DeviceTimeKey, 0) + elapsedSeconds * timeScale;
        ElectricalParameters[DeviceTimeKey] = deviceTime;
        ElectricalParameters[nameof(EnergyConsumptionWh)] = EnergyConsumptionWh + ElectricalInputPower * elapsedSeconds * timeScale / 3600;
    }
    public ACFanComponent() { Pins.Add(new("L", "Line", -20, 0)); Pins.Add(new("N", "Neutral", 20, 0)); }
}

/// <summary>Single-diode PV panel with temperature/irradiance adjusted operating-point estimates.</summary>
public sealed class SolarPanelComponent : PowerSystemComponent
{
    public override string ComponentType => "SolarPanel";
    public override string StableTypeId => "source.solar-panel";
    public override string SpicePrefix => "PV";
    public override string Description => "Photovoltaic single-diode panel source";
    public double MaximumPowerRating { get => Number(nameof(MaximumPowerRating), 300); set => SetNumber(nameof(MaximumPowerRating), value, 0.1); }
    public double OpenCircuitVoltage { get => Number(nameof(OpenCircuitVoltage), 40); set => SetNumber(nameof(OpenCircuitVoltage), value, 0.1); }
    public double ShortCircuitCurrent { get => Number(nameof(ShortCircuitCurrent), 9); set => SetNumber(nameof(ShortCircuitCurrent), value, 0.01); }
    public double VoltageAtMaximumPower { get => Number(nameof(VoltageAtMaximumPower), 32); set => SetNumber(nameof(VoltageAtMaximumPower), value, 0.1); }
    public double CurrentAtMaximumPower { get => Number(nameof(CurrentAtMaximumPower), 9.375); set => SetNumber(nameof(CurrentAtMaximumPower), value, 0.01); }
    public double Pmax { get => Number(nameof(Pmax), 300); set => SetNumber(nameof(Pmax), value, 0.1); }
    public int NumberOfCells { get => (int)Number(nameof(NumberOfCells), 60); set => SetNumber(nameof(NumberOfCells), Math.Max(value, 1)); }
    public int SeriesCells { get => (int)Number(nameof(SeriesCells), 60); set => SetNumber(nameof(SeriesCells), Math.Max(value, 1)); }
    public int ParallelStrings { get => (int)Number(nameof(ParallelStrings), 1); set => SetNumber(nameof(ParallelStrings), Math.Max(value, 1)); }
    public double VocTemperatureCoefficient { get => Number(nameof(VocTemperatureCoefficient), -0.003); set => SetNumber(nameof(VocTemperatureCoefficient), value); }
    public double IscTemperatureCoefficient { get => Number(nameof(IscTemperatureCoefficient), 0.0005); set => SetNumber(nameof(IscTemperatureCoefficient), value); }
    public double PowerTemperatureCoefficient { get => Number(nameof(PowerTemperatureCoefficient), -0.004); set => SetNumber(nameof(PowerTemperatureCoefficient), value); }
    public double CellTemperature { get => Number(nameof(CellTemperature), 25); set => SetNumber(nameof(CellTemperature), value, -273.15); }
    public double AmbientTemperature { get => Number(nameof(AmbientTemperature), 25); set => SetNumber(nameof(AmbientTemperature), value, -273.15); }
    public double SolarIrradiance { get => Number(nameof(SolarIrradiance), 1000); set => SetNumber(nameof(SolarIrradiance), value, 0); }
    public double PanelEfficiencyPercent { get => Number(nameof(PanelEfficiencyPercent), 20); set => SetNumber(nameof(PanelEfficiencyPercent), Math.Clamp(value, 0, 100)); }
    public double SeriesResistance { get => Number(nameof(SeriesResistance), 0.3); set => SetNumber(nameof(SeriesResistance), value, 0); }
    public double ShuntResistance { get => Number(nameof(ShuntResistance), 500); set => SetNumber(nameof(ShuntResistance), value, 1e-6); }
    public double InitialVoltage { get => Number(nameof(InitialVoltage), 0); set => SetNumber(nameof(InitialVoltage), value, 0); }
    /// <summary>Optional semicolon-separated device-time,irradiance pairs, for example 0,0;3600,800;7200,0.</summary>
    public string IrradianceProfile { get => Text(nameof(IrradianceProfile), string.Empty); set { CustomProperties[nameof(IrradianceProfile)] = value ?? string.Empty; NotifyModified(nameof(IrradianceProfile)); } }
    /// <summary>Optional semicolon-separated device-time,temperature-C pairs.</summary>
    public string TemperatureProfile { get => Text(nameof(TemperatureProfile), string.Empty); set { CustomProperties[nameof(TemperatureProfile)] = value ?? string.Empty; NotifyModified(nameof(TemperatureProfile)); } }
    public double TemperatureAdjustedVoc => Math.Max(0, OpenCircuitVoltage * (1 + VocTemperatureCoefficient * (CellTemperature - 25)) * SeriesCells / Math.Max(NumberOfCells, 1));
    public double IrradianceAdjustedIsc => ShortCircuitCurrent * SolarIrradiance / 1000 * (1 + IscTemperatureCoefficient * (CellTemperature - 25)) * ParallelStrings;
    public double OutputVoltage => Math.Min(TemperatureAdjustedVoc, VoltageAtMaximumPower * SeriesCells / Math.Max(NumberOfCells, 1));
    public double OutputCurrent => Math.Max(0, Math.Min(IrradianceAdjustedIsc, CurrentAtMaximumPower * SolarIrradiance / 1000 * ParallelStrings));
    public double OutputPower => Math.Min(Pmax * SolarIrradiance / 1000 * ParallelStrings * (1 + PowerTemperatureCoefficient * (CellTemperature - 25)), OutputVoltage * OutputCurrent);
    public double ConversionEfficiencyPercent => PanelEfficiencyPercent;
    public IReadOnlyList<(double Time, double Irradiance)> IrradianceProfilePoints => ParseProfile(IrradianceProfile);
    public IReadOnlyList<(double Time, double Temperature)> TemperatureProfilePoints => ParseProfile(TemperatureProfile);
    public double PhotocurrentAtDeviceTime(double deviceTimeSeconds)
    {
        double irradiance = InterpolateProfile(IrradianceProfile, deviceTimeSeconds) ?? SolarIrradiance;
        double temperature = InterpolateProfile(TemperatureProfile, deviceTimeSeconds) ?? CellTemperature;
        return Math.Max(0, ShortCircuitCurrent * irradiance / 1000 * ParallelStrings *
            Math.Max(0, 1 + IscTemperatureCoefficient * (temperature - 25)));
    }
    public void ApplyIrradianceProfile(double deviceTimeSeconds)
    {
        double? irradiance = InterpolateProfile(IrradianceProfile, deviceTimeSeconds);
        if (irradiance.HasValue) SolarIrradiance = irradiance.Value;
        double? temperature = InterpolateProfile(TemperatureProfile, deviceTimeSeconds);
        if (temperature.HasValue) CellTemperature = temperature.Value;
    }

    private static double? InterpolateProfile(string profile, double deviceTimeSeconds)
    {
        var points = ParseProfile(profile);
        if (points.Length == 0) return null;
        if (deviceTimeSeconds <= points[0].Time) return points[0].Irradiance;
        for (int index = 1; index < points.Length; index++)
        {
            if (deviceTimeSeconds > points[index].Time) continue;
            var before = points[index - 1];
            var after = points[index];
            double fraction = (deviceTimeSeconds - before.Time) / Math.Max(after.Time - before.Time, 1e-12);
            return before.Irradiance + (after.Irradiance - before.Irradiance) * fraction;
        }
        return points[^1].Irradiance;
    }

    private static (double Time, double Irradiance)[] ParseProfile(string profile) =>
        string.IsNullOrWhiteSpace(profile)
            ? Array.Empty<(double Time, double Irradiance)>()
            : profile.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split(',', StringSplitOptions.TrimEntries))
                .Where(parts => parts.Length == 2 && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) &&
                                double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                .Select(parts => (Time: double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), Irradiance: double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)))
                .OrderBy(point => point.Time).ToArray();
    public SolarPanelComponent() { Pins.Add(new("positive", "+", -20, 0)); Pins.Add(new("negative", "−", 20, 0)); }
}

/// <summary>Dynamic battery with SOC-limited open-circuit voltage and equivalent series resistance.</summary>
public sealed class BatteryComponent : PowerSystemComponent
{
    public override string ComponentType => "Battery";
    public override string StableTypeId => "storage.battery";
    public override string SpicePrefix => "BAT";
    public override string Description => "Dynamic battery equivalent circuit with SOC, efficiency, and current limits";
    public double NominalVoltage { get => Number(nameof(NominalVoltage), 12); set => SetNumber(nameof(NominalVoltage), value, 0.1); }
    public double MaximumVoltage { get => Number(nameof(MaximumVoltage), 14.4); set => SetNumber(nameof(MaximumVoltage), value, 0.1); }
    public double MinimumVoltage { get => Number(nameof(MinimumVoltage), 10.5); set => SetNumber(nameof(MinimumVoltage), value, 0.1); }
    public double InitialVoltage { get => Number(nameof(InitialVoltage), 12.6); set => SetNumber(nameof(InitialVoltage), value, 0); }
    public double InternalResistance { get => Number(nameof(InternalResistance), 0.05); set => SetNumber(nameof(InternalResistance), value, 1e-6); }
    public double MaximumDischargeCurrent { get => Number(nameof(MaximumDischargeCurrent), 20); set => SetNumber(nameof(MaximumDischargeCurrent), value, 0); }
    public double MaximumChargeCurrent { get => Number(nameof(MaximumChargeCurrent), 10); set => SetNumber(nameof(MaximumChargeCurrent), value, 0); }
    public double MaximumDischargePower { get => Number(nameof(MaximumDischargePower), 240); set => SetNumber(nameof(MaximumDischargePower), value, 0); }
    public double MaximumChargePower { get => Number(nameof(MaximumChargePower), 120); set => SetNumber(nameof(MaximumChargePower), value, 0); }
    public double CapacityAh { get => Number(nameof(CapacityAh), 100); set => SetNumber(nameof(CapacityAh), value, 0.01); }
    public double CapacityMah { get => CapacityAh * 1000; set => CapacityAh = value / 1000; }
    public double NominalEnergyWh { get => Number(nameof(NominalEnergyWh), 1200); set => SetNumber(nameof(NominalEnergyWh), value, 0.1); }
    public double InitialSocPercent { get => Number(nameof(InitialSocPercent), 80); set => SetNumber(nameof(InitialSocPercent), Math.Clamp(value, 0, 100)); }
    public double MinimumSocPercent { get => Number(nameof(MinimumSocPercent), 10); set => SetNumber(nameof(MinimumSocPercent), Math.Clamp(value, 0, 100)); }
    public double MaximumSocPercent { get => Number(nameof(MaximumSocPercent), 100); set => SetNumber(nameof(MaximumSocPercent), Math.Clamp(value, 0, 100)); }
    public double DepthOfDischargePercent { get => Number(nameof(DepthOfDischargePercent), 90); set => SetNumber(nameof(DepthOfDischargePercent), Math.Clamp(value, 0, 100)); }
    public double ChargeEfficiencyPercent { get => Number(nameof(ChargeEfficiencyPercent), 95); set => SetNumber(nameof(ChargeEfficiencyPercent), Math.Clamp(value, 1, 100)); }
    public double DischargeEfficiencyPercent { get => Number(nameof(DischargeEfficiencyPercent), 95); set => SetNumber(nameof(DischargeEfficiencyPercent), Math.Clamp(value, 1, 100)); }
    public string Chemistry { get => Text(nameof(Chemistry), "Lithium-ion"); set { if (value is not ("Lithium-ion" or "LiFePO4" or "Lead-acid" or "NiMH" or "Custom")) throw new ArgumentException("Unsupported battery chemistry."); SetText(nameof(Chemistry), value); } }
    /// <summary>Optional semicolon-separated SOC-percent,voltage pairs, e.g. 0,10.5;50,12.1;100,14.4.</summary>
    public string VoltageSocCurve { get => Text(nameof(VoltageSocCurve), string.Empty); set { CustomProperties[nameof(VoltageSocCurve)] = value ?? string.Empty; NotifyModified(nameof(VoltageSocCurve)); } }
    public double InitialTemperature { get => Number(nameof(InitialTemperature), 25); set => SetNumber(nameof(InitialTemperature), value, -273.15); }
    public double AmbientTemperature { get => Number(nameof(AmbientTemperature), 25); set => SetNumber(nameof(AmbientTemperature), value, -273.15); }
    public double ThermalCapacity { get => Number(nameof(ThermalCapacity), 5000); set => SetNumber(nameof(ThermalCapacity), value, 1); }
    public double ThermalResistance { get => Number(nameof(ThermalResistance), 1); set => SetNumber(nameof(ThermalResistance), value, 0.001); }
    public double MinimumTemperature { get => Number(nameof(MinimumTemperature), -20); set => SetNumber(nameof(MinimumTemperature), value, -273.15); }
    public double MaximumTemperature { get => Number(nameof(MaximumTemperature), 60); set => SetNumber(nameof(MaximumTemperature), value, -273.15); }
    public double ResistanceTemperatureCoefficient { get => Number(nameof(ResistanceTemperatureCoefficient), 0.003); set => SetNumber(nameof(ResistanceTemperatureCoefficient), value); }
    public double CapacityTemperatureCoefficient { get => Number(nameof(CapacityTemperatureCoefficient), 0.001); set => SetNumber(nameof(CapacityTemperatureCoefficient), value); }
    public double InitialHealthPercent { get => Number(nameof(InitialHealthPercent), 100); set => SetNumber(nameof(InitialHealthPercent), Math.Clamp(value, 1, 100)); }
    public double EquivalentFullCycles => Number(nameof(EquivalentFullCycles), 0);
    public double StateOfHealthPercent => Math.Clamp(InitialHealthPercent - EquivalentFullCycles * CapacityDegradationCoefficient, 1, 100);
    public double CycleLifeEstimate { get => Number(nameof(CycleLifeEstimate), 1000); set => SetNumber(nameof(CycleLifeEstimate), value, 1); }
    public double CapacityDegradationCoefficient { get => Number(nameof(CapacityDegradationCoefficient), 0); set => SetNumber(nameof(CapacityDegradationCoefficient), value, 0); }
    public double ResistanceGrowthCoefficient { get => Number(nameof(ResistanceGrowthCoefficient), 0); set => SetNumber(nameof(ResistanceGrowthCoefficient), value, 0); }
    public string InitialState { get => Text(nameof(InitialState), "Connected"); set { if (value is not ("Connected" or "Disconnected")) throw new ArgumentException("State must be Connected or Disconnected."); SetText(nameof(InitialState), value); } }
    public string OperatingMode { get => Text(nameof(OperatingMode), "Automatic"); set { if (value is not ("Charging" or "Discharging" or "Idle" or "Automatic")) throw new ArgumentException("Unsupported battery operating mode."); SetText(nameof(OperatingMode), value); } }
    public string OperatingSchedule { get => Text(nameof(OperatingSchedule), "AlwaysOn"); set => SetText(nameof(OperatingSchedule), value); }
    public bool ExternalControlEnabled { get => Flag(nameof(ExternalControlEnabled), false); set => SetFlag(nameof(ExternalControlEnabled), value); }
    public bool UseGlobalTimeScaling { get => Flag(nameof(UseGlobalTimeScaling), true); set => SetFlag(nameof(UseGlobalTimeScaling), value); }
    public double CustomTimeScalingRatio { get => Number(nameof(CustomTimeScalingRatio), 1); set => SetNumber(nameof(CustomTimeScalingRatio), value, 1e-9); }
    public double StateOfChargePercent { get => Number(nameof(StateOfChargePercent), InitialSocPercent); set => SetNumber(nameof(StateOfChargePercent), Math.Clamp(value, MinimumSocPercent, MaximumSocPercent)); }
    public double TemperatureAdjustedCapacityAh => CapacityAh * StateOfHealthPercent / 100 * Math.Clamp(1 + CapacityTemperatureCoefficient * (BatteryTemperature - 25), 0.1, 1.2);
    public double RemainingCapacityAh => TemperatureAdjustedCapacityAh * StateOfChargePercent / 100;
    public double RemainingEnergyWh => NominalEnergyWh * StateOfHealthPercent / 100 * StateOfChargePercent / 100;
    public double OpenCircuitVoltage
    {
        get
        {
            if (!ElectricalParameters.ContainsKey(nameof(StateOfChargePercent)))
                return Math.Clamp(InitialVoltage, MinimumVoltage, MaximumVoltage);
            var points = VoltageSocCurve.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split(',', StringSplitOptions.TrimEntries))
                .Where(parts => parts.Length == 2 && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) &&
                                double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                .Select(parts => (Soc: double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), Voltage: double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)))
                .OrderBy(point => point.Soc).ToArray();
            if (points.Length > 0)
            {
                double soc = StateOfChargePercent;
                if (soc <= points[0].Soc) return Math.Clamp(points[0].Voltage, MinimumVoltage, MaximumVoltage);
                for (int index = 1; index < points.Length; index++)
                {
                    if (soc > points[index].Soc) continue;
                    var lower = points[index - 1];
                    var upper = points[index];
                    double ratio = (soc - lower.Soc) / Math.Max(upper.Soc - lower.Soc, 1e-12);
                    return Math.Clamp(lower.Voltage + (upper.Voltage - lower.Voltage) * ratio, MinimumVoltage, MaximumVoltage);
                }
                return Math.Clamp(points[^1].Voltage, MinimumVoltage, MaximumVoltage);
            }
            double curve = Chemistry switch
            {
                "LiFePO4" => Math.Pow(Math.Clamp(StateOfChargePercent / 100, 0, 1), 0.35),
                "Lead-acid" => Math.Pow(Math.Clamp(StateOfChargePercent / 100, 0, 1), 0.75),
                "NiMH" => Math.Pow(Math.Clamp(StateOfChargePercent / 100, 0, 1), 0.55),
                _ => Math.Clamp(StateOfChargePercent / 100, 0, 1)
            };
            return MinimumVoltage + (MaximumVoltage - MinimumVoltage) * curve;
        }
    }
    public double TerminalVoltage => Math.Clamp(OpenCircuitVoltage - LimitedCurrent * TemperatureAdjustedResistance, MinimumVoltage, MaximumVoltage);
    public double BatteryCurrent => LiveCurrent;
    public double ChargingCurrent => Math.Max(0, -LiveCurrent);
    public double DischargingCurrent => Math.Max(0, LiveCurrent);
    public double TemperatureAdjustedResistance => InternalResistance * Math.Max(0.1, 1 + ResistanceTemperatureCoefficient * (BatteryTemperature - 25));
    public double InternalLosses => LiveCurrent * LiveCurrent * TemperatureAdjustedResistance;
    public double BatteryTemperature => Number(nameof(BatteryTemperature), InitialTemperature);
    public double ChargeDischargeEfficiencyPercent => LiveCurrent < 0 ? ChargeEfficiencyPercent : DischargeEfficiencyPercent;
    public double EstimatedRemainingOperatingTimeHours => DischargingCurrent <= 1e-9 ? double.PositiveInfinity : RemainingCapacityAh / DischargingCurrent;
    public double LimitedCurrent
    {
        get
        {
            double chargeLimit = Math.Min(MaximumChargeCurrent,
                MaximumChargePower > 0 ? MaximumChargePower / Math.Max(OpenCircuitVoltage, 1e-6) : MaximumChargeCurrent);
            double dischargeLimit = Math.Min(MaximumDischargeCurrent,
                MaximumDischargePower > 0 ? MaximumDischargePower / Math.Max(OpenCircuitVoltage, 1e-6) : MaximumDischargeCurrent);
            return OperatingMode switch
            {
                "Idle" => 0,
                "Charging" => Math.Clamp(LiveCurrent, -chargeLimit, 0),
                "Discharging" => Math.Clamp(LiveCurrent, 0, dischargeLimit),
                _ => Math.Clamp(LiveCurrent, -chargeLimit, dischargeLimit)
            };
        }
    }
    public void AdvanceDeviceTime(double elapsedSeconds, double timeScale)
    {
        if (elapsedSeconds <= 0) return;
        double effectiveSeconds = elapsedSeconds * (UseGlobalTimeScaling ? timeScale : CustomTimeScalingRatio);
        double deviceTime = Number(DeviceTimeKey, 0) + effectiveSeconds;
        ElectricalParameters[DeviceTimeKey] = deviceTime;
        if (InitialState != "Connected" || !IsScheduleActive(OperatingSchedule, deviceTime)) return;
        double current = LimitedCurrent;
        double deltaAh = current >= 0
            ? -current * effectiveSeconds / 3600 / Math.Max(DischargeEfficiencyPercent / 100, 1e-6)
            : -current * effectiveSeconds / 3600 * ChargeEfficiencyPercent / 100;
        double maximum = MaximumSocPercent;
        if (DepthOfDischargePercent > 0) maximum = Math.Min(maximum, 100);
        StateOfChargePercent = Math.Clamp(StateOfChargePercent + deltaAh / Math.Max(CapacityAh * InitialHealthPercent / 100, 1e-9) * 100,
            MinimumSocPercent, maximum);
        ElectricalParameters[nameof(EquivalentFullCycles)] = EquivalentFullCycles +
            Math.Abs(current) * effectiveSeconds / 3600 / Math.Max(2 * CapacityAh * InitialHealthPercent / 100, 1e-9);
        double thermalEquilibrium = AmbientTemperature + InternalLosses * ThermalResistance;
        double thermalAlpha = 1 - Math.Exp(-effectiveSeconds / Math.Max(ThermalCapacity * ThermalResistance, 1e-6));
        ElectricalParameters[nameof(BatteryTemperature)] = Math.Clamp(
            BatteryTemperature + (thermalEquilibrium - BatteryTemperature) * thermalAlpha,
            MinimumTemperature, MaximumTemperature);
    }
    public BatteryComponent() { Pins.Add(new("positive", "+", -20, 0)); Pins.Add(new("negative", "−", 20, 0)); Pins.Add(new("control", "Enable", 0, 20)); }
    public override void ValidateConfiguration() { if (MinimumVoltage >= MaximumVoltage) throw new InvalidOperationException("Minimum battery voltage must be below maximum voltage."); if (MinimumSocPercent > MaximumSocPercent) throw new InvalidOperationException("Minimum SOC must not exceed maximum SOC."); }
}
