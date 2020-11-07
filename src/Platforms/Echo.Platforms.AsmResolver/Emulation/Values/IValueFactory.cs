
using System;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Memory;
using AsmResolver.DotNet.Signatures.Types;
using Echo.Concrete.Values;
using Echo.Concrete.Values.ReferenceType;

namespace Echo.Platforms.AsmResolver.Emulation.Values
{
    /// <summary>
    /// Provides factory members for constructing values by type. 
    /// </summary>
    public interface IValueFactory : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether a single pointer returned by this value factory is 32-bits or 64-bits wide.  
        /// </summary>
        bool Is32Bit
        {
            get;
        }
        
        /// <summary>
        /// Creates a value for the provided type that is initialized with the default contents.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The default value.</returns>
        IConcreteValue CreateDefault(TypeSignature type);

        /// <summary>
        /// Creates an object reference to a value for the provided type that is initialized with the default contents.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The default value.</returns>
        ObjectReference CreateDefaultObject(TypeSignature type);

        /// <summary>
        /// Creates an unknown value for the provided type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The unknown value.</returns>
        IConcreteValue CreateUnknown(TypeSignature type);
        
        /// <summary>
        /// Creates an object reference to an unknown value for the provided type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The unknown value.</returns>
        ObjectReference CreateUnknownObject(TypeSignature type); 
        
        /// <summary>
        /// Allocates a chunk of addressable memory on the virtual heap, and returns a pointer value to the start of
        /// the memory chunk.  
        /// </summary>
        /// <param name="size">The size of the region to allocate.</param>
        /// <param name="initializeWithZeroes">Indicates the memory region should be initialized with zeroes.</param>
        /// <returns>A pointer to the memory.</returns>
        MemoryPointerValue AllocateMemory(int size, bool initializeWithZeroes);

        /// <summary>
        /// Allocates an array on the virtual heap.
        /// </summary>
        /// <param name="elementType">The type of elements to store in the array.</param>
        /// <param name="length">The number of elements.</param>
        /// <returns>The array.</returns>
        IDotNetArrayValue AllocateArray(TypeSignature elementType, int length);

        /// <summary>
        /// Allocates a structure.
        /// </summary>
        /// <param name="type">The type of object to allocate.</param>
        /// <param name="initializeWithZeroes">Indicates the memory region should be initialized with zeroes.</param>
        /// <returns>The allocated object.</returns>
        IDotNetStructValue AllocateStruct(TypeSignature type, bool initializeWithZeroes);

        /// <summary>
        /// Gets the string value for the fully known string literal.
        /// </summary>
        /// <param name="value">The string literal.</param>
        /// <returns>The string value.</returns>
        StringValue GetStringValue(string value);

        /// <summary>
        /// Gets the raw memory layout of a type within the virtual machine.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The memory layout.</returns>
        TypeMemoryLayout GetTypeMemoryLayout(ITypeDescriptor type);
    }
}