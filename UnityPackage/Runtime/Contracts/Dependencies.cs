using System;
using System.Collections.Generic;

namespace reromanlee.Mocker
{
    /// <summary>
    /// Registry of non-component constructor dependencies supplied to a composite at construction time.
    /// Only selected implementors query their required types; unselected implementors never trigger lookups.
    /// </summary>
    public sealed class Dependencies
    {
        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();

        /// <summary>
        /// Registers or replaces the instance resolved for <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Exact parameter type requested by implementor constructors.</typeparam>
        /// <param name="instance">Non-null instance to supply.</param>
        /// <returns>This registry for fluent chaining.</returns>
        public Dependencies Register<T>(T instance)
        {
            if (instance is null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            _instances[typeof(T)] = instance;

            return this;
        }

        /// <summary>
        /// Returns whether an instance is registered for <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Registered dependency type.</typeparam>
        /// <returns>True when a registration exists.</returns>
        public bool Contains<T>()
        {
            return _instances.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Attempts to fetch the registered instance for <typeparamref name="T"/> without throwing.
        /// </summary>
        /// <typeparam name="T">Registered dependency type.</typeparam>
        /// <param name="instance">Resolved instance when found; otherwise default.</param>
        /// <returns>True when a registration exists.</returns>
        public bool TryGet<T>(out T instance)
        {
            object stored;

            if (_instances.TryGetValue(typeof(T), out stored))
            {
                instance = (T)stored;

                return true;
            }

            instance = default(T);

            return false;
        }

        /// <summary>
        /// Resolves a required dependency for generated wiring code, throwing an actionable error
        /// naming the requesting implementor, component path, and missing type when absent.
        /// </summary>
        /// <typeparam name="T">Constructor parameter type requested by generated wiring.</typeparam>
        /// <param name="component">Component path being constructed.</param>
        /// <param name="implementor">Implementor type name requesting the dependency.</param>
        /// <returns>The registered instance.</returns>
        public T Require<T>(string component, string implementor)
        {
            object instance;

            if (_instances.TryGetValue(typeof(T), out instance))
            {
                return (T)instance;
            }

            throw new InvalidOperationException(
                $"{implementor} needs {typeof(T)} to provide {component}, but nothing was registered for it. " +
                $"Add .Register(...) for {typeof(T)} before constructing the composite.");
        }
    }
}
