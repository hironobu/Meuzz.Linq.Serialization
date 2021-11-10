using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Xunit;
using Meuzz.Linq.Serialization;
using System.Linq;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text.Json;

namespace Meuzz.Linq.Serialization.Tests
{
    public class SampleItem
    {
        public int Id { get; }

        public string Name { get; }

        public SampleItem? Next { get; }

        public SampleItem(int id, string name, SampleItem? next = null)
        {
            Id = id;
            Name = name;
            Next = next;
        }
    }


    public class UnitTest1
    {
        private void TryTest(Expression<Func<SampleItem, bool>> f, bool expected)
        {
            var ff = (Func<SampleItem, bool>)ExpressionSerializer.DoSerialize(f);

            var obj = new SampleItem(1, "bbb");
            var ret = ff(obj);

            Assert.Equal(expected, ret);
        }

        [Fact]
        public void Test01()
        {
            var dict = new Dictionary<ParameterExpression, string>();

            var pe0 = Expression.Parameter(typeof(SampleItem), "x");
            var pe1 = Expression.Parameter(typeof(SampleItem), "y");

            var pe = Expression.Parameter(typeof(SampleItem), "x");

            dict.Add(pe0, "aaa");
            dict.Add(pe1, "bbb");

            var obj = new SampleItem(11, "bbb");
            TryTest(x => x.Name == "bbb", true);
            TryTest(x => x.Name == "bbb" || x.Name == "aaa", true);
            TryTest(x => x.Name == "ccc" || x.Name == "ddd", false);

            var d = new Dictionary<string, string>() { { "aaa", "aaa" } };
            TryTest(x => x.Name == d["aaa"], false);

            TryTest(x => new string[] { "aaa", "bbb" }.Contains(x.Name), true);
            var ss = new string[] { "aaa", "bbb" };
            TryTest(x => ss.Contains(x.Name), true);
        }

        [Fact]
        public void Test02()
        {
            var obj = new SampleItem(111, "aaa");

            var s = JsonSerializer.Serialize(obj);

            Assert.NotNull(s);
        }

        [Fact]
        public void Test03()
        {
            var obj = new SampleItem(111, "aaa", new SampleItem(222, "bbb"));

            var s = JsonSerializer.Serialize(obj);

            Assert.NotNull(s);
        }

        static string GetSampleValue(SampleItem x)
        {
            return x.Name;
        }
    }
}
