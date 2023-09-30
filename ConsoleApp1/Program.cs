using ConsoleApp1;

using Mono.Cecil;
using Mono.Cecil.Cil;

using System.Runtime.InteropServices;

internal partial class Program
{
    private readonly Func<MethodDefinition, SanitizationService, Sanitizer>[] _sanitizers =
    {
        (m, t) => new SystemIOFileSanitizer(m, t)
    };

    private static void Main(string[] args)
    {
        new Program().Run();
    }

    private void Run()
    {
        var asmResolver = new DefaultAssemblyResolver();
        asmResolver.AddSearchDirectory(@"D:\Source\b-editor\beutl\src\Beutl\bin\Debug\net7.0");
        asmResolver.AddSearchDirectory(RuntimeEnvironment.GetRuntimeDirectory());
        var metadataResolver = new MetadataResolver(asmResolver);

        var asmDef = AssemblyDefinition.ReadAssembly(
            @"D:\Source\b-editor\beutl\src\Beutl\bin\Debug\net7.0\Beutl.ExceptionHandler.dll",
            new ReaderParameters
            {
                AssemblyResolver = asmResolver,
                MetadataResolver = metadataResolver,
                ReadWrite = true,
                ThrowIfSymbolsAreNotMatching = false
            });

        var module = asmDef.MainModule;
        var genType = new TypeDefinition("Beutl.ILSanitization.Generated", "ILSanitizationService", TypeAttributes.NotPublic | TypeAttributes.Class);
        var requestReadFileDefinition = SanitizationService.DefineRequestReadAccessToFile(module);
        var requestReadWriteFileDefinition = SanitizationService.DefineRequestReadWriteAccessToFile(module);
        genType.Methods.Add(requestReadFileDefinition);
        genType.Methods.Add(requestReadWriteFileDefinition);
        module.Types.Add(genType);

        var sanitizationService = new SanitizationService
        {
            RequestReadAccessToFile = requestReadFileDefinition,
            RequestReadWriteAccessToFile = requestReadWriteFileDefinition
        };

        foreach (var item in module.GetTypes())
        {
            if (item == genType)
                continue;

            foreach (var meth in item.Methods)
            {
                if (meth.Body == null)
                    continue;

                var sanitizers = _sanitizers.Select(v => v(meth, sanitizationService)).ToArray();
                var processor = meth.Body.GetILProcessor();
                for (int i = 0; i < meth.Body.Instructions.Count; i++)
                {
                    Instruction? instuction = meth.Body.Instructions[i];
                    switch (instuction.OpCode.OperandType)
                    {
                        case OperandType.InlineMethod:
                            if (instuction.Operand is MethodReference referencedMethod)
                            {
                                var referencedMethodDefinition = metadataResolver.Resolve(referencedMethod);
                                foreach (var s in sanitizers)
                                {
                                    if (s.ShouldSanitize(referencedMethodDefinition))
                                    {
                                        s.Sanitize(instuction, processor, ref i);
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