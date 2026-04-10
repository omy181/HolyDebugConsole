using System.Collections.Generic;
using UnityEngine;

namespace Holylib.DebugConsole {

    // This is a template for adding more categories 
public static partial class HolyDebugGroupStyles {
    
    public const string Uncategorized = "Uncategorized";
    [DebugCommandGroup(Uncategorized)] public static readonly DebugGroupStyle UncategorizedStyle 
        = new DebugGroupStyle("❔ Uncategorized", Color.white);
}

public readonly struct DebugGroupStyle {

    public readonly string Name;
    public readonly Color Color;

    public DebugGroupStyle(string name, Color color)
    {
        Name = name;
        Color = color;
    }
}
}