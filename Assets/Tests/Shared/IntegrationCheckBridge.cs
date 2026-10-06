using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace PluggerHead.Tests
{
    /// <summary>
    /// Test assemblies cannot reference Unity's predefined Assembly-CSharp assemblies.
    /// Resolve the existing integration helpers without changing the game's assembly layout.
    /// 测试程序集通过此入口复用已有验证脚本, 保留原异常及堆栈.
    /// </summary>
    public static class IntegrationCheckBridge
    {
        public static Type FindType(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            throw new InvalidOperationException(
                $"Integration helper {typeName} is unavailable. Run these tests inside the Unity Editor.");
        }

        public static object Invoke(string typeName, string methodName, params object[] arguments)
        {
            MethodInfo method = FindType(typeName).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                throw new MissingMethodException(typeName, methodName);
            }

            try
            {
                return method.Invoke(null, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
