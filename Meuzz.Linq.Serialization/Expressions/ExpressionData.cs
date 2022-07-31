using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Meuzz.Linq.Serialization.Core;

namespace Meuzz.Linq.Serialization.Expressions
{/*
    public class ExpressionArchiver
    {
        public static void Archive(BinaryWriter writer, Expression e)
        {
            switch (e)
            {
                case LambdaExpression le:
                    LambdaExpressionData.Pack(writer, le);
                    return;

                case ParameterExpression pe:
                    ParameterExpressionData.Pack(pe);
                    return;

                case BinaryExpression bine:
                    BinaryExpressionData.Pack(bine);
                    return;

                case MemberExpression membe:
                    MemberExpressionData.Pack(membe);
                    return;

                case ConstantExpression ce:
                    ConstantExpressionData.Pack(ce);
                    return;

                case MethodCallExpression mce:
                    MethodCallExpressionData.Pack(mce);
                    return;

                case NewExpression ne:
                    NewExpressionData.Pack(ne);
                    return;

                case NewArrayExpression nae:
                    NewArrayExpressionData.Pack(nae);
                    return;

                default:
                    throw new NotImplementedException();
            }
        }

        public static Expression Unarchive(BinaryReader reader)
        {
            var nodeType = (ExpressionType)reader.ReadByte();

            switch (nodeType)
            {
                case ExpressionType.Lambda:
                    return LambdaExpressionData.Unpack(reader);

                default:
                    throw new NotImplementedException();
            }
        }

        public static void Pack(BinaryWriter writer, Expression e)
        {
            Debug.Assert(Enum.GetValues(typeof(ExpressionType)).Cast<int>().Max() < byte.MaxValue);

            writer.Write((byte)e.NodeType);
            writer.WriteType(e.Type);
            writer.Write(e.CanReduce);
        }

        public static (ExpressionType, Type, bool) Unpack(BinaryReader reader)
        {
            var nodeType = reader.ReadByte();
            var type = reader.ReadType();
            var canReduce = reader.ReadBoolean();

            return ((ExpressionType)nodeType, type, canReduce);
        }
    }*/

    public class ExpressionPacket
    {
        public ExpressionPacket(string data, IEnumerable<TypeData> types)
        {
            Data = data;
            Types = types;
        }

        public string Data { get; set; }
        public IEnumerable<TypeData> Types { get; set; }
    }

    public class ExpressionData
    {
        public bool? CanReduce { get; set; }
        public ExpressionType? NodeType { get; set; }
        public string? Type { get; set; }
        
        public ExpressionData() { }

        public static ExpressionData Pack(Expression e, TypeDataManager typeDataManager)
        {
            switch (e)
            {
                case LambdaExpression le:
                    return LambdaExpressionData.Pack(le, typeDataManager);

                case ParameterExpression pe:
                    return ParameterExpressionData.Pack(pe, typeDataManager);

                case BinaryExpression bine:
                    return BinaryExpressionData.Pack(bine, typeDataManager);

                case MemberExpression membe:
                    return MemberExpressionData.Pack(membe, typeDataManager);

                case ConstantExpression ce:
                    return ConstantExpressionData.Pack(ce, typeDataManager);

                case MethodCallExpression mce:
                    return MethodCallExpressionData.Pack(mce, typeDataManager);

                case NewExpression ne:
                    return NewExpressionData.Pack(ne, typeDataManager);

                case NewArrayExpression nae:
                    return NewArrayExpressionData.Pack(nae, typeDataManager);

                default:
                    throw new NotImplementedException();
            }
        }

        public virtual Expression Unpack(TypeDataManager typeDataManager) { throw new NotImplementedException(); }
    }

    public class LambdaExpressionData : ExpressionData
    {
        public ExpressionData? Body { get; set; }
        public string? Name { get; set; }
        public IReadOnlyCollection<ParameterExpressionData>? Parameters { get; set; }
        public string? ReturnType { get; set; }
        public bool? TailCall { get; set; }

        public LambdaExpressionData() : base() { }

        public static LambdaExpressionData Pack(LambdaExpression le, TypeDataManager typeDataManager)
        {
            var data = new LambdaExpressionData();

            data.CanReduce = le.CanReduce;
            data.NodeType = le.NodeType;
            data.Type = typeDataManager.Pack(le.Type);

            data.Body = ExpressionData.Pack(le.Body, typeDataManager);
            data.Name = le.Name;
            data.Parameters = le.Parameters.Select(x => ParameterExpressionData.Pack(x, typeDataManager)).ToArray();
            data.ReturnType = typeDataManager.Pack(le.ReturnType);
            data.TailCall = le.TailCall;

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            return Expression.Lambda(Body!.Unpack(typeDataManager), Name, (bool)TailCall!, Parameters.Select(x => (ParameterExpression)x.Unpack(typeDataManager)!));
        }
    }

    public class ParameterExpressionData : ExpressionData
    {
        public bool? IsByRef { get; set; }
        public string? Name { get; set; }

        public ParameterExpressionData() { }

        public static ParameterExpressionData Pack(ParameterExpression pe, TypeDataManager typeDataManager)
        {
            var data = new ParameterExpressionData();

            data.CanReduce = pe.CanReduce;
            data.NodeType = pe.NodeType;
            data.Type = typeDataManager.Pack(pe.Type);

            data.IsByRef = pe.IsByRef;
            data.Name = pe.Name;

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            lock (Instance)
            {
                var t = typeDataManager.UnpackFromName(Type!);

                if (Instance.TryGetValue((t, Type!), out var value))
                {
                    return value;
                }

                value = Expression.Parameter(t, Name);
                Instance.Add((t, Type!), value);
                return value;
            }
        }

        private static Dictionary<(Type, string), ParameterExpression> Instance { get => _instance.Value; }

        private static readonly Lazy<Dictionary<(Type, string), ParameterExpression>> _instance = new Lazy<Dictionary<(Type, string), ParameterExpression>>();
    }

    public class BinaryExpressionData : ExpressionData
    {
        public LambdaExpressionData? Conversion { get; set; }
        public bool IsLifted { get; set; }
        public bool IsLiftedToNull { get; set; }
        public ExpressionData? Left { get; set; }
        public MethodInfoData? Method { get; set; }
        public ExpressionData? Right { get; set; }

        public BinaryExpressionData() { }

        public static BinaryExpressionData Pack(BinaryExpression bine, TypeDataManager typeDataManager)
        {
            var data = new BinaryExpressionData();

            data.CanReduce = bine.CanReduce;
            data.NodeType = bine.NodeType;
            data.Type = typeDataManager.Pack(bine.Type);

            data.Conversion = bine.Conversion != null ? LambdaExpressionData.Pack(bine.Conversion, typeDataManager) : null;
            data.IsLifted = bine.IsLifted;
            data.IsLiftedToNull = bine.IsLiftedToNull;
            data.Left = ExpressionData.Pack(bine.Left, typeDataManager);
            data.Method = bine.Method != null ? MethodInfoData.Pack(bine.Method, typeDataManager) : null;
            data.Right = ExpressionData.Pack(bine.Right, typeDataManager);

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            if (Conversion != null)
            {
                // return Expression.MakeBinary((ExpressionType)(NodeType!), Left!.Unpack(), Right!.Unpack(), IsLiftedToNull, Method.Unpack(), (LambdaExpression)Conversion.Unpack());
            }
            return Expression.MakeBinary((ExpressionType)(NodeType!), Left!.Unpack(typeDataManager), Right!.Unpack(typeDataManager)); //, IsLiftedToNull, Method.Unpack());
        }
    }

    public class MemberExpressionData : ExpressionData
    {
        public ExpressionData? Expression { get; set; }
        public MemberInfoData? Member { get; set; }
        public MemberExpressionData() { }

        public static MemberExpressionData Pack(MemberExpression membe, TypeDataManager typeDataManager)
        {
            var data = new MemberExpressionData();

            data.CanReduce = membe.CanReduce;
            data.NodeType = membe.NodeType;
            data.Type = typeDataManager.Pack(membe.Type);

            data.Expression = ExpressionData.Pack(membe.Expression, typeDataManager);
            data.Member = MemberInfoData.Pack(membe.Member, typeDataManager);

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            var e = Expression!.Unpack(typeDataManager);
            var memberInfo = Member!.Unpack(typeDataManager);
            return System.Linq.Expressions.Expression.MakeMemberAccess(e, memberInfo);
        }
    }

    public class ConstantExpressionData : ExpressionData
    {
        public object? Value { get; set; }
        public string? CustomTypeString { get; set; }
        public IDictionary<string, object>? CustomTypeValue { get; set; }

        public ConstantExpressionData() { }

        public static ConstantExpressionData Pack(ConstantExpression ce, TypeDataManager typeDataManager)
        {
            var data = new ConstantExpressionData();

            data.CanReduce = ce.CanReduce;
            data.NodeType = ce.NodeType;
            data.Type = typeDataManager.Pack(ce.Type);

            data.Value = ce.Value;
    
            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            return Expression.Constant(Value);
        }
    }

    public class MethodCallExpressionData : ExpressionData
    {
        public IReadOnlyCollection<ExpressionData>? Arguments { get; set; }
        public MethodInfoData? Method { get; set; }
        public ExpressionData? Object { get; set; }

        public MethodCallExpressionData() { }

        public static MethodCallExpressionData Pack(MethodCallExpression mce, TypeDataManager typeDataManager)
        {
            var data = new MethodCallExpressionData();

            data.CanReduce = mce.CanReduce;
            data.NodeType = mce.NodeType;
            data.Type = typeDataManager.Pack(mce.Type);

            data.Arguments = mce.Arguments.Select(x => ExpressionData.Pack(x, typeDataManager)).ToArray();
            data.Method = MethodInfoData.Pack(mce.Method, typeDataManager);
            data.Object = mce.Object != null ? ExpressionData.Pack(mce.Object, typeDataManager) : null;

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            var o = Object?.Unpack(typeDataManager);
            if (o != null)
            {
                return Expression.Call(o, Method!.Unpack(typeDataManager), Arguments.Select(x => x.Unpack(typeDataManager)!)!);
            }
            else
            {
                var method = Method!.Unpack(typeDataManager);
                var args = Arguments.Select(x => x.Unpack(typeDataManager)!)!;
                return Expression.Call(method, args);
            }
        }
    }

    public class NewExpressionData : ExpressionData
    {
        public IReadOnlyCollection<ExpressionData>? Arguments { get; set; }
        public ConstructorInfoData? ConstructorInfo { get; set; }

        public NewExpressionData() { }

        public static NewExpressionData Pack(NewExpression ne, TypeDataManager typeDataManager)
        {
            var data = new NewExpressionData();

            data.CanReduce = ne.CanReduce;
            data.NodeType = ne.NodeType;
            data.Type = typeDataManager.Pack(ne.Type);

            data.Arguments = ne.Arguments.Select(x => ExpressionData.Pack(x, typeDataManager)).ToArray();
            data.ConstructorInfo = ConstructorInfoData.Pack(ne.Constructor, typeDataManager);

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            return Expression.New(ConstructorInfo?.Unpack(typeDataManager)!, Arguments?.Select(x => x.Unpack(typeDataManager)));
        }
    }

    public class NewArrayExpressionData : ExpressionData
    {
        public IReadOnlyCollection<ExpressionData>? Expressions { get; set; }

        public NewArrayExpressionData() { }

        public static NewArrayExpressionData Pack(NewArrayExpression nae, TypeDataManager typeDataManager)
        {
            var data = new NewArrayExpressionData();

            data.CanReduce = nae.CanReduce;
            data.NodeType = nae.NodeType;
            if (nae.Type.IsArray)
            {
                data.Type = typeDataManager.Pack(nae.Type.GetElementType()!);
            }

            data.Expressions = nae.Expressions.Select(x => ExpressionData.Pack(x, typeDataManager)).ToArray();

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            var t = typeDataManager.UnpackFromName(Type!);
            return Expression.NewArrayInit(t, Expressions.Select(x => x.Unpack(typeDataManager)!));
        }
    }

    public static class BinaryWriterExtensions
    {
        public static void WriteType(this BinaryWriter writer, Type t)
        {
            if (t.AssemblyQualifiedName == null)
            {
                throw new InvalidOperationException();
            }

            writer.Write(t.AssemblyQualifiedName);
        }
    }

    public static class BinaryReaderExtensions
    {
        public static Type ReadType(this BinaryReader reader)
        {
            var s = reader.ReadString();
            return ReconstructType(s);
        }

        private static Type ReconstructType(string assemblyQualifiedName, params Assembly[] referencedAssemblies)
        {
            Type? type = null;

            foreach (var asm in referencedAssemblies)
            {
                var fullNameWithoutAssemblyName = assemblyQualifiedName.Replace($", {asm.FullName}", "");
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
                    type = ConstructGenericType(assemblyQualifiedName);
                }
                else
                {
                    type = Type.GetType(assemblyQualifiedName, false);
                }
            }

            if (type == null)
            {
                throw new Exception($"The type \"{assemblyQualifiedName}\" cannot be found in referenced assemblies.");
            }

            return type;
        }

        private static Type? ConstructGenericType(string assemblyQualifiedName)
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
            var genericType = ReconstructType(typeName);
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
                    var t = ReconstructType(typeNames[i]);
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
    }
}
