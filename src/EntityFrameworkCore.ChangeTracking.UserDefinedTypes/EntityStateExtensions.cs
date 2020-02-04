using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EntityFrameworkCore.ChangeTracking.UserDefinedTypes
{
    public static class EntityStateExtensions
    {
        public static void UpdateModificationState ( this PropertyEntry property )
        {
            if ( property == null )
                throw new ArgumentNullException ( nameof ( property ) );

            if ( ! property.IsModified )
                return;

            var comparer    = property.Metadata.GetValueComparer ( );
            var isUnchanged = comparer?.Equals ( property.CurrentValue, property.OriginalValue ) ??
                                        Equals ( property.CurrentValue, property.OriginalValue );

            if ( isUnchanged )
                property.IsModified = false;
        }
    }
}