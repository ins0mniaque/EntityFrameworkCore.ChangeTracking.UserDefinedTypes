using System;
using System.Collections.Concurrent;
using System.ComponentModel;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.ChangeTracking.UserDefinedTypes
{
    /// <summary>
    ///     <para>
    ///         Adds user-defined types change notifications support to InternalEntityEntrySubscriber
    ///     </para>
    ///     <para>
    ///         This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///         the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///         any release. You should only use it directly in your code with extreme caution and knowing that
    ///         doing so can result in application failures when updating to a new Entity Framework Core release.
    ///     </para>
    ///     <para>
    ///         The service lifetime is <see cref="ServiceLifetime.Singleton" />. This means a single instance
    ///         is used by many <see cref="DbContext" /> instances. The implementation must be thread-safe.
    ///         This service cannot depend on services registered as <see cref="ServiceLifetime.Scoped" />.
    ///     </para>
    /// </summary>
    public class EntityEntrySubscriber : InternalEntityEntrySubscriber
    {
        private readonly ConcurrentDictionary < object, ChangeListener > listeners = new ConcurrentDictionary < object, ChangeListener > ( ReferenceEqualityComparer.Default );

        public override bool SnapshotAndSubscribe ( InternalEntityEntry entry )
        {
            if ( ! base.SnapshotAndSubscribe ( entry ) )
                return false;

            if ( ! ( entry.Entity is INotifyPropertyChanged notify ) )
                return true;

            var entityType = entry.EntityType;
            var listener   = new PropertyChangeListener ( notify, property => entityType.FindProperty ( property.Name ) != null );
            var strategy   = entityType.GetChangeTrackingStrategy ( );

            if ( strategy != ChangeTrackingStrategy.ChangedNotifications )
            {
                listener.PropertyChanging += (o, e) =>
                {
                    var rootProperty  = GetRootProperty ( e.PropertyName );
                    var isUDTProperty = entityType.FindProperty ( rootProperty ) != null;

                    if ( isUDTProperty && IsSubProperty ( e.PropertyName ) )
                        entry.HandleINotifyPropertyChanging ( notify, new PropertyChangingEventArgs ( rootProperty ) );
                };
            }

            listener.PropertyChanged += (o, e) =>
            {
                var rootProperty  = GetRootProperty ( e.PropertyName );
                var isUDTProperty = entityType.FindProperty ( rootProperty ) != null;

                if ( isUDTProperty && IsSubProperty ( e.PropertyName ) )
                    entry.HandleINotifyPropertyChanged ( notify, new PropertyChangedEventArgs ( rootProperty ) );

                var entityEntry = entry.ToEntityEntry ( );

                if ( ! isUDTProperty )
                {
                    var navigation = entityType.FindNavigation ( rootProperty );
                    if ( navigation != null )
                    {
                        Issue19137Workaround.OnNavigationChanged ( entityEntry.Navigation ( navigation.Name ) );

                        foreach ( var foreignKey in navigation.ForeignKey.Properties )
                            entityEntry.Property ( foreignKey.Name ).UpdateModificationState ( );
                    }
                }
                else
                    entityEntry.Property ( rootProperty ).UpdateModificationState ( );
            };

            listeners.AddOrUpdate ( notify, listener, (_, oldListener) => { oldListener.Dispose ( ); return listener; } );

            listener.Subscribe ( );

            return true;
        }

        public override void Unsubscribe ( InternalEntityEntry entry )
        {
            var strategy = entry.EntityType.GetChangeTrackingStrategy ( );

            if ( strategy != ChangeTrackingStrategy.Snapshot && listeners.TryRemove ( entry.Entity, out var listener ) )
                listener.Dispose ( );

            base.Unsubscribe ( entry );
        }

        private static bool    IsSubProperty   ( string propertyPath ) => propertyPath?.IndexOf ( '.', StringComparison.Ordinal ) >= 0 ||
                                                                          propertyPath?.IndexOf ( '[', StringComparison.Ordinal ) >= 0;
        private static string? GetRootProperty ( string propertyPath ) => propertyPath?.Split ( '.', '[' ) [ 0 ];
    }
}