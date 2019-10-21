using System;
using System.Linq;
using System.Reflection;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic
{
    public class DataMapper
    {
        public static void RefMap<T, T2>(T source, T2 target, params string[] exclude)
        {
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in typeof(T2).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var p = props.FirstOrDefault(x => x.Name == prop.Name);
                if (p == null) continue;
                if (exclude != null && exclude.Equals(p.Name)) continue;
                try
                {
                    var value = prop.GetValue(source);
                    if (prop.PropertyType.IsEnum)
                    {
                        var intValue = Convert.ToInt32(value);
                        p.SetValue(target, intValue);
                    }
                    else
                    {
                        p.SetValue(target, value);
                    }
                }
                catch
                {
                }
            }
        }

        public static T Map<T, T2>(T2 data) where T : new()
        {
            var output = new T();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in typeof(T2).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var p = props.FirstOrDefault(x => x.Name == prop.Name);
                if (p == null)
                {
                    continue;
                }

                try
                {
                    if (data == null) continue;
                    var value = prop.GetValue(data);
                    if (prop.PropertyType.IsEnum)
                    {
                        var intValue = Convert.ToInt32(value);
                        p.SetValue(output, intValue);
                    }
                    else
                    {
                        p.SetValue(output, value);
                    }
                }
                catch (Exception exc)
                {
                }
            }
            return output;
        }

    }
}
