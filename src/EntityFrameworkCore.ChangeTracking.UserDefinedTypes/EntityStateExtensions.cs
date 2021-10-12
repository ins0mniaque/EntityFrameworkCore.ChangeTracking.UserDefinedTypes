using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EntityFrameworkCore.ChangeTracking.UserDefinedTypes
{
    public static class EntityStateExtensions
    {
        public static void UpdateModificationState ( this EntityEntry entity )
        {
            if ( entity == null )
                throw new ArgumentNullException ( nameof ( entity ) );

            foreach ( var property in entity.Properties )
                property.UpdateModificationState ( );
        }

        public static void UpdateModificationState ( this PropertyEntry property )
        {
            if ( property == null )
                throw new ArgumentNullException ( nameof ( property ) );

            var comparer    = property.Metadata.GetValueComparer ( );
            var isUnchanged = comparer?.Equals ( property.CurrentValue, property.OriginalValue ) ??
                                        Equals ( property.CurrentValue, property.OriginalValue );

            property.IsModified = ! isUnchanged;
        }

        public static void TryResetModificationState ( this PropertyEntry property )
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