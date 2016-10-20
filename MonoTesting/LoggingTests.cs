using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono_target;
using System.Collections.Generic;
using System;
using System.IO;
using System.Reflection;

namespace MonoTesting
{

    [TestClass]
    public class LoggingTests
    {

        [TestMethod]
        public void TestLogging()
        {
            Assembly assembly = Assembly.LoadFile(
                @"D:\Учёба\Предметы\Сем 5\Лабораторные\СПП\Mono target\Mono target\bin\Debug\Mono target.exe"
            );
            object targetAOP = assembly.CreateInstance(typeof(TargetAopClass).FullName);
            Dictionary<string, object[]> methodsToCall = new Dictionary<string, object[]>();
            methodsToCall.Add("Second", new object[] { 1 });
            methodsToCall.Add("Third", new object[] { 2, 3 });
            foreach (KeyValuePair<string, object[]> method in methodsToCall)
            {
                MethodInfo methodInfo = targetAOP.GetType().GetMethod(method.Key);
                object res = methodInfo.Invoke(targetAOP, method.Value);
                int linesCount = File.ReadAllLines("Log.txt").Length;
                string logString = File.ReadAllLines("Log.txt")[linesCount - 1];
                Assert.AreEqual(method.Key, GetMethodName(logString));
                IEnumerator<string> logArgsEnumerator = GetParameters(logString).GetEnumerator();
                foreach(object arg in method.Value)
                {
                    logArgsEnumerator.MoveNext();
                    Assert.AreEqual(arg.ToString(), logArgsEnumerator.Current);
                }
                if(methodInfo.ReturnType.Name != "Void")
                {
                    Assert.AreEqual(res.ToString(), GetResult(logString));
                }
            }
        }

        [TestMethod]
        public void TestEventLogging()
        {
            Assembly assembly = Assembly.LoadFile(
                @"D:\Учёба\Предметы\Сем 5\Лабораторные\СПП\Mono target\Mono target\bin\Debug\Mono target.exe"
            );
            object eventSource = assembly.CreateInstance(typeof(EventSource).FullName);
            Type eventListenerType = assembly.GetType(typeof(EventListener).FullName);
            object eventListener = eventListenerType.GetConstructor(
                new Type[] {eventSource.GetType()}).Invoke(
                new object[] {eventSource}
            );
            eventListenerType.GetMethod("StartListeningEvent").Invoke(eventListener, null);
            eventSource.GetType().GetMethod("CallEvent").Invoke(eventSource, null);
            eventListenerType.GetMethod("StopListeningEvent").Invoke(eventListener, null);
            string[] fileContent = File.ReadAllLines("EventsLog.txt");
            Assert.AreEqual(true, fileContent[fileContent.Length - 1].
                Contains(eventListenerType.FullName + " processed event")
            );
        }

        private string GetMethodName(string logString)
        {
            return GetInsideString(logString, "METHOD: {", '}');
        }

        private List<string> GetParameters(string logString)
        {
            List<string> paramsStrings = new List<string>();
            string paramsString = GetInsideString(logString, "PARAMETERS: {", '}');
            string[] args = paramsString.Split(',');
            foreach(string argStr in args)
            {
                paramsStrings.Add(argStr.Substring(argStr.IndexOf('=') + 2));
            }
            return paramsStrings;
        }

        private string GetResult(string logString)
        {
            return GetInsideString(logString, "RETURNS {", '}');
        }

        private string GetInsideString(string sourceString ,string beginString, char endSymbol)
        {
            int beginStringLen = beginString.Length;
            int beginStringIndex = sourceString.IndexOf(beginString);
            int methodNameLen = 0;
            int i = beginStringIndex + beginStringLen;
            while (sourceString[i] != endSymbol)
            {
                i++;
                methodNameLen++;
            }
            string insideString = sourceString.Substring(
                beginStringIndex + beginStringLen, methodNameLen
            );
            return insideString;
        }

    }

}