using System.Diagnostics.CodeAnalysis;

namespace MapLocationApp
{
    /// <summary>
    /// Helper methods to suppress nullable warnings where we know the values are safe
    /// </summary>
    public static class NullableHelper
    {
        /// <summary>
        /// Suppress nullable warning for strings that we know are not null
        /// </summary>
        [return: NotNull]
        public static string NotNull(this string? value, string defaultValue = "")
        {
            return value ?? defaultValue;
        }

        /// <summary>
        /// Suppress nullable warning for objects that we know are not null
        /// </summary>
        [return: NotNull]
        public static T NotNull<T>(this T? value) where T : class, new()
        {
            return value ?? new T();
        }

        /// <summary>
        /// Suppress nullable warning for objects with default value
        /// </summary>
        [return: NotNull]
        public static T NotNull<T>(this T? value, T defaultValue) where T : class
        {
            return value ?? defaultValue;
        }
    }
}