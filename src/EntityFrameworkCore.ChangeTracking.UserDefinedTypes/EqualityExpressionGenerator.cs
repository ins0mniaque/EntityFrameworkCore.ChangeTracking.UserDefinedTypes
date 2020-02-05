using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EntityFrameworkCore.ChangeTracking.UserDefinedTypes
{
    using static System.Linq.Expressions.Expression;
    using static System.Reflection.BindingFlags;

    public class EqualityExpressionGenerator
    {
        public static IEnumerable < MemberInfo > ByFields     ( Type type ) => type.GetFields     ( Instance | Public | NonPublic );
        public static IEnumerable < MemberInfo > ByProperties ( Type type ) => type.GetProperties ( Instance | Public | NonPublic );
        public static bool DoesNotOverridesEqualsMethod       ( Type type ) => type.GetMethod     ( nameof ( Equals ),
                                                                                                    Public | Instance,
                                                                                                    null,
                                                                                                    new [ ] { typeof ( object ) },
                                                                                                    null )
                                                                                   .DeclaringType == typeof ( object );

        private readonly Func < Type, IEnumerable < MemberInfo > > memberSelector;
        private readonly Func < Type, bool > generateEqualityFor;

        public EqualityExpressionGenerator ( ) : this ( DoesNotOverridesEqualsMethod ) { }
        public EqualityExpressionGenerator ( Func < Type, bool > generateEqualityFor ) : this ( ByFields, generateEqualityFor ) { }
        public EqualityExpressionGenerator ( Func < Type, IEnumerable < MemberInfo > > memberSelector, Func < Type, bool > generateEqualityFor )
        {
            this.memberSelector      = memberSelector;
            this.generateEqualityFor = generateEqualityFor;
        }

        public  Expression < Func < T,      T,      bool > > GenerateEquals < T > ( )           => GenerateEquals < T > ( typeof ( T ) );
        public  Expression < Func < object, object, bool > > GenerateEquals       ( Type type ) => GenerateEquals < object > ( type );
        private Expression < Func < T,      T,      bool > > GenerateEquals < T > ( Type type )
        {
            var left  = Parameter ( typeof ( T ), "left"  );
            var right = Parameter ( typeof ( T ), "right" );

            return Lambda < Func < T, T, bool > > ( GenerateEquals ( type, left, right ), left, right );
        }

        private Expression GenerateEquals ( Type type, Expression left, Expression right )
        {
            var typedLeft  = type != left .Type ? Convert ( left,  type ) : left;
            var typedRight = type != right.Type ? Convert ( right, type ) : right;

            var equals = memberSelector ( type ).Select    ( member => MakeEqualsExpression ( member, typedLeft, typedRight ) )
                                                .Aggregate ( (Expression) Constant ( true ), AndAlso );

            return Condition ( AndAlso ( TypeIs ( left, type ), TypeIs ( right, type ) ),
                               equals,
                               Equal ( left, right ) );
        }

        private Expression GenerateSequenceEquals ( Type type, Expression left, Expression right )
        {
            if ( IsDictionaryType ( type ) )
            {
                var (keyType, valueType) = GetKeyValueType ( type );

                return Call ( null, Dictionary.EqualsMethod, left, right, Constant ( GetComparer ( keyType ) ), Constant ( GetComparer ( valueType ) ) );
            }

            var itemType = GetItemType ( type );

            if ( IsSetType ( type ) )
                return Call ( null, Unordered.EqualsMethod, left, right, Constant ( GetComparer ( itemType ) ) );

            return Call ( null, Ordered.EqualsMethod, left, right, Constant ( GetComparer ( itemType ) ) );
        }

        private static readonly MethodInfo objectEqualsMethod = new Func < object, object, bool > ( Equals ).Method;

        private Expression MakeEqualsExpression ( MemberInfo member, Expression left, Expression right )
        {
            left  = MakeMemberAccess ( left,  member );
            right = MakeMemberAccess ( right, member );

            var memberType = left.Type;

            if ( left.Type.IsValueType )
            {
                var boxedLeft  = Convert ( left,  typeof ( object ) );
                var boxedRight = Convert ( right, typeof ( object ) );

                return Call ( objectEqualsMethod, boxedLeft, boxedRight );
            }

            return IsSequenceType      ( memberType ) ? GenerateSequenceEquals ( memberType, left, right ) :
                   generateEqualityFor ( memberType ) ? GenerateEquals         ( memberType, left, right ) :
                                                        Call ( objectEqualsMethod, left, right );
        }

        public  Expression < Func < T,      int > > GenerateGetHashCode < T > ( )           => GenerateGetHashCode < T > ( typeof ( T ) );
        public  Expression < Func < object, int > > GenerateGetHashCode       ( Type type ) => GenerateGetHashCode < object > ( type );
        private Expression < Func < T,      int > > GenerateGetHashCode < T > ( Type type )
        {
            var obj = Parameter ( typeof ( T ), "obj" );

            return Lambda < Func < T, int > > ( GenerateGetHashCode ( type, obj ), obj );
        }

        private Expression GenerateGetHashCode ( Type memberType, Expression expression )
        {
            expression = memberType != expression.Type ? Convert ( expression, memberType ) : expression;

            return memberSelector ( memberType ).Select    ( member => MakeGetHashCodeExpression(member, expression))
                                                .Aggregate ( (Expression) Constant ( 29 ),
                                                             (leftHash, rightHash) => ExclusiveOr ( Multiply ( leftHash, Constant ( 486187739 ) ), rightHash ) );
        }

        private Expression GenerateSequenceGetHashCode ( Type type, Expression expression )
        {
            if ( IsDictionaryType ( type ) )
            {
                var (keyType, valueType) = GetKeyValueType ( type );

                return Call ( null, Dictionary.GetHashCodeMethod, expression, Constant ( GetComparer ( keyType ) ), Constant ( GetComparer ( valueType ) ) );
            }

            var itemType = GetItemType ( type );

            if ( IsSetType ( type ) )
                return Call ( null, Unordered.GetHashCodeMethod, expression, Constant ( GetComparer ( itemType ) ) );

            return Call ( null, Ordered.GetHashCodeMethod, expression, Constant ( GetComparer ( itemType ) ) );
        }

        private Expression MakeGetHashCodeExpression ( MemberInfo member, Expression expression )
        {
            expression = MakeMemberAccess ( expression, member );

            var memberType  = expression.Type;
            var getHashCode = IsSequenceType      ( memberType ) ? GenerateSequenceGetHashCode ( memberType, expression ) :
                              generateEqualityFor ( memberType ) ? GenerateGetHashCode         ( memberType, expression ) :
                                                                   Call ( expression, nameof ( GetHashCode ), Type.EmptyTypes );

            return Condition ( ReferenceEqual ( Constant ( null ), Convert ( expression, typeof ( object ) ) ),
                               Constant ( 0 ),
                               getHashCode );
        }

        private Dictionary < Type, Comparer > comparers = new Dictionary < Type, Comparer > ( );

        private Comparer GetComparer ( Type type )
        {
            if ( ! comparers.TryGetValue ( type, out var comparer ) )
                comparers [ type ] = comparer = new Comparer ( this, type );

            return comparer;
        }

        private class Comparer : IEqualityComparer < object >
        {
            private readonly Func < object, object, bool > equals;
            private readonly Func < object, int >          getHashCode;

            public Comparer ( EqualityExpressionGenerator generator, Type type )
            {
                equals      = generator.GenerateEquals      ( type ).Compile ( );
                getHashCode = generator.GenerateGetHashCode ( type ).Compile ( );
            }

            public bool Equals      ( object left, object right ) => equals ( left, right );
            public int  GetHashCode ( object obj )                => getHashCode ( obj );
        }

        private static class Ordered
        {
            public static MethodInfo EqualsMethod      = ( (Func < IEnumerable, IEnumerable, IEqualityComparer < object >, bool >) Equals      ).Method;
            public static MethodInfo GetHashCodeMethod = ( (Func < IEnumerable,              IEqualityComparer < object >, int  >) GetHashCode ).Method;

            public static bool Equals ( IEnumerable left, IEnumerable right, IEqualityComparer < object > comparer )
            {
                if ( ReferenceEquals ( left, right ) ) return true;
                if ( ReferenceEquals ( null, left  ) ) return false;
                if ( ReferenceEquals ( null, right ) ) return false;

                return left.Cast < object > ( ).SequenceEqual ( right.Cast < object > ( ), comparer );
            }

            public static int GetHashCode ( IEnumerable enumerable, IEqualityComparer < object > comparer )
            {
                unchecked
                {
                    // NOTE: Ensure that different order produces different hash codes
                    return enumerable?.Cast < object > ( ).Aggregate ( 17, ( current, item ) => ( current * 486187739 ) ^ comparer.GetHashCode ( item ) ) ?? 0;
                }
            }
        }

        private static class Unordered
        {
            public static MethodInfo EqualsMethod      = ( (Func < IEnumerable, IEnumerable, IEqualityComparer < object >, bool >) Equals      ).Method;
            public static MethodInfo GetHashCodeMethod = ( (Func < IEnumerable,              IEqualityComparer < object >, int  >) GetHashCode ).Method;

            public static bool Equals ( IEnumerable left, IEnumerable right, IEqualityComparer < object > comparer )
            {
                if ( ReferenceEquals ( left, right ) ) return true;
                if ( ReferenceEquals ( null, left  ) ) return false;
                if ( ReferenceEquals ( null, right ) ) return false;

                return SetEquals ( left.Cast < object > ( ), right.Cast < object > ( ), comparer );
            }

            public static int GetHashCode ( IEnumerable enumerable, IEqualityComparer < object > comparer )
            {
                unchecked
                {
                    // NOTE: The sequence does not define order, so just XOR all elements
                    return enumerable?.Cast < object > ( ).Aggregate ( 17, (current, item) => current ^ comparer.GetHashCode ( item ) ) ?? 0;
                }
            }
        }

        // TODO: Add support for read-only dictionaries
        private static class Dictionary
        {
            public static MethodInfo EqualsMethod      = ( (Func < IDictionary, IDictionary, IEqualityComparer < object >, IEqualityComparer < object >, bool >) Equals      ).Method;
            public static MethodInfo GetHashCodeMethod = ( (Func < IDictionary,              IEqualityComparer < object >, IEqualityComparer < object >, int  >) GetHashCode ).Method;

            public static bool Equals ( IDictionary left, IDictionary right, IEqualityComparer < object > keyComparer, IEqualityComparer < object > valueComparer )
            {
                if ( ReferenceEquals ( left, right ) ) return true;
                if ( ReferenceEquals ( null, left  ) ) return false;
                if ( ReferenceEquals ( null, right ) ) return false;

                var keys = left.Keys.Cast < object > ( );
                if ( SetEquals ( keys, right.Keys.Cast < object > ( ), keyComparer ) )
                    return keys.Select ( key => left [ key ] ).SequenceEqual ( keys.Select ( key => right [ key ] ), valueComparer );

                return false;
            }

            public static int GetHashCode ( IDictionary dictionary, IEqualityComparer < object > keyComparer, IEqualityComparer < object > valueComparer )
            {
                if ( ReferenceEquals ( null, dictionary ) )
                    return 0;

                unchecked
                {
                    var code = 17;
                    foreach ( DictionaryEntry entry in dictionary )
                        code = code ^ keyComparer.GetHashCode ( entry.Key ) ^ ( 486187739 * valueComparer.GetHashCode ( entry.Value ) );

                    return code;
                }
            }
        }

        private static bool SetEquals < T > ( IEnumerable < T > left, IEnumerable < T > right, IEqualityComparer < T > comparer )
        {
            var counters = new Dictionary < T, int > ( comparer );

            foreach ( var item in left )
            {
                if ( counters.ContainsKey ( item ) )
                    counters [ item ]++;
                else
                    counters.Add ( item, 1 );
            }

            foreach ( var item in right )
            {
                if ( counters.ContainsKey ( item ) )
                    counters [ item ]--;
                else
                    return false;
            }

            return counters.Values.All ( counter => counter == 0 );
        }

        private static bool IsSequenceType ( Type type ) => typeof ( IEnumerable ).IsAssignableFrom ( type ) && type != typeof ( string );

        // TODO: Add support for read-only dictionaries
        private static bool IsDictionaryType ( Type type )
        {
            if ( typeof ( IDictionary ).IsAssignableFrom ( type ) )
                return true;

            return false;
        }

        private static bool IsSetType ( Type type )
        {
            return type.IsGenericType && typeof ( ISet < > ).IsAssignableFrom ( type.GetGenericTypeDefinition ( ) );
        }

        private static Type GetItemType ( Type type )
        {
            return type.GetInterfaces ( )
                       .Where  ( @interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition ( ) == typeof ( IEnumerable < > ) )
                       .Select ( @interface => @interface.GetGenericArguments ( ) [ 0 ] )
                       .DefaultIfEmpty ( typeof ( object ) )
                       .First ( );
        }

        private static (Type, Type) GetKeyValueType ( Type type )
        {
            return type.GetInterfaces ( )
                       .Where ( @interface  => @interface.IsGenericType &&
                                               ( @interface.GetGenericTypeDefinition ( ) == typeof ( IDictionary < , > ) ||
                                                 @interface.GetGenericTypeDefinition ( ) == typeof ( IReadOnlyDictionary < , > ) ) )
                       .Select ( @interface => @interface.GetGenericArguments ( ) )
                       .Select ( arguments  => ( arguments [ 0 ], arguments [ 1 ] ) )
                       .DefaultIfEmpty ( ( typeof ( object ), typeof ( object ) ) )
                       .First ( );
        }
    }
}