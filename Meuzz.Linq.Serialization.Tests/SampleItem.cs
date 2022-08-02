using TestClass;

namespace Meuzz.Linq.Serialization.Tests
{
    public class ExtendedSampleItem : SampleItem
    {
        public string Description { get; }

        public ExtendedSampleItem(int id, string name, string description, SampleItem? parent = null) : base(id, name, parent)
        {
            Description = description;
        }
    }
}
