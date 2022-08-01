using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using Xunit;

namespace Meuzz.Linq.Serialization.Tests
{
    public class SampleItemAndJsonNetPolymorphicConvertTest
    {
        [Fact]
        public void TestSampleItem01()
        {
            var obj = new SampleItem(111, "aaa");

            var s = JsonConvert.SerializeObject(obj);

            var obj2 = JsonConvert.DeserializeObject<SampleItem>(s);

            Assert.Equal(obj.Id, obj2?.Id);
            Assert.Equal(obj.Name, obj2?.Name);
            Assert.Null(obj2?.Parent);
        }

        [Fact]
        public void TestSampleItem02()
        {
            var obj = new SampleItem(111, "aaa", new ExtendedSampleItem(222, "bbb", "bbbb"));

            var s = JsonConvert.SerializeObject(obj, new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.All });

            var obj2 = JsonConvert.DeserializeObject<SampleItem>(s, new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.All });
            Assert.NotNull(obj2);
            Debug.Assert(obj2 != null);

            Assert.Equal(obj.Id, obj2.Id);
            Assert.Equal(obj.Name, obj2.Name);
            Assert.NotNull(obj2.Parent);
            Assert.IsType<ExtendedSampleItem>(obj2.Parent);
            Debug.Assert(obj2.Parent != null);
            Assert.Equal("bbbb", ((ExtendedSampleItem)obj2.Parent).Description);
        }
    }
}
