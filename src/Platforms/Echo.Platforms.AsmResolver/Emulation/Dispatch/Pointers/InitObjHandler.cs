using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using Echo.Memory;

namespace Echo.Platforms.AsmResolver.Emulation.Dispatch.Pointers
{
    /// <summary>
    /// Implements a CIL instruction handler for <c>initobj</c> operations and its derivatives.
    /// </summary>
    [DispatcherTableEntry(CilCode.Initobj)]
    public class InitObjHandler : FallThroughOpCodeHandler
    {
        /// <inheritdoc />
        protected override CilDispatchResult DispatchInternal(CilExecutionContext context, CilInstruction instruction)
        {
            var factory = context.Machine.ValueFactory;
            
            var type = (ITypeDefOrRef) instruction.Operand!;
            var address = context.CurrentFrame.EvaluationStack.Pop();

            if (type is TypeSpecification { Signature: GenericParameterSignature parameterSignature }) {
                var genericContext = GenericContext.FromMethod(context.CurrentFrame.Method);
                type = genericContext.Method!.TypeArguments[parameterSignature.Index].ToTypeDefOrRef();
            }

            try
            {
                // Object/structure was pushed by reference onto the stack. Concretize address.
                long? resolvedAddress = address.Contents.IsFullyKnown
                    ? address.Contents.AsSpan().ReadNativeInteger(context.Machine.Is32Bit)
                    : context.Machine.UnknownResolver.ResolveDestinationPointer(context, instruction, address);

                switch (resolvedAddress)
                {
                    case null:
                        // If address is unknown even after resolution, assume it writes to "somewhere" successfully.
                        return CilDispatchResult.Success();

                    case 0:
                        // A null reference was passed.
                        return CilDispatchResult.NullReference(context);

                    case { } actualAddress:
                        // A non-null reference was passed.

                        // Allocate a temporary buffer to write into memory.
                        var buffer = factory.CreateValue(type.ToTypeSignature(), true);;

                        // Write it.
                        context.Machine.Memory.Write(actualAddress, buffer);
                        break;
                }
            }
            finally
            {
                factory.BitVectorPool.Return(address.Contents);
            }
            
            return CilDispatchResult.Success();
        }
    }
}