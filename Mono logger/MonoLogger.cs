using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Mono_logger
{

    class MonoLogger
    {

        public static void DumpAssemblyMethod(string assemblyPath, string methodName)
        {
            using(StreamWriter writer = new StreamWriter(new FileStream(methodName + "IL.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite)))
            {
                var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
                foreach(var typeDef in assembly.MainModule.Types)
                {
                    foreach(var method in typeDef.Methods)
                    {
                        if(method.Name == methodName)
                        {
                            foreach(var ilCodeInstruction in method.Body.Instructions)
                            {
                                writer.WriteLine(ilCodeInstruction.ToString());
                            }
                            return;
                        }
                    }
                }
            }
        }

        public void InjectCodeIntoAssembly(string assemblyPath)
        {
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
            InjectLoggingIntoAssembly(assembly);
            InjectEventsLoggingIntoAssembly(assembly);
            assembly.Write(assemblyPath);
        }

        private void InjectEventsLoggingIntoAssembly(AssemblyDefinition assembly)
        {
            var weakAttributeTypeDef = GetTypeDefFromAssembly(assembly, "Weak");
            var assemblyTypes = assembly.MainModule.Types;
            var typesEnumerator = assemblyTypes.GetEnumerator();
            int countAssemblyTypes = assemblyTypes.Count();
            for(int i = 0; i < countAssemblyTypes; i++)
            {
                typesEnumerator.MoveNext();
                TypeDefinition type = typesEnumerator.Current;
                Mono.Collections.Generic.Collection<PropertyDefinition> typeProperties = type.Properties;
                for(int j = 0; j < typeProperties.Count; j++)
                {
                    if(typeProperties[j].CustomAttributes.Where(
                        attribute => (attribute.AttributeType == weakAttributeTypeDef)).Count() != 0
                    )
                    {
                        InjectWeakDelegateIntoPropertySetMethod(typeProperties[j].SetMethod, assembly);
                    }
                }
            }
        }

        private void InjectWeakDelegateIntoPropertySetMethod(MethodDefinition setMethod, AssemblyDefinition assembly)
        {
            TypeDefinition weakDelegateTypeDef = GetTypeDefFromAssembly(assembly, "WeakDelegate");
            ILProcessor ilProcessor = setMethod.Body.GetILProcessor();
            var weakDelegateCtor = weakDelegateTypeDef.Methods.First(
                method => method.IsConstructor &&
                method.Parameters.Count() == 1 &&
                method.Parameters[0].ParameterType.Name == "Delegate"
            );
            var weakDelegateGetWeakProp = weakDelegateTypeDef.Methods.First(method => method.Name == "get_Weak");
            List<Instruction> instructionsToInsert = new List<Instruction>();
            instructionsToInsert.Add(Instruction.Create(OpCodes.Nop));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Ldarg_1));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Newobj, weakDelegateCtor));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Call, weakDelegateGetWeakProp));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Starg_S, setMethod.Parameters[0]));
            Instruction firstInstruction = setMethod.Body.Instructions.First();
            foreach (Instruction instruction in instructionsToInsert)
            {
                ilProcessor.InsertBefore(firstInstruction, instruction);
            }
        }

        private void InjectLoggingIntoAssembly(AssemblyDefinition assembly)
        {
            var logAttributeTypeDef = GetTypeDefFromAssembly(assembly, "Log");
            //LOCAL VARS TYPES REFS
            var methodBaseRef = assembly.MainModule.Import(typeof(System.Reflection.MethodBase));
            var typeRef = assembly.MainModule.Import(typeof(Type));
            var attributeRef = assembly.MainModule.Import(typeof(CustomAttribute));
            var dictionaryStringObjectRef = assembly.MainModule.Import(typeof(Dictionary<string, object>));
            //
            var assemblyTypesWhichMethodsToLog = GetTypesWithAttribute(assembly, logAttributeTypeDef);
            var typesEnumerator = assemblyTypesWhichMethodsToLog.GetEnumerator();
            int countTypesToInject = assemblyTypesWhichMethodsToLog.Count();
            for(int i = 0; i < countTypesToInject; i++)
            {
                typesEnumerator.MoveNext();
                TypeDefinition type = typesEnumerator.Current;
                MethodDefinition[] typeMethods = type.Methods.ToArray();
                foreach(var method in typeMethods)
                {
                    var targetMethodDefinition = method;
                    var methodILProc = method.Body.GetILProcessor();
                    if (method.ReturnType.Name != "Void")
                    {
                        MethodDefinition methodCopyDefinition = CreateMethodDynamicCopy(method);
                        type.Methods.Add(methodCopyDefinition);
                        methodILProc.Body.Instructions.Clear();
                        targetMethodDefinition = methodCopyDefinition;
                    }
                    List<Instruction> instructionsToInsert = GetInstructionsToInsert(
                        assembly,
                        targetMethodDefinition,
                        methodILProc,
                        logAttributeTypeDef,
                        methodBaseRef,
                        typeRef,
                        attributeRef,
                        dictionaryStringObjectRef
                    );
                    WriteILInstructionsToMethodBody(instructionsToInsert, method);
                }
            }
        }

        private void WriteILInstructionsToMethodBody(List<Instruction> instructions, MethodDefinition method)
        {
            bool methodHasResult = method.ReturnType.Name == "Void" ? false : true;
            ILProcessor methodILProc = method.Body.GetILProcessor();
            if (methodHasResult)
            {
                foreach (Instruction insertInstruction in instructions)
                {
                    methodILProc.Body.Instructions.Add(insertInstruction);
                }
            }
            else
            {
                if (method.IsConstructor)
                {
                    Instruction instructionAfterFirstCall = method.Body.Instructions.First(
                        instruction =>
                        (
                            instruction.Previous != null &&
                            instruction.Previous.OpCode == OpCodes.Call
                        )
                    );
                    foreach (Instruction instruction in instructions)
                    {
                        methodILProc.InsertBefore(instructionAfterFirstCall, instruction);
                    }
                }
                else
                {
                    Instruction firstInstruction = method.Body.Instructions.First();
                    foreach (Instruction instruction in instructions)
                    {
                        methodILProc.InsertBefore(firstInstruction, instruction);
                    }
                }
            }
        }

        private MethodDefinition CreateMethodDynamicCopy(MethodDefinition methodDefinition)
        {
            String methodCopyName = methodDefinition.Name + "Source";
            MethodDefinition methodCopyDefinition = new MethodDefinition(methodCopyName, methodDefinition.Attributes, methodDefinition.ReturnType);
            foreach(var parameter in methodDefinition.Parameters)
            {
                methodCopyDefinition.Parameters.Add(parameter);
            }
            methodCopyDefinition.DeclaringType = methodDefinition.DeclaringType;
            methodCopyDefinition.Body.InitLocals = true;
            foreach(VariableDefinition var in methodDefinition.Body.Variables)
            {
                methodCopyDefinition.Body.Variables.Add(var);
            }
            foreach (Instruction instruction in methodDefinition.Body.Instructions)
            {
                methodCopyDefinition.Body.Instructions.Add(instruction);
            }
            return methodCopyDefinition;
        }

        private List<Instruction> GetInstructionsToInsert(
            AssemblyDefinition assembly,
            MethodDefinition targetMethodDefinition,
            ILProcessor ilProcessor,
            TypeDefinition attributeTypeDef,
            TypeReference methodBaseRef,
            TypeReference typeRef,
            TypeReference attributeRef,
            TypeReference dictionaryStringObjectRef
        )
        {
            bool methodHasResult = targetMethodDefinition.ReturnType.Name == "Void" ? false : true;
            List<Instruction> instructionsToInsert = new List<Instruction>();
            ilProcessor.Body.InitLocals = true;
            ///create local vars
            var currentMethodVar = new VariableDefinition(methodBaseRef);
            var currentTypeVar = new VariableDefinition(typeRef);
            var attributeVar = new VariableDefinition(attributeRef);
            var parametersVar = new VariableDefinition(dictionaryStringObjectRef);
            var returnTypeRef = assembly.MainModule.Import(targetMethodDefinition.ReturnType);
            ///add local vars
            ilProcessor.Body.Variables.Add(currentMethodVar);
            ilProcessor.Body.Variables.Add(currentTypeVar);
            ilProcessor.Body.Variables.Add(attributeVar);
            ilProcessor.Body.Variables.Add(parametersVar);
            ///pointers to methods
            var getCurrentMethodRef = assembly.MainModule.Import(typeof(System.Reflection.MethodBase).GetMethod("GetCurrentMethod"));
            var getDeclayringTypeRef = assembly.MainModule.Import(typeof(System.Reflection.MemberInfo).GetMethod("get_DeclaringType"));
            var getCustomAttributeRef = assembly.MainModule.Import(typeof(Attribute).GetMethod("GetCustomAttribute",
                new Type[] { typeof(System.Reflection.MemberInfo), typeof(Type) }));
            var getTypeFromHandleRef = assembly.MainModule.Import(typeof(Type).GetMethod("GetTypeFromHandle",
                new Type[] { typeof(RuntimeTypeHandle) }));
            var onLoggerTargetMethodExitRef = GetOnLoggerTargetMethodDef(attributeTypeDef);
            var voidRef = assembly.MainModule.Import(typeof(void));
            ///local res var
            var resultVar = new VariableDefinition(returnTypeRef);
            if (methodHasResult)
            {
                ilProcessor.Body.Variables.Add(resultVar);
            }
            ///dictionary type and methods refs
            var dictionaryType = typeof(Dictionary<string, object>);
            var dictConstructorRef = assembly.MainModule.Import(dictionaryType.GetConstructor(Type.EmptyTypes));
            var dictMethodAddRef = assembly.MainModule.Import(dictionaryType.GetMethod("Add"));
            ///TYPES REFERENCES
            var Int32TypeRef = assembly.MainModule.Import(typeof(System.Int32));
            ///
            instructionsToInsert.Add(Instruction.Create(OpCodes.Nop));
            //
            instructionsToInsert.Add(Instruction.Create(OpCodes.Call, getCurrentMethodRef));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Stloc, currentMethodVar));
            //
            instructionsToInsert.Add(Instruction.Create(OpCodes.Ldloc, currentMethodVar));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Callvirt, getDeclayringTypeRef));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Stloc, currentTypeVar));
            //
            instructionsToInsert.Add(Instruction.Create(OpCodes.Ldloc, currentTypeVar));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Ldtoken, attributeTypeDef));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Call, getTypeFromHandleRef));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Call, getCustomAttributeRef));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Castclass, attributeTypeDef));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Stloc, attributeVar));
            //
            instructionsToInsert.Add(Instruction.Create(OpCodes.Newobj, dictConstructorRef));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Stloc, parametersVar));
            ///load parameters values
            foreach(var arg in targetMethodDefinition.Parameters)
            {
                instructionsToInsert.Add(Instruction.Create(OpCodes.Ldloc, parametersVar));
                instructionsToInsert.Add(Instruction.Create(OpCodes.Ldstr, arg.Name));
                instructionsToInsert.Add(Instruction.Create(OpCodes.Ldarg, arg));
                if (arg.IsOut || arg.ParameterType.Name.Contains('&'))
                {
                    instructionsToInsert.AddRange(GetRefOutBoxingInstructions(assembly, arg));
                }
                if (arg.ParameterType.IsPrimitive)
                {
                    TypeReference typeToBox = assembly.MainModule.Import(arg.ParameterType);
                    instructionsToInsert.Add(Instruction.Create(OpCodes.Box, typeToBox));   
                }
                instructionsToInsert.Add(Instruction.Create(OpCodes.Call, dictMethodAddRef));
            }
            ///Load OnLoggerTargetMethodExit args
            instructionsToInsert.Add(Instruction.Create(OpCodes.Ldloc, attributeVar));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Ldloc, currentMethodVar));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Ldloc, parametersVar));
            ///GET RETURN VALUE
            if(!methodHasResult)
            {
                instructionsToInsert.Add(Instruction.Create(OpCodes.Ldnull));
            }
            else
            {
                //CALL TARGET METHOD TO GET RESULT
                if (!targetMethodDefinition.IsStatic)
                {
                    instructionsToInsert.Add(Instruction.Create(OpCodes.Ldarg_0));
                }
                foreach (var arg in targetMethodDefinition.Parameters)
                {
                    instructionsToInsert.Add(Instruction.Create(OpCodes.Ldarg, arg));
                }
                instructionsToInsert.Add(Instruction.Create(OpCodes.Call, targetMethodDefinition));
                instructionsToInsert.Add(Instruction.Create(OpCodes.Stloc, resultVar));
                instructionsToInsert.Add(Instruction.Create(OpCodes.Ldloc, resultVar));
                TypeReference typeToBox = assembly.MainModule.Import(resultVar.VariableType);
                if (resultVar.VariableType.IsPrimitive)
                {
                    instructionsToInsert.Add(Instruction.Create(OpCodes.Box, typeToBox));
                }
            }
            ///
            instructionsToInsert.Add(Instruction.Create(OpCodes.Callvirt, onLoggerTargetMethodExitRef));
            instructionsToInsert.Add(Instruction.Create(OpCodes.Nop));
            if (methodHasResult)
            {
                instructionsToInsert.Add(Instruction.Create(OpCodes.Ldloc, resultVar));
                instructionsToInsert.Add(Instruction.Create(OpCodes.Ret));
            }
            ///
            return instructionsToInsert;
        }

        private List<Instruction> GetRefOutBoxingInstructions(AssemblyDefinition assembly, ParameterDefinition arg)
        {
            List<Instruction> boxingInstructions = new List<Instruction>();
            bool needBoxing = true;
            String typeName = arg.ParameterType.FullName.Substring(0, arg.ParameterType.FullName.Length - 1);
            switch (typeName)
            {
                case "System.Byte":
                case "System.UInt16":
                case "System.UInt32":
                case "System.UInt64":
                case "System.Int8":
                case "System.Int16":
                case "System.Int64":
                case "System.Int32":
                    boxingInstructions.Add(Instruction.Create(OpCodes.Ldind_I));
                    break;
                case "System.Single":
                    boxingInstructions.Add(Instruction.Create(OpCodes.Ldind_R4));
                    break;
                case "System.Double":
                    boxingInstructions.Add(Instruction.Create(OpCodes.Ldind_R8));
                    break;
                default:
                    needBoxing = false;
                    boxingInstructions.Add(Instruction.Create(OpCodes.Ldind_Ref));
                    break;
            }
            if (needBoxing)
            {
                Type typeToBox = Type.GetType(arg.ParameterType.FullName.Substring(0, arg.ParameterType.FullName.Length - 1));
                TypeReference typeToBoxRef = assembly.MainModule.Import(typeToBox);
                boxingInstructions.Add(Instruction.Create(OpCodes.Box, typeToBoxRef));
            }
            return boxingInstructions;
        }

        private MethodDefinition GetOnLoggerTargetMethodDef(TypeDefinition attributeDefinition)
        {
            Mono.Collections.Generic.Collection<MethodDefinition> methodsDefinitions = attributeDefinition.Methods;
            foreach(MethodDefinition methodDefinition in methodsDefinitions)
            {
                if(methodDefinition.Name == "OnLoggerTargetMethodExit")
                {
                    return methodDefinition;
                }
            }
            return null;
        }

        private TypeReference[] GetVarsTypeRefs(AssemblyDefinition assembly, params Type[] types)
        {
            TypeReference[] varsTypeRefs = new TypeReference[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                varsTypeRefs[i] = assembly.MainModule.Import(types[i]);
            }
            return varsTypeRefs;
        }

        private void DefineLocalVariablesInMethod(ILProcessor ilProcessor, params VariableDefinition[] varsDefinitions)
        {
            foreach(VariableDefinition varDefinition in varsDefinitions)
            {
                ilProcessor.Body.Variables.Add(varDefinition);
            }
        }

        private TypeDefinition GetTypeDefFromAssembly(AssemblyDefinition assembly, String typeName)
        {
            Mono.Collections.Generic.Collection<TypeDefinition> allAssemblyTypes = assembly.MainModule.Types;
            foreach(TypeDefinition typeDefinition in allAssemblyTypes)
            {
                if(typeDefinition.Name == typeName)
                {
                    return typeDefinition;
                }
            }
            return null;
        }

        private IEnumerable<TypeDefinition> GetTypesWithAttribute(
            AssemblyDefinition assembly, TypeDefinition logAttributeTypeDef)
        {
            return assembly.MainModule.Types.ToArray().Where(
                typeDef => (typeDef.CustomAttributes.Where(
                                attribute => attribute.AttributeType == logAttributeTypeDef).Count() != 0)
            );
        }

        public static void DumpMonoTargetOnLoggerTargetMethodExitIL()
        {
            string assemblyPath = @"D:\Учёба\Предметы\Сем 5\Лабораторные\СПП\Mono target\Mono target\bin\Debug\Mono target.exe";
            DumpAssemblyMethod(assemblyPath, "OnLoggerTargetMethodExit");
        }

    }

}