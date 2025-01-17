using AsmResolver.DotNet.Signatures;

namespace Echo.Platforms.AsmResolver.Emulation.Invocation
{
    /// <summary>
    /// Provides methods for constructing method invokers using a set of default invoker implementations. 
    /// </summary>
    public static class DefaultInvokers
    {
        /// <summary>
        /// Gets the method invoker that always lets the emulator step into the requested method, effectively letting
        /// the emulator emulate all called methods recursively.
        /// </summary>
        public static IMethodInvoker StepIn => StepInInvoker.Instance;
        
        /// <summary>
        /// Gets the method invoker that always steps over the requested method and produces an unknown result
        /// according to the method's return type.
        /// </summary>
        public static IMethodInvoker ReturnUnknown => ReturnDefaultInvoker.ReturnUnknown;
        
        /// <summary>
        /// Gets the method invoker that always steps over the requested method and produces the default result
        /// according to the method's return type.
        /// </summary>
        public static IMethodInvoker ReturnDefault => ReturnDefaultInvoker.ReturnDefault;

        /// <summary>
        /// Gets the method invoker that steps over any method that is not within the resolution scope of the current
        /// stack frame. That is, any method that is defined in an external assembly will be emulated as a step-over
        /// method with an unknown result. 
        /// </summary>
        public static ExternalMethodInvoker ReturnUnknownForExternal { get; } = new(ReturnUnknown, SignatureComparer.Default);
        
        /// <summary>
        /// Gets the method invoker that steps over any method by invoking it via System.Reflection. 
        /// </summary>
        public static ReflectionInvoker ReflectionInvoke => ReflectionInvoker.Instance;
        
        /// <summary>
        /// Gets the method invoker that forwards any method that is not within the resolution scope of the current
        /// stack frame to the provided method invoker, and otherwise leaves the invocation result inconclusive.
        /// </summary>
        /// <param name="baseInvoker">The invoker to use for producing a result when stepping over a method.</param>
        public static ExternalMethodInvoker HandleExternalWith(IMethodInvoker baseInvoker) => new(baseInvoker, SignatureComparer.Default);
        
        /// <summary>
        /// Creates a new method shim invoker that multiplexes a set of methods to individual handlers.  
        /// </summary>
        public static MethodShimInvoker CreateShim() => new();

        /// <summary>
        /// Chains the first method invoker with the provided method invoker in such a way that if the result of the
        /// first invoker is inconclusive, the second invoker will be used as a fallback invoker.  
        /// </summary>
        /// <param name="self">The first method invoker</param>
        /// <param name="other">The fallback method invoker</param>
        /// <returns>The constructed invoker chain.</returns>
        public static MethodInvokerChain WithFallback(this IMethodInvoker self, IMethodInvoker other)
        {
            if (self is not MethodInvokerChain chain)
            {
                chain = new MethodInvokerChain();
                chain.Invokers.Add(self);
            }

            chain.Invokers.Add(other);
            return chain;
        }
    }
}