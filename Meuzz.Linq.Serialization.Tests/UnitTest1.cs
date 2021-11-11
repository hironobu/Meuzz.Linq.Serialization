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
        private void TryTest(Expression<Func<SampleItem, bool>> f, SampleItem obj, bool expected)
        {
            var data = ExpressionSerializer.Serialize(f);

            var ff = (Func<SampleItem, bool>)ExpressionSerializer.Deserialize(data);

            var ret = ff(obj);

            Assert.Equal(expected, ret);
        }

        [Fact]
        public void Test01()
        {
            TryTest(x => x.Name == "bbb", new SampleItem(1, "bbb"), true);
        }

        [Fact]
        public void Test02()
        {
            TryTest(x => x.Name == "bbb" || x.Name == "aaa", new SampleItem(1, "bbb"), true);
            TryTest(x => x.Name == "ccc" || x.Name == "ddd", new SampleItem(1, "bbb"), false);
        }

        [Fact]
        public void TestWithObject()
        {
            var obj = new SampleItem(11, "bbb");

            TryTest(x => x.Name == obj.Name, new SampleItem(1, "bbb"), false);
        }

        [Fact]
        public void TestWithDict()
        {
            /*var dict = new Dictionary<ParameterExpression, string>();

            var pe0 = Expression.Parameter(typeof(SampleItem), "x");
            var pe1 = Expression.Parameter(typeof(SampleItem), "y");

            var pe = Expression.Parameter(typeof(SampleItem), "x");

            dict.Add(pe0, "aaa");
            dict.Add(pe1, "bbb");

            var obj = new SampleItem(11, "bbb");*/

            var d = new Dictionary<string, string>() { { "aaa", "aaa" } };
            TryTest(x => x.Name == d["aaa"], new SampleItem(1, "bbb"), false);
        }

        [Fact]
        public void TestWithArray()
        { 
            TryTest(x => new string[] { "aaa", "bbb" }.Contains(x.Name), new SampleItem(1, "bbb"), true);
            var ss = new string[] { "aaa", "bbb" };
            TryTest(x => ss.Contains(x.Name), new SampleItem(1, "bbb"), true);
        }

        
        [Fact]
        public void TestSampleItem01()
        {
            var obj = new SampleItem(111, "aaa");

            var s = JsonSerializer.Serialize(obj);

            Assert.NotNull(s);
        }

        [Fact]
        public void TestSampleItem02()
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
