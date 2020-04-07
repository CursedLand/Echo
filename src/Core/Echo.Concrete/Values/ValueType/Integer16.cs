using System;
using System.Collections;
using Echo.Core.Values;

namespace Echo.Concrete.Values.ValueType
{
    /// <summary>
    /// Represents a (partially) known concrete 16 bit integral value.
    /// </summary>
    public class Integer16 : PrimitiveNumberValue
    {
        /// <summary>
        /// Wraps an unsigned 16 bit integer into a fully concrete and known instance of <see cref="Integer16"/>.
        /// </summary>
        /// <param name="value">The 16 bit integer to wrap.</param>
        /// <returns>The concrete 16 bit integer.</returns>
        public static implicit operator Integer16(ushort value)
        {
            return new Integer16(value);
        }

        /// <summary>
        /// Wraps a signed 16 bit integer into a fully concrete and known instance of <see cref="Integer16"/>.
        /// </summary>
        /// <param name="value">The 16 bit integer to wrap.</param>
        /// <returns>The concrete 16 bit integer.</returns>
        public static implicit operator Integer16(short value)
        {
            return new Integer16(value);
        }
        
        /// <summary>
        /// Represents the bitmask that is used for a fully known concrete 16 bit integral value. 
        /// </summary>
        public const ushort FullyKnownMask = 0xFFFF;
        
        private ushort _value;
        
        /// <summary>
        /// Creates a new, fully known concrete 16 bit integral value.
        /// </summary>
        /// <param name="value">The raw 16 bit value.</param>
        public Integer16(ushort value)
            : this(value, FullyKnownMask)
        {
        }

        /// <summary>
        /// Creates a new, fully known concrete 16 bit integral value.
        /// </summary>
        /// <param name="value">The raw 16 bit value.</param>
        public Integer16(short value)
            : this(value, FullyKnownMask)
        {
        }

        /// <summary>
        /// Creates a new, partially known concrete 16 bit integral value.
        /// </summary>
        /// <param name="value">The raw 16 bit value.</param>
        /// <param name="mask">The bit mask indicating the bits that are known.</param>
        public Integer16(short value, ushort mask)
            : this(unchecked((ushort) value), mask)
        {
        }

        /// <summary>
        /// Creates a new, partially known concrete 16 bit integral value.
        /// </summary>
        /// <param name="value">The raw 16 bit value.</param>
        /// <param name="mask">The bit mask indicating the bits that are known.</param>
        public Integer16(ushort value, ushort mask)
        {
            _value = value;
            Mask = mask;
        }

        /// <inheritdoc />
        public override bool IsKnown => Mask == FullyKnownMask;

        /// <inheritdoc />
        public override int Size => sizeof(ushort);

        /// <summary>
        /// Gets the signed representation of this 16 bit value.
        /// </summary>
        public short I16
        {
            get => unchecked((short) U16);
            set => U16 = unchecked((ushort) value);
        }

        /// <summary>
        /// Gets the unsigned representation of this 16 bit value.
        /// </summary>
        public ushort U16
        {
            get => (ushort) (_value & Mask);
            set => _value = value;
        }

        /// <summary>
        /// Gets a value indicating which bits in the integer are known.
        /// If bit at location <c>i</c> equals 1, bit <c>i</c> in <see cref="I16"/> and <see cref="U16"/> is known,
        /// and unknown otherwise.  
        /// </summary>
        public ushort Mask
        {
            get;
            set;
        }

        /// <inheritdoc />
        public override BitArray GetBits() => new BitArray(BitConverter.GetBytes(U16));

        /// <inheritdoc />
        public override BitArray GetMask() => new BitArray(BitConverter.GetBytes(Mask));

        /// <inheritdoc />
        public override IValue Copy() => new Integer16(U16);
    }
}