using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RavenNest.DataModels.Generators
{
    [Generator]
    public class PersistentDataPropertyGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Find all class declarations
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: (node, _) => node is ClassDeclarationSyntax,
                    transform: (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
                .Where(cls => cls is not null);

            // Combine with the compilation for semantic analysis
            var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

            context.RegisterSourceOutput(compilationAndClasses, (spc, source) =>
            {
                var (compilation, classes) = source;
                foreach (var classNode in classes)
                {
                    var semanticModel = compilation.GetSemanticModel(classNode.SyntaxTree);
                    var classSymbol = semanticModel.GetDeclaredSymbol(classNode) as INamedTypeSymbol;
                    if (classSymbol == null) continue;

                    var fields = classNode.Members
                        .OfType<FieldDeclarationSyntax>()
                        .Where(f =>
                            f.AttributeLists
                                .SelectMany(a => a.Attributes)
                                .Any(attr =>
                                    attr.Name.ToString().Contains("PersistentData")));

                    if (!fields.Any()) continue;

                    var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
                    var className = classSymbol.Name;

                    var sb = new StringBuilder();
                    sb.AppendLine("using System;");
                    sb.AppendLine($"namespace {namespaceName}");
                    sb.AppendLine("{");
                    sb.AppendLine($"    public partial class {className}");
                    sb.AppendLine("    {");

                    foreach (var field in fields)
                    {
                        var variable = field.Declaration.Variables.First();
                        var fieldName = variable.Identifier.Text;
                        var propertyName = char.ToUpper(fieldName[0]) + fieldName.Substring(1);
                        var type = field.Declaration.Type.ToString();

                        sb.AppendLine($@"
        public {type} {propertyName}
        {{
            get => {fieldName};
            set => Set(ref {fieldName}, value);
        }}");
                    }

                    sb.AppendLine("    }");
                    sb.AppendLine("}");

                    spc.AddSource($"{className}_PersistentDataProperties.g.cs", sb.ToString());
                }
            });
        }
    }
}
