using System;
using System.Collections.Generic;

namespace reromanlee.Mocker
{
    /// <summary>
    /// The things an implementor needs in its constructor that are not components: settings, configs,
    /// anything the project owns. Fill it before constructing a composite. Only the implementors actually
    /// selected are ever asked for theirs, so an unselected vendor's settings are never required.
    /// </summary>
    public sealed class Dependencies
    {
        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();

        /// <summary>
        /// Makes an instance available to any implementor that asks for this type in its constructor.
        /// </summary>
        /// <typeparam name="T">Type implementors will ask for.</typeparam>
        /// <param name="instance">The instance to hand them.</param>
        /// <returns>This, so registrations can be chained.</returns>
        public Dependencies Register<T>(T instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            _instances[typeof(T)] = instance;

            return this;
        }

        /// <summary>
        /// Whether something was registered for this type.
        /// </summary>
        /// <typeparam name="T">Type to look for.</typeparam>
        /// <returns>True when it is available.</returns>
        public bool Contains<T>()
        {
            return _instances.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Called by generated code to fill a constructor parameter. Fails with the component and the
        /// implementor that wanted it, so a missing registration says what to add and why.
        /// </summary>
        /// <typeparam name="T">Type the constructor asked for.</typeparam>
        /// <param name="component">Component being built.</param>
        /// <param name="implementor">Implementor whose constructor asked for it.</param>
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
