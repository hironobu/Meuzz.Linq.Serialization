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

    public class ExpressionData
    {
        public bool? CanReduce { get; set; }
        public ExpressionType? NodeType { get; set; }
        public string? Type { get; set; }
        
        public ExpressionData() { }

        public static ExpressionData Pack(Expression e)
        {
            switch (e)
            {
                case LambdaExpression le:
                    return LambdaExpressionData.Pack(le);

                case ParameterExpression pe:
                    return ParameterExpressionData.Pack(pe);

                case BinaryExpression bine:
                    return BinaryExpressionData.Pack(bine);

                case MemberExpression membe:
                    return MemberExpressionData.Pack(membe);

                case ConstantExpression ce:
                    return ConstantExpressionData.Pack(ce);

                case MethodCallExpression mce:
                    return MethodCallExpressionData.Pack(mce);

                case NewExpression ne:
                    return NewExpressionData.Pack(ne);

                case NewArrayExpression nae:
                    return NewArrayExpressionData.Pack(nae);

                default:
                    throw new NotImplementedException();
            }
        }

        public virtual Expression Unpack() { throw new NotImplementedException(); }
    }

    public class LambdaExpressionData : ExpressionData
    {
        public ExpressionData? Body { get; set; }
        public string? Name { get; set; }
        public IEnumerable<ParameterExpressionData>? Parameters { get; set; }
        public TypeData? ReturnType { get; set; }
        public bool? TailCall { get; set; }

        public LambdaExpressionData() : base() { }

        public static LambdaExpressionData Pack(LambdaExpression le)
        {
            var data = new LambdaExpressionData();

            data.CanReduce = le.CanReduce;
            data.NodeType = le.NodeType;
            data.Type = le.Type.FullName;

            data.Body = ExpressionData.Pack(le.Body);
            data.Name = le.Name;
            data.Parameters = le.Parameters.Select(x => ParameterExpressionData.Pack(x)).ToArray();
            data.ReturnType = TypeData.Pack(le.ReturnType);
            data.TailCall = le.TailCall;

            return data;
        }

        public override Expression Unpack()
        {
            return Expression.Lambda(Body!.Unpack(), Name, (bool)TailCall!, Parameters.Select(x => (ParameterExpression)x.Unpack()!));
        }
    }

    public class ParameterExpressionData : ExpressionData
    {
        public bool? IsByRef { get; set; }
        public string? Name { get; set; }

        public ParameterExpressionData() { }

        public static ParameterExpressionData Pack(ParameterExpression pe)
        {
            var data = new ParameterExpressionData();

            data.CanReduce = pe.CanReduce;
            data.NodeType = pe.NodeType;
            data.Type = pe.Type.FullName;

            data.IsByRef = pe.IsByRef;
            data.Name = pe.Name;

            return data;
        }

        public override Expression Unpack()
        {
            lock (Instance)
            {
                var t = TypeData.FromName(Type!).Unpack();

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

        public static BinaryExpressionData Pack(BinaryExpression bine)
        {
            var data = new BinaryExpressionData();

            data.CanReduce = bine.CanReduce;
            data.NodeType = bine.NodeType;
            data.Type = bine.Type.FullName;

            data.Conversion = bine.Conversion != null ? LambdaExpressionData.Pack(bine.Conversion) : null;
            data.IsLifted = bine.IsLifted;
            data.IsLiftedToNull = bine.IsLiftedToNull;
            data.Left = ExpressionData.Pack(bine.Left);
            data.Method = bine.Method != null ? MethodInfoData.Pack(bine.Method) : null;
            data.Right = ExpressionData.Pack(bine.Right);

            return data;
        }

        public override Expression Unpack()
        {
            if (Conversion != null)
            {
                // return Expression.MakeBinary((ExpressionType)(NodeType!), Left!.Unpack(), Right!.Unpack(), IsLiftedToNull, Method.Unpack(), (LambdaExpression)Conversion.Unpack());
            }
            return Expression.MakeBinary((ExpressionType)(NodeType!), Left!.Unpack(), Right!.Unpack()); //, IsLiftedToNull, Method.Unpack());
        }
    }

    public class MemberExpressionData : ExpressionData
    {
        public ExpressionData? Expression { get; set; }
        public MemberInfoData? Member { get; set; }
        public MemberExpressionData() { }

        public static MemberExpressionData Pack(MemberExpression membe)
        {
            var data = new MemberExpressionData();

            data.CanReduce = membe.CanReduce;
            data.NodeType = membe.NodeType;
            data.Type = membe.Type.FullName;

            data.Expression = ExpressionData.Pack(membe.Expression);
            data.Member = MemberInfoData.Pack(membe.Member);

            return data;
        }

        public override Expression Unpack()
        {
            var e = Expression!.Unpack();
            var memberInfo = Member!.Unpack();
            return System.Linq.Expressions.Expression.MakeMemberAccess(e, memberInfo);
        }
    }

    public class ConstantExpressionData : ExpressionData
    {
        public object? Value { get; set; }
        public string? CustomTypeString { get; set; }
        public IDictionary<string, object>? CustomTypeValue { get; set; }

        public ConstantExpressionData() { }

        public static ConstantExpressionData Pack(ConstantExpression ce)
        {
            var data = new ConstantExpressionData();

            data.CanReduce = ce.CanReduce;
            data.NodeType = ce.NodeType;
            data.Type = ce.Type.FullName;

            data.Value = ce.Value;
    
            return data;
        }

        public override Expression Unpack()
        {
            return Expression.Constant(Value);
        }
    }

    public class MethodCallExpressionData : ExpressionData
    {
        public IEnumerable<ExpressionData>? Arguments { get; set; }
        public MethodInfoData? Method { get; set; }
        public ExpressionData? Object { get; set; }

        public MethodCallExpressionData() { }

        public static MethodCallExpressionData Pack(MethodCallExpression mce)
        {
            var data = new MethodCallExpressionData();

            data.CanReduce = mce.CanReduce;
            data.NodeType = mce.NodeType;
            data.Type = mce.Type.FullName;

            data.Arguments = mce.Arguments.Select(x => ExpressionData.Pack(x));
            data.Method = MethodInfoData.Pack(mce.Method);
            data.Object = mce.Object != null ? ExpressionData.Pack(mce.Object) : null;

            return data;
        }

        public override Expression Unpack()
        {
            var o = Object?.Unpack();
            if (o != null)
            {
                return Expression.Call(o, Method!.Unpack(), Arguments.Select(x => x.Unpack()!)!);
            }
            else
            {
                var method = Method!.Unpack();
                var args = Arguments.Select(x => x.Unpack()!)!;
                return Expression.Call(method, args);
            }
        }
    }

    public class NewExpressionData : ExpressionData
    {
        public IEnumerable<ExpressionData>? Arguments { get; set; }
        public ConstructorInfoData? ConstructorInfo { get; set; }

        public NewExpressionData() { }

        public static NewExpressionData Pack(NewExpression ne)
        {
            var data = new NewExpressionData();

            data.CanReduce = ne.CanReduce;
            data.NodeType = ne.NodeType;
            data.Type = ne.Type.FullName;

            data.Arguments = ne.Arguments.Select(x => ExpressionData.Pack(x));
            data.ConstructorInfo = ConstructorInfoData.Pack(ne.Constructor);

            return data;
        }

        public override Expression Unpack()
        {
            return Expression.New(ConstructorInfo?.Unpack()!, Arguments?.Select(x => x.Unpack()));
        }
    }

    public class NewArrayExpressionData : ExpressionData
    {
        public IEnumerable<ExpressionData>? Expressions { get; set; }

        public NewArrayExpressionData() { }

        public static NewArrayExpressionData Pack(NewArrayExpression nae)
        {
            var data = new NewArrayExpressionData();

            data.CanReduce = nae.CanReduce;
            data.NodeType = nae.NodeType;
            if (nae.Type.IsArray)
            {
                data.Type = nae.Type.GetElementType()!.FullName;
            }

            data.Expressions = nae.Expressions.Select(x => ExpressionData.Pack(x)).ToArray();

            return data;
        }

        public override Expression Unpack()
        {
            var t = TypeData.FromName(Type!).Unpack();
            return Expression.NewArrayInit(t, Expressions.Select(x => x.Unpack()!));
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
