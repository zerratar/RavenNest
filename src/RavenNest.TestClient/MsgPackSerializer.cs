using System;
using System.Linq;
using System.Reflection;

namespace RavenNest.BusinessLogic.Serializers
{
    public class MsgPackSerializer : IBinarySerializer
    {
        private MethodInfo deserializeMethod;
        private MethodInfo serializeMethod;

        public MsgPackSerializer()
        {
            this.deserializeMethod = typeof(MessagePack.MessagePackSerializer).GetMethod("Deserialize", new Type[] { typeof(byte[]) });
            this.serializeMethod = typeof(MessagePack.MessagePackSerializer).GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault(x => x.Name == "Serialize" && x.GetParameters().Length == 1);
        }

        public object Deserialize(byte[] data, Type type)
        {
            var deserializer = this.deserializeMethod.MakeGenericMethod(type);
            return deserializer.Invoke(null, new object[] { data });
        }

        public byte[] Serialize(object data)
        {
            var serializer = this.serializeMethod.MakeGenericMethod(data.GetType());
            return (byte[])serializer.Invoke(null, new object[] { data });
        }
    }
}