﻿//HintName: EnumExtensionsAttribute.g.cs
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the NetEscapades.EnumGenerators source generator
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

#nullable enable

#if NETESCAPADES_ENUMGENERATORS_EMBED_ATTRIBUTES
namespace NetEscapades.EnumGenerators
{
    /// <summary>
    /// Add to enums to indicate that extension methods should be generated for the type
    /// </summary>
    [global::System.AttributeUsage(global::System.AttributeTargets.Enum)]
    [global::System.Diagnostics.Conditional("NETESCAPADES_ENUMGENERATORS_USAGES")]
#if NET5_0_OR_GREATER
    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Generated by the NetEscapades.EnumGenerators source generator.")]
#else
    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
#endif
    public class EnumExtensionsAttribute : global::System.Attribute
    {
        /// <summary>
        /// The namespace to generate the extension class.
        /// If not provided the namespace of the enum will be used
        /// </summary>
        public string? ExtensionClassNamespace { get; set; }

        /// <summary>
        /// The name to use for the extension class.
        /// If not provided, the enum name with "Extensions" will be used.
        /// For example for an Enum called StatusCodes, the default name
        /// will be StatusCodesExtensions
        /// </summary>
        public string? ExtensionClassName { get; set; }
    }
}
#endif
