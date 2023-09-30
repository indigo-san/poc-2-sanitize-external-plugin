using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace ConsoleApp1;

public class SanitizationService
{
    public required MethodDefinition RequestReadAccessToFile { get; init; }

    public required MethodDefinition RequestReadWriteAccessToFile { get; init; }

    public static MethodDefinition DefineRequestReadAccessToFile(ModuleDefinition module)
    {
        return DefineRequestReadOrWriteAccessToFile(
            "RequestReadAccessToFile",
            "Read access to file requested.",
            module);
    }
    
    public static MethodDefinition DefineRequestReadWriteAccessToFile(ModuleDefinition module)
    {
        return DefineRequestReadOrWriteAccessToFile(
            "RequestReadWriteAccessToFile",
            "Read or Write access to file requested.",
            module);
    }
    
    private static MethodDefinition DefineRequestReadOrWriteAccessToFile(string methodName, string message, ModuleDefinition module)
    {
        var corelib = (AssemblyNameReference)module.TypeSystem.CoreLibrary;
        var console = module.AssemblyResolver.Resolve(new AssemblyNameReference("System.Console", corelib.Version)).MainModule;
        var privateCoreLib = module.AssemblyResolver.Resolve(new AssemblyNameReference("System.Private.CoreLib", corelib.Version)).MainModule;
        var system = module.AssemblyResolver.Resolve(corelib).MainModule;

        var methodDefinition = new MethodDefinition(
            methodName,
            MethodAttributes.Static | MethodAttributes.Public,
            module.TypeSystem.Void);

        var fileParameterDefinition = new ParameterDefinition(
            "fileName",
            ParameterAttributes.None,
            module.TypeSystem.String);

        methodDefinition.Parameters.Add(fileParameterDefinition);
        methodDefinition.ReturnType = module.TypeSystem.String;

        MethodBody body = methodDefinition.Body;

        MethodReference writeLine = module.ImportReference(console.GetType("System", "Console").Methods.Single(PredicateWriteLine));
        MethodReference readLine = module.ImportReference(console.GetType("System", "Console").Methods.Single(PredicateReadLine));
        MethodReference equals = module.ImportReference(privateCoreLib.GetType("System", "String").Methods.Single(PredicateStringEquals));
        MethodReference newException = module.ImportReference(privateCoreLib.GetType("System", "Exception").Methods.Single(PredicateNewException));

        var boolDefinition = new VariableDefinition(module.TypeSystem.Boolean);
        body.Variables.Add(boolDefinition);

        ILProcessor processor = body.GetILProcessor();

        processor.Emit(OpCodes.Ldstr, $"{message} File: '{0}'");
        processor.Emit(OpCodes.Ldarg_0);
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

    private static bool PredicateNewException(MethodDefinition method)
    {
        return method.IsConstructor
            && method.Parameters.Count == 1
            && method.Parameters[0].ParameterType.Name == "String";
    }

    private static bool PredicateStringEquals(MethodDefinition method)
    {
        return method.Name == "Equals"
            && method.Parameters.Count == 2
            && method.ReturnType.Name == "Boolean"
            && method.Parameters[0].ParameterType.Name == "String"
            && method.Parameters[1].ParameterType.Name == "StringComparison";
    }

    private static bool PredicateReadLine(MethodDefinition method)
    {
        return method.Name == "ReadLine"
            && method.Parameters.Count == 0
            && method.ReturnType.Name == "String";
    }

    private static bool PredicateWriteLine(MethodDefinition method)
    {
        return method.Name == "WriteLine"
            && method.Parameters.Count == 3
            && method.Parameters[0].ParameterType.Name == "String"
            && method.Parameters[1].ParameterType.Name == "Object"
            && method.Parameters[2].ParameterType.Name == "Object";
    }
}
