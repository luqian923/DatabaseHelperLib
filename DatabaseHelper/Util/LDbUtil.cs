namespace LQ.DatabaseHelper.Util;

public static class LDbUtil
{
    public static Type GetNestedType(object obj)
    {
        // check if obj is generic
        if (!obj.GetType().IsGenericType) return obj.GetType();

        // get the generic type
        var type = obj.GetType().GetGenericTypeDefinition();
        return type;
    }
}