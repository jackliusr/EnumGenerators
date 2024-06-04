using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace NetEscapades.EnumGenerators;

public static class SourceGenerationHelper
{
    public const string Attribute = @"

namespace NetEscapades.EnumGenerators
{
    [System.AttributeUsage(System.AttributeTargets.Enum)]
    public class EnumExtensionsAttribute : System.Attribute
    {
        public EnumExtensionsAttribute(string extensionClassName)
        {
            ExtensionClassName = extensionClassName;
        }
        public string ExtensionClassName { get; set; }
        public string ExtensionNamespaceName { get; set; }
    }
}";
    public static string GenerateExtensionClass(List<EnumToGenerate> enumsToGenerate)
    {
        var sb = new StringBuilder();
        sb.Append(@"
namespace NetEscapades.EnumGenerators
{");
        foreach (var enumToGenerate in enumsToGenerate)
        {
            sb.Append(@"
    public static partial class ").Append(enumToGenerate.ExtensionName).Append(@"
    {
        public static string ToStringFast(this ").Append(enumToGenerate.Name).Append(@" value)
            => value switch
            {");
            foreach (var member in enumToGenerate.Values)
            {
                sb.Append(@"
                ")
                    .Append(enumToGenerate.Name).Append('.').Append(member)
                    .Append(" => nameof(")
                    .Append(enumToGenerate.Name).Append('.').Append(member).Append("),");
            }

            sb.Append(@"
                _ => value.ToString(),
            };
    }
");
        }
        sb.Append('}');

        return sb.ToString();
    }
    static ParentClass? GetParentClasses(BaseTypeDeclarationSyntax typeSyntax)
    {
        // Try and get the parent syntax. If it isn't a type like class/struct, this will be null
        TypeDeclarationSyntax? parentSyntax = typeSyntax.Parent as TypeDeclarationSyntax;
        ParentClass? parentClassInfo = null;

        // Keep looping while we're in a supported nested type
        while (parentSyntax != null && IsAllowedKind(parentSyntax.Kind()))
        {
            // Record the parent type keyword (class/struct etc), name, and constraints
            parentClassInfo = new ParentClass(
                keyword: parentSyntax.Keyword.ValueText,
                name: parentSyntax.Identifier.ToString() + parentSyntax.TypeParameterList,
                constraints: parentSyntax.ConstraintClauses.ToString(),
                child: parentClassInfo); // set the child link (null initially)

            // Move to the next outer type
            parentSyntax = (parentSyntax.Parent as TypeDeclarationSyntax);
        }

        // return a link to the outermost parent type
        return parentClassInfo;

    }

    // We can only be nested in class/struct/record
    static bool IsAllowedKind(SyntaxKind kind) =>
    kind == SyntaxKind.ClassDeclaration ||
    kind == SyntaxKind.StructDeclaration ||
    kind == SyntaxKind.RecordDeclaration;

    static public string GetResource(string nameSpace, ParentClass? parentClass)
    {
        var sb = new StringBuilder();
        int parentsCount = 0;

        // If we don't have a namespace, generate the code in the "default"
        // namespace, either global:: or a different <RootNamespace>
        var hasNamespace = !string.IsNullOrEmpty(nameSpace);
    if (hasNamespace)
        {
            // We could use a file-scoped namespace here which would be a little impler, 
            // but that requires C# 10, which might not be available. 
            // Depends what you want to support!
            sb
                .Append("namespace ")
                .Append(nameSpace)
                .AppendLine(@"
    {");
        }

        // Loop through the full parent type hiearchy, starting with the outermost
        while (parentClass is not null)
        {
            sb
                .Append("    partial ")
                .Append(parentClass.Keyword) // e.g. class/struct/record
                .Append(' ')
                .Append(parentClass.Name) // e.g. Outer/Generic<T>
                .Append(' ')
                .Append(parentClass.Constraints) // e.g. where T: new()
                .AppendLine(@"
        {");
            parentsCount++; // keep track of how many layers deep we are
            parentClass = parentClass.Child; // repeat with the next child
        }

        // Write the actual target generation code here. Not shown for brevity
        sb.AppendLine(@"public partial readonly struct TestId
    {
    }");

        // We need to "close" each of the parent types, so write
        // the required number of '}'
        for (int i = 0; i < parentsCount; i++)
        {
            sb.AppendLine(@"    }");
        }

        // Close the namespace, if we had one
        if (hasNamespace)
        {
            sb.Append('}').AppendLine();
        }

        return sb.ToString();
    }
}