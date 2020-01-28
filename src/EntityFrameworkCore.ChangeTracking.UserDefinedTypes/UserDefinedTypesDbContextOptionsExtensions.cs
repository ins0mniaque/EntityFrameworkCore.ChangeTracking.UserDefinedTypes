using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;

using EntityFrameworkCore.ChangeTracking.UserDefinedTypes;

namespace Microsoft.EntityFrameworkCore
{
    public static class UserDefinedTypesDbContextOptionsExtensions
    {
        public static DbContextOptionsBuilder EnableUserDefinedTypesNotificationsSupport ( this DbContextOptionsBuilder builder )
        {
            return builder.ReplaceService < IInternalEntityEntrySubscriber, EntityEntrySubscriber > ( );
        }

        public static DbContextOptionsBuilder < TContext > EnableUserDefinedTypesNotificationsSupport < TContext > ( this DbContextOptionsBuilder < TContext > builder ) where TContext : DbContext
        {
            return builder.ReplaceService < IInternalEntityEntrySubscriber, EntityEntrySubscriber > ( );
        }
    }
}