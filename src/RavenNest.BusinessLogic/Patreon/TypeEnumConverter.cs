using System;
using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    internal class TypeEnumConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(TypeEnum) || t == typeof(TypeEnum?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "campaign":
                    return TypeEnum.Campaign;
                case "goal":
                    return TypeEnum.Goal;
                case "reward":
                    return TypeEnum.Reward;
                case "user":
                    return TypeEnum.User;
            }
            throw new Exception("Cannot unmarshal type TypeEnum");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (TypeEnum)untypedValue;
            switch (value)
            {
                case TypeEnum.Campaign:
                    serializer.Serialize(writer, "campaign");
                    return;
                case TypeEnum.Goal:
                    serializer.Serialize(writer, "goal");
                    return;
                case TypeEnum.Reward:
                    serializer.Serialize(writer, "reward");
                    return;
                case TypeEnum.User:
                    serializer.Serialize(writer, "user");
                    return;
            }
            throw new Exception("Cannot marshal type TypeEnum");
        }

        public static readonly TypeEnumConverter Singleton = new TypeEnumConverter();
    }
}

