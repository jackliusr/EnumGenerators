﻿using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NetEscapades.EnumGenerators;

[Generator]
public class EnumGenerator : IIncrementalGenerator
{
    private const string EnumExtensionsAttribute = "NetEscapades.EnumGenerators.EnumExtensionsAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx => ctx.AddSource(
            "EnumExtensionsAttribute.g.cs", SourceText.From(SourceGenerationHelper.Attribute, Encoding.UTF8)));

        IncrementalValuesProvider<EnumDeclarationSyntax?> enumDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        IncrementalValueProvider<(Compilation, ImmutableArray<EnumDeclarationSyntax>)> compilationAndEnums
                   = context.CompilationProvider.Combine(enumDeclarations.Collect());


        context.RegisterSourceOutput(compilationAndEnums,
            static (spc, source) => Execute(source.Item1, source.Item2, spc));
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

    static void Execute(EnumToGenerate? enumToGenerate, SourceProductionContext context)
    {
        if (enumToGenerate is { } value)
        {
            // generate the source code and add it to the output
            string result = SourceGenerationHelper.GenerateExtensionClass(value);
            context.AddSource($"EnumExtensions.{value.Name}.g.cs", SourceText.From(result, Encoding.UTF8));
        }
    }
    static void Execute(Compilation compilation, ImmutableArray<EnumDeclarationSyntax> enums, SourceProductionContext context)
    {
        if (enums.IsDefaultOrEmpty)
        {
            return;
        }

        // Add a dummy diagnostic
        context.ReportDiagnostic(CreateDiagnostic(enums[0]));

        // ...
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
    static EnumToGenerate? GetEnumToGenerate(SemanticModel semanticModel, SyntaxNode enumDeclarationSyntax)
    {
        if (semanticModel.GetDeclaredSymbol(enumDeclarationSyntax) is not INamedTypeSymbol enumSymbol)
        {
            // something went wrong
            return null;
        }

        string enumName = enumSymbol.ToString();
        ImmutableArray<ISymbol> enumMembers = enumSymbol.GetMembers();
        var members = new List<string>(enumMembers.Length);

        foreach (ISymbol member in enumMembers)
        {
            if (member is IFieldSymbol field && field.ConstantValue is not null)
            {
                members.Add(member.Name);
            }
        }

        return new EnumToGenerate(enumName, members);
    }
}