using System;
using System.IO;
using System.Collections.Generic;

namespace Mono_target
{

    class Log : Attribute
    {

        public void OnLoggerTargetMethodExit(System.Reflection.MethodBase method, Dictionary<string, object> parameters,
            object result = null)
        {
            using (StreamWriter writer = new StreamWriter(new FileStream("Log.txt", FileMode.Append, FileAccess.Write)))
            {
                writer.Write("CLASS: " + "{" + method.DeclaringType + "}. ");
                writer.Write("METHOD: " + "{" + method.Name + "}. ");
                writer.Write("PARAMETERS: " + "{");
                if (parameters.Count != 0)
                {
                    int i = 0;
                    foreach (KeyValuePair<string, object> keyValuePair in parameters)
                    {
                        writer.Write(keyValuePair.Key + " = " + keyValuePair.Value);
                        i++;
                        if (i < parameters.Count)
                        {
                            writer.Write(", ");
                        }
                    }
                }
                else
                {
                    writer.Write("none");
                }
                writer.Write("}");
                if (result != null)
                {
                    writer.Write(" and RETURNS {" + result + "}");
                }
                writer.WriteLine();
            }
        }

    }

}