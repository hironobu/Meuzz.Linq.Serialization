using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Meuzz.Linq.Serialization.Core
{
    public static class TypeGenericExtensions
    {
        /// <summary>
        ///   型情報クラスインスタンスを比較するためのクラス。
        /// </summary>
        /// <remarks>
        ///   <list type="bullet">
        ///     <item>アセンブリ</item>
        ///     <item>名前空間</item>
        ///     <item>型名</item>
        ///   </list>
        ///   <para>以上すべてが一致する場合、同一の型と見做す。</para>
        /// </remarks>
        private class SimpleTypeComparer : IEqualityComparer<Type>
        {
            public bool Equals(Type? x, Type? y)
            {
                if (x?.IsGenericParameter == true || y?.IsGenericParameter == true)
                {
                    return true;
                }

                return x?.Assembly == y?.Assembly &&
                    x?.Namespace == y?.Namespace &&
                    x?.Name == y?.Name;
            }

            public int GetHashCode(Type obj) => throw new NotImplementedException();
        }

        /// <summary>
        ///   型パラメーターを持つジェネリックメソッドを取得する。
        /// </summary>
        /// <param name="type">メソッドが定義されているクラス。</param>
        /// <param name="name">メソッド名。</param>
        /// <param name="parameterTypes">型パラメーター。</param>
        /// <returns>取得されたメソッド情報(<see cref="MethodInfo"/>)。存在しなければ<c>null</c>。</returns>
        public static MethodInfo? GetGenericMethod(this Type type, string name, Type[] parameterTypes)
        {
            var methods = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Where(m => m.Name == name);
            foreach (var method in methods)
            {
                var methodParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();

                if (methodParameterTypes.SequenceEqual(parameterTypes, new SimpleTypeComparer()))
                {
                    return method;
                }
            }

            return null;
        }
    }
}
