﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNet.SignalR;
using SignalR.EventAggregatorProxy.Constraint;
using SignalR.EventAggregatorProxy.Extensions;

namespace SignalR.EventAggregatorProxy.Event
{
    public class TypeFinder<TEvent> : ITypeFinder
    {
        private readonly IAssemblyLocator assemblyLocator;
        private IDictionary<string, Type> eventTypes;
        private IDictionary<string, Type> types;
        private IDictionary<Type, IEnumerable<Type>> constraintHandlerTypes;
        private IDictionary<Type, IEnumerable<Type>> lookup;
        
        public TypeFinder()
        {
            assemblyLocator = GlobalHost.DependencyResolver.Resolve<IAssemblyLocator>();
            types = new Dictionary<string, Type>();

            InitEventTypes();
            InitConstraintHandlerTypes();
        }

        private void InitEventTypes()
        {
            var type = typeof (TEvent);
            eventTypes = assemblyLocator.GetAssemblies()
                                   .SelectMany(GetTypesSafely)
                                   .Where(t => !t.IsAbstract && type.IsAssignableFrom(t))
                                   .ToDictionary(t => t.GetFullNameWihoutGenerics(), t => t);
        }

        private void InitConstraintHandlerTypes()
        {
            var lookupType = typeof(IEventConstraintHandler<>);
            var predicate = new Func<Type, bool>(t => t.IsGenericType && t.GetGenericTypeDefinition() == lookupType);

            lookup = assemblyLocator
                .GetAssemblies()
                .SelectMany(GetTypesSafely)
                .Where(t => t.GetInterfaces().Any(predicate))
                .Select(t => new { Handler = t, Type = t.GetInterfaces().First(predicate).GetGenericArguments()[0] })
                .GroupBy(t => t.Type)
                .ToDictionary(g => g.Key, g => g.Select(t => t.Handler));

            constraintHandlerTypes = new Dictionary<Type, IEnumerable<Type>>();
        }

        private IEnumerable<Type> GetTypesSafely(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch
            {
                return new List<Type>();
            }
        } 

        public IEnumerable<Type> ListEventTypes()
        {
            return eventTypes.Values;
        }

        public Type GetEventType(string typeName)
        {
            return eventTypes[typeName];
        }

        public Type GetType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type == null)
            {
                if (!types.ContainsKey(typeName))
                {
                    types[typeName] = assemblyLocator.GetAssemblies().Select(a => a.GetType(typeName)).Single(t => t != null);
                }
                type = types[typeName];
            }

            return type;
        }

        public IEnumerable<Type> GetConstraintHandlerTypes(Type type)
        {
            if (!constraintHandlerTypes.ContainsKey(type))
            {
                var handlers = lookup
                    .Where(kvp => kvp.Key.IsAssignableFrom(type))
                    .SelectMany(kvp => kvp.Value)
                    .ToList();

                constraintHandlerTypes[type] = handlers;
            }
            return constraintHandlerTypes[type];
        }
    }
}
