using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace EntityFrameworkCore.ChangeTracking.UserDefinedTypes
{
    public class CollectionChangeListener : ChangeListener
    {
        private readonly Dictionary < object, ChangeListener > listeners = new Dictionary < object, ChangeListener > ( ReferenceEqualityComparer.Default );

        public CollectionChangeListener ( INotifyCollectionChanged collection ) : base ( collection ) { }

        public override void Subscribe ( )
        {
            if ( Instance is INotifyCollectionChanged collection )
                collection.CollectionChanged += OnCollectionChanged;

            if ( Instance is INotifyPropertyChanging notifyChanging )
                notifyChanging.PropertyChanging += RaisePropertyChanging;

            if ( Instance is INotifyPropertyChanged notifyChanged )
                notifyChanged.PropertyChanged += RaisePropertyChanged;

            if ( Instance is IEnumerable enumerable )
                foreach ( var item in enumerable )
                    ResetListener ( item );
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

                listeners.Add ( item, listener );

                listener.Subscribe ( );
            }
        }

        private void RemoveListener ( object? item )
        {
            if ( item == null )
                return;

            if ( listeners.TryGetValue ( item, out var listener ) )
            {
                listener.Dispose ( );

                listeners.Remove ( item );
            }
        }

        private void ClearListeners ( )
        {
            foreach ( var listener in listeners.Values )
                listener.Dispose ( );

            listeners.Clear ( );
        }
    }
}