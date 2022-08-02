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
        public void TestWithPrivateField()
        {
            Assert.Throws<InvalidOperationException>(() => TestSerializeAndDeserialize(x => _testValues.Contains(x.Name), new SampleItem(1, "bbb"), true));
        }

        [Fact]
        public void TestWithAutoVariable()
        {
            var ss = new string[] { "aaa", "bbb" };
            TestSerializeAndDeserialize(x => x.Name == ss.Last(), new SampleItem(1, "bbb"), true);
            TestSerializeAndDeserialize(x => x.Name != ss.Last(), new SampleItem(1, "bbb"), false);
        }

        [Fact]
        public void TestWithAutoVariableAsCustomType()
        {
            var obj = new SampleItem(2, "xxx");
            TestSerializeAndDeserialize(x => x.Name != obj.Name, new SampleItem(1, "bbb"), true);
        }

        [Fact]
        public void TestWithPrivateFieldIntoAutoVariable()
        {
            var testValues = _testValues;
            TestSerializeAndDeserialize(x => testValues.Contains(x.Name), new SampleItem(1, "bbb"), true);
        }

        [Fact]
        public void TesteWithPrivateFieldAndFunctionArgument()
        { 
            Action<string[]> action = (testValues) => TestSerializeAndDeserialize(x => testValues.Contains(x.Name), new SampleItem(1, "bbb"), true);
            action(_testValues);
        }

        private string[] _testValues = new[] { "aaa", "bbb" };
        // private SampleItem _sampleItem = new SampleItem(1, "bbb");
    }
}
