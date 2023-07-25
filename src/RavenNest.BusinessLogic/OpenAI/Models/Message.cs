/* 
 * This file is part of RavenBot (https://www.github.com/zerratar/ravenbot/).
 * Copyright (c) 2017-2023 Shinobytes, Karl Patrik Johansson, zerratar@gmail.com
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.  
 **/

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Shinobytes.OpenAI.Models
{
    public class Message
    {
        [JsonProperty("role")]
        [JsonConverter(typeof(StringEnumConverter))]
        public MessageRole Role { get; set; }
        [JsonProperty("content")]//, NullValueHandling = NullValueHandling.Ignore)]
        public string Content { get; set; }
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string name { get; set; }
        [JsonProperty("function_call", NullValueHandling = NullValueHandling.Ignore)]
        public FunctionCall FunctionCall { get; set; }

        public static Message Create(MessageRole role, string content)
        {
            return new Message
            {
                Role = role,
                Content = content,
            };
        }

        public static Message CreateFunctionResult(string functionName, object result)
        {
            return new Message
            {
                Role = MessageRole.Function,
                Content = result == null ? "{}" : Newtonsoft.Json.JsonConvert.SerializeObject(result, Formatting.Indented),
                name = functionName
            };
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (base.Equals(obj) || object.ReferenceEquals(this, obj) || this == obj)
                return true;

            if (obj is Message b)
            {
                if (b == this) return true;
                if (b.Content == this.Content && b.Role == this.Role)
                {
                    if (b.FunctionCall == this.FunctionCall) return true;
                    if (b.FunctionCall != null && this.FunctionCall != null)
                    {
                        return b.FunctionCall.Equals(this.FunctionCall);
                    }

                    if (b.FunctionCall == null && this.FunctionCall == null)
                    {
                        return b.GetHashCode() == this.FunctionCall.GetHashCode();
                    }
                }
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Role, Content, FunctionCall);
        }
    }

    public class FunctionCall
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("arguments")]
        public string Arguments { get; set; }

        public override bool Equals(object obj)
        {
            return obj is FunctionCall call &&
                   Name == call.Name &&
                   Arguments == call.Arguments;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Arguments);
        }
    }

    public enum MessageRole
    {
        [EnumMember(Value = "user")] User,
        [EnumMember(Value = "assistant")] Assistant,
        [EnumMember(Value = "function")] Function,
        [EnumMember(Value = "system")] System
    }

    public class Function
    {
        private MethodInfo invocationTarget;
        private object instance;

        public static Function Create<T1, T2>(Func<T1, T2> method, object obj = null, string description = null, bool preventDefault = false)
        {
            return new Function(method.GetMethodInfo(), obj, description, preventDefault);
        }

        public static Function Create<T1>(Func<T1> method, object obj = null, string description = null, bool preventDefault = false)
        {
            return new Function(method.GetMethodInfo(), obj, description, preventDefault);
        }

        public static Function Create<T1>(Action<T1> method, object obj = null, string description = null, bool preventDefault = false)
        {
            return new Function(method.GetMethodInfo(), obj, description, preventDefault);
        }

        public override string ToString()
        {
            return invocationTarget.ReturnType.Name + " " + Name + "(" + string.Join(", ", Parameters.Select(x => x.ParameterType.Name + " " + x.Name)) + ")";
        }

        public object? Invoke(string argumentsJson)
        {
            var obj = JToken.Parse(argumentsJson);
            var parameterValue = string.Empty;
            try
            {
                var parameters = new List<object>();
                foreach (var p in this.Parameters)
                {
                    var pobj = obj[p.Name];
                    var json = pobj.ToString();
                    parameterValue = json;
                    var value = ParseParameter(json, p.ParameterType);
                    parameters.Add(value);
                }

                return invocationTarget.Invoke(instance, parameters.ToArray());
            }
            catch (Exception exc)
            {
                return "Using value: '" + parameterValue + "' when calling '" + Name + "' threw an exception: " + exc.ToString();
            }
        }

        public object? Invoke()
        {
            try
            {
                return invocationTarget.Invoke(instance, null);
            }
            catch (Exception exc)
            {
                return "The function '" + Name + "' threw an exception: " + exc.ToString();
            }
        }


        private object ParseParameter(string json, Type t)
        {
            // get normal ones first
            if (t == typeof(string))
            {
                return json;
            }

            if (json == "null" || string.IsNullOrEmpty(json))
            {
                return null;
            }

            if (t == typeof(TimeSpan) || t == typeof(TimeSpan?)) return TimeSpan.Parse(json);
            if (t == typeof(DateTime) || t == typeof(DateTime?)) return DateTime.Parse(json);
            if (t == typeof(DateTimeOffset) || t == typeof(DateTimeOffset?)) return DateTimeOffset.Parse(json);
            if (t == typeof(sbyte) || t == typeof(sbyte?)) return sbyte.Parse(json);
            if (t == typeof(byte) || t == typeof(byte?)) return byte.Parse(json);
            if (t == typeof(ushort) || t == typeof(ushort?)) return ushort.Parse(json);
            if (t == typeof(short) || t == typeof(short?)) return short.Parse(json);
            if (t == typeof(int) || t == typeof(int?)) return int.Parse(json);
            if (t == typeof(uint) || t == typeof(uint?)) return uint.Parse(json);
            if (t == typeof(long) || t == typeof(long?)) return long.Parse(json);
            if (t == typeof(ulong) || t == typeof(ulong?)) return ulong.Parse(json);
            if (t == typeof(float) || t == typeof(float?)) return float.Parse(json, System.Globalization.NumberStyles.Any, CultureInfo.CreateSpecificCulture("en-US"));
            if (t == typeof(double) || t == typeof(double?)) return double.Parse(json, System.Globalization.NumberStyles.Any, CultureInfo.CreateSpecificCulture("en-US"));
            if (t == typeof(decimal) || t == typeof(decimal?)) return decimal.Parse(json, System.Globalization.NumberStyles.Any, CultureInfo.CreateSpecificCulture("en-US"));
            if (t == typeof(Guid) || t == typeof(Guid?)) return Guid.Parse(json);

            if (!FunctionConverter.IsSimpleType(t))
            {
                return JsonConvert.DeserializeObject(json, t);
            }

            return json;
        }

        public Function(MethodInfo invocationTarget, object instance, string description, bool preventDefault)
        {
            this.invocationTarget = invocationTarget;
            this.instance = instance;
            this.Name = invocationTarget.Name;
            this.Parameters = invocationTarget.GetParameters();
            this.PreventDefault = preventDefault;

            if (string.IsNullOrEmpty(description))
            {
                var descAttribute = invocationTarget.GetCustomAttribute<DescriptionAttribute>();
                if (descAttribute == null)
                {
                    return;
                }

                description = descAttribute.Description;
            }

            this.Description = description;
        }

        public Function() { }
        public string Name { get; set; }
        public string Description { get; set; }
        public ParameterInfo[] Parameters { get; }

        /// <summary>
        ///     Whether or not prevent the result of this function call to be sent to openAI API.
        /// </summary>
        public bool PreventDefault { get; set; }
    }

    public class FunctionConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var function = value as Function;

            /* Expected Format
             "name": "get_current_weather",
              "description": "Get the current weather in a given location",
              "parameters": {
                "type": "object",
                "properties": {
                  "location": {
                    "type": "string",
                    "description": "The city and state, e.g. San Francisco, CA"
                  },
                  "unit": {
                    "type": "string",
                    "enum": ["celsius", "fahrenheit"]
                  }
                },
                "required": ["location"]
              }             
             */

            writer.WriteStartObject();
            {
                var requiredParameters = new List<string>();
                writer.WritePropertyName("name");
                writer.WriteValue(function.Name);
                writer.WritePropertyName("description");
                writer.WriteValue(function.Description);
                writer.WritePropertyName("parameters");
                writer.WriteStartObject();
                {
                    writer.WritePropertyName("type");
                    writer.WriteValue("object");
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    {
                        for (var i = 0; i < function.Parameters.Length; i++)
                        {
                            var parameter = function.Parameters[i];
                            var name = parameter.Name;
                            var type = parameter.ParameterType;
                            var descriptionAttribute = parameter.GetCustomAttribute<DescriptionAttribute>();

                            WriteType(writer, name, type, descriptionAttribute?.Description);

                            if (!parameter.HasDefaultValue)
                            {
                                requiredParameters.Add(parameter.Name);
                            }
                        }
                    }
                    writer.WriteEndObject();

                    writer.WritePropertyName("required");
                    writer.WriteStartArray();
                    foreach (var parameter in requiredParameters)
                    {
                        writer.WriteValue(parameter);
                    }
                    writer.WriteEndArray();

                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }

        private void WriteType(JsonWriter writer, string name, Type type, string description)
        {
            writer.WritePropertyName(name);
            writer.WriteStartObject();

            writer.WritePropertyName("type");
            writer.WriteValue(ResolveTypeName(type, out var isObject));

            if (isObject)
            {
                // we deemed this is an object type.
                // we have to write out the properties avaiable in the type.
                writer.WritePropertyName("properties");
                writer.WriteStartObject();
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var descriptionAttribute = property.GetCustomAttribute<DescriptionAttribute>();
                    WriteType(writer, property.Name, property.PropertyType, descriptionAttribute?.Description);
                }
                writer.WriteEndObject();
            }
            else
            {
                if (type.IsEnum)
                {
                    writer.WritePropertyName("enum");
                    writer.WriteStartArray();
                    foreach (var enumValue in Enum.GetValues(type))
                    {
                        writer.WriteValue(enumValue.ToString());
                    }
                    writer.WriteEndArray();
                }

                if (type.IsArray)
                {
                    throw new NotImplementedException();
                }
            }

            if (!string.IsNullOrEmpty(description))
            {
                writer.WritePropertyName("description");
                writer.WriteValue(description);
            }
            writer.WriteEndObject();
        }

        private string ResolveTypeName(Type parameterType, out bool isObject)
        {
            // openai is super picky, we have limited types we can use.
            // "string", "boolean", "object", "array", "enum"

            isObject = false;

            if (parameterType.IsArray)
            {
                return "array";
            }

            if (parameterType.IsEnum)
            {
                return "enum";
            }

            // we will check for most common ones
            if (parameterType == typeof(bool))
            {
                return "boolean";
            }

            if (parameterType == typeof(bool?) || IsSimpleType(parameterType))
            {
                return "string";
            }

            isObject = true;
            return "object";
        }

        public static bool IsSimpleType(Type type)
        {
            return
                type.IsPrimitive ||
                new Type[] {
            typeof(string),
            typeof(decimal),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(TimeSpan),
            typeof(Guid)
                }.Contains(type) ||
                Convert.GetTypeCode(type) != TypeCode.Object ||
                (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) && IsSimpleType(type.GetGenericArguments()[0]));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(Function) == objectType;
        }
    }
}
