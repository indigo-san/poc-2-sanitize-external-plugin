using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ConsoleApp1;

public class SystemIOFileSanitizer : Sanitizer
{
    private readonly MethodDefinition _target;
    private readonly SanitizationService _sanitizationService;

    public SystemIOFileSanitizer(
        MethodDefinition target,
        SanitizationService sanitizationService)
    {
        _target = target;
        _sanitizationService = sanitizationService;
    }

    public override string Name => "System.IO.File";

    public override void Sanitize(Instruction instruction, ILProcessor processor, ref int index)
    {
        if (instruction.Operand is MethodReference referencedMethod)
        {
            if (referencedMethod.Name is "ReadAllText")
            {
                var add = Instruction.Create(OpCodes.Call, _sanitizationService.RequestReadAccessToFile);

                switch (referencedMethod.Parameters.Count)
                {
                    case 1:
                        // ldarg.0
                        // 
                        // これが追加される
                        // call [XXXX]Beutl.ILSanitization.Generated.ILSanitizationService::RequestFileAccess(string)
                        // 
                        // call string [System.Runtime]System.IO.File::ReadAllText(string)

                        processor.Body.Instructions.Insert(index, add);
                        break;

                    case 2:
                        // ldarg.0
                        // 
                        // これが追加される
                        // call [XXXX]Beutl.ILSanitization.Generated.ILSanitizationService::RequestFileAccess(string)
                        // 
                        // call class [System.Runtime]System.Text.Encoding [System.Runtime]System.Text.Encoding::get_UTF8()
                        // call string [System.Runtime]System.IO.File::ReadAllText(string, class [System.Runtime]System.Text.Encoding)

                        processor.Body.Instructions.Insert(index - 1, add);
                        break;

                    default:
                        throw new Exception("Invalid Prammeters.Count");
                }

                index += 1;
            }
            else if (referencedMethod.Name is "OpenText" or "CreateText" or "AppendText")
            {
                var add = Instruction.Create(OpCodes.Call, _sanitizationService.RequestReadWriteAccessToFile);

                processor.Body.Instructions.Insert(index, add);
                index += 1;
            }
            else if (referencedMethod.Name is "Create")
            {
                var add = Instruction.Create(OpCodes.Call, _sanitizationService.RequestReadAccessToFile);

                processor.Body.Instructions.Insert(index - (referencedMethod.Parameters.Count - 1), add);
                index += 1;
            }
        }
    }

    public override bool ShouldSanitize(MethodDefinition method)
    {
        return method.DeclaringType.FullName == "System.IO.File";
    }

    public override bool ShouldSanitize(FieldDefinition field)
    {
        return false;
    }
}
