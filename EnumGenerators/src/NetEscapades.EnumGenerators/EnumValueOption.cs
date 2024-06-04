using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetEscapades.EnumGenerators;

public readonly struct EnumValueOption : IEquatable<EnumValueOption>
{
    /// <summary>
    /// Custom name set by the <c>[Display(Name)]</c> attribute.
    /// </summary>
    public string? DisplayName { get; }
    public bool IsDisplayNameTheFirstPresence { get; }

    public EnumValueOption(string? displayName, bool isDisplayNameTheFirstPresence)
    {
        DisplayName = displayName;
        IsDisplayNameTheFirstPresence = isDisplayNameTheFirstPresence;
    }

    public bool Equals(EnumValueOption other)
    {
        return DisplayName == other.DisplayName &&
               IsDisplayNameTheFirstPresence == other.IsDisplayNameTheFirstPresence;
    }
}