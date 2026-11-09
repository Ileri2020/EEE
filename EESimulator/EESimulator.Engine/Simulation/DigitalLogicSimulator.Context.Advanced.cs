using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EESimulator.Core.Components.Base;
using EESimulator.Core.Components.Electrical;
using EESimulator.Core.Components.Electronics;
using EESimulator.Core.Electrical;
using EESimulator.Core.Models;

namespace EESimulator.Engine.Simulation;

public static partial class DigitalLogicSimulator
{
    private sealed partial class Context
    {
        private void EvaluateNamedGate(ComponentNode gate)
        {
            bool a = Read(gate, "in1"), b = Read(gate, "in2");
            bool result = gate.ComponentType switch
            {
                "AND" => a && b,
                "OR" => a || b,
                "NOT" => !a,
                "NAND" => !(a && b),
                "NOR" => !(a || b),
                "XOR" => a ^ b,
                "XNOR" => !(a ^ b),
                _ => false
            };
            Set(gate, "out", result, 5, 0);
        }

        private bool TtlRead(TtlPackageComponent ttl, int pin) => Read(ttl, $"pin{pin}");

        private void EvaluateTtlCombinational(TtlPackageComponent ttl)
        {
            foreach (var gate in TtlPinCatalog.GetGates(ttl.PartNumber))
            {
                bool[] inputs = gate.Inputs.Select(pin => TtlRead(ttl, pin)).ToArray();
                bool value = gate.Operation switch
                {
                    "AND" => inputs.All(input => input),
                    "OR" => inputs.Any(input => input),
                    "NAND" => !inputs.All(input => input),
                    "NOR" => !inputs.Any(input => input),
                    "NOT" => !inputs[0],
                    "BUF" => inputs[0],
                    "XOR" => inputs.Aggregate(false, (current, input) => current ^ input),
                    "XNOR" => !inputs.Aggregate(false, (current, input) => current ^ input),
                    "AO22" => (inputs[0] && inputs[1]) || (inputs[2] && inputs[3]),
                    "AOI22" => !((inputs[0] && inputs[1]) || (inputs[2] && inputs[3])),
                    "AO32" => (inputs[0] && inputs[1] && inputs[2]) || (inputs[3] && inputs[4] && inputs[5]),
                    _ => false
                };
                if ((ttl.PartNumber is "74266" or "747266") && value) SetHighZ(ttl, $"pin{gate.Output}");
                else Set(ttl, $"pin{gate.Output}", value, 5, 0);
            }

            switch (ttl.PartNumber)
            {
                case "74138":
                    int selected = (TtlRead(ttl, 1) ? 1 : 0) | (TtlRead(ttl, 2) ? 2 : 0) | (TtlRead(ttl, 3) ? 4 : 0);
                    bool enabled = TtlRead(ttl, 6) && !TtlRead(ttl, 4) && !TtlRead(ttl, 5);
                    int[] outputs = { 15, 14, 13, 12, 11, 10, 9, 7 };
                    for (int i = 0; i < outputs.Length; i++) SetTtl(ttl, outputs[i], !enabled || selected != i);
                    break;
                case "74139":
                    EvaluateTtlDecoder2(ttl, 1, 2, 3, new[] { 4, 5, 6, 7 });
                    EvaluateTtlDecoder2(ttl, 15, 14, 13, new[] { 12, 11, 10, 9 });
                    break;
                case "74151":
                    int mux = (TtlRead(ttl, 11) ? 1 : 0) | (TtlRead(ttl, 10) ? 2 : 0) | (TtlRead(ttl, 9) ? 4 : 0);
                    int[] muxInputs = { 4, 3, 2, 1, 15, 14, 13, 12 };
                    bool data = TtlRead(ttl, muxInputs[mux]);
                    SetTtl(ttl, 5, !TtlRead(ttl, 7) && data);
                    SetTtl(ttl, 6, TtlRead(ttl, 7) || !data);
                    break;
                case "74153":
                    EvaluateTtlMux4(ttl, 1, 14, 2, new[] { 6, 5, 4, 3 }, 7);
                    EvaluateTtlMux4(ttl, 15, 14, 2, new[] { 10, 11, 12, 13 }, 9);
                    break;
                case "74157":
                    int[] inA = { 2, 5, 10, 13 }, inB = { 3, 6, 11, 14 }, outPins = { 4, 7, 9, 12 };
                    bool selectB = TtlRead(ttl, 1), disabled = TtlRead(ttl, 15);
                    for (int i = 0; i < 4; i++) SetTtl(ttl, outPins[i], !disabled && TtlRead(ttl, selectB ? inB[i] : inA[i]));
                    break;
                case "7442":
                case "7443":
                case "7444":
                    int encoded = (TtlRead(ttl, 15) ? 1 : 0) | (TtlRead(ttl, 14) ? 2 : 0) | (TtlRead(ttl, 13) ? 4 : 0) | (TtlRead(ttl, 12) ? 8 : 0);
                    int decoded = ttl.PartNumber switch
                    {
                        "7442" => encoded < 10 ? encoded : -1,
                        // 7443 decodes the excess-3 code (decimal 0 is encoded as 3).
                        "7443" => encoded is >= 3 and <= 12 ? encoded - 3 : -1,
                        // 7444 decodes the 4-bit Gray code into decimal digits.
                        "7444" => encoded switch
                        {
                            2 => 0, 6 => 1, 7 => 2, 5 => 3, 4 => 4,
                            12 => 5, 13 => 6, 15 => 7, 14 => 8, 10 => 9,
                            _ => -1
                        },
                        _ => -1
                    };
                    int[] bcdOutputs = { 1, 2, 3, 4, 5, 6, 7, 9, 10, 11 };
                    for (int i = 0; i < bcdOutputs.Length; i++) SetTtl(ttl, bcdOutputs[i], decoded != i);
                    break;
                case "7447":
                    EvaluateTtl7447(ttl);
                    break;
                case "7485":
                    EvaluateTtl7485(ttl);
                    break;
                case "74283":
                    EvaluateTtl74283(ttl);
                    break;
                case "74240":
                case "74241":
                case "74244":
                case "74541":
                    EvaluateTtlOctalBuffer(ttl);
                    break;
                case "74245":
                    EvaluateTtlTransceiver245(ttl);
                    break;
                case "7454":
                    SetTtl(ttl, 8, !((TtlRead(ttl, 1) && TtlRead(ttl, 13)) || (TtlRead(ttl, 2) && TtlRead(ttl, 3)) || (TtlRead(ttl, 4) && TtlRead(ttl, 5)) || (TtlRead(ttl, 9) && TtlRead(ttl, 10))));
                    break;
                case "7464":
                    SetTtl(ttl, 8, !((TtlRead(ttl, 2) && TtlRead(ttl, 3)) || (TtlRead(ttl, 4) && TtlRead(ttl, 5) && TtlRead(ttl, 6)) || (TtlRead(ttl, 9) && TtlRead(ttl, 10)) || (TtlRead(ttl, 11) && TtlRead(ttl, 12) && TtlRead(ttl, 13) && TtlRead(ttl, 1))));
                    break;
                case "7487":
                    EvaluateTtl7487(ttl);
                    break;
                case "74381":
                    EvaluateTtl74381(ttl);
                    break;
            }
        }

        private void EvaluateTtl7487(TtlPackageComponent ttl)
        {
            bool controlB = TtlRead(ttl, 8);
            bool controlC = TtlRead(ttl, 1);
            int[] inputs = { 2, 5, 10, 13 };
            int[] outputs = { 3, 6, 9, 12 };
            for (int bit = 0; bit < inputs.Length; bit++)
            {
                bool value = controlB ? !controlC : TtlRead(ttl, inputs[bit]) ^ !controlC;
                SetTtl(ttl, outputs[bit], value);
            }
        }

        private void EvaluateTtl74381(TtlPackageComponent ttl)
        {
            bool[] a = { TtlRead(ttl, 3), TtlRead(ttl, 1), TtlRead(ttl, 19), TtlRead(ttl, 17) };
            bool[] b = { TtlRead(ttl, 4), TtlRead(ttl, 2), TtlRead(ttl, 18), TtlRead(ttl, 16) };
            bool[] select = { TtlRead(ttl, 5), TtlRead(ttl, 6), TtlRead(ttl, 7) };
            bool u = select[0] || select[1];
            bool v = select[1] || select[2];
            bool w = select[0] || !select[1];
            bool x = !(select[0] && select[1]) || select[2];
            bool y = (select[0] && select[1]) || !select[2];
            bool z = !select[2] && u;
            bool ci = TtlRead(ttl, 15);
            bool[] level1 = a.Concat(a.Select(value => !value))
                .Concat(b).Concat(b.Select(value => !value))
                .Concat(new[] { y, x, w, v, u }).ToArray();
            bool[] j =
            {
                Pal(level1, 0x000c1010, 0x00161001, 0x000a0110),
                Pal(level1, 0x000c2020, 0x00162002, 0x000a0220),
                Pal(level1, 0x000c4040, 0x00164004, 0x000a0440),
                Pal(level1, 0x000c8080, 0x00168008, 0x000a0880)
            };
            bool[] k =
            {
                Pal(level1, 0x00131010, 0x000c1001, 0x000c0110, 0x00120101),
                Pal(level1, 0x00132020, 0x000c2002, 0x000c0220, 0x00120202),
                Pal(level1, 0x00134040, 0x000c4004, 0x000c0440, 0x00120404),
                Pal(level1, 0x00138080, 0x000c8008, 0x000c0880, 0x00120808)
            };
            bool[] level2 = j.Concat(k).Concat(new[] { z, ci }).ToArray();
            bool[] l =
            {
                Pal(level2, 0x00000300),
                Pal(level2, 0x00000301, 0x00000111),
                Pal(level2, 0x00000303, 0x00000113, 0x00000122),
                Pal(level2, 0x00000307, 0x00000117, 0x00000126, 0x00000144)
            };
            bool propagate = Pal(level2, 0x0000000f);
            bool generate = Pal(level2, 0x00000088, 0x0000004c, 0x0000002e, 0x0000001f);
            SetTtl(ttl, 8, l[0] ^ k[0]);
            SetTtl(ttl, 9, l[1] ^ k[1]);
            SetTtl(ttl, 11, l[2] ^ k[2]);
            SetTtl(ttl, 12, l[3] ^ k[3]);
            SetTtl(ttl, 14, propagate);
            SetTtl(ttl, 13, generate);
        }

        private static bool Pal(IReadOnlyList<bool> inputs, params int[] products)
        {
            foreach (int product in products)
            {
                bool term = true;
                for (int bit = 0; bit < inputs.Count && term; bit++)
                    if ((product & (1 << bit)) != 0) term = inputs[bit];
                if (term) return false;
            }
            return true;
        }

        private void EvaluateTtl7447(TtlPackageComponent ttl)
        {
            int digit = (TtlRead(ttl, 7) ? 1 : 0) | (TtlRead(ttl, 1) ? 2 : 0) | (TtlRead(ttl, 2) ? 4 : 0) | (TtlRead(ttl, 6) ? 8 : 0);
            // a,b,c,d,e,f,g; outputs are active LOW.
            int[] segments = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };
            bool lampTest = !TtlRead(ttl, 3);
            bool blank = !TtlRead(ttl, 4);
            bool blankRipple = !TtlRead(ttl, 5);
            int pattern = lampTest ? 0x7F : blank || (blankRipple && digit == 0) ? 0 : digit < 10 ? segments[digit] : 0;
            int[] segmentPins = { 13, 12, 11, 10, 9, 15, 14 };
            for (int bit = 0; bit < segmentPins.Length; bit++)
            {
                if (((pattern >> bit) & 1) != 0) SetTtl(ttl, segmentPins[bit], false);
                else SetHighZ(ttl, $"pin{segmentPins[bit]}");
            }
        }

        private void EvaluateTtl7485(TtlPackageComponent ttl)
        {
            int a = (TtlRead(ttl, 10) ? 1 : 0) | (TtlRead(ttl, 12) ? 2 : 0) | (TtlRead(ttl, 15) ? 4 : 0) | (TtlRead(ttl, 1) ? 8 : 0);
            int b = (TtlRead(ttl, 9) ? 1 : 0) | (TtlRead(ttl, 11) ? 2 : 0) | (TtlRead(ttl, 14) ? 4 : 0) | (TtlRead(ttl, 13) ? 8 : 0);
            bool gt = a > b, eq = a == b, lt = a < b;
            if (eq) { gt = TtlRead(ttl, 4); lt = TtlRead(ttl, 2); eq = TtlRead(ttl, 3); }
            SetTtl(ttl, 7, gt); SetTtl(ttl, 6, eq); SetTtl(ttl, 5, lt);
        }

        private void EvaluateTtl74283(TtlPackageComponent ttl)
        {
            int a = (TtlRead(ttl, 5) ? 1 : 0) | (TtlRead(ttl, 3) ? 2 : 0) | (TtlRead(ttl, 14) ? 4 : 0) | (TtlRead(ttl, 12) ? 8 : 0);
            int b = (TtlRead(ttl, 6) ? 1 : 0) | (TtlRead(ttl, 2) ? 2 : 0) | (TtlRead(ttl, 15) ? 4 : 0) | (TtlRead(ttl, 11) ? 8 : 0);
            int sum = a + b + (TtlRead(ttl, 7) ? 1 : 0);
            SetTtl(ttl, 4, (sum & 1) != 0); SetTtl(ttl, 1, (sum & 2) != 0);
            SetTtl(ttl, 13, (sum & 4) != 0); SetTtl(ttl, 10, (sum & 8) != 0); SetTtl(ttl, 9, (sum & 16) != 0);
        }

        private void EvaluateTtlOctalBuffer(TtlPackageComponent ttl)
        {
            int[] inputs = { 2, 4, 6, 8, 11, 13, 15, 17 };
            int[] outputs = { 18, 16, 14, 12, 9, 7, 5, 3 };
            for (int bit = 0; bit < inputs.Length; bit++)
            {
                int enablePin = bit < 4 ? 1 : 19;
                bool enabled = ttl.PartNumber == "74241" && bit >= 4 ? TtlRead(ttl, enablePin) : !TtlRead(ttl, enablePin);
                if (!enabled) SetHighZ(ttl, $"pin{outputs[bit]}");
                else SetTtl(ttl, outputs[bit], TtlRead(ttl, inputs[bit]) ^ (ttl.PartNumber == "74240"));
            }
        }

        private void EvaluateTtlTransceiver245(TtlPackageComponent ttl)
        {
            bool enabled = !TtlRead(ttl, 19);
            bool aToB = TtlRead(ttl, 1);
            int[] aPins = { 2, 3, 4, 5, 6, 7, 8, 9 };
            int[] bPins = { 18, 17, 16, 15, 14, 13, 12, 11 };
            for (int bit = 0; bit < 8; bit++)
            {
                if (!enabled)
                {
                    SetHighZ(ttl, $"pin{aPins[bit]}");
                    SetHighZ(ttl, $"pin{bPins[bit]}");
                }
                else if (aToB)
                {
                    SetHighZ(ttl, $"pin{aPins[bit]}");
                    SetTtl(ttl, bPins[bit], TtlRead(ttl, aPins[bit]));
                }
                else
                {
                    SetTtl(ttl, aPins[bit], TtlRead(ttl, bPins[bit]));
                    SetHighZ(ttl, $"pin{bPins[bit]}");
                }
            }
        }

        private void AdvanceTtl191(TtlPackageComponent ttl)
        {
            long value = (long)Number(ttl, "_ttlCounter", 0) & 0xF;
            bool enabled = !TtlRead(ttl, 14);
            if (!TtlRead(ttl, 11)) value = (TtlRead(ttl, 15) ? 1 : 0) | (TtlRead(ttl, 1) ? 2 : 0) | (TtlRead(ttl, 10) ? 4 : 0) | (TtlRead(ttl, 9) ? 8 : 0);
            else if (enabled && ClockEdges(ttl, "pin5", ClockTriggerType.RisingEdge) > 0) value = (value + 1) & 0xF;
            else if (enabled && ClockEdges(ttl, "pin4", ClockTriggerType.RisingEdge) > 0) value = (value - 1) & 0xF;
            SetNumber(ttl, "_ttlCounter", value);
            SetTtl(ttl, 3, (value & 1) != 0); SetTtl(ttl, 2, (value & 2) != 0);
            SetTtl(ttl, 6, (value & 4) != 0); SetTtl(ttl, 7, (value & 8) != 0);
            SetTtl(ttl, 13, value == 15); SetTtl(ttl, 12, value == 0);
        }

        private void AdvanceTtl194(TtlPackageComponent ttl)
        {
            long value = (long)Number(ttl, "_ttlShift", 0) & 0xF;
            if (!TtlRead(ttl, 1)) value = 0;
            else if (ClockEdges(ttl, "pin11", ClockTriggerType.RisingEdge) > 0)
            {
                int mode = (TtlRead(ttl, 10) ? 1 : 0) | (TtlRead(ttl, 9) ? 2 : 0);
                if (mode == 3) value = (TtlRead(ttl, 3) ? 1 : 0) | (TtlRead(ttl, 4) ? 2 : 0) | (TtlRead(ttl, 5) ? 4 : 0) | (TtlRead(ttl, 6) ? 8 : 0);
                else if (mode == 1) value = ((value >> 1) | (TtlRead(ttl, 7) ? 8L : 0L)) & 0xF;
                else if (mode == 2) value = ((value << 1) | (TtlRead(ttl, 2) ? 1L : 0L)) & 0xF;
            }
            SetNumber(ttl, "_ttlShift", value);
            SetTtl(ttl, 15, (value & 1) != 0); SetTtl(ttl, 14, (value & 2) != 0);
            SetTtl(ttl, 13, (value & 4) != 0); SetTtl(ttl, 12, (value & 8) != 0);
        }

        private void AdvanceTtl299(TtlPackageComponent ttl)
        {
            long value = (long)Number(ttl, "_ttlShift", 0) & 0xFF;
            if (!TtlRead(ttl, 9)) value = 0;
            else if (ClockEdges(ttl, "pin12", ClockTriggerType.RisingEdge) > 0)
            {
                int mode = (TtlRead(ttl, 19) ? 2 : 0) | (TtlRead(ttl, 1) ? 1 : 0);
                int[] ioPins = { 16, 4, 15, 5, 14, 6, 13, 7 };
                if (mode == 3)
                {
                    value = 0;
                    for (int bit = 0; bit < 8; bit++) if (TtlRead(ttl, ioPins[bit])) value |= 1L << bit;
                }
                else if (mode == 1) value = ((value >> 1) | (TtlRead(ttl, 11) ? 0x80L : 0L)) & 0xFF;
                else if (mode == 2) value = ((value << 1) | (TtlRead(ttl, 18) ? 1L : 0L)) & 0xFF;
            }
            SetNumber(ttl, "_ttlShift", value);
            SetTtl(ttl, 8, (value & 0x80) != 0);
            SetTtl(ttl, 17, (value & 1) != 0);
            bool outputEnabled = !TtlRead(ttl, 2) && !TtlRead(ttl, 3) && (!TtlRead(ttl, 1) || !TtlRead(ttl, 19));
            int[] outputs = { 16, 4, 15, 5, 14, 6, 13, 7 };
            for (int bit = 0; bit < outputs.Length; bit++)
            {
                if (outputEnabled) SetTtl(ttl, outputs[bit], ((value >> bit) & 1) != 0);
                else SetHighZ(ttl, $"pin{outputs[bit]}");
            }
        }

        private void AdvanceTtl74670(TtlPackageComponent ttl)
        {
            if (!ttl.CustomProperties.TryGetValue("_ttl74670", out var raw) || raw is not Dictionary<int, long>)
                ttl.CustomProperties["_ttl74670"] = new Dictionary<int, long>();
            var memory = (Dictionary<int, long>)ttl.CustomProperties["_ttl74670"];
            int readAddress = (TtlRead(ttl, 4) ? 2 : 0) | (TtlRead(ttl, 5) ? 1 : 0);
            int writeAddress = (TtlRead(ttl, 13) ? 2 : 0) | (TtlRead(ttl, 14) ? 1 : 0);
            if (ClockEdges(ttl, "pin12", ClockTriggerType.FallingEdge) > 0 && !TtlRead(ttl, 12))
            {
                long write = (TtlRead(ttl, 15) ? 1L : 0L) | (TtlRead(ttl, 1) ? 2L : 0L) | (TtlRead(ttl, 2) ? 4L : 0L) | (TtlRead(ttl, 3) ? 8L : 0L);
                memory[writeAddress] = write;
            }
            bool outputEnabled = !TtlRead(ttl, 11);
            long value = memory.TryGetValue(readAddress, out var stored) ? stored : 0;
            int[] outputs = { 10, 9, 7, 6 };
            for (int bit = 0; bit < outputs.Length; bit++)
            {
                if (outputEnabled) SetTtl(ttl, outputs[bit], ((value >> bit) & 1) != 0);
                else SetHighZ(ttl, $"pin{outputs[bit]}");
            }
        }

        private void EvaluateTtlDecoder2(TtlPackageComponent ttl, int enablePin, int aPin, int bPin, int[] outputPins)
        {
            int selected = (TtlRead(ttl, aPin) ? 1 : 0) | (TtlRead(ttl, bPin) ? 2 : 0);
            bool enabled = !TtlRead(ttl, enablePin);
            for (int i = 0; i < outputPins.Length; i++) SetTtl(ttl, outputPins[i], !enabled || selected != i);
        }

        private void EvaluateTtlMux4(TtlPackageComponent ttl, int enablePin, int selectA, int selectB, int[] dataPins, int outputPin)
        {
            int index = (TtlRead(ttl, selectA) ? 1 : 0) | (TtlRead(ttl, selectB) ? 2 : 0);
            SetTtl(ttl, outputPin, !TtlRead(ttl, enablePin) && TtlRead(ttl, dataPins[index]));
        }

        private void AdvanceTtlSequential(TtlPackageComponent ttl)
        {
            if (ttl.PartNumber == "7474")
            {
                AdvanceTtlDff(ttl, 1, 2, 3, 4, 5, 6, ClockTriggerType.RisingEdge);
                AdvanceTtlDff(ttl, 13, 12, 11, 10, 9, 8, ClockTriggerType.RisingEdge);
            }
            else if (ttl.PartNumber == "7476")
            {
                AdvanceTtlJk(ttl, 3, 4, 16, 1, 2, 15, 14);
                AdvanceTtlJk(ttl, 8, 10, 13, 6, 7, 12, 11);
            }
            else if (ttl.PartNumber == "74175")
            {
                AdvanceTtlRegister4(ttl, 15, 14, new[] { 3, 4, 7, 11 }, new[] { 1, 6, 9, 13 }, new[] { 2, 5, 10, 12 });
            }
            else if (ttl.PartNumber == "74273")
            {
                AdvanceTtlRegister8(ttl, 1, 11, new[] { 3, 4, 7, 8, 13, 14, 17, 18 }, new[] { 2, 5, 6, 9, 12, 15, 16, 19 });
            }
            else if (ttl.PartNumber == "74377")
            {
                AdvanceTtlRegister8Enabled(ttl, 1, 11, new[] { 3, 4, 7, 8, 13, 14, 17, 18 }, new[] { 2, 5, 6, 9, 12, 15, 16, 19 });
            }
            else if (ttl.PartNumber == "74165")
            {
                AdvanceTtlShiftRegister(ttl, 2, 1, 15, 10, new[] { 11, 12, 13, 14, 3, 4, 5, 6 }, 9, 7, parallelLoadActiveLow: true, clearPin: 0, asynchronousLoad: true);
            }
            else if (ttl.PartNumber == "74166")
            {
                AdvanceTtlShiftRegister(ttl, 7, 15, 6, 1, new[] { 2, 3, 4, 5, 10, 11, 12, 14 }, 13, -1, parallelLoadActiveLow: true, clearPin: 9);
            }
            else if (ttl.PartNumber == "74191") AdvanceTtl191(ttl);
            else if (ttl.PartNumber == "74194") AdvanceTtl194(ttl);
            else if (ttl.PartNumber == "74299") AdvanceTtl299(ttl);
            else if (ttl.PartNumber == "74670") AdvanceTtl74670(ttl);
            else if (ttl.PartNumber is "74161" or "74163")
            {
                AdvanceTtlCounter4(ttl);
            }
        }

        private void AdvanceTtlJk(TtlPackageComponent ttl, int clearPin, int jPin, int kPin, int clockPin, int presetPin, int qPin, int qBarPin)
        {
            string key = $"_ttlQ:{qPin}";
            bool q = Bool(ttl, key, false);
            if (!TtlRead(ttl, clearPin) && !TtlRead(ttl, presetPin))
            {
                SetHighZ(ttl, $"pin{qPin}");
                SetHighZ(ttl, $"pin{qBarPin}");
                return;
            }
            if (!TtlRead(ttl, clearPin)) q = false;
            else if (!TtlRead(ttl, presetPin)) q = true;
            else if (ClockEdges(ttl, $"pin{clockPin}", ClockTriggerType.FallingEdge) > 0)
            {
                bool j = TtlRead(ttl, jPin), k = TtlRead(ttl, kPin);
                if (j && k) q = !q;
                else if (j) q = true;
                else if (k) q = false;
            }
            SetBool(ttl, key, q);
            SetTtl(ttl, qPin, q);
            SetTtl(ttl, qBarPin, !q);
        }

        private void AdvanceTtlRegister4(TtlPackageComponent ttl, int clearPin, int clockPin, int[] dataPins, int[] qPins, int[] qBarPins)
        {
            long value = (long)Number(ttl, "_ttlRegister", 0) & 0xF;
            if (!TtlRead(ttl, clearPin)) value = 0;
            else if (ClockEdges(ttl, $"pin{clockPin}", ClockTriggerType.RisingEdge) > 0)
            {
                value = 0;
                for (int bit = 0; bit < 4; bit++) if (TtlRead(ttl, dataPins[bit])) value |= 1L << bit;
            }
            SetNumber(ttl, "_ttlRegister", value);
            for (int bit = 0; bit < 4; bit++) { SetTtl(ttl, qPins[bit], ((value >> bit) & 1) != 0); SetTtl(ttl, qBarPins[bit], ((value >> bit) & 1) == 0); }
        }

        private void AdvanceTtlRegister8(TtlPackageComponent ttl, int clearPin, int clockPin, int[] dataPins, int[] qPins)
        {
            long value = (long)Number(ttl, "_ttlRegister", 0) & 0xFF;
            if (!TtlRead(ttl, clearPin)) value = 0;
            else if (ClockEdges(ttl, $"pin{clockPin}", ClockTriggerType.RisingEdge) > 0)
            {
                value = 0;
                for (int bit = 0; bit < 8; bit++) if (TtlRead(ttl, dataPins[bit])) value |= 1L << bit;
            }
            SetNumber(ttl, "_ttlRegister", value);
            for (int bit = 0; bit < 8; bit++) SetTtl(ttl, qPins[bit], ((value >> bit) & 1) != 0);
        }

        private void AdvanceTtlRegister8Enabled(TtlPackageComponent ttl, int enablePin, int clockPin, int[] dataPins, int[] qPins)
        {
            long value = (long)Number(ttl, "_ttlRegister", 0) & 0xFF;
            if (ClockEdges(ttl, $"pin{clockPin}", ClockTriggerType.RisingEdge) > 0 && !TtlRead(ttl, enablePin))
            {
                value = 0;
                for (int bit = 0; bit < 8; bit++) if (TtlRead(ttl, dataPins[bit])) value |= 1L << bit;
            }
            SetNumber(ttl, "_ttlRegister", value);
            for (int bit = 0; bit < 8; bit++) SetTtl(ttl, qPins[bit], ((value >> bit) & 1) != 0);
        }

        private void AdvanceTtlShiftRegister(TtlPackageComponent ttl, int clockPin, int loadPin, int inhibitPin, int serialPin, int[] dataPins, int outputPin, int invertedOutputPin, bool parallelLoadActiveLow, int clearPin, bool asynchronousLoad = false)
        {
            long value = (long)Number(ttl, "_ttlShift", 0) & 0xFF;
            bool clear = clearPin > 0 && !TtlRead(ttl, clearPin);
            int clockEdges = ClockEdges(ttl, $"pin{clockPin}", ClockTriggerType.RisingEdge);
            bool load = TtlRead(ttl, loadPin) == !parallelLoadActiveLow;
            if (clear) value = 0;
            else if (asynchronousLoad && load) value = ReadTtlParallelData(ttl, dataPins);
            else if (clockEdges > 0 && (inhibitPin <= 0 || !TtlRead(ttl, inhibitPin)))
            {
                if (load) value = ReadTtlParallelData(ttl, dataPins);
                else value = ((value << 1) | (TtlRead(ttl, serialPin) ? 1L : 0L)) & 0xFF;
            }
            SetNumber(ttl, "_ttlShift", value);
            SetTtl(ttl, outputPin, ((value >> 7) & 1) != 0);
            if (invertedOutputPin > 0) SetTtl(ttl, invertedOutputPin, ((value >> 7) & 1) == 0);
        }

        private long ReadTtlParallelData(TtlPackageComponent ttl, int[] dataPins)
        {
            long value = 0;
            for (int bit = 0; bit < dataPins.Length; bit++)
                if (TtlRead(ttl, dataPins[bit])) value |= 1L << bit;
            return value;
        }

        private void AdvanceTtlDff(TtlPackageComponent ttl, int clearPin, int dataPin, int clockPin, int presetPin, int qPin, int qBarPin, ClockTriggerType trigger)
        {
            string key = $"_ttlQ:{qPin}";
            bool q = Bool(ttl, key, false);
            if (!TtlRead(ttl, clearPin) && !TtlRead(ttl, presetPin))
            {
                SetHighZ(ttl, $"pin{qPin}"); SetHighZ(ttl, $"pin{qBarPin}");
                return;
            }
            if (!TtlRead(ttl, clearPin)) q = false;
            else if (!TtlRead(ttl, presetPin)) q = true;
            else if (ClockEdges(ttl, $"pin{clockPin}", trigger) > 0) q = TtlRead(ttl, dataPin);
            SetBool(ttl, key, q);
            SetTtl(ttl, qPin, q);
            SetTtl(ttl, qBarPin, !q);
        }

        private void AdvanceTtlCounter4(TtlPackageComponent ttl)
        {
            long value = (long)Number(ttl, "_ttlCounter", 0) & 0xF;
            bool clear = !TtlRead(ttl, 1);
            int clockPin = 2;
            int edges = ClockEdges(ttl, $"pin{clockPin}", ClockTriggerType.RisingEdge);
            if (ttl.PartNumber == "74161" && clear) value = 0;
            else if (edges > 0)
            {
                if (ttl.PartNumber == "74163" && clear) value = 0;
                else if (!TtlRead(ttl, 9)) value = (TtlRead(ttl, 3) ? 1 : 0) | (TtlRead(ttl, 4) ? 2 : 0) | (TtlRead(ttl, 5) ? 4 : 0) | (TtlRead(ttl, 6) ? 8 : 0);
                else if (TtlRead(ttl, 7) && TtlRead(ttl, 9)) value = (value + 1) & 0xF;
            }
            SetNumber(ttl, "_ttlCounter", value);
            SetTtl(ttl, 14, (value & 1) != 0);
            SetTtl(ttl, 13, (value & 2) != 0);
            SetTtl(ttl, 12, (value & 4) != 0);
            SetTtl(ttl, 11, (value & 8) != 0);
            SetTtl(ttl, 15, value == 15 && TtlRead(ttl, 10));
        }

        private void SetTtl(TtlPackageComponent ttl, int pin, bool value) => Set(ttl, $"pin{pin}", value, 5, 0);

        private void EvaluatePla(PLAComponent pla)
        {
            int inputWord = 0;
            for (int i = 0; i < pla.InputCount; i++) if (Read(pla, $"in{i}")) inputWord |= 1 << i;
            long word = 0;
            bool matched = false;
            foreach (var rawRow in pla.TruthTable.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries).Take(pla.AndTerms))
            {
                string[] sides = rawRow.Split(new[] { "->", "=", ":" }, 2, StringSplitOptions.TrimEntries);
                if (sides.Length != 2)
                {
                    sides = rawRow.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                }
                if (sides.Length != 2) continue;
                string pattern = new string(sides[0].Where(ch => !char.IsWhiteSpace(ch) && ch != ',').ToArray());
                if (pattern.Length != pla.InputCount || !MatchesPlaPattern(pattern, inputWord)) continue;
                word = ParsePlaOutput(sides[1]);
                matched = true;
                break;
            }

            // Backward-compatible truth-vector format: one decimal/hex output word per input index.
            if (!matched)
            {
                string[] entries = pla.TruthTable.Split(new[] { ',', ';', '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (inputWord < entries.Length) word = ParseWord(entries[inputWord]);
            }
            for (int i = 0; i < pla.OutputCount; i++) Set(pla, $"out{i}", (word & (1L << i)) != 0, 5, 0);
        }

        private static bool MatchesPlaPattern(string pattern, int inputWord)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                char expected = char.ToLowerInvariant(pattern[i]);
                if (expected is 'x' or '-') continue;
                int bit = pattern.Length - 1 - i;
                bool actual = ((inputWord >> bit) & 1) != 0;
                if ((expected == '1') != actual) return false;
                if (expected is not ('0' or '1')) return false;
            }
            return true;
        }

        private static long ParseWord(string text)
        {
            string value = text.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return long.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex) ? hex : 0;
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                ? number
                : long.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hexValue) ? hexValue : 0;
        }

        private static long ParsePlaOutput(string text)
        {
            string value = new string(text.Trim().Where(ch => !char.IsWhiteSpace(ch) && ch != ',').ToArray());
            if (value.Length > 0 && value.All(ch => ch is '0' or '1') && value.Length <= 62)
                return Convert.ToInt64(value, 2);
            return ParseWord(value);
        }

        private static long ReadRomWord(ROMComponent rom, int address)
        {
            if (rom.MemoryData.TryGetValue(address, out var value)) return value;
            string contents = rom.ContentsHex.Trim();
            if (contents.Length == 0) return 0;
            string[] tokens = contents.Split(new[] { ',', ';', '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 1 && tokens[0].Length > (rom.DataBitWidth + 3) / 4)
            {
                string compact = tokens[0].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? tokens[0][2..] : tokens[0];
                int digits = Math.Max(1, (rom.DataBitWidth + 3) / 4);
                tokens = Enumerable.Range(0, (compact.Length + digits - 1) / digits)
                    .Select(index => compact.Substring(index * digits, Math.Min(digits, compact.Length - index * digits)))
                    .ToArray();
            }
            return address >= 0 && address < tokens.Length ? ParseHexWord(tokens[address]) & Mask(rom.DataBitWidth) : 0;
        }

        private static long ParseHexWord(string text)
        {
            string value = text.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) value = value[2..];
            return long.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var word) ? word : 0;
        }

        private static string BusPin(ComponentNode component, string prefix, int bit, int width, string suffix = "")
        {
            if (width == 1 && component.GetPin(prefix) != null) return prefix;
            string indexed = suffix.Length == 0 ? $"{prefix}{bit}" : $"{prefix}{suffix}{bit}";
            return component.GetPin(indexed) != null ? indexed : $"{prefix}{bit}";
        }
    
    }
}
