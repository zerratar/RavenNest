using RavenNest.BusinessLogic.ScriptParser;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

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
                    // ignored
                }
            }
        }

        //public static List<TTo> MapMany<TTo, TFrom>(IEnumerable<TFrom> data) where TTo : new()
        //{
        //    var result = new List<TTo>();
        //    foreach (var dataItem in data)
        //    {
        //        result.Add(Map<TTo, TFrom>(dataItem));
        //    }
        //    return result;
        //}

        public static List<TTo> MapMany<TTo>(IEnumerable<object> data) where TTo : new()
        {
            var result = new List<TTo>();
            foreach (var dataItem in data)
            {
                result.Add(Map<TTo>(dataItem));
            }
            return result;
        }

        public static T Clone<T>(T obj) where T : new()
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Newtonsoft.Json.JsonConvert.SerializeObject(obj));
        }


        [Obsolete("Use Map<TTo> instead")]
        public static TTo Map<TTo, TFrom>(TFrom data) where TTo : new()
        {
            return Map<TTo>(data);
        }

        public static TTo Map<TTo>(object data) where TTo : new()
        {
            var output = new TTo();
            if (data != null)
            {
                var targetType = data.GetType();
                var props = typeof(TTo).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var p = props.FirstOrDefault(x => x.Name == prop.Name);
                    if (p == null)
                        continue;

                    try
                    {
                        var value = prop.GetValue(data);
                        if (prop.PropertyType.IsEnum)
                            p.SetValue(output, Convert.ToInt32(value));
                        else
                        {
                            if ((p.PropertyType == typeof(DateTime?) || p.PropertyType == typeof(DateTime)) && value is string dateString)
                            {
                                if (DateTime.TryParse(dateString, out var dt))
                                {
                                    p.SetValue(output, dt);
                                    continue;
                                }

                                string format = "MMM dd yyyy h:mmtt";
                                DateTime dateTime;

                                if (DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture,
                                                           DateTimeStyles.AdjustToUniversal, out dateTime))
                                {
                                    DateTime utcDateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                                    p.SetValue(output, utcDateTime);
                                    continue;
                                }
                            }

                            p.SetValue(output, value);
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            return output;
        }

        //public static TTo Map<TTo, TFrom>(TFrom data) where TTo : new()
        //{
        //    var output = new TTo();
        //    var props = typeof(TTo).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        //    foreach (var prop in typeof(TFrom).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        //    {
        //        var p = props.FirstOrDefault(x => x.Name == prop.Name);
        //        if (p == null)
        //            continue;

        //        try
        //        {
        //            if (data == null) continue;

        //            var value = prop.GetValue(data);
        //            if (prop.PropertyType.IsEnum)
        //                p.SetValue(output, Convert.ToInt32(value));
        //            else
        //                p.SetValue(output, value);
        //        }
        //        catch
        //        {
        //            // ignored
        //        }
        //    }
        //    return output;
        //}

    }
}
