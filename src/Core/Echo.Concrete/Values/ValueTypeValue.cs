using Echo.Core.Values;

namespace Echo.Concrete.Values
{
    /// <summary>
    /// Represents a single value that is passed on by copying the contents to the new location.
    /// </summary>
    public abstract class ValueTypeValue : IConcreteValue
    {
        /// <inheritdoc />
        public abstract bool IsKnown
        {
            get;
        }

        /// <inheritdoc />
        public abstract int Size
        {
            get;
        }

        public abstract IValue Copy();
    }
}