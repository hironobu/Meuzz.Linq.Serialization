﻿using System;
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
        ///   パック化した型データキーから型情報を復元する。
        /// </summary>
        /// <param name="key">パック化された型データキー。</param>
        /// <returns>アンパック化された型情報。</returns>
        public Type UnpackFromKey(string key)
        {
            var typeData = _typeDataTable[key];
            return ReconstructType(typeData.FullName, typeData.Fields);
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

                    if (usingFieldSpecs && !d.Fields.Any())
                    {
                        d.Fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(x => new FieldData() { Name = x.Name, TypeKey = Pack(x.FieldType) }).ToArray();
                    }
                    return k;
                }

                var data = new TypeData();

                if (usingFieldSpecs)
                {
                    data.Fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(x => new FieldData() { Name = x.Name, TypeKey = Pack(x.FieldType) }).ToArray();

                }

                var random = new Random();
                while (true)
                {
                    var key = random.Next().ToString("X8");
                    if (!_typeDataTable.TryGetValue(key, out var _))
                    {
                        data.Key = key;
                        data.FullName = usingFieldSpecs ? fullName + "@" + key : assemblyQualifiedName;
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
        private Type? CreateType(string typeName, FieldData[] fields, Type? parentType)
        {
            TypeBuilder typeBuilder = _moduleBuilder.DefineType(typeName,
                TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.AutoLayout | (parentType == null ? TypeAttributes.Public : TypeAttributes.Sealed), // | TypeAttributes.ExplicitLayout,
                parentType);

            foreach (var f in fields)
            {
                typeBuilder.DefineField(f.Name, UnpackFromKey(f.TypeKey), FieldAttributes.Public);
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
        private Type? ConstructGenericType(string assemblyQualifiedName, FieldData[] fieldSpecs)
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
        private Type ReconstructType(string assemblyQualifiedName, FieldData[] fieldSpecs)
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

    /// <summary>
    ///   シリアライズ可能な型データ。
    /// </summary>
    public class TypeData
    {
        public string Key { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public FieldData[] Fields { get; set; } = new FieldData[] { };
    }

    public class FieldData
    {
        public string Name { get; set; } = string.Empty;

        public string TypeKey { get; set; } = string.Empty;
    }
}
