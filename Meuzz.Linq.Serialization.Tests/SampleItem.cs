using System;
using System.Collections.Generic;
using System.Text;

namespace Meuzz.Linq.Serialization.Tests
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

    public class ExtendedSampleItem : SampleItem
    {
        public string Description { get; }

        public ExtendedSampleItem(int id, string name, string description, SampleItem? parent = null) : base(id, name, parent)
        {
            Description = description;
        }
    }
}
