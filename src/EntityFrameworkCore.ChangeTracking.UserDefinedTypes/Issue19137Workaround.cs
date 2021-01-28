using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ChangeTracking.UserDefinedTypes
{
    /// <summary>
    /// Issue: https://github.com/dotnet/efcore/issues/19137
    /// Fix: https://github.com/dotnet/efcore/commit/9041838f8c6c1bd32bf00ae6f31a6220db8556f5
    /// Fix scheduled for 3.1.3: https://github.com/dotnet/efcore/milestone/80
    /// ETA for 3.1.3: https://github.com/dotnet/efcore/issues/18982#issuecomment-586526058
    /// </summary>
    public static class Issue19137Workaround
    {
        public static void OnNavigationChanged ( NavigationEntry navigation )
        {
            if ( navigation.IsModified && navigation.CurrentValue == null && navigation.Metadata is INavigation metadata )
            {
                foreach ( var property in metadata.ForeignKey.Properties )
                {
                    var foreignKey = navigation.EntityEntry.Property ( property.Name );
                    if ( foreignKey.Metadata.IsNullable && foreignKey.CurrentValue != null )
                        foreignKey.CurrentValue = null;
                }
            }
        }
    }
}