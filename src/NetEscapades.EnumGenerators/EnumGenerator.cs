using System;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NetEscapades.EnumGenerators;

[Generator]
public class EnumGenerator : IIncrementalGenerator
{
    private const string EnumExtensionsAttribute = "NetEscapades.EnumGenerators.EnumExtensionsAttribute";
    private const string DisplayAttribute = "System.ComponentModel.DataAnnotations.DisplayAttribute";
    private const string DescriptionAttribute = "System.ComponentModel.DescriptionAttribute";
    private const string FlagsAttribute = "System.FlagsAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx => ctx.AddSource(
            "EnumExtensionsAttribute.g.cs", SourceText.From(SourceGenerationHelper.Attribute, Encoding.UTF8)));

        IncrementalValuesProvider<EnumToGenerate?> enumDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(EnumExtensionsAttribute,
                predicate: static (s, _) => s is EnumDeclarationSyntax,
                transform: GetTypeToGenerate)
            .WithTrackingName(TrackingNames.InitialExtraction)
            .Where(static m => m is not null)
            .WithTrackingName(TrackingNames.RemovingNulls);


        context.RegisterSourceOutput(enumDeclarations,
            static (spc, source) => Execute(source, spc));
    }

    static EnumToGenerate? GetTypeToGenerate(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        INamedTypeSymbol? enumSymbol = context.TargetSymbol as INamedTypeSymbol;
        if (enumSymbol is null)
        {
            // nothing to do if this type isn't available
            return null;
        }

        ct.ThrowIfCancellationRequested();

        string name = enumSymbol.Name + "Extensions";
        string nameSpace = enumSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : enumSymbol.ContainingNamespace.ToString();
        var hasFlags = false;

        foreach (AttributeData attributeData in enumSymbol.GetAttributes())
        {
            if ((attributeData.AttributeClass?.Name == "FlagsAttribute" ||
                 attributeData.AttributeClass?.Name == "Flags") &&
                attributeData.AttributeClass.ToDisplayString() == FlagsAttribute)
            {
                hasFlags = true;
                continue;
            }

            if (attributeData.AttributeClass?.Name != "EnumExtensionsAttribute" ||
                attributeData.AttributeClass.ToDisplayString() != EnumExtensionsAttribute)
            {
                continue;
            }

            foreach (KeyValuePair<string, TypedConstant> namedArgument in attributeData.NamedArguments)
            {
                if (namedArgument.Key == "ExtensionClassNamespace"
                    && namedArgument.Value.Value?.ToString() is { } ns)
                {
                    nameSpace = ns;
                    continue;
                }

                if (namedArgument.Key == "ExtensionClassName"
                    && namedArgument.Value.Value?.ToString() is { } n)
                {
                    name = n;
                }
            }
        }

        string fullyQualifiedName = enumSymbol.ToString();
        string underlyingType = enumSymbol.EnumUnderlyingType?.ToString() ?? "int";

        var enumMembers = enumSymbol.GetMembers();
        var members = new List<(string, EnumValueOption)>(enumMembers.Length);
        HashSet<string>? displayNames = null;
        var isDisplayNameTheFirstPresence = false;

        foreach (var member in enumMembers)
        {
            if (member is not IFieldSymbol field
                || field.ConstantValue is null)
            {
                continue;
            }

            string? displayName = null;
            foreach (var attribute in member.GetAttributes())
            {
                if (attribute.AttributeClass?.Name == "DisplayAttribute" &&
                    attribute.AttributeClass.ToDisplayString() == DisplayAttribute)
                {
                    foreach (var namedArgument in attribute.NamedArguments)
                    {
                        if (namedArgument.Key == "Name" && namedArgument.Value.Value?.ToString() is { } dn)
                        {
                            // found display attribute, all done
                            displayName = dn;
                            goto addDisplayName;
                        }
                    }
                }

                if (attribute.AttributeClass?.Name == "DescriptionAttribute"
                    && attribute.AttributeClass.ToDisplayString() == DescriptionAttribute
                    && attribute.ConstructorArguments.Length == 1)
                {
                    if (attribute.ConstructorArguments[0].Value?.ToString() is { } dn)
                    {
                        // found display attribute, all done
                        // Handle cases where contains a quote or a backslash
                        displayName = dn
                            .Replace(@"\", @"\\")
                            .Replace("\"", "\\\"");
                        goto addDisplayName;
                    }
                }
            }

        addDisplayName:
            if (displayName is not null)
            {
                displayNames ??= new();
                isDisplayNameTheFirstPresence = displayNames.Add(displayName);
            }

            members.Add((member.Name, new EnumValueOption(displayName, isDisplayNameTheFirstPresence)));
        }

        return new EnumToGenerate(
            name: name,
            fullyQualifiedName: fullyQualifiedName,
            ns: nameSpace,
            underlyingType: underlyingType,
            isPublic: enumSymbol.DeclaredAccessibility == Accessibility.Public,
            hasFlags: hasFlags,
            names: members,
            isDisplayAttributeUsed: displayNames?.Count > 0);
    }


    static bool IsSyntaxTargetForGeneration(SyntaxNode node)
        => node is EnumDeclarationSyntax m && m.AttributeLists.Count > 0;

    static EnumDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        // we know the node is a EnumDeclarationSyntax thanks to IsSyntaxTargetForGeneration
        var enumDeclarationSyntax = (EnumDeclarationSyntax)context.Node;

        // loop through all the attributes on the method
        foreach (AttributeListSyntax attributeListSyntax in enumDeclarationSyntax.AttributeLists)
        {
            foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
            {
                if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is not IMethodSymbol attributeSymbol)
                {
                    // weird, we couldn't get the symbol, ignore it
                    continue;
                }

                INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                string fullName = attributeContainingTypeSymbol.ToDisplayString();

                // Is the attribute the [EnumExtensions] attribute?
                if (fullName == EnumExtensionsAttribute)
                {
                    // return the enum
                    return enumDeclarationSyntax;
                }
            }
        }

        // we didn't find the attribute we were looking for
        return null;
    }
    static void Execute(in EnumToGenerate? enumToGenerate, SourceProductionContext context)
    {
        if (enumToGenerate is { } eg)
        {
            StringBuilder sb = new StringBuilder();
            var result = SourceGenerationHelper.GenerateExtensionClass(sb, in eg);
            context.AddSource(eg.Name + "_EnumExtensions.g.cs", SourceText.From(result, Encoding.UTF8));
        }
    }

    

    static Diagnostic CreateDiagnostic(EnumDeclarationSyntax syntax)
    {
        var descriptor = new DiagnosticDescriptor(
            id: "TEST01",
            title: "A test diagnostic",
            messageFormat: "A description about the problem",
            category: "tests",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        return Diagnostic.Create(descriptor, syntax.GetLocation());
    }
    static string GetNamespace(BaseTypeDeclarationSyntax syntax)
    {
        string nameSpace = string.Empty;

        SyntaxNode? potentialNamespaceParent = syntax.Parent;
        while( potentialNamespaceParent != null && 
            potentialNamespaceParent is not NamespaceDeclarationSyntax  &&
            potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax) {
            potentialNamespaceParent = potentialNamespaceParent.Parent;
        }

        if (potentialNamespaceParent is BaseNamespaceDeclarationSyntax namespaceParent) { 
            nameSpace = namespaceParent.Name.ToString();
            while (true)
            {
                if(namespaceParent is not NamespaceDeclarationSyntax parent)
                {
                    break;
                }
                nameSpace = $"{namespaceParent.Name}.{nameSpace}";
                namespaceParent = parent;
            }
        }
        return nameSpace;
    }


}
