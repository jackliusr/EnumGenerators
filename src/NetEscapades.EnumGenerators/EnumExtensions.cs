using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace NetEscapades.EnumGenerators;

public static  class EnumExtensions
{
    public static string ToStringFast(this Colour colour)
    => colour switch
    {
        Colour.Red => nameof(Colour.Red),
        Colour.Blue => nameof(Colour.Blue),
        _ => colour.ToString(),
    };
}

