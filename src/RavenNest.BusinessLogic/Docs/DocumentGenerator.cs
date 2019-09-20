using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using RavenNest.BusinessLogic.Docs.Attributes;
using RavenNest.BusinessLogic.Docs.Models;
using RavenNest.Models;
using Remotion.Linq.Parsing.Structure.IntermediateModel;

namespace RavenNest.BusinessLogic.Docs
{
    public class DocumentGenerator : IDocumentGenerator
    {
        public IDocument Generate(IDocumentSettings documentSettings, IGeneratorSettings generatorSettings)
        {
            var document = new Document();
            var pages = new List<DocumentPage>();

            document.Settings = documentSettings;
            document.Apis = GenerateApiDocumentation(generatorSettings);
            document.Pages = pages;
            return document;
        }

        private IReadOnlyList<DocumentApi> GenerateApiDocumentation(IGeneratorSettings generatorSettings)
        {
            var asm = generatorSettings.Assembly;
            var controllers = asm.GetExportedTypes()
                .Where(x => x.CustomAttributes.Any(y => y.AttributeType.FullName.Contains("ApiController")));
            return (from controller in controllers select GenerateDocumentApi(controller)).ToList();
        }

        private DocumentApi GenerateDocumentApi(Type controller)
        {
            string desc = null;
            string name = controller.Name.Replace("Controller", "");
            string path = null;

            foreach (var data in controller.GetCustomAttributesData())
            {
                var descValue = GetValue(data,
                    nameof(ApiDescriptorAttribute.Description),
                    nameof(ApiDescriptorAttribute))?.ToString();

                if (!string.IsNullOrEmpty(descValue))
                {
                    desc = descValue;
                }

                var nameValue = GetValue(data,
                    nameof(ApiDescriptorAttribute.Name),
                    nameof(ApiDescriptorAttribute))?.ToString();

                if (!string.IsNullOrEmpty(descValue))
                {
                    name = nameValue;
                }

                var pathValue = GetValue(data, "Template", "RouteAttribute")?.ToString();
                if (!string.IsNullOrEmpty(pathValue))
                {
                    path += pathValue.Replace("[controller]", controller.Name.ToLower().Replace("controller", ""));
                }

                if (data.ConstructorArguments.Count > 0)
                {
                    path += data.ConstructorArguments.First().Value?.ToString();
                    path = path.Replace("[controller]", controller.Name.ToLower().Replace("controller", ""));
                }
            }


            var methods = new List<DocumentApiMethod>();
            var filteredMethods = controller
                .GetMethods()
                .Where(x => x.CustomAttributes.Any(y =>
                {
                    return y.AttributeType.Name.Contains("HttpGet")
                    || y.AttributeType.Name.Contains("HttpPost")
                    || y.AttributeType.Name.Contains("HttpPut")
                    || y.AttributeType.Name.Contains("HttpDelete");
                }));

            foreach (var method in filteredMethods)
            {
                methods.Add(GenerateMethodApi(method));
            }

            return new DocumentApi(name, desc, path, methods);
        }

        private DocumentApiMethod GenerateMethodApi(MethodInfo method)
        {
            string name = null;
            string methodName = null;
            string path = "/";
            string description = null;
            bool requiresAdmin = false;
            bool requiresAuth = false;
            bool requiresSession = false;
            bool requiresTwitch = false;

            foreach (var data in method.GetCustomAttributesData())
            {
                var descValue = GetValue(data, nameof(MethodDescriptorAttribute.Description), nameof(MethodDescriptorAttribute))?.ToString();
                if (!string.IsNullOrEmpty(descValue)) description = descValue;

                var nameValue = GetValue(data, nameof(MethodDescriptorAttribute.Name), nameof(MethodDescriptorAttribute))?.ToString();
                if (!string.IsNullOrEmpty(nameValue)) name = nameValue;

                var reqAuth = GetValue(data, nameof(MethodDescriptorAttribute.RequiresAuth), nameof(MethodDescriptorAttribute));
                if (reqAuth != null) requiresAuth = (bool)reqAuth;

                var reqAdmin = GetValue(data, nameof(MethodDescriptorAttribute.RequiresAdmin), nameof(MethodDescriptorAttribute));
                if (reqAdmin != null) requiresAdmin = (bool)reqAdmin;

                var reqTwitch = GetValue(data, nameof(MethodDescriptorAttribute.RequiresTwitchAuth), nameof(MethodDescriptorAttribute));
                if (reqTwitch != null) requiresTwitch = (bool)reqTwitch;

                var reqSession = GetValue(data, nameof(MethodDescriptorAttribute.RequiresSession), nameof(MethodDescriptorAttribute));
                if (reqSession != null) requiresSession = (bool)reqSession;

                if (data.AttributeType.Name == "HttpGetAttribute") methodName = "GET";
                if (data.AttributeType.Name == "HttpPutAttribute") methodName = "PUT";
                if (data.AttributeType.Name == "HttpPostAttribute") methodName = "POST";
                if (data.AttributeType.Name == "HttpDeleteAttribute") methodName = "DELETE";

                if (data.ConstructorArguments.Count > 0)
                {
                    var arg = data.ConstructorArguments.First();
                    if (arg.ArgumentType == typeof(string))
                    {
                        path += arg.Value?.ToString();
                    }
                }
            }

            if (string.IsNullOrEmpty(name)) name = method.Name;

            var methodPath = path.Replace("{", ":").Replace("}", "");
            var methodParameters = method.GetParameters();
            var parameters = new List<DocumentApiMethodParameter>();
            var example = "{}";

            foreach (var parameter in methodParameters)
            {
                var fromBodyAttribute = parameter.GetCustomAttributes().Any(x => x.GetType().Name == "FromBodyAttribute");

                if (fromBodyAttribute || !methodPath.Contains(":" + parameter.Name, StringComparison.OrdinalIgnoreCase))
                {
                    example = GenerateTypeExample(parameter.ParameterType);
                }
                else
                {
                    parameters.Add(GenerateParameter(parameter));
                }
            }

            return new DocumentApiMethod(
                name,
                methodName,
                methodPath,
                description,
                parameters,
                new DocumentApiMethodAuthentication(requiresTwitch, requiresAuth, requiresSession, requiresAdmin),
                methodName == "GET" ? null : new DocumentApiMethodRequest("application/json", example),
                GenerateResponse(method));
        }

        private DocumentApiMethodParameter GenerateParameter(ParameterInfo parameter)
        {
            string type = parameter.ParameterType.Name;
            string name = parameter.Name;
            string desc = "";
            string def = "";
            bool optional = false;

            return new DocumentApiMethodParameter(type, name, desc, def, optional);
        }

        private static DocumentApiMethodResponse GenerateResponse(MethodInfo method)
        {
            const string ContentType = "application/json";
            string example = null;
            var returnTypeName = method.ReturnType.ToString();

            if (method.ReturnType.GenericTypeArguments.Length > 0)
            {
                var genericArgs = method.ReturnType.GenericTypeArguments;
                var returnType = genericArgs.FirstOrDefault();

                if (!returnType.IsAbstract)
                {
                    example = GenerateTypeExample(returnType);
                }
                else if (returnType.IsArray)
                {
                    example = $"[{GenerateTypeExample(returnType.GetElementType())}]";
                }

                if (method.ReturnType.Name.Contains("Task"))
                {
                    returnTypeName = genericArgs.FirstOrDefault()?.Name ?? typeof(void).Name;
                }
                //else
                //{
                //    // handle lists
                //    throw new NotImplementedException("Lists have not been implemented yet");
                //}
            }
            else
            {
                if (method.ReturnType.Name.Contains("Task"))
                {
                    returnTypeName = typeof(void).Name;
                    example = "{}";
                }
                else
                {
                    example = GenerateTypeExample(method.ReturnType);
                }
            }

            return new DocumentApiMethodResponse(ContentType, returnTypeName, example);
        }

        private static string GenerateTypeExample(Type returnType)
        {
            // handle Ravenfall special Many<> and Single<> types

            if (returnType.IsGenericType)
            {
                var gTypeDef = returnType.GetGenericTypeDefinition();
                if (gTypeDef == typeof(Many<>))
                {
                    var elementType = returnType.GenericTypeArguments.FirstOrDefault();
                    var data = GenerateTypeExample(elementType);
                    return $"{{\"Values\": [{data}]}}";
                }
                if (gTypeDef == typeof(Single<>))
                {
                    var elementType = returnType.GenericTypeArguments.FirstOrDefault();
                    var data = GenerateTypeExample(elementType);
                    return $"{{\"Value\": {data}}}";
                }
            }

            if (returnType.IsAbstract) return "{}";

            if (returnType == typeof(string))
            {
                return "\"\"";
            }

            if (returnType == typeof(int) ||
                returnType == typeof(uint) ||
                returnType == typeof(float) ||
                returnType == typeof(decimal) ||
                returnType == typeof(short) ||
                returnType == typeof(ushort) ||
                returnType == typeof(double) ||
                returnType == typeof(long) ||
                returnType == typeof(ulong) ||
                returnType == typeof(byte) ||
                returnType == typeof(sbyte))
            {
                return "0";
            }

            if (returnType == typeof(bool))
            {
                return "false";
            }

            if (returnType == typeof(void))
            {
                return "";
            }

            try
            {
                var obj = Activator.CreateInstance(returnType);
                var interfaces = returnType.GetInterfaces();
                if (interfaces.Length > 0)
                {
                    var collectionType = interfaces.FirstOrDefault(x =>
                        x.Name.Contains("IList") || x.Name.Contains("IEnumerable") || x.Name.Contains("ICollection"));
                    if (collectionType != null)
                    {
                        var elementType = collectionType.GenericTypeArguments.FirstOrDefault();
                        var data = GenerateTypeExample(elementType);
                        return $"[{data}]";
                    }
                }
                var generateTypeExample = JSON.Stringify(obj);
                return generateTypeExample;
            }
            catch
            {
            }

            try
            {
                var obj = FormatterServices.GetUninitializedObject(returnType);
                return JSON.Stringify(obj);
            }
            catch { return null; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static object GetValue(CustomAttributeData data, string name, string typeName)
        {
            if (data.AttributeType.Name != typeName) return null;
            return GetValue(data.NamedArguments.FirstOrDefault(x => x.MemberName == name));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static object GetValue(CustomAttributeNamedArgument arg)
        {
            if (arg == null) return null;
            if (arg.TypedValue == null) return null;
            return arg.TypedValue.Value;
        }
    }
}
