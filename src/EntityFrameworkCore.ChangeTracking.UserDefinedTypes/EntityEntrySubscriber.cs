using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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
        private readonly Dictionary < object, ChangeListener > listeners = new Dictionary < object, ChangeListener > ( );

        public override bool SnapshotAndSubscribe ( InternalEntityEntry entry )
        {
            if ( ! base.SnapshotAndSubscribe ( entry ) )
                return false;

            if ( ! ( entry.Entity is INotifyPropertyChanged notify ) )
                return true;

            var entityType = entry.EntityType;
            var properties = new HashSet < string > ( entityType.GetProperties ( ).Select ( property => property.Name ) );
            var listener   = new PropertyChangeListener ( notify, property => properties.Contains ( property.Name ) );
            var strategy   = entityType.GetChangeTrackingStrategy ( );

            if ( strategy != ChangeTrackingStrategy.ChangedNotifications )
            {
                listener.PropertyChanging += (o, e) =>
                {
                    var rootProperty = GetRootProperty ( e.PropertyName );
                    if ( ! properties.Contains ( rootProperty ) )
                        return;

                    if ( IsSubProperty ( e.PropertyName ) )
                        entry.HandleINotifyPropertyChanging ( notify, new PropertyChangingEventArgs ( rootProperty ) );
                };
            }

            listener.PropertyChanged += (o, e) =>
            {
                var rootProperty = GetRootProperty ( e.PropertyName );
                if ( ! properties.Contains ( rootProperty ) )
                    return;

                if ( IsSubProperty ( e.PropertyName ) )
                    entry.HandleINotifyPropertyChanged ( notify, new PropertyChangedEventArgs ( rootProperty ) );

                entry.ToEntityEntry ( ).Property ( rootProperty ).UpdateModificationState ( );
            };

            listeners.Add ( notify, listener );

            listener.Subscribe ( );

            return true;
        }

        public override void Unsubscribe ( InternalEntityEntry entry )
        {
            var strategy = entry.EntityType.GetChangeTrackingStrategy ( );

            if ( strategy != ChangeTrackingStrategy.Snapshot && listeners.TryGetValue ( entry.Entity, out var listener ) )
            {
                listeners.Remove ( listener );

                listener.Dispose ( );
            }

            base.Unsubscribe ( entry );
        }

        private static bool    IsSubProperty   ( string propertyPath ) => propertyPath?.IndexOf ( '.', StringComparison.Ordinal ) >= 0 ||
                                                                          propertyPath?.IndexOf ( '[', StringComparison.Ordinal ) >= 0;
        private static string? GetRootProperty ( string propertyPath ) => propertyPath?.Split ( '.', '[' ) [ 0 ];
    }
}