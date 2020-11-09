using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace EntityFrameworkCore.ChangeTracking.UserDefinedTypes
{
    public class PropertyChangeListener : ChangeListener
    {
        private readonly ConcurrentDictionary < string, ChangeListener > listeners = new ConcurrentDictionary < string, ChangeListener > ( );
        private readonly Dictionary           < string, PropertyInfo   > properties;

        public PropertyChangeListener ( INotifyPropertyChanged instance ) : base ( instance )
        {
            properties = TypePropertyCache.For ( instance );
        }

        public PropertyChangeListener ( INotifyPropertyChanged instance, Func < PropertyInfo, bool > filter ) : base ( instance )
        {
            properties = TypePropertyCache.For ( instance, filter );
        }

        public override void Subscribe ( )
        {
            if ( Instance is INotifyPropertyChanging notifyChanging )
                notifyChanging.PropertyChanging += RaisePropertyChanging;

            if ( Instance is INotifyPropertyChanged notifyChanged )
                notifyChanged.PropertyChanged += ResetAndRaisePropertyChanged;

            foreach ( var property in properties )
                ResetListener ( property.Value.Name );
        }

        public override void Unsubscribe ( )
        {
            if ( Instance is INotifyPropertyChanging notifyChanging )
                notifyChanging.PropertyChanging -= RaisePropertyChanging;

            if ( Instance is INotifyPropertyChanged notifyChanged )
                notifyChanged.PropertyChanged -= ResetAndRaisePropertyChanged;

            foreach ( var listener in listeners.Values )
                listener?.Dispose ( );

            listeners.Clear ( );
        }

        private void ResetListener ( string propertyName )
        {
            if ( listeners.TryRemove ( propertyName, out var listener ) )
                listener.Dispose ( );

            if ( ! properties.TryGetValue ( propertyName, out var property ) )
                return;

            var value = property.GetValue ( Instance, null );

            listener = CreateListener ( propertyName, value );

            if ( listener != null )
            {
                listener.PropertyChanging += RaisePropertyChanging;
                listener.PropertyChanged  += RaisePropertyChanged;

                listeners.AddOrUpdate ( propertyName, listener, (_, oldListener) => { oldListener.Dispose ( ); return listener; } );

                listener.Subscribe ( );
            }
        }

        private void ResetAndRaisePropertyChanged ( object sender, PropertyChangedEventArgs e )
        {
            ResetListener ( e.PropertyName );

            RaisePropertyChanged ( sender, e );
        }
    }
}