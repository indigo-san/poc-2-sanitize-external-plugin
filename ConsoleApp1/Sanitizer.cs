using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ConsoleApp1;

public abstract class Sanitizer
{
    public abstract string Name { get; }

    // method, fieldにアクセスできるか
    public abstract bool ShouldSanitize(MethodDefinition method);

    public abstract bool ShouldSanitize(FieldDefinition field);

    public abstract void Sanitize(Instruction instruction, ILProcessor processor, ref int index);
}
