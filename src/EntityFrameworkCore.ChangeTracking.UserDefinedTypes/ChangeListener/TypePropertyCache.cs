using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EntityFrameworkCore.ChangeTracking.UserDefinedTypes
{
    public static class TypePropertyCache
    {
        private static readonly Dictionary < Type, Dictionary < string, PropertyInfo > > cache = new Dictionary < Type, Dictionary < string, PropertyInfo > > ( );

        public static Dictionary < string, PropertyInfo > For ( object instance ) => For ( instance.GetType ( ) );
        public static Dictionary < string, PropertyInfo > For ( Type   type     )
        {
            if ( ! cache.TryGetValue ( type, out var properties ) )
                cache [ type ] = properties = GetProperties ( type );

            return properties;
        }

        public static Dictionary < string, PropertyInfo > For ( object instance, Func < PropertyInfo, bool > filter ) => For ( instance.GetType ( ), filter );
        public static Dictionary < string, PropertyInfo > For ( Type   type,     Func < PropertyInfo, bool > filter )
        {
            return For ( type ).Where        ( entry => filter ( entry.Value ) )
                               .ToDictionary ( entry => entry.Key,
                                               entry => entry.Value );
        }

        public static void Clear ( ) => cache.Clear ( );

        private static Dictionary < string, PropertyInfo > GetProperties ( Type type )
        {
            return type.GetProperties ( BindingFlags.Public | BindingFlags.Instance )
                       .Where         ( property => property.CanRead )
                       .ToDictionary  ( property => property.Name );
        }
    }
}