﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
            }

            return type;
        }

        public IDictionary<string, TypeData> TypeNameTable { get => _typeNameTable; }

        private IDictionary<string, TypeData> _typeNameTable = new Dictionary<string, TypeData>();

        private AssemblyName _assemblyName = new AssemblyName("Meuzz.Linq.Serialization.Tests");
        private AssemblyBuilder _assemblyBuilder;
        private ModuleBuilder _moduleBuilder;

        public static void Register(TypeData typeData)
        {

        }
    }

    [Serializable]
    public class TypeData
    {
        private TypeData() { }

        public string? FullQualifiedTypeString { get; set; }

        public (string, Type)[] FieldSpecifications { get; set; } = new (string, Type)[] { };

        public static TypeData Pack(Type t)
        {
            if (t.AssemblyQualifiedName == null || t.FullName == null)
            {
                throw new InvalidOperationException();
            }

            var data = new TypeData();

            //data.FullQualifiedTypeString = _typeDataManager.GetShortName(t.AssemblyQualifiedName);
            data.FullQualifiedTypeString = t.AssemblyQualifiedName.Replace("+", "__");
            //data.FieldSpecifications = new[] { ("s", typeof(string)), ("ss", typeof(string[])), ("d", typeof(Dictionary<string, string>)) };
            data.FieldSpecifications = t.GetFields().Select(x => (x.Name, x.FieldType)).ToArray();

            return data;
        }

        public Type Unpack()
        {
            // return ReconstructType(_typeDataManager.GetLongName(FullQualifiedTypeString!));
            return _typeDataManager.ReconstructType(FullQualifiedTypeString!, FieldSpecifications);
        }

        public static TypeDataManager TypeDataManager { get => _typeDataManager; }

        private static TypeDataManager _typeDataManager = new TypeDataManager();

        public static TypeData FromName(string name)
        {
            return new TypeData() { FullQualifiedTypeString = name };
        }

        public static TypeData Build(string name, IEnumerable<(string, Type)> specs)
        {
            var data = new TypeData()
            {
                FullQualifiedTypeString = name,
                FieldSpecifications = specs.ToArray()
            };

            _typeDataManager.TypeNameTable[name] = data;
            return data;
        }
    }

    [Serializable]
    public class ConstructorInfoData
    {
        public TypeData? DeclaringType { get; set; }

        public IEnumerable<TypeData>? Types { get; set; }

        public static ConstructorInfoData Pack(ConstructorInfo ci)
        {
            var data = new ConstructorInfoData();

            data.DeclaringType = ci.DeclaringType != null ? TypeData.Pack(ci.DeclaringType) : null;
            data.Types = ci.GetParameters().Select(x => TypeData.Pack(x.ParameterType));

            return data;
        }

        public ConstructorInfo Unpack()
        {
            var t = DeclaringType!.Unpack();
            return t.GetConstructor(Types != null ? Types.Select(x => x.Unpack()).ToArray() : new Type[] { })!;
        }
    }

    [Serializable]
    public class MethodInfoData
    {
        public MethodInfoData() { }

        public string? Name { get; set; }

        public TypeData? DeclaringType { get; set; }

        public int? GenericParameterCount { get; set; }

        public IReadOnlyCollection<TypeData>? GenericParameterTypes { get; set; }

        public IReadOnlyCollection<TypeData>? Types { get; set; }

        public static MethodInfoData Pack(MethodInfo mi)
        {
            var data = new MethodInfoData();

            data.Name = mi.Name;
            data.DeclaringType = mi.DeclaringType != null ? TypeData.Pack(mi.DeclaringType) : null;
            var partypes = mi.GetParameters().Select(x => x.ParameterType).ToArray();
            data.Types = partypes != null ? partypes.Select(x => TypeData.Pack(x)).ToArray() : null;

            if (mi.IsGenericMethod)
            {
                data.GenericParameterCount = mi.GetGenericArguments().Length;

                data.GenericParameterTypes = mi.GetGenericArguments().Select(x => TypeData.Pack(x)).ToArray();
            }

            return data;
        }

        public MethodInfo Unpack()
        {
            if (Name == null)
            {
                throw new ArgumentNullException("Name is null");
            }

            var t = DeclaringType!.Unpack();
            if (GenericParameterCount > 0)
            {
                var gmethod = t.GetGenericMethod(Name!, Types.Select(x => x.Unpack()).ToArray())!;
                return gmethod.MakeGenericMethod(GenericParameterTypes!.Select(x => x.Unpack()).ToArray());
            }

            return t.GetMethod(Name, Types!.Select(x => x.Unpack()!).ToArray())!;
        }
    }

    [Serializable]
    public class MemberInfoData
    {
        public string? MemberString { get; set; }

        public TypeData? DeclaringType { get; set; }

        public static MemberInfoData Pack(MemberInfo mi)
        {
            var data = new MemberInfoData();

            data.MemberString = mi.Name;
            data.DeclaringType = mi.DeclaringType != null ? TypeData.Pack(mi.DeclaringType) : null;

            return data;
        }

        public MemberInfo Unpack()
        {
            var t = DeclaringType?.Unpack();
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
