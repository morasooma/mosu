using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Realms;

namespace osu.Game.Database
{
    /// <summary>
    /// Reuses open property getters without emitting and JIT-compiling a dynamic method on
    /// the Realm update thread. Cached delegates never close over a model or subscription.
    /// </summary>
    internal static class RealmPropertyAccessor<TModel, TProperty>
        where TModel : RealmObjectBase
    {
        private static readonly ConditionalWeakTable<MemberInfo, Func<TModel, TProperty>> accessors = new ConditionalWeakTable<MemberInfo, Func<TModel, TProperty>>();

        public static Func<TModel, TProperty> Get(MemberInfo member, Expression<Func<TModel, TProperty>> expression)
        {
            if (accessors.TryGetValue(member, out var accessor))
                return accessor;

            return getOrCreate(member, expression);
        }

        private static Func<TModel, TProperty> getOrCreate(MemberInfo member, Expression<Func<TModel, TProperty>> expression)
            => accessors.GetValue(member, key => create(key, expression));

        private static Func<TModel, TProperty> create(MemberInfo member, Expression<Func<TModel, TProperty>> expression)
        {
            // An exact receiver/property type permits an open delegate to the existing getter.
            // Inherited/field/unusual access keeps expression semantics, using the interpreter
            // rather than generating another dynamic method at this critical point.
            if (member is PropertyInfo property && property.DeclaringType == typeof(TModel) && property.PropertyType == typeof(TProperty)
                                                && property.GetMethod is { IsStatic: false } getter)
                return getter.CreateDelegate<Func<TModel, TProperty>>();

            return expression.Compile(preferInterpretation: true);
        }
    }
}
