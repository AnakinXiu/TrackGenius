using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using MoreLinq;

namespace TrackGenius.Const;

public static class TypeBinding
{
    public static void Raise([NotNull] this PropertyChangedEventHandler handler, [NotNull] object sender, [NotNull] string propertyName, [NotNull][ItemNotNull] params string[] propertyNames)
    {
        Raise(handler, sender, new[] { propertyName }.Concat(propertyNames));
    }

    private static void Raise([NotNull] PropertyChangedEventHandler handler, [NotNull] object sender, [NotNull][ItemNotNull] IEnumerable<string> propertyNames)
    {
        if (handler == null)
        {
            return;
        }

        var setOfPropertyNames = propertyNames.ToArray();

        if (setOfPropertyNames.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentNullException(nameof(propertyNames), "One of the specified property names is null or empty.");
        }

        var args = setOfPropertyNames
            .Select(name => new PropertyChangedEventArgs(name));

        args.ForEach(arg => handler(sender, arg));
    }


    public static void Raise([System.Diagnostics.CodeAnalysis.NotNull] this PropertyChangedEventHandler handler, [System.Diagnostics.CodeAnalysis.NotNull] object sender, [System.Diagnostics.CodeAnalysis.NotNull] Expression<Func<object>> prop)
    {
        Raise(handler, sender, GetPropName(prop));
    }

    public static bool RaiseIfChanged([System.Diagnostics.CodeAnalysis.NotNull] this PropertyChangedEventHandler handler, [System.Diagnostics.CodeAnalysis.NotNull] object sender, ref bool? current, bool? value, [System.Diagnostics.CodeAnalysis.NotNull] string propertyName, [System.Diagnostics.CodeAnalysis.NotNull][ItemNotNull] params string[] propertyNames)
    {
        return RaiseIfChanged(handler, sender, ref current, value, NullableEquals, new[] { propertyName }.Concat(propertyNames));
    }

    public static bool RaiseIfChanged([System.Diagnostics.CodeAnalysis.NotNull] this PropertyChangedEventHandler handler, [System.Diagnostics.CodeAnalysis.NotNull] object sender, [System.Diagnostics.CodeAnalysis.NotNull] ref string current, [System.Diagnostics.CodeAnalysis.NotNull] string value, [System.Diagnostics.CodeAnalysis.NotNull] string propertyName, [System.Diagnostics.CodeAnalysis.NotNull][ItemNotNull] params string[] propertyNames)
    {
        return RaiseIfChanged(handler, sender, ref current, value, (r, l) => r == l, new[] { propertyName }.Concat(propertyNames));
    }

    public static bool RaiseIfChanged<T>([System.Diagnostics.CodeAnalysis.NotNull] this PropertyChangedEventHandler handler, [System.Diagnostics.CodeAnalysis.NotNull] object sender, ref T current, T value, [System.Diagnostics.CodeAnalysis.NotNull] string propertyName, [System.Diagnostics.CodeAnalysis.NotNull][ItemNotNull] params string[] propertyNames) where T : struct
    {
        return RaiseIfChanged(handler, sender, ref current, value, (r, l) => Equals(r, l), new[] { propertyName }.Concat(propertyNames));
    }

    public static bool RaiseIfChanged<T>([System.Diagnostics.CodeAnalysis.NotNull] this PropertyChangedEventHandler handler, [System.Diagnostics.CodeAnalysis.NotNull] object sender, ref T current, T value, [System.Diagnostics.CodeAnalysis.NotNull] Func<T, T, bool> equal, [System.Diagnostics.CodeAnalysis.NotNull] string propertyName, [System.Diagnostics.CodeAnalysis.NotNull][ItemNotNull] params string[] propertyNames)
    {
        return RaiseIfChanged(handler, sender, ref current, value, equal, new[] { propertyName }.Concat(propertyNames));
    }

    public static bool NullableEqual<T>(T? left, T? right) where T : struct
    {
        if (left.HasValue != right.HasValue)
            return false;
        if (!left.HasValue && !right.HasValue)
            return true;

        return Equals(left.Value, right.Value);
    }

    private static bool RaiseIfChanged<T>([System.Diagnostics.CodeAnalysis.NotNull] PropertyChangedEventHandler handler, [System.Diagnostics.CodeAnalysis.NotNull] object sender, ref T current, T value, [System.Diagnostics.CodeAnalysis.NotNull] Func<T, T, bool> equal, [System.Diagnostics.CodeAnalysis.NotNull][ItemNotNull] IEnumerable<string> propertyNames)
    {
        if (equal(current, value))
        {
            return false;
        }

        current = value;

        Raise(handler, sender, propertyNames);

        return true;
    }

    [NotNull]
    private static string GetPropName([NotNull] LambdaExpression expression)
    {
        // Get property name
        var memberExpression = GetMemberExpression(expression);
        var propertyInfo = memberExpression?.Member as PropertyInfo;
        return propertyInfo?.Name;
    }

    [NotNull]
    private static MemberExpression GetMemberExpression([NotNull] LambdaExpression lambda)
    {
        var unaryExpression = lambda.Body as UnaryExpression;
        return (unaryExpression?.Operand ?? lambda.Body) as MemberExpression;
    }

    private static bool NullableEquals(bool? left, bool? right)
    {
        if (left.HasValue && right.HasValue)
            return Equals(left.Value, right.Value);

        return !left.HasValue && !right.HasValue;
    }
}