using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace NetEscapades.EnumGenerators;

public class SourceGenerationHelper
{
    public const string Attribute = @"
namespace NetEscapades.EnumGenerators
{
    [System.AttributeUsage(System.AttributeTargets.Enum)]
    public class EnumExtensionsAttribute : System.Attribute
    {
    }
}";
    public static string GenerateExtensionClass(EnumToGenerate enumToGenerate)
    {
        var sb = new StringBuilder();
        sb.Append(@"
namespace NetEscapades.EnumGenerators
{
    public static partial class EnumExtensions
    {");
        sb.Append(@"
            public static string ToStringFast(this ").Append(enumToGenerate.Name).Append(@" value)
                => value switch
                {");
        foreach (var member in enumToGenerate.Values)
        {
            sb.Append(@"
            ").Append(enumToGenerate.Name).Append('.').Append(member)
                .Append(" => nameof(")
                .Append(enumToGenerate.Name).Append('.').Append(member).Append("),");
        }

        sb.Append(@"
                _ => value.ToString(),
            };
");

        sb.Append(@"
    }
}");

        return sb.ToString();
    }

}
