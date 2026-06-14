using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;

namespace LQ.DatabaseHelper.Analyser;

[Generator]
public class DbTableGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: (s, _) => s is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: (n, _) => GetEntityInfo(n))
            .Where(t => t != null);  // filter

        context.RegisterSourceOutput(provider.Collect(), Execute);
    }

    private static (string Name, bool IsCritical, uint AllowDbId)? GetEntityInfo(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var model = context.SemanticModel.GetDeclaredSymbol(classDeclaration);

        var attr = model?.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name.Contains("LDbEntity") == true);
        if (attr == null) return null;

        var isCritical = false;
        if (attr.ConstructorArguments.Length > 0)
        {
            isCritical = (bool)(attr.ConstructorArguments[0].Value ?? false);
        }
        else if (attr.NamedArguments.Any(x => x.Key == "IsCritical"))
        {
            isCritical = (bool)(attr.NamedArguments.First(x => x.Key == "IsCritical").Value.Value ?? false);
        }

        var allowDbId = 0u;
        if (attr.ConstructorArguments.Length > 1)
        {
            allowDbId = (uint)(attr.ConstructorArguments[1].Value ?? false);
        } else if (attr.NamedArguments.Any(x => x.Key == "AllowDbId"))
        {
            allowDbId = (uint)(attr.NamedArguments.First(x => x.Key == "AllowDbId").Value.Value ?? 0);
        }

        return (model.ToDisplayString(), isCritical, allowDbId);
    }

    private void Execute(SourceProductionContext context, System.Collections.Immutable.ImmutableArray<(string Name, bool IsCritical, uint AllowDbId)?> entities)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("namespace LQ.DatabaseHelper.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    public static class DbInitializer");
        sb.AppendLine("    {");
        sb.AppendLine("        [ModuleInitializer]");
        sb.AppendLine("        public static void Init()");
        sb.AppendLine("        {");
        foreach (var entity in entities)
        {
            if (entity == null) continue;

            // register
            sb.AppendLine($"            LDatabaseHelper.Register<{entity.Value.Name}>({entity.Value.IsCritical.ToString().ToLower()}, {entity.Value.AllowDbId.ToString().ToLower()});");
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("DbInitializer.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}
