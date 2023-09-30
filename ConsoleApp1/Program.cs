using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

using System.Runtime.InteropServices;

internal partial class Program
{
    private static void Main(string[] args)
    {
        new Program().Run();
    }

    private static MethodDefinition Define_RequestFileAccess(ModuleDefinition module)
    {
        var corelib = (AssemblyNameReference)module.TypeSystem.CoreLibrary;
        var console = module.AssemblyResolver.Resolve(new AssemblyNameReference("System.Console", corelib.Version)).MainModule;
        var privateCoreLib = module.AssemblyResolver.Resolve(new AssemblyNameReference("System.Private.CoreLib", corelib.Version)).MainModule;
        var system = module.AssemblyResolver.Resolve(corelib).MainModule;

        var methodDefinition = new MethodDefinition(
            "RequestFileAccess",
            MethodAttributes.Static | MethodAttributes.Public,
            module.TypeSystem.Void);

        var fileParameterDefinition = new ParameterDefinition(
            "fileName",
            ParameterAttributes.None,
            module.TypeSystem.String);
        var callerParameterDefinition = new ParameterDefinition(
            "callerName",
            ParameterAttributes.None,
            module.TypeSystem.String);

        methodDefinition.Parameters.Add(fileParameterDefinition);
        methodDefinition.Parameters.Add(callerParameterDefinition);
        methodDefinition.ReturnType = module.TypeSystem.String;

        MethodBody body = methodDefinition.Body;

        static bool PredicateWriteLine(MethodDefinition method)
        {
            return method.Name == "WriteLine"
                && method.Parameters.Count == 3
                && method.Parameters[0].ParameterType.Name == "String"
                && method.Parameters[1].ParameterType.Name == "Object"
                && method.Parameters[2].ParameterType.Name == "Object";
        }
        static bool PredicateReadLine(MethodDefinition method)
        {
            return method.Name == "ReadLine"
                && method.Parameters.Count == 0
                && method.ReturnType.Name == "String";
        }
        static bool PredicateStringEquals(MethodDefinition method)
        {
            return method.Name == "Equals"
                && method.Parameters.Count == 2
                && method.ReturnType.Name == "Boolean"
                && method.Parameters[0].ParameterType.Name == "String"
                && method.Parameters[1].ParameterType.Name == "StringComparison";
        }
        static bool PredicateNewException(MethodDefinition method)
        {
            return method.IsConstructor
                && method.Parameters.Count == 1
                && method.Parameters[0].ParameterType.Name == "String";
        }

        MethodReference writeLine = module.ImportReference(console.GetType("System", "Console").Methods.Single(PredicateWriteLine));
        MethodReference readLine = module.ImportReference(console.GetType("System", "Console").Methods.Single(PredicateReadLine));
        MethodReference equals = module.ImportReference(privateCoreLib.GetType("System", "String").Methods.Single(PredicateStringEquals));
        MethodReference newException = module.ImportReference(privateCoreLib.GetType("System", "Exception").Methods.Single(PredicateNewException));

        var boolDefinition = new VariableDefinition(module.TypeSystem.Boolean);
        body.Variables.Add(boolDefinition);

        ILProcessor processor = body.GetILProcessor();

        processor.Emit(OpCodes.Ldstr, "File: '{0}', Caller: '{1}'");
        processor.Emit(OpCodes.Ldarg_0);
        processor.Emit(OpCodes.Ldarg_1);
        processor.Emit(OpCodes.Call, writeLine);

        processor.Emit(OpCodes.Call, readLine);
        processor.Emit(OpCodes.Ldstr, "y");
        processor.Emit(OpCodes.Ldc_I4_5);
        processor.Emit(OpCodes.Callvirt, equals);
        processor.Emit(OpCodes.Ldc_I4_0);
        processor.Emit(OpCodes.Ceq);
        var ldArgsInstuction = Instruction.Create(OpCodes.Ldarg_0);
        processor.Emit(OpCodes.Brfalse_S, ldArgsInstuction);

        processor.Emit(OpCodes.Ldstr, "Access Denied");
        processor.Emit(OpCodes.Newobj, newException);
        processor.Emit(OpCodes.Throw);

        processor.Append(ldArgsInstuction);
        processor.Emit(OpCodes.Ret);

        body.Optimize();

        return methodDefinition;
    }

    private void Run()
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(@"D:\Source\b-editor\beutl\src\Beutl\bin\Debug\net7.0");
        resolver.AddSearchDirectory(RuntimeEnvironment.GetRuntimeDirectory());

        var asmDef = AssemblyDefinition.ReadAssembly(
            @"D:\Source\b-editor\beutl\src\Beutl\bin\Debug\net7.0\Beutl.ExceptionHandler.dll",
            new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadWrite = true,
                ThrowIfSymbolsAreNotMatching = false
            });

        var module = asmDef.MainModule;
        var genType = new TypeDefinition("Beutl.ILSanitization.Generated", "ILSanitizationService", TypeAttributes.NotPublic | TypeAttributes.Class);
        var requestFileAccessDefinition = Define_RequestFileAccess(module);
        genType.Methods.Add(requestFileAccessDefinition);
        module.Types.Add(genType);

        foreach (var item in module.GetTypes())
        {
            if (item == genType)
                continue;

            foreach (var meth in item.Methods)
            {
                if (meth.Body == null)
                    continue;

                var processor = meth.Body.GetILProcessor();
                for (int i = 0; i < meth.Body.Instructions.Count; i++)
                {
                    Instruction? instuction = meth.Body.Instructions[i];
                    switch (instuction.OpCode.OperandType)
                    {
                        case OperandType.InlineMethod:
                            if (instuction.Operand is MethodReference referencedMethod)
                            {
                                if (referencedMethod.DeclaringType.FullName == "System.IO.File")
                                {
                                    if (referencedMethod.Name is "ReadAllText")
                                    {
                                        var ldMethodName = Instruction.Create(OpCodes.Ldstr, meth.FullName);
                                        var add = Instruction.Create(OpCodes.Call, requestFileAccessDefinition);
                                        processor.InsertBefore(instuction, ldMethodName);
                                        processor.InsertBefore(instuction, add);
                                        i += 2;
                                    }
                                }
                            }
                            break;
                        case OperandType.InlineField:
                            if (instuction.Operand is FieldReference referencedField)
                            {

                            }
                            break;
                    }
                }
            }
        }

        asmDef.Write("Beutl.ExceptionHandler.dll");
    }
}