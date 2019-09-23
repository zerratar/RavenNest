using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace RavenNest.TestClient
{
    public interface IBinarySerializer
    {
        object Deserialize(byte[] data, Type type);
        byte[] Serialize(object data);
    }

    public class BinarySerializer : IBinarySerializer
    {
        public object Deserialize(byte[] data, Type type)
        {
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                var res = Deserialize(br, type);
                if (res != null) return res;
                return DeserializeComplex(br, type);
            }
        }

        public byte[] Serialize(object data)
        {
            if (data == null) return new byte[0];
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                if (!Serialize(bw, data))
                {
                    SerializeComplex(bw, data, data.GetType());
                }
                return ms.ToArray();
            }
        }

        private bool Serialize(BinaryWriter bw, object data)
        {
            var type = data.GetType();

            if (SerializeSpecial(bw, data, type)) return true;

            var targetMethod = bw.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(x => MatchWriteMethod(x, type));

            if (targetMethod != null)
            {
                targetMethod.Invoke(bw, new object[] { data });
                return true;
            }

            return false;
        }

        private void Serialize(BinaryWriter bw, object data, PropertyInfo property)
        {
            var type = property.PropertyType;
            var value = property.GetValue(data);
            Serialize(bw, value, type);
        }

        private void Serialize(BinaryWriter bw, object data, FieldInfo field)
        {
            var type = field.FieldType;
            var value = field.GetValue(data);
            Serialize(bw, value, type);
        }

        private void Serialize(BinaryWriter bw, object value, Type type)
        {
            if (SerializeSpecial(bw, value, type)) return;

            var targetMethod = bw.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(x => MatchWriteMethod(x, type));

            if (targetMethod != null)
            {
                targetMethod.Invoke(bw, new object[] { value });
                return;
            }

            SerializeComplex(bw, value, type);
        }

        private object Deserialize(BinaryReader br, PropertyInfo property)
        {
            var type = property.PropertyType;
            return Deserialize(br, type);
        }

        private object Deserialize(BinaryReader br, FieldInfo field)
        {
            var type = field.FieldType;
            return Deserialize(br, type);
        }

        private object Deserialize(BinaryReader br, Type type)
        {
            if (TryDeserializeSpecial(br, type, out var res))
            {
                return res;
            }

            var targetMethod = br.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(x => MatchReadName(x.Name, type.Name));

            if (targetMethod != null)
            {
                return targetMethod.Invoke(br, null);
            }

            return DeserializeComplex(br, type);
        }

        private void SerializeComplex(BinaryWriter bw, object data, Type type)
        {
            var hasData = data != null ? 1 : 0;
            bw.Write((byte)hasData);
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                Serialize(bw, data, prop);
            }

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (!field.IsInitOnly)
                {
                    Serialize(bw, data, field);
                }
            }
        }

        private object DeserializeComplex(BinaryReader br, Type type)
        {
            // checks if the reference type is null or not
            if (br.ReadByte() == 0)
            {
                return null;
            }

            var obj = FormatterServices.GetUninitializedObject(type);
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                // still need to deserialize so we read from the stream
                var value = Deserialize(br, prop);
                if (prop.CanWrite)
                {
                    prop.SetValue(obj, value);
                }
            }

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                // still need to deserialize so we read from the stream
                var value = Deserialize(br, field);
                if (!field.IsInitOnly)
                {
                    field.SetValue(obj, value);
                }
            }

            return obj;
        }

        private bool SerializeSpecial(BinaryWriter bw, object value, Type type)
        {
            if (type.IsArray)
            {
                if (type.HasElementType)
                {
                    var elementType = type.GetElementType();
                    if (SerializeArray(bw, value, elementType, type))
                        return true;
                }
            }

            if (type == typeof(string))
            {
                var hasValue = value != null;
                bw.Write(hasValue);
                if (hasValue) bw.Write(value.ToString());
                return true;
            }

            if (type == typeof(Guid))
            {
                bw.Write(((Guid)value).ToByteArray());
                return true;
            }

            if (type == typeof(DateTime))
            {
                bw.Write(((DateTime)value).ToBinary());
                return true;
            }

            if (type == typeof(TimeSpan))
            {
                bw.Write(((TimeSpan)value).Ticks);
                return true;
            }

            return false;
        }

        private bool SerializeArray(BinaryWriter bw, object value, Type elementType, Type type)
        {
            if (elementType == null || !type.IsArray) return false;
            if (value == null)
            {
                bw.Write(-1);
                return true;
            }

            var array = (Array)value;
            var len = array.Length;
            bw.Write(len);
            for (var i = 0; i < len; ++i)
            {
                Serialize(bw, array.GetValue(i));
            }
            return true;
        }

        private bool TryDeserializeArray(BinaryReader br, Type elementType, Type arrayType, out object result)
        {
            result = null;
            if (elementType == null || !arrayType.IsArray) return false;
            var size = br.ReadInt32();
            if (size == -1)
            {
                result = null;
                return true;
            }

            var array = (Array)Activator.CreateInstance(arrayType, new object[] { size });
            result = array;

            if (size == 0)
            {
                return true;
            }

            for (var i = 0; i < size; ++i)
            {
                var value = Deserialize(br, elementType);
                array.SetValue(value, i);
            }

            return true;
        }

        private bool TryDeserializeSpecial(BinaryReader br, Type type, out object result)
        {
            result = null;

            if (type.IsArray)
            {
                if (type.HasElementType)
                {
                    var elementType = type.GetElementType();
                    if (TryDeserializeArray(br, elementType, type, out result))
                        return true;
                }
            }

            if (type == typeof(Guid))
            {
                result = new Guid(br.ReadBytes(16));
                return true;
            }

            if (type == typeof(string))
            {
                var hasValue = br.ReadByte() == 1;
                if (hasValue) result = br.ReadString();
                return true;
            }

            if (type == typeof(DateTime))
            {
                result = DateTime.FromBinary(br.ReadInt64());
                return true;
            }

            if (type == typeof(TimeSpan))
            {
                result = TimeSpan.FromTicks(br.ReadInt64());
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool MatchReadName(string methodName, string typeName)
        {
            if (!methodName.StartsWith("Read", StringComparison.OrdinalIgnoreCase)) return false;
            return methodName.StartsWith("Read" + typeName, StringComparison.OrdinalIgnoreCase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool MatchWriteMethod(MethodInfo x, Type type)
        {
            if (x.Name != "Write") return false;
            var parameters = x.GetParameters();
            if (parameters.Length != 1) return false;
            return parameters.All(y => y.ParameterType == type);
        }
    }
}