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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf<T>(this T[] items, T value)
        {
            return Array.IndexOf<T>(items, value);
        }

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


        public static List<T> AsList<T>(this IEnumerable<T> items, Func<T, bool> predicateWhere)
        {
            var result = new List<T>();
            foreach (var item in items)
            {
                if (predicateWhere(item))
                {
                    result.Add(item);
                }
            }
            return result;
        }

        public static List<T2> SelectWhere<T, T2>(this IEnumerable<T> items, Func<T, bool> predicateWhere, Func<T, T2> select)
        {
            var result = new List<T2>();
            foreach (var item in items)
            {
                if (predicateWhere(item))
                {
                    result.Add(select(item));
                }
            }
            return result;
        }

        public static List<T> Slice<T>(this T[] src, int skip, int take)
        {
            var result = new List<T>();
            for (int i = skip, j = 0; i < src.Length && j < take; ++i, ++j)
            {
                result.Add(src[i]);
            }
            return result;
        }

        public static List<T> Slice<T>(this IReadOnlyList<T> src, int skip, int take)
        {
            var result = new List<T>();
            for (int i = skip, j = 0; i < src.Count && j < take; ++i, ++j)
            {
                result.Add(src[i]);
            }
            return result;
        }

        public static List<T> Slice<T>(this IEnumerable<T> src, int skip, int take)
        {
            var result = new List<T>();
            var j = 0;
            foreach (var item in src)
            {
                if (result.Count >= take)
                {
                    break;
                }
                if (skip <= j)
                {
                    result.Add(item);
                }
                ++j;
            }
            return result;
        }

        public static List<T> SliceAs<T, T2>(this IReadOnlyList<T2> src, int skip, int take, Func<T2, T> select)
        {
            var result = new List<T>();
            for (int i = skip, j = 0; i < src.Count && j < take; ++i, ++j)
            {
                result.Add(select(src[i]));
            }
            return result;
        }
        public static T[] SelectAsArray<T, T2>(this IReadOnlyList<T2> src, Func<T2, T> select)
        {
            var result = new T[src.Count];
            for (var i = 0; i < src.Count; ++i)
            {
                result[i] = select(src[i]);
            }
            return result;
        }

        public static IReadOnlyList<T> SelectAsReadOnly<T, T2>(this IReadOnlyList<T2> src, Func<T2, T> select)
        {
            var result = new List<T>(src.Count);
            for (var i = 0; i < src.Count; ++i)
            {
                result.Add(select(src[i]));
            }
            return result;
        }

        public static IReadOnlyList<T> AsReadOnlyList<T>(this IEnumerable<T> items)
        {
            if (items is T[] array) return array;
            if (items is IReadOnlyList<T> list) return list;
            return items.AsList();
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
