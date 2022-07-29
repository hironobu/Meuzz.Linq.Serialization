using System;
using System.Linq.Expressions;
using Meuzz.Linq.Serialization.Expressions;
using Newtonsoft.Json;
using Xunit;

namespace Meuzz.Linq.Serialization.Tests
{
    public class ExpressionAndJsonNetTest
    {
#if false
        private void TryTest(Expression<Func<SampleItem, bool>> f, SampleItem obj, bool expected)
        {
            var s = JsonConvert.SerializeObject(f, new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.All });

            var ff = JsonConvert.DeserializeObject<Expression<Func<SampleItem, bool>>>(s, new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.All });

            var compiled = ff.Compile();

            var ret = compiled(obj);

            Assert.Equal(expected, ret);
        }
#endif

        private object TrySerialize<T>(Expression<T> f)
        {
            var data = ExpressionData.Pack(f);

            return JsonConvert.SerializeObject(data, new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.Objects });
        }

        private Expression<T> TryDeserialize<T>(object o) where T : Delegate
        {
            var data2 = JsonConvert.DeserializeObject<ExpressionData>((string)o, new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.Objects });

            return (Expression<T>)data2!.Unpack();
        }


        [Fact]
        public void Test01()
        {
            var data = JsonNetSerializer.Serialize<Func<SampleItem, bool>>(x => x.Name == "bbb");

            var ff = JsonNetSerializer.Deserialize<Func<SampleItem, bool>>(data);

            var obj = new SampleItem(1, "bbb");

            var ret = ff(obj);

            Assert.True(ret);
        }

        [Fact]
        public void Test02()
        {
            var s = "bbb";
            var data = JsonNetSerializer.Serialize<Func<SampleItem, bool>>(x => x.Name == s);

            var ff = JsonNetSerializer.Deserialize<Func<SampleItem, bool>>(data);

            var obj = new SampleItem(1, "bbb");

            var ret = ff(obj);

            Assert.True(ret);
        }
    }
}
