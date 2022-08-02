using System.Collections.Generic;

namespace Meuzz.Linq.Serialization.Core
{
    public class JsonData
    {
        public string? Type { get; set; }

        public IDictionary<string, object>? Data { get; set; }
    }
}
