using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ConsoleApp1;

public class DllImportSanitizer : Sanitizer
{
    public override string Name => "DllImport";

    public override void Sanitize(Instruction instruction, ILProcessor processor, ref int index)
    {
        // Todo
    }

    public override bool ShouldSanitize(MethodDefinition method)
    {
        return method.IsPInvokeImpl;
    }

    public override bool ShouldSanitize(FieldDefinition field)
    {
        return false;
    }
}
