﻿using System;
using System.Collections.Immutable;
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

    static void Execute(Compilation compilation, ImmutableArray<EnumDeclarationSyntax> enums, SourceProductionContext context)
    {
        if (enums.IsDefaultOrEmpty)
        {
            return;
        }

        // Add a dummy diagnostic
        context.ReportDiagnostic(CreateDiagnostic(enums[0]));

        IEnumerable<EnumDeclarationSyntax> distinctEnums = enums.Distinct();

        // Convert each EnumDeclarationSyntax to an EnumToGenerate
        List<EnumToGenerate> enumsToGenerate = GetTypesToGenerate(compilation, distinctEnums, context.CancellationToken);

        // If there were errors in the EnumDeclarationSyntax, we won't create an
        // EnumToGenerate for it, so make sure we have something to generate
        if (enumsToGenerate.Count > 0)
        {
            // generate the source code and add it to the output
            string result = SourceGenerationHelper.GenerateExtensionClass(enumsToGenerate);
            context.AddSource("EnumExtensions.g.cs", SourceText.From(result, Encoding.UTF8));
        }

    }

    private static List<EnumToGenerate> GetTypesToGenerate(
          Compilation compilation, 
          IEnumerable<EnumDeclarationSyntax> enums,
          CancellationToken ct)
    {
        var enumsToGenerate = new List<EnumToGenerate>();
        INamedTypeSymbol? enumAttribute = compilation.GetTypeByMetadataName(EnumExtensionsAttribute);
        if (enumAttribute == null)
        {
            // nothing to do if this type isn't available
            return enumsToGenerate;
        }

        foreach (var enumDeclarationSyntax in enums)
        {
            // stop if we're asked to
            ct.ThrowIfCancellationRequested();

            SemanticModel semanticModel = compilation.GetSemanticModel(enumDeclarationSyntax.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(enumDeclarationSyntax) is not INamedTypeSymbol enumSymbol)
            {
                // something went wrong
                continue;
            }

            string enumName = enumSymbol.ToString();
            string extensionName = "EnumExtensions";

            string className = null;
            string namespaceName = null;


            foreach (AttributeData attributeData in enumSymbol.GetAttributes())
            {
                if (!enumAttribute.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
                {
                    continue;
                }
                if(!attributeData.ConstructorArguments.IsEmpty)
                {
                    ImmutableArray<TypedConstant> args = attributeData.ConstructorArguments;
                    foreach (var arg in args)
                    {
                        if(arg.Kind == TypedConstantKind.Error)
                        {
                            return enumsToGenerate;
                        }
                    }
                    switch( args.Length )
                    {
                        case 1:
                            className = (string)args[0].Value;
                            break;
                    }

                }

                if(!attributeData.NamedArguments.IsEmpty)
                {
                    foreach (KeyValuePair<string, TypedConstant> namedArgument in attributeData.NamedArguments)
                    {
                        TypedConstant typedConstant = namedArgument.Value;
                        if(typedConstant.Kind == TypedConstantKind.Error)
                        {
                            return enumsToGenerate;
                        } else
                        {
                            switch(namedArgument.Key)
                            {
                                case "extensionClassName":
                                    className = namedArgument.Value.ToString();
                                    break;
                                case "ExtensionNamespaceName":
                                    namespaceName = namedArgument.Value.ToString();
                                    break;
                            }
                        }
                    }
                }
          
                break;
            }

       
            ImmutableArray<ISymbol> enumMembers = enumSymbol.GetMembers();
            var members = new List<string>(enumMembers.Length);

            foreach (ISymbol member in enumMembers)
            {
                if (member is IFieldSymbol field && field.ConstantValue is not null)
                {
                    members.Add(member.Name);
                }
            }

            enumsToGenerate.Add(new EnumToGenerate(enumName, extensionName, members));
        }

        return enumsToGenerate;
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
   
}