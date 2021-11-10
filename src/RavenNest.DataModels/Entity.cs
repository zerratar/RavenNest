using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RavenNest.DataModels
{
    public interface IEntity { }
    public class Entity<TModel> : IEntity, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool Set<TProp>(ref TProp item, TProp value, [CallerMemberName] string propertyName = null)
        {
            if (object.Equals(item, value) || object.ReferenceEquals(item, value)) return false;
            item = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public static class EnumerableExtensions
    {
        /// <summary>
        /// Gets a list of the enumeration with the least allocation possible.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <returns></returns>
        public static List<T> AsList<T>(this IEnumerable<T> items)
        {
            if (items is List<T> list) return list;
            return items.ToList();
        }

        public static IReadOnlyList<T> AsIReadOnlyList<T>(this IEnumerable<T> items)
        {
            if (items is IReadOnlyList<T> list) return list;
            return items.ToList();
        }

        public static void ForEach<T>(this IEnumerable<T> items, Action<T> forEach)
        {
            foreach (var item in items)
            {
                forEach(item);
            }
        }

        public static IEnumerable<KeyValuePair<TKey, TValue>> ToKeyValuePair<TKey, TValue>(this IEnumerable<TValue> values, Func<TValue, TKey> keySelector, Func<TValue, TValue> valueSelector)
        {
            return values.Select(x => new KeyValuePair<TKey, TValue>(keySelector(x), valueSelector(x)));
        }
    }

    //public enum EntityState
    //{
    //    Unchanged,
    //    Added,
    //    Modified,
    //    Removed
    //}
}
