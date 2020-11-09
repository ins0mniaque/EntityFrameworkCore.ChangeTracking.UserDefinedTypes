using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace EntityFrameworkCore.ChangeTracking.UserDefinedTypes
{
    public sealed class ReferenceEqualityComparer : IEqualityComparer < object >
    {
        public static ReferenceEqualityComparer Default { get; } = new ReferenceEqualityComparer ( );

        public new bool Equals      ( object x, object y ) => x == y;
        public     int  GetHashCode ( object value       ) => RuntimeHelpers.GetHashCode ( value );
    }
}