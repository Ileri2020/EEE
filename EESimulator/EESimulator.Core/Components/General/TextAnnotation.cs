using System;
using EESimulator.Core.Components.Base;

namespace EESimulator.Core.Components.General;

/// <summary>
/// Free-floating schematic text note or title block annotation.
/// </summary>
public class TextAnnotationComponent : GeneralComponent
{
    public const string StableType = "general.textannotation";

    public override string StableTypeId => StableType;
    public override string ComponentType => "TextAnnotation";
    public override string Description => "Schematic text note or annotation";

    public string Text { get; set; } = "Note";
    public double FontSize { get; set; } = 12.0;

    public TextAnnotationComponent()
    {
    }

    public TextAnnotationComponent(string name, string text, double x = 0, double y = 0)
    {
        Name = name;
        Reference = name;
        Text = text;
        X = x;
        Y = y;
    }
}
