using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Meuzz.Linq.Serialization.Core
{
    public class MethodInfoData
    {
        public MethodInfoData() { }

        public string Name { get; set; } = string.Empty;

        public string? DeclaringType { get; set; }

        public int? GenericParameterCount { get; set; }

        public IReadOnlyCollection<string>? GenericParameterTypes { get; set; }

        public IReadOnlyCollection<string> Types { get; set; } = Array.Empty<string>();

        public static MethodInfoData Pack(MethodInfo mi, TypeDataManager typeDataManager)
        {
            var data = new MethodInfoData();

            data.Name = mi.Name;
            data.DeclaringType = mi.DeclaringType != null ? typeDataManager.Pack(mi.DeclaringType) : null;
            var partypes = mi.GetParameters().Select(x => x.ParameterType).ToArray();
            data.Types = partypes.Select(x => typeDataManager.Pack(x)).ToArray();

            if (mi.IsGenericMethod)
            {
                data.GenericParameterCount = mi.GetGenericArguments().Length;

                data.GenericParameterTypes = mi.GetGenericArguments().Select(x => typeDataManager.Pack(x)).ToArray();
            }

            return data;
        }

        public MethodInfo Unpack(TypeDataManager typeDataManager)
        {
            var t = DeclaringType != null ? typeDataManager.UnpackFromName(DeclaringType) : null;
            if (GenericParameterCount > 0)
            {
                var gmethod = t?.GetGenericMethod(Name, Types.Select(x => typeDataManager.UnpackFromName(x)).ToArray());
                if (gmethod == null)
                {
                    throw new NotImplementedException();
                }
                return gmethod.MakeGenericMethod(GenericParameterTypes.Select(x => typeDataManager.UnpackFromName(x)).ToArray());
            }

            var mi = t?.GetMethod(Name, Types.Select(x => typeDataManager.UnpackFromName(x)).ToArray());
            if (mi == null)
            {
                throw new NotImplementedException();
            }
            return mi;
        }
    }
}
