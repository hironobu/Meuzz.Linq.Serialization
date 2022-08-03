using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Meuzz.Linq.Serialization.Core;

namespace Meuzz.Linq.Serialization.Core.Expressions
{
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
        public ExpressionData() { }

        public ExpressionType NodeType { get; set; }
        public string Type { get; set; } = string.Empty;
        public bool CanReduce { get; set; }

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

        public static ExpressionData None => _none;

        private static ExpressionData _none = new ExpressionData();
    }

    public class LambdaExpressionData : ExpressionData
    {
        public LambdaExpressionData() : base() { }

        public ExpressionData? Body { get; set; }
        public string Name { get; set; } = string.Empty;
        public IReadOnlyCollection<ParameterExpressionData> Parameters { get; set; } = Array.Empty<ParameterExpressionData>();
        public string ReturnType { get; set; } = string.Empty;
        public bool TailCall { get; set; }

        public static LambdaExpressionData Pack(LambdaExpression le, TypeDataManager typeDataManager)
        {
            var data = new LambdaExpressionData();

            data.NodeType = le.NodeType;
            data.Type = typeDataManager.Pack(le.Type);
            data.CanReduce = le.CanReduce;

            data.Body = Pack(le.Body, typeDataManager);
            data.Name = le.Name;
            data.Parameters = le.Parameters.Select(x => ParameterExpressionData.Pack(x, typeDataManager)).ToArray();
            data.ReturnType = typeDataManager.Pack(le.ReturnType);
            data.TailCall = le.TailCall;

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            if (Body == null)
            {
                throw new NotImplementedException();
            }
            return Expression.Lambda(Body.Unpack(typeDataManager), Name, TailCall, Parameters.Select(x => (ParameterExpression)x.Unpack(typeDataManager)));
        }
    }

    public class ParameterExpressionData : ExpressionData
    {
        public ParameterExpressionData() { }

        public bool IsByRef { get; set; }
        public string Name { get; set; } = string.Empty;

        public static ParameterExpressionData Pack(ParameterExpression pe, TypeDataManager typeDataManager)
        {
            var data = new ParameterExpressionData();

            data.NodeType = pe.NodeType;
            data.Type = typeDataManager.Pack(pe.Type);
            data.CanReduce = pe.CanReduce;

            data.IsByRef = pe.IsByRef;
            data.Name = pe.Name;

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            lock (Instance)
            {
                var t = typeDataManager.UnpackFromName(Type);

                if (Instance.TryGetValue((t, Type), out var value))
                {
                    return value;
                }

                value = Expression.Parameter(t, Name);
                Instance.Add((t, Type), value);
                return value;
            }
        }

        private static Dictionary<(Type, string), ParameterExpression> Instance { get => _instance.Value; }

        private static readonly Lazy<Dictionary<(Type, string), ParameterExpression>> _instance = new Lazy<Dictionary<(Type, string), ParameterExpression>>();
    }

    public class BinaryExpressionData : ExpressionData
    {
        public BinaryExpressionData() { }

        public LambdaExpressionData? Conversion { get; set; }
        public bool IsLifted { get; set; }
        public bool IsLiftedToNull { get; set; }
        public ExpressionData Left { get; set; } = None;
        public MethodInfoData? Method { get; set; }
        public ExpressionData Right { get; set; } = None;

        public static BinaryExpressionData Pack(BinaryExpression bine, TypeDataManager typeDataManager)
        {
            var data = new BinaryExpressionData();

            data.NodeType = bine.NodeType;
            data.Type = typeDataManager.Pack(bine.Type);
            data.CanReduce = bine.CanReduce;

            data.Conversion = bine.Conversion != null ? LambdaExpressionData.Pack(bine.Conversion, typeDataManager) : null;
            data.IsLifted = bine.IsLifted;
            data.IsLiftedToNull = bine.IsLiftedToNull;
            data.Left = Pack(bine.Left, typeDataManager);
            data.Method = bine.Method != null ? MethodInfoData.Pack(bine.Method, typeDataManager) : null;
            data.Right = Pack(bine.Right, typeDataManager);

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            return Expression.MakeBinary(NodeType, Left.Unpack(typeDataManager), Right.Unpack(typeDataManager));
        }
    }

    public class MemberExpressionData : ExpressionData
    {
        public MemberExpressionData() { }

        public ExpressionData Expression { get; set; } = None;
        public MemberInfoData? Member { get; set; }

        public static MemberExpressionData Pack(MemberExpression membe, TypeDataManager typeDataManager)
        {
            var data = new MemberExpressionData();

            data.NodeType = membe.NodeType;
            data.Type = typeDataManager.Pack(membe.Type);
            data.CanReduce = membe.CanReduce;

            data.Expression = Pack(membe.Expression, typeDataManager);
            data.Member = membe.Member != null ? MemberInfoData.Pack(membe.Member, typeDataManager) : null;

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            var e = Expression.Unpack(typeDataManager);
            var memberInfo = Member?.Unpack(typeDataManager);
            return System.Linq.Expressions.Expression.MakeMemberAccess(e, memberInfo);
        }
    }

    public class ConstantExpressionData : ExpressionData
    {
        public ConstantExpressionData() { }

        public object? Value { get; set; }

        public static ConstantExpressionData Pack(ConstantExpression ce, TypeDataManager typeDataManager)
        {
            var data = new ConstantExpressionData();

            data.NodeType = ce.NodeType;
            data.Type = typeDataManager.Pack(ce.Type, true);
            data.CanReduce = ce.CanReduce;

#if false
            var t = typeDataManager.UnpackFromName(data.Type);
            var obj = Activator.CreateInstance(t);
            foreach (var f in t.GetFields())
            {
                var f0 = ce.Type.GetField(f.Name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var v0 = f0?.GetValue(ce.Value);
                f.SetValue(obj, v0);
            }
#else
            var obj = ce.Value;
#endif
            data.Value = obj;

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            return Expression.Constant(Value);
        }
    }

    public class MethodCallExpressionData : ExpressionData
    {
        public MethodCallExpressionData() { }

        public IReadOnlyCollection<ExpressionData> Arguments { get; set; } = Array.Empty<ExpressionData>();
        public MethodInfoData? Method { get; set; }
        public ExpressionData? Object { get; set; }

        public static MethodCallExpressionData Pack(MethodCallExpression mce, TypeDataManager typeDataManager)
        {
            var data = new MethodCallExpressionData();

            data.NodeType = mce.NodeType;
            data.Type = typeDataManager.Pack(mce.Type);
            data.CanReduce = mce.CanReduce;

            data.Arguments = mce.Arguments.Select(x => Pack(x, typeDataManager)).ToArray();
            data.Method = MethodInfoData.Pack(mce.Method, typeDataManager);
            data.Object = mce.Object != null ? Pack(mce.Object, typeDataManager) : null;

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            var o = Object?.Unpack(typeDataManager);
            if (o != null)
            {
                return Expression.Call(o, Method?.Unpack(typeDataManager), Arguments.Select(x => x.Unpack(typeDataManager)));
            }
            else
            {
                var method = Method?.Unpack(typeDataManager);
                var args = Arguments.Select(x => x.Unpack(typeDataManager));
                return Expression.Call(method, args);
            }
        }
    }

    public class NewExpressionData : ExpressionData
    {
        public NewExpressionData() { }

        public IReadOnlyCollection<ExpressionData> Arguments { get; set; } = Array.Empty<ExpressionData>();
        public ConstructorInfoData? ConstructorInfo { get; set; }

        public static NewExpressionData Pack(NewExpression ne, TypeDataManager typeDataManager)
        {
            var data = new NewExpressionData();

            data.NodeType = ne.NodeType;
            data.Type = typeDataManager.Pack(ne.Type);
            data.CanReduce = ne.CanReduce;

            data.Arguments = ne.Arguments.Select(x => Pack(x, typeDataManager)).ToArray();
            data.ConstructorInfo = ne.Constructor != null ? ConstructorInfoData.Pack(ne.Constructor, typeDataManager) : null;

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            return Expression.New(ConstructorInfo?.Unpack(typeDataManager), Arguments.Select(x => x.Unpack(typeDataManager)));
        }
    }

    public class NewArrayExpressionData : ExpressionData
    {
        public NewArrayExpressionData() { }

        public IReadOnlyCollection<ExpressionData> Expressions { get; set; } = Array.Empty<ExpressionData>();

        public static NewArrayExpressionData Pack(NewArrayExpression nae, TypeDataManager typeDataManager)
        {
            var data = new NewArrayExpressionData();

            data.CanReduce = nae.CanReduce;
            data.NodeType = nae.NodeType;
            if (!nae.Type.IsArray)
            {
                throw new NotImplementedException();
            }
            var et = nae.Type.GetElementType();
            if (et == null)
            {
                throw new NotImplementedException();
            }
            data.Type = typeDataManager.Pack(et);

            data.Expressions = nae.Expressions.Select(x => Pack(x, typeDataManager)).ToArray();

            return data;
        }

        public override Expression Unpack(TypeDataManager typeDataManager)
        {
            var t = typeDataManager.UnpackFromName(Type);
            return Expression.NewArrayInit(t, Expressions.Select(x => x.Unpack(typeDataManager)));
        }
    }
}
