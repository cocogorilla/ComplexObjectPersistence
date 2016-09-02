using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;

namespace ComplexObjectPersistence
{
    public static class LinqExtensions
    {
        public static void Iter<T>(this IEnumerable<T> @this, Action<T> act)
        {
            foreach (var item in @this)
            {
                act(item);
            }
        }

        public static void Iter<T>(this IEnumerable<T> @this, Action<T, int> act)
        {
            var counter = 0;
            foreach (var item in @this)
            {
                act(item, counter++);
            }
        }

        public static async Task IterAsync<T>(this IEnumerable<T> @this, Func<T, Task> act)
        {
            foreach (var item in @this)
            {
                await act(item);
            }
        }

        public static async Task IterAsync<T>(this IEnumerable<T> @this, Func<T, int, Task> act)
        {
            var counter = 0;
            foreach (var item in @this)
            {
                await act(item, counter++);
            }
        }

        public static IReadOnlyCollection<T> AsReadonlyCollection<T>(this IEnumerable<T> @this)
        {
            return @this.ToList().AsReadOnly();
        }

        /// <summary>
        /// This extension converts an enumerable set to a Dapper TVP
        /// </summary>
        /// <typeparam name="T">type of enumerbale</typeparam>
        /// <param name="enumerable">list of values</param>
        /// <param name="typeName">database type name</param>
        /// <param name="orderedColumnNames">if more than one column in a TVP, 
        /// columns order must match order of columns in TVP</param>
        /// <returns>a custom query parameter</returns>
        public static SqlMapper.ICustomQueryParameter AsTableValuedParameter<T>(this IEnumerable<T> enumerable,
            string typeName, IEnumerable<string> orderedColumnNames = null)
        {
            var dataTable = new DataTable();
            if (typeof(T).IsValueType || typeof(T).FullName.Equals("System.String"))
            {
                dataTable.Columns.Add(orderedColumnNames == null ?
                    "NONAME" : orderedColumnNames.First(), typeof(T));
                foreach (T obj in enumerable)
                {
                    dataTable.Rows.Add(obj);
                }
            }
            else
            {
                PropertyInfo[] properties = typeof(T).GetProperties
                    (BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo[] readableProperties = properties.Where
                    (w => w.CanRead).ToArray();
                if (readableProperties.Length > 1 && orderedColumnNames == null)
                    throw new ArgumentException("Ordered list of column names must be provided when TVP contains more than one column");

                var columnNames = (orderedColumnNames ??
                    readableProperties.Select(s => s.Name)).ToArray();
                foreach (string name in columnNames)
                {
                    dataTable.Columns.Add(name,
                        (from a in readableProperties.Single(s => s.Name.Equals(name)).PropertyType.AsEnumerable()
                         let b = Nullable.GetUnderlyingType(a)
                         select b ?? a).Single());
                }

                foreach (T obj in enumerable)
                {
                    dataTable.Rows.Add(
                        columnNames.Select(s => readableProperties.Single
                            (s2 => s2.Name.Equals(s)).GetValue(obj))
                            .ToArray());
                }
            }
            return dataTable.AsTableValuedParameter(typeName);
        }

        public static IEnumerable<T> AsEnumerable<T>(this T instance)
        {
            yield return instance;
        }
    }
}
