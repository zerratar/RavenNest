using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RavenNest.BusinessLogic.Providers
{
    public interface IPropertyProvider
    {
        IReadOnlyList<PropertyInfo> GetProperties<TType, TPropertyType>();
    }

    public class MemoryCachedPropertyProvider : IPropertyProvider
    {

        private readonly ConcurrentDictionary<Type, List<PropertyInfo>> typeProperties
            = new ConcurrentDictionary<Type, List<PropertyInfo>>();

        public IReadOnlyList<PropertyInfo> GetProperties<TType, TPropertyType>()
        {
            var type = typeof(TType);
            if (!typeProperties.TryGetValue(type, out var storedProps))
            {
                storedProps = typeProperties[type] = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();
            }

            return storedProps.Where(x => x.PropertyType == typeof(TPropertyType)).ToList();
        }
    }
}
