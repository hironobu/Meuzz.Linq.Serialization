using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace Meuzz.Linq.Serialization.Tests
{
    public class UnitTest2
    {
        private void TryTest(Expression<Func<SampleItem, bool>> f, SampleItem obj, bool expected)
        {
#if true
            var data = SystemTextJsonSerializer.Serialize(f);

            var ff = SystemTextJsonSerializer.Deserialize<Func<SampleItem, bool>>(data);
#else
            var data = JsonNetSerializer.Serialize(f);
            var ff = JsonNetSerializer.Deserialize<Func<SampleItem, bool>>(data);
#endif
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

            TryTest(x => x.Name == obj.Name, new SampleItem(1, "bbb"), true);
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

#if falses
        [Fact]
        public void TestNestedClass()
        {
            Type t = typeof(Nested);
            Debug.WriteLine(t);
        }

        class Nested
        {

        }

        static string GetSampleValue(SampleItem x)
        {
            return x.Name;
        }
#endif
    }
}