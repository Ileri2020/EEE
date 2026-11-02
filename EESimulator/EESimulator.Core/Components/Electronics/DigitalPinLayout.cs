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

    public static double Position(int index, int count, double halfSpan = 18.0) =>
        count <= 1 ? 0 : -halfSpan + (2 * halfSpan * index / (count - 1));
}
