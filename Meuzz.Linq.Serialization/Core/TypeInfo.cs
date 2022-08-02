using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace Meuzz.Linq.Serialization.Core
{
    /// <summary>
    ///   型情報をシリアライズ可能な形態(<see cref="TypeData"/>)に変換するためのクラス。
    /// </summary>
    public class TypeDataManager
    {
        /// <summary>
        ///   コンストラクター。
        /// </summary>
        public TypeDataManager()
        {
            _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(_assemblyName, AssemblyBuilderAccess.Run);
            _moduleBuilder = _assemblyBuilder.DefineDynamicModule("MyDynamicModule");
        }

        /// <summary>
        ///   パック化された型データ。
        /// </summary>
        public IEnumerable<TypeData> Types => _typeDataTable.Values;

        /// <summary>
        ///   パック化されたデータから型情報を復元する。
        /// </summary>
        /// <param name="name">パック化された型データキー。</param>
        /// <returns>アンパック化された型情報。</returns>
        public Type UnpackFromName(string name)
        {
            var typeData = _typeDataTable[name];
            return ReconstructType(typeData.FullQualifiedTypeString, typeData.FieldSpecifications);
        }

        /// <summary>
        ///   対象の型情報について、フィールド情報もパック済みか否か。
        /// </summary>
        /// <param name="t">対象の型情報クラス。</param>
        /// <returns>フィールド情報を含んだパック化が行われていれば<c>true</c>。</returns>
        public bool IsUsingFieldSpecs(Type t)
        {
            if (t.AssemblyQualifiedName == null || t.FullName == null)
            {
                return false;
            }

            lock (_typeDataTable)
            {
                return _typeKeyReverseTable.ContainsKey(t.FullName);
            }
        }

        /// <summary>
        ///   型情報(<see cref="System.Type"/>)をシリアライズ可能な状態に変換する。
        /// </summary>
        /// <param name="t">対象の型情報クラス。</param>
        /// <param name="usingFieldSpecs">シリアライズ時にフィールド情報も保存する場合は<c>true</c>。</param>
        /// <returns>変換されたキー文字列(ランダム16進)。</returns>
        /// <exception cref="ArgumentException"><paramref name="t"/>のプロパティ(<see cref="Type.AssemblyQualifiedName"/>または<see cref="Type.FullName"/>)が<c>null</c>だった時。</exception>
        public string Pack(Type t, bool usingFieldSpecs = false)
        {
            if (t.AssemblyQualifiedName == null || t.FullName == null)
            {
                throw new ArgumentException("t");
            }

            lock (_typeDataTable)
            {
                var fullName = t.FullName.Replace("+", "@");
                var assemblyQualifiedName = t.AssemblyQualifiedName.Replace("+", "@");

                if (_typeKeyReverseTable.TryGetValue(fullName, out var k))
                {
                    var d = _typeDataTable[k];

                    if (usingFieldSpecs && !d.FieldSpecifications.Any())
                    {
                        d.FieldSpecifications = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(x => (x.Name, Pack(x.FieldType))).ToArray();
                    }
                    return k;
                }

                var data = new TypeData();

                if (usingFieldSpecs)
                {
                    data.FieldSpecifications = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(x => (x.Name, Pack(x.FieldType))).ToArray();
                }

                var random = new Random();
                while (true)
                {
                    var key = random.Next().ToString("X8");
                    if (!_typeDataTable.TryGetValue(key, out var _))
                    {
                        data.Key = key;
                        data.FullQualifiedTypeString = usingFieldSpecs ? fullName + "@" + key : assemblyQualifiedName;
                        _typeDataTable.Add(key, data);
                        _typeKeyReverseTable.Add(fullName, key);
                        return key;
                    }
                }
            }
        }

        /// <summary>
        ///   パック化された型データを読み込む。
        /// </summary>
        /// <param name="typeDatas">パック化された型情報。</param>
        public void LoadTypes(IEnumerable<TypeData> typeDatas)
        {
            _typeDataTable = typeDatas.ToDictionary(x => x.Key, x => x);
        }

        /// <summary>
        ///   型名とフィールド情報から新しい型を生成する。
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="fields"></param>
        /// <param name="parentType"></param>
        /// <returns></returns>
        private Type? CreateType(string typeName, (string, string)[] fields, Type? parentType)
        {
            TypeBuilder typeBuilder = _moduleBuilder.DefineType(typeName,
                TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.AutoLayout | (parentType == null ? TypeAttributes.Public : TypeAttributes.Sealed), // | TypeAttributes.ExplicitLayout,
                parentType);

            foreach (var (k, v) in fields)
            {
                typeBuilder.DefineField(k, UnpackFromName(v), FieldAttributes.Public);
            }

            return typeBuilder.CreateType();
        }

        /// <summary>
        ///   ジェネリック型を生成する。
        /// </summary>
        /// <param name="assemblyQualifiedName">アセンブリ名も形式での含む型名。</param>
        /// <param name="fieldSpecs">生成する型のフィールド情報。</param>
        /// <returns>生成された型情報。</returns>
        /// <exception cref="ArgumentException"></exception>
        private Type? ConstructGenericType(string assemblyQualifiedName, (string, string)[] fieldSpecs)
        {
            var regex = new Regex(@"^(?<name>\w+(\.\w+)*)`(?<count>\d)\[(?<subtypes>\[.*\])\](, (?<assembly>\w+(\.\w+)*)[\w\s,=\.]+)$?", RegexOptions.Singleline | RegexOptions.ExplicitCapture);
            var match = regex.Match(assemblyQualifiedName);
            if (!match.Success)
            {
                throw new ArgumentException($"Unable to parse the type's assembly qualified name: {assemblyQualifiedName}");
            }

            var typeName = match.Groups["name"].Value;
            int n = int.Parse(match.Groups["count"].Value);
            // var asmName = match.Groups["assembly"].Value;
            var subtypes = match.Groups["subtypes"].Value;

            typeName = typeName + $"`{n}";
            var genericType = ReconstructType(typeName, fieldSpecs);
            if (genericType == null) return null;

            List<string> typeNames = new List<string>();
            int ofs = 0;
            while (ofs < subtypes.Length && subtypes[ofs] == '[')
            {
                int end = ofs, level = 0;
                do
                {
                    switch (subtypes[end++])
                    {
                        case '[':
                            level++;
                            break;
                        case ']':
                            level--;
                            break;
                    }
                } while (level > 0 && end < subtypes.Length);

                if (level == 0)
                {
                    typeNames.Add(subtypes.Substring(ofs + 1, end - ofs - 2));
                    if (end < subtypes.Length && subtypes[end] == ',')
                        end++;
                }

                ofs = end;
                n--;  // just for checking the count
            }

            if (n != 0)
            {
                throw new ArgumentException("Generic type argument count mismatch! Type name: " + assemblyQualifiedName);
            }

            var types = new Type[typeNames.Count];
            for (int i = 0; i < types.Length; i++)
            {
                try
                {
                    var t = ReconstructType(typeNames[i], fieldSpecs);
                    if (t == null)
                    {
                        return null;
                    }

                    types[i] = t;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Unable to reconstruct generic type. Failed on creating the type argument {(i + 1)}: {typeNames[i]}. Error message: {ex.Message}", ex);
                }
            }

            return genericType.MakeGenericType(types);
        }

        /// <summary>
        ///   型情報を生成する。
        /// </summary>
        /// <param name="assemblyQualifiedName">アセンブリ名を含む形式の型名。</param>
        /// <param name="fieldSpecs">フィールド定義情報。</param>
        /// <returns>生成された型情報。</returns>
        /// <exception cref="NotImplementedException"></exception>
        private Type ReconstructType(string assemblyQualifiedName, (string, string)[] fieldSpecs)
        {
            Type? type = null;

            var fullNameWithoutAssemblyName = assemblyQualifiedName.Split(",").First();
            var referencedAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.GetName().Name == _assemblyName.Name).ToArray();

            foreach (var asm in referencedAssemblies)
            {
                type = asm.GetType(fullNameWithoutAssemblyName, throwOnError: false);
                if (type != null)
                {
                    break;
                }
            }

            if (type == null)
            {
                if (assemblyQualifiedName.Contains("[["))
                {
                    type = ConstructGenericType(assemblyQualifiedName, fieldSpecs);
                }
                else
                {
                    type = Type.GetType(assemblyQualifiedName, false);
                }
            }

            if (type == null)
            {
                type = CreateType(fullNameWithoutAssemblyName, fieldSpecs, null);
                if (type == null)
                {
                    throw new NotImplementedException();
                }
            }

            return type;
        }

        private AssemblyName _assemblyName = new AssemblyName("MyDynamicAssembly");
        private AssemblyBuilder _assemblyBuilder;
        private ModuleBuilder _moduleBuilder;

        private IDictionary<string, TypeData> _typeDataTable = new Dictionary<string, TypeData>();
        private IDictionary<string, string> _typeKeyReverseTable = new Dictionary<string, string>();
    }

    public class TypeData
    {

        public string Key { get; set; } = string.Empty;

        public string FullQualifiedTypeString { get; set; } = string.Empty;

        public (string, string)[] FieldSpecifications { get; set; } = new (string, string)[] { };
    }

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
            var t = DeclaringType != null ? typeDataManager.UnpackFromName(DeclaringType) : null;
            var ctor = t?.GetConstructor(Types.Select(x => typeDataManager.UnpackFromName(x)).ToArray());
            if (ctor == null)
            {
                throw new NotImplementedException();
            }
            return ctor;
        }
    }

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
            var t = DeclaringType != null ? typeDataManager.UnpackFromName(DeclaringType) : null;
            if (t == null)
            {
                throw new NotImplementedException();
            }
            return t.GetMember(MemberString, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).First();
        }
    }

    public static class TypeExtensions
    {
        private class SimpleTypeComparer : IEqualityComparer<Type>
        {
            public bool Equals(Type? x, Type? y)
            {
                if (x?.IsGenericParameter == true || y?.IsGenericParameter == true)
                {
                    return true;
                }

                return x?.Assembly == y?.Assembly &&
                    x?.Namespace == y?.Namespace &&
                    x?.Name == y?.Name;
            }

            public int GetHashCode(Type obj)
            {
                throw new NotImplementedException();
            }
        }

        public static MethodInfo? GetGenericMethod(this Type type, string name, Type[] parameterTypes)
        {
            var methods = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Where(m => m.Name == name);
            foreach (var method in methods)
            {
                var methodParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();

                if (methodParameterTypes.SequenceEqual(parameterTypes, new SimpleTypeComparer()))
                {
                    return method;
                }
            }

            return null;
        }
    }

    public static class ReflectionHelper
    {
        //...
        // here are methods described in the post 
        // http://dotnetfollower.com/wordpress/2012/12/c-how-to-set-or-get-value-of-a-private-or-internal-property-through-the-reflection/
        //...

        private static FieldInfo? GetFieldInfo(Type? type, string fieldName)
        {
            FieldInfo? fieldInfo;
            do
            {
                fieldInfo = type?.GetField(fieldName,
                       BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                type = type?.BaseType;
            }
            while (fieldInfo == null && type != null);
            return fieldInfo;
        }

        public static object? GetFieldValue(this object obj, string fieldName)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");
            Type objType = obj.GetType();
            FieldInfo? fieldInfo = GetFieldInfo(objType, fieldName);
            if (fieldInfo == null)
                throw new ArgumentOutOfRangeException("fieldName",
                  string.Format("Couldn't find field {0} in type {1}", fieldName, objType.FullName));
            return fieldInfo.GetValue(obj);
        }

        public static void SetFieldValue(this object obj, string fieldName, object val)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");
            Type objType = obj.GetType();
            FieldInfo? fieldInfo = GetFieldInfo(objType, fieldName);
            if (fieldInfo == null)
                throw new ArgumentOutOfRangeException("fieldName",
                  string.Format("Couldn't find field {0} in type {1}", fieldName, objType.FullName));
            fieldInfo.SetValue(obj, val);
        }
    }
}
