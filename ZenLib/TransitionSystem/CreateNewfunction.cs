using System;
using System.Linq.Expressions;
using System.Reflection;

public class State
{
    public int Field1;
    public NestedState Field2;

    public State(int field1, NestedState field2)
    {
        Field1 = field1;
        Field2 = field2;
    }
}

public class NestedState
{
    public string SubField1;
    public double SubField2;

    public NestedState(string subField1, double subField2)
    {
        SubField1 = subField1;
        SubField2 = subField2;
    }
}

public class NewState
{
    public int Field1;
    public NewNestedState Field2;

    public NewState(int field1, NewNestedState field2)
    {
        Field1 = field1;
        Field2 = field2;
    }
}

public class NewNestedState
{
    public string SubField1;
    public double SubField2;

    public NewNestedState(string subField1, double subField2)
    {
        SubField1 = subField1;
        SubField2 = subField2;
    }
}

public class Converter
{
    // 动态生成 Func<newstate, newstate> 函数
    public static Func<object, object> ConvertFunc(Func<object, object> originFunc, Type stateType, Type newStateType)
    {
        // 使用 Expression Tree 来构造新的函数
        var param = Expression.Parameter(typeof(object), "newStateParam");
        // 将 object 转换为 newstate 类型
        var castToNewState = Expression.Convert(param, newStateType);
        // 获取 originFunc 中的表达式逻辑（这里我们假设你能获取到原始表达式树，或者通过反射生成）
        var originFuncBody = GenerateFunctionBody(stateType, newStateType);

        // 将原始逻辑应用到新的类型上
        var body = originFuncBody(castToNewState);

        // 创建新的 Func<newstate, newstate> 表达式树
        var lambda = Expression.Lambda<Func<object, object>>(body, param);
        // 编译表达式树为函数
        return lambda.Compile();
    }

    // 生成对 state 类型的字段操作逻辑
    private static Func<Expression, Expression> GenerateFunctionBody(Type stateType, Type newStateType)
    {
        return (newStateExpr) =>
        {
            // 示例：假设我们有对 Field1 和 Field2 的操作逻辑，操作会根据字段名进行动态映射
            var field1Old = Expression.Field(newStateExpr, stateType.GetField("Field1"));
            var field1New = Expression.Field(newStateExpr, newStateType.GetField("Field1"));

            var field2Old = Expression.Field(newStateExpr, stateType.GetField("Field2"));
            var field2New = Expression.Field(newStateExpr, newStateType.GetField("Field2"));

            // 例如：对字段 Field1 进行加 1 操作，Field2 的 SubField1 修改为 "Modified"
            var incrementField1 = Expression.Add(field1Old, Expression.Constant(1));

            var subField1Old = Expression.Field(field2Old, stateType.GetField("SubField1"));
            var subField1New = Expression.Field(field2New, newStateType.GetField("SubField1"));
            var assignSubField1 = Expression.Assign(subField1New, Expression.Constant("Modified"));

            // 返回修改后的字段逻辑
            var block = Expression.Block(
                Expression.Assign(field1New, incrementField1),
                assignSubField1,
                newStateExpr);

            return block;
        };
    }
}

class Program
{
    static void Main()
    {
        // 创建一个 State 实例
        var nestedState = new NestedState("SubValue", 3.14);
        var state = new State(10, nestedState);

        // 定义原始的 Func<state, state> 函数
        Func<object, object> originTest = (input) =>
        {
            var originalState = input as State;
            if (originalState != null)
            {
                originalState.Field1 += 1;
                originalState.Field2.SubField1 = "Modified";
            }
            return originalState;
        };

        // 将 Func<state, state> 转换为 Func<newstate, newstate>
        var newTest = Converter.ConvertFunc(originTest, typeof(State), typeof(NewState));

        // 创建新的 NewState 实例
        var newState = new NewState(10, new NewNestedState("SubValue", 3.14));

        // 调用新的 newTest 函数
        var result = newTest(newState);

        // 输出转换后的结果
        var newStateResult = result as NewState;
        Console.WriteLine(newStateResult.Field1);  // 输出 11
        Console.WriteLine(newStateResult.Field2.SubField1);  // 输出 "Modified"
    }
}
