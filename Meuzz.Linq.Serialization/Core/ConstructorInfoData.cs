using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Meuzz.Linq.Serialization.Core
{
    public class ConstructorInfoData
    {
        public string? DeclaringType { get; set; }

        public IReadOnlyCollection<string> Types { get; set; } = Array.Empty<string>();

        public static ConstructorInfoData Pack(ConstructorInfo ci, TypeDataManager typeDataManager)
        {
            var data = new ConstructorInfoData();

            data.DeclaringType = ci.DeclaringType != null ? typeDataManager.Pack(ci.DeclaringType) : null;
            data.Types = ci.GetParameters().Select(x => typeDataManager.Pack(x.ParameterType)).ToArray();

            return data;
        }

        public ConstructorInfo Unpack(TypeDataManager typeDataManager)
        {
            var t = DeclaringType != null ? typeDataManager.UnpackFromKey(DeclaringType) : null;
            var ctor = t?.GetConstructor(Types.Select(x => typeDataManager.UnpackFromKey(x)).ToArray());
            if (ctor == null)
            {
                throw new NotImplementedException();
            }
            return ctor;
        }
    }
}
