using System;
using System.Text;
using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Serializers
{
    public class JsonSerializer : IBinarySerializer
    {
        public object Deserialize(byte[] data, Type type)
        {
            var json = Encoding.UTF8.GetString(data);
            if (type == null)
            {
                return JsonConvert.DeserializeObject(json);
            }

            return JsonConvert.DeserializeObject(json, type);
        }

        public byte[] Serialize(object data)
        {
            var json = JsonConvert.SerializeObject(data);
            return Encoding.UTF8.GetBytes(json);
        }
    }
}
