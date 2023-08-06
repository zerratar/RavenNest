using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RavenNest.DataModels
{
    public interface IEntity
    {
        Guid Id { get; set; }
    }

    public class Entity<TModel> : IEntity, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
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

        public static IEnumerable<T> OrderByRandomWeighted<T>(this IEnumerable<T> source, Func<T, double> weightSelector, Random random = null)
        {
            if (random == null)
                random = new Random();

            var weightedList = source.Select(item => (item, weight: weightSelector(item))).ToList();
            var totalWeight = weightedList.Sum(x => x.weight);
            var orderedList = new List<T>();

            while (weightedList.Count > 0)
            {
                var target = random.NextDouble() * totalWeight;
                double cumulativeWeight = 0;
                for (int i = 0; i < weightedList.Count; i++)
                {
                    cumulativeWeight += weightedList[i].weight;
                    if (cumulativeWeight >= target)
                    {
                        orderedList.Add(weightedList[i].item);
                        totalWeight -= weightedList[i].weight;
                        weightedList.RemoveAt(i);
                        break;
                    }
                }
            }

            return orderedList;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf<T>(this T[] items, T value)
        {
            return Array.IndexOf<T>(items, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(this string[] items, string value, StringComparison comparison)
        {
            for (var i = 0; i < items.Length; i++)
            {
                if (string.Compare(items[i], value, comparison) == 0) return i;
            }
            return -1;
        }

        /// <summary>
        /// Gets a list of the enumeration with the least allocation possible.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <returns></returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> ToSpan<T>(this IEnumerable<T> items)
        {
            return new Span<T>(items.AsArray());
        }

        /// <summary>
        /// Gets a list of the enumeration with the least allocation possible.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <returns></returns>
        /// 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] AsArray<T>(this IEnumerable<T> items)
        {
            if (items is T[] list) return list;
            return items.ToArray();
        }


        /// <summary>
        /// Gets a list of the enumeration with the least allocation possible.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <returns></returns>
        /// 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> AsList<T>(this IEnumerable<T> items)
        {
            if (items is List<T> list) return list;
            return items.ToList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T2> AsList<T, T2>(this IEnumerable<T> items, Func<T, T2> select)
        {
            var result = new List<T2>();
            foreach (var item in items)
            {
                result.Add(select(item));
            }
            return result;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> Slice<T>(this T[] src, int skip, int take)
        {
            var result = new List<T>();
            for (int i = skip, j = 0; i < src.Length && j < take; ++i, ++j)
            {
                result.Add(src[i]);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> Slice<T>(this IReadOnlyList<T> src, int skip, int take)
        {
            var result = new List<T>();
            for (int i = skip, j = 0; i < src.Count && j < take; ++i, ++j)
            {
                result.Add(src[i]);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> SliceAs<T, T2>(this IReadOnlyList<T2> src, int skip, int take, Func<T2, T> select)
        {
            var result = new List<T>();
            for (int i = skip, j = 0; i < src.Count && j < take; ++i, ++j)
            {
                result.Add(select(src[i]));
            }
            return result;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] SelectAsArray<T, T2>(this IReadOnlyList<T2> src, Func<T2, T> select)
        {
            var result = new T[src.Count];
            for (var i = 0; i < src.Count; ++i)
            {
                result[i] = select(src[i]);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyList<T> SelectAsReadOnly<T, T2>(this IReadOnlyList<T2> src, Func<T2, T> select)
        {
            var result = new List<T>(src.Count);
            for (var i = 0; i < src.Count; ++i)
            {
                result.Add(select(src[i]));
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IReadOnlyList<T> AsReadOnlyList<T>(this IEnumerable<T> items)
        {
            if (items is T[] array) return array;
            if (items is IReadOnlyList<T> list) return list;
            return items.AsList();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ForEach<T>(this IEnumerable<T> items, Action<T> forEach)
        {
            foreach (var item in items)
            {
                forEach(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
