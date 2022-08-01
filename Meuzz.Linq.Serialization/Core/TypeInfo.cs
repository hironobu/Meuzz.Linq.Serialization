using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Meuzz.Linq.Serialization.Core
{
    public class TypeDataManager
    {
        public TypeDataManager()
        {
            _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(_assemblyName, AssemblyBuilderAccess.Run);
            _moduleBuilder = _assemblyBuilder.DefineDynamicModule("MyDynamicModule");
        }
        /*
        public string GetShortName(string longName)
        {
            lock (_typeNameTable)
            {
                var k = _typeNameTable.FirstOrDefault(x => x.Value == longName).Key;
                if (k != null)
                {
                    return k;
                }

                var c = $"_{_typeNameTable.Count()}";
                _typeNameTable.Add(c, longName);
                return c;
            }
        }

        public string GetLongName(string shortName)
        {
            return _typeNameTable[shortName];
        }*/

        public Type? CreateType(string typeName, (string, Type)[] fields, Type? parentType)
        {
            TypeBuilder typeBuilder = _moduleBuilder.DefineType(typeName,
                TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.AutoLayout | (parentType == null ? TypeAttributes.Public : TypeAttributes.Sealed), // | TypeAttributes.ExplicitLayout,
                parentType);

            foreach (var (k, v) in fields)
            {
                typeBuilder.DefineField(k, v, FieldAttributes.Public);
            }

            return typeBuilder.CreateType();
        }

        private Type? ConstructGenericType(string assemblyQualifiedName, (string, Type)[] fieldSpecs)
        {
            Regex regex = new Regex(@"^(?<name>\w+(\.\w+)*)`(?<count>\d)\[(?<subtypes>\[.*\])\](, (?<assembly>\w+(\.\w+)*)[\w\s,=\.]+)$?", RegexOptions.Singleline | RegexOptions.ExplicitCapture);
            Match match = regex.Match(assemblyQualifiedName);
            if (!match.Success)
            {
                throw new Exception($"Unable to parse the type's assembly qualified name: {assemblyQualifiedName}");
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
                // This shouldn't ever happen!
                throw new Exception("Generic type argument count mismatch! Type name: " + assemblyQualifiedName);
            }

            Type[] types = new Type[typeNames.Count];
            for (int i = 0; i < types.Length; i++)
            {
                try
                {
                    var t = ReconstructType(typeNames[i], fieldSpecs);
                    if (t == null)
                    {
                        // if throwOnError, should not reach this point if couldn't create the type
                        return null;
                    }

                    types[i] = t;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Unable to reconstruct generic type. Failed on creating the type argument {(i + 1)}: {typeNames[i]}. Error message: {ex.Message}");
                }
            }

            return genericType.MakeGenericType(types);
        }

        public Type ReconstructType(string assemblyQualifiedName, (string, Type)[] fieldSpecs)
        {
            Type? type = null;

            var referencedAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.GetName().Name == _assemblyName.Name).ToArray();

            foreach (var asm in referencedAssemblies)
            {
                var fullNameWithoutAssemblyName = assemblyQualifiedName.Split(",").First();
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
                var fullNameWithoutAssemblyName = assemblyQualifiedName.Split(",").First();
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

        private Type Unpack(TypeData typeData)
        {
            return ReconstructType(typeData.FullQualifiedTypeString, typeData.FieldSpecifications);
        }

        public Type UnpackFromName(string name)
        {
            var typeData = _typeDataTable[name];
            return Unpack(typeData);
        }

        public string Pack(Type t)
        {
            if (t.AssemblyQualifiedName == null || t.FullName == null)
            {
                throw new InvalidOperationException();
            }

            lock (_typeDataTable)
            {
                var fullQualifiedName = t.AssemblyQualifiedName;

                if (_typeKeyReverseTable.TryGetValue(fullQualifiedName, out var k))
                {
                    return k;
                }

                var data = new TypeData();

                data.FullQualifiedTypeString = fullQualifiedName;

                if (t.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
                {
                    data.FieldSpecifications = t.GetFields().Select(x => (x.Name, x.FieldType)).ToArray();
                }

                var random = new Random();
                while (true)
                {
                    var key = random.Next().ToString("X8");
                    if (!_typeDataTable.TryGetValue(key, out var _))
                    {
                        data.Key = key;
                        _typeDataTable.Add(key, data);
                        _typeKeyReverseTable.Add(fullQualifiedName, key);
                        return key;
                    }
                }
            }
        }

        public IEnumerable<TypeData> Types => _typeDataTable.Values;
        public void LoadTypes(IEnumerable<TypeData> typeDatas)
        {
            _typeDataTable = typeDatas.ToDictionary(x => x.Key, x => x);
        }

        private IDictionary<string, TypeData> _typeDataTable = new Dictionary<string, TypeData>();
        private IDictionary<string, string> _typeKeyReverseTable = new Dictionary<string, string>();
    }

    public class TypeData
    {

        public string Key { get; set; } = string.Empty;

        public string FullQualifiedTypeString { get; set; } = string.Empty;

        public (string, Type)[] FieldSpecifications { get; set; } = new (string, Type)[] { };
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

            var mi = t.GetMethod(Name, Types.Select(x => typeDataManager.UnpackFromName(x)).ToArray());
            if (mi == null)
            {
                throw new NotImplementedException();
            }
            return mi;
        }
    }

    public class MemberInfoData
    {
        public string? MemberString { get; set; }

        public string? DeclaringType { get; set; }

        public static MemberInfoData Pack(MemberInfo mi, TypeDataManager typeDataManager)
        {
            var data = new MemberInfoData();

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
            return t.GetMember(MemberString!).First();
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
