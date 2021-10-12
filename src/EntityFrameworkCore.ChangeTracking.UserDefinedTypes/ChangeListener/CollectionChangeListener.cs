using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;

namespace EntityFrameworkCore.ChangeTracking.UserDefinedTypes
{
    public class CollectionChangeListener : ChangeListener
    {
        private readonly ConcurrentDictionary < object, ChangeListener > listeners = new ConcurrentDictionary < object, ChangeListener > ( ReferenceEqualityComparer.Default );

        public CollectionChangeListener ( object collection ) : base ( collection ) { }

        public override void Subscribe ( )
        {
            if ( Instance is INotifyCollectionChanged collection )
                collection.CollectionChanged += OnCollectionChanged;

            if ( Instance is INotifyPropertyChanging notifyChanging )
                notifyChanging.PropertyChanging += RaisePropertyChanging;

            if ( Instance is INotifyPropertyChanged notifyChanged )
                notifyChanged.PropertyChanged += RaisePropertyChanged;

            ResetListeners ( );
        }

        public override void Unsubscribe ( )
        {
            ClearListeners ( );

            if ( Instance is INotifyCollectionChanged collection )
                collection.CollectionChanged -= OnCollectionChanged;

            if ( Instance is INotifyPropertyChanging notifyChanging )
                notifyChanging.PropertyChanging -= RaisePropertyChanging;

            if ( Instance is INotifyPropertyChanged notifyChanged )
                notifyChanged.PropertyChanged -= RaisePropertyChanged;
        }

        private void OnCollectionChanged ( object sender, NotifyCollectionChangedEventArgs e )
        {
            if ( e.Action != NotifyCollectionChangedAction.Reset )
            {
                if ( e.OldItems != null )
                    foreach ( var item in e.OldItems )
                        RemoveListener ( item );

                if ( e.NewItems != null )
                    foreach ( var item in e.NewItems )
                        ResetListener ( item );
            }
            else
                ClearListeners ( );

            RaisePropertyChanged ( BuildPropertyPath ( PropertyName, "[]" ) );
        }

        private void ResetListener ( object? item )
        {
            if ( item == null )
                return;

            RemoveListener ( item );

            var listener = CreateListener ( "[]", item );

            if ( listener != null )
            {
                listener.PropertyChanging += RaisePropertyChanging;
                listener.PropertyChanged  += RaisePropertyChanged;

                listeners.AddOrUpdate ( item, listener, (_, oldListener) => { oldListener.Dispose ( ); return listener; } );

                listener.Subscribe ( );
            }
        }

        private void ResetListeners ( )
        {
            if ( ! ( Instance is IEnumerable enumerable ) )
                return;

            while ( true )
            {
                try
                {
                    foreach ( var item in enumerable )
                        ResetListener ( item );

                    return;
                }
                catch ( Exception exception )
                when  ( exception is InvalidOperationException ||
                        exception is IndexOutOfRangeException )
                {
                    Trace.TraceWarning ( "Collection was changed while enumerating; retrying..." );

                    ClearListeners ( );
                }
            }
        }

        private void RemoveListener ( object? item )
        {
            if ( item == null )
                return;

            if ( listeners.TryRemove ( item, out var listener ) )
                listener.Dispose ( );
        }

        private void ClearListeners ( )
        {
            foreach ( var listener in listeners.Values )
                listener.Dispose ( );

            listeners.Clear ( );
        }
    }
}