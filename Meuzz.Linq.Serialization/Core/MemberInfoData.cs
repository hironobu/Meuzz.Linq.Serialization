using System;
using System.Linq;
using System.Reflection;

namespace Meuzz.Linq.Serialization.Core
{
    public class MemberInfoData
    {
        public string MemberString { get; set; } = string.Empty;

        public string? DeclaringType { get; set; }

        public static MemberInfoData Pack(MemberInfo mi, TypeDataManager typeDataManager)
        {
            var data = new MemberInfoData();

            if (mi is FieldInfo fi && !fi.IsPublic)
            {
                throw new InvalidOperationException($"Member access to private field is not allowd: {mi.DeclaringType.Name}.{mi.Name}");
            }

            data.MemberString = mi.Name;
            data.DeclaringType = mi.DeclaringType != null ? typeDataManager.Pack(mi.DeclaringType) : null;

            return data;
        }

        public MemberInfo Unpack(TypeDataManager typeDataManager)
        {
            var t = DeclaringType != null ? typeDataManager.UnpackFromKey(DeclaringType) : null;
            if (t == null)
            {
                throw new NotImplementedException();
            }
            return t.GetMember(MemberString, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).First();
        }
    }
}
