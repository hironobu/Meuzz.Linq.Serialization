using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace Meuzz.Linq.Serialization.Tests
{
    public class VariableParameterPassingTest
    {
        private void TestSerializeAndDeserialize(Expression<Func<SampleItem, bool>> f, SampleItem obj, bool expected)
        {
#if false
            var data = ExpressionSerializer.Serialize(f);

            var ff = (Func<SampleItem, bool>)ExpressionSerializer.Deserialize(data);
#else
            var data = JsonNetSerializer.Serialize(f);
            var ff = JsonNetSerializer.Deserialize<Func<SampleItem, bool>>(data);
#endif

            var ret = ff(obj);

            Assert.Equal(expected, ret);
        }

        [Fact]
        public void TestSingleFunction()
        {
            TestSerializeAndDeserialize(x => x.Name == "bbb", new SampleItem(1, "bbb"), true);
        }

        [Fact]
        public void TestOrElseFunction()
        {
            TestSerializeAndDeserialize(x => x.Name == "bbb" || x.Name == "aaa", new SampleItem(1, "bbb"), true);
            TestSerializeAndDeserialize(x => x.Name == "ccc" || x.Name == "ddd", new SampleItem(1, "bbb"), false);
        }

        [Fact]
        public void TestWithObject()
        {
            var obj = new SampleItem(11, "bbb");

            TestSerializeAndDeserialize(x => x.Name == obj.Name, new SampleItem(1, "bbb"), true);
        }

        [Fact]
        public void TestWithDictionary()
        {
            /*var dict = new Dictionary<ParameterExpression, string>();

            var pe0 = Expression.Parameter(typeof(SampleItem), "x");
            var pe1 = Expression.Parameter(typeof(SampleItem), "y");

            var pe = Expression.Parameter(typeof(SampleItem), "x");

            dict.Add(pe0, "aaa");
            dict.Add(pe1, "bbb");

            var obj = new SampleItem(11, "bbb");*/

            var d = new Dictionary<string, string>() { { "aaa", "aaa" } };
            TestSerializeAndDeserialize(x => x.Name == d["aaa"], new SampleItem(1, "bbb"), false);
        }

        [Fact]
        public void TestWithArray()
        { 
            TestSerializeAndDeserialize(x => new string[] { "aaa", "bbb" }.Contains(x.Name), new SampleItem(1, "bbb"), true);
            var ss = new string[] { "aaa", "bbb" };
            TestSerializeAndDeserialize(x => ss.Contains(x.Name), new SampleItem(1, "bbb"), true);
        }

        [Fact]
        public void TestWithFieldOfMyInstance()
        {
            Assert.ThrowsAny<Exception>(() => TestSerializeAndDeserialize(x => _testValues.Contains(x.Name), new SampleItem(1, "bbb"), true));
            var testValues = _testValues;
            TestSerializeAndDeserialize(x => testValues.Contains(x.Name), new SampleItem(1, "bbb"), true);
        }

        [Fact]
        public void TestWithFieldOfOuterInstance2()
        {
            var ss = new string[] { "aaa", "bbb" };
            TestSerializeAndDeserialize(x => x.Name == ss.Last(), new SampleItem(1, "bbb"), true);
            TestSerializeAndDeserialize(x => x.Name != ss.Last(), new SampleItem(1, "bbb"), false);
        }

        private string[] _testValues = new[] { "aaa", "bbb" };
        // private SampleItem _sampleItem = new SampleItem(1, "bbb");
    }
}
