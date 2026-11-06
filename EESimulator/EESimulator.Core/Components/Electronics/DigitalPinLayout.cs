using System.Collections.Generic;
using EESimulator.Core.Models;

namespace EESimulator.Core.Components.Electronics;

internal static class DigitalPinLayout
{
    public static void Replace(ComponentNode component, IEnumerable<ComponentPin> pins)
    {
        component.Pins.Clear();
        component.Pins.AddRange(pins);
    }

    public static double Position(int index, int count, double halfSpan = 18.0)
    {
        if (count <= 1) return 0;
        double spacedHalfSpan = (count - 1) * 5.0;
        double effectiveHalfSpan = System.Math.Max(halfSpan, spacedHalfSpan);
        return -effectiveHalfSpan + (2 * effectiveHalfSpan * index / (count - 1));
    }
}
