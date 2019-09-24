using System;

namespace RavenNest.BusinessLogic.Serializers
{
    public interface IBinarySerializer
    {
        object Deserialize(byte[] data, Type type);
        byte[] Serialize(object data);
    }
}