using System;
using System.IO;

namespace Mono_target
{

    class Program
    {

        private delegate void OutAction<T1, T2>(out T1 a, out T2 b);
        private delegate void RefAction<T1, T2>(ref T1 a, ref T2 b);

        static void Main(string[] args)
        {
            //CallTargetAOPClassMethods();
            //CallWeakEvent();
        }

        private static void CallTargetAOPClassMethods()
        {
            TargetAopClass target = new TargetAopClass(3);
            CallMethod((Func<int, int>)TargetAopClass.StatMethodThatCallsStatMethod, 3);
            CallMethod((Action)target.First);
            CallMethod((Func<int, object, int>)target.MethodWithFewParameters, 3, "3");
            short shortParam = 3;
            byte byteParam = 21;
            object objParam = "Hello";
            CallMethod((OutAction<byte, object>)target.MethodWithOutParameters, byteParam, objParam);
            CallMethod((RefAction<short, object>)target.MethodWithRefParameters, shortParam, objParam);
        }

        private static void CallWeakEvent()
        {
            EventSource eventSource = new EventSource();
            EventListener eventListener = new EventListener(eventSource);
            eventListener.StartListeningEvent();
            eventSource.CallEvent();
            eventListener.StopListeningEvent();
        }

        private static void CallMethod(Delegate deleg, params Object[] args)
        {
            DrawLineInLog();
            deleg.Method.Invoke(deleg.Target, args);
            DrawLineInLog();
        }
        
        private static void DrawLineInLog()
        {
            using(StreamWriter writer = new StreamWriter(new FileStream("Log.txt", FileMode.Append, FileAccess.Write)))
            {
                for (int i = 0; i < 10; i++)
                {
                    writer.Write('-');
                }
                writer.WriteLine();
            }
        }

    }

}