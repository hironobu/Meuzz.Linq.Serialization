using System;
using System.Linq.Expressions;
using Meuzz.Linq.Serialization.Serializers;

namespace Meuzz.Linq.Serialization
{
    public abstract class ExpressionSerializer
    {
        /// <summary>
        ///   関数をシリアライズする。
        /// </summary>
        /// <typeparam name="T">対象となる関数の型。</typeparam>
        /// <param name="f">関数オブジェクト。</param>
        /// <returns>シリアライズされたデータ。</returns>
        public abstract string Serialize<T>(Expression<T> f) where T : Delegate;

        /// <summary>
        ///   関数をデシリアライズする。
        /// </summary>
        /// <typeparam name="T">対象となる関数の型。</typeparam>
        /// <param name="s">シリアライズされたデータ。</param>
        /// <returns>デシリアライズされた関数オブジェクト。</returns>
        public abstract T Deserialize<T>(string s) where T : Delegate;

        /// <summary>
        ///   シリアライザーを生成する。
        /// </summary>
        /// <returns>シリアライザー。</returns>
        public static ExpressionSerializer CreateInstance()
        {
            return new JsonNetSerializer();
        }
    }
}
