using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuantConnect.PythonWrapper
{
    /// <summary>
    /// Collection of methods that are shared by some of the classes in this namespace 
    /// </summary>
    public static class Util
    {
        /// <summary>
        /// Converts name from C# style to Pythonista
        /// </summary>
        /// <param name="name">Name in C# style</param>
        /// <returns>string with the converted name</returns>
        public static string ToPythonista(this string name)
        {
            // When all characted are in lower case, does nothing
            if (name.All(c => char.IsLower(c)))
            {
                return name;
            }

            var outname = new StringBuilder();
            var lowered = name.ToLower();

            // Indicator API are upper case. In this case, simply returns the lower case name
            if (name.All(c => char.IsUpper(c)))
            {
                return lowered;
            }

            for (var i = 0; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]) && i > 0 && name[i - 1] != '_')
                {
                    outname.Append("_");
                }
                outname.Append(lowered[i]);
            }

            return outname.ToString();
        }

        /// <summary>
        /// Converts Type to String
        /// </summary>
        /// <param name="type">Type to convert into String</param>
        /// <param name="isOut">Informs if is a out parameter</param>
        /// <returns></returns>
        public static string ToTypeString(this Type type, bool isOut)
        {
            var genericArguments = type.GetGenericArguments();

            if (genericArguments.Count() > 0)
            {
                var hasBrackets = type.Name.Contains("[]");
                var excess = type.Name.Substring(type.Name.IndexOf('`'));
                var collectionName = type.Name.Replace(excess, "<");
                var typeStringBuilder = new StringBuilder(collectionName);

                foreach (var genericArgument in genericArguments)
                {
                    typeStringBuilder.Append(genericArgument.ToTypeString(false) + ", ");
                }

                typeStringBuilder.Append(">");

                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    typeStringBuilder.Replace("Nullable<", "").Replace(", >", "?");
                }

                typeStringBuilder.Replace(", >", hasBrackets ? ">[]" : ">");

                return typeStringBuilder.ToString();
            }

            var systemObjects = new Dictionary<Type, string>
            {
                { typeof(void), "void" },       { typeof(void).MakeByRefType(), "void" },
                { typeof(short), "short" },     { typeof(short).MakeByRefType(), "short" },
                { typeof(int), "int" },         { typeof(int).MakeByRefType(), "int" },
                { typeof(long), "long" },       { typeof(long).MakeByRefType(), "long" },
                { typeof(double), "double" },   { typeof(double).MakeByRefType(), "double" },
                { typeof(decimal), "decimal" }, { typeof(decimal).MakeByRefType(), "decimal" },
                { typeof(string), "string" },   { typeof(string).MakeByRefType(), "string" },
                { typeof(bool), "bool" },       { typeof(bool).MakeByRefType(), "bool" },
            };

            var name = systemObjects.ContainsKey(type) ? systemObjects[type] : type.Name;

            // Special case
            if (name == "UnchangedUniverse") name = "Universe." + name;
            if (name == "AlgorithmNodePacket") name = "Packets." + name;

            return (isOut ? "out " : "") + name.Replace("&", " ");
        }

        /// <summary>
        /// Traverses an object hierarchy and return a flattened list of elements based on a predicate.
        /// </summary>
        /// <typeparam name="TSource">The type of object in your collection.</typeparam>
        /// <param name="source">The collection of your topmost TSource objects.</param>
        /// <param name="selectorFunction">A predicate for choosing the objects you want.</param>
        /// <param name="getChildrenFunction">A function that fetches the child collection from an object.</param>
        /// <returns>A flattened list of objects which meet the criteria in selectorFunction.</returns>
        public static IEnumerable<TSource> Map<TSource>(
          this IEnumerable<TSource> source,
          Func<TSource, bool> selectorFunction,
          Func<TSource, IEnumerable<TSource>> getChildrenFunction)
        {
            // Add what we have to the stack
            var flattenedList = source.Where(selectorFunction);

            // Go through the input enumerable looking for children,
            // and add those if we have them
            foreach (TSource element in source)
            {
                flattenedList = flattenedList
                    .Concat(getChildrenFunction(element)
                    .Map(selectorFunction, getChildrenFunction)
                );
            }
            return flattenedList;
        }

        /// <summary>
        /// DistinctBy:  
        /// </summary>
        /// <typeparam name="TSource">The type of object in your collection.</typeparam>
        /// <typeparam name="TKey"></typeparam>
        /// <param name="source">The collection of your topmost TSource objects.</param>
        /// <param name="keySelector"></param>
        /// <returns></returns>
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
            this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }
    }
}