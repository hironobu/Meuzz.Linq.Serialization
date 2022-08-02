namespace TestClass
{
    public class SampleItem
    {
        public int Id { get; }

        public string Name { get; }

        public SampleItem? Parent { get; }

        public SampleItem(int id, string name, SampleItem? parent = null)
        {
            Id = id;
            Name = name;
            Parent = parent;
        }
    }
}