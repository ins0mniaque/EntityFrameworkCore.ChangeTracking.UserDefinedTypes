using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace EntityFrameworkCore.ChangeTracking.UserDefinedTypes
{
    public class PropertyChangeListener : ChangeListener
    {
        private readonly Dictionary < string, PropertyInfo   > properties;
        private readonly Dictionary < string, ChangeListener > listeners;

        public PropertyChangeListener ( INotifyPropertyChanged instance ) : this ( instance, _ => true ) { }
        public PropertyChangeListener ( INotifyPropertyChanged instance, Func < PropertyInfo, bool > filter ) : base ( instance )
        {
            properties = instance.GetType       ( )
                                 .GetProperties ( BindingFlags.Public | BindingFlags.Instance )
                                 .Where         ( property => property.CanRead )
                                 .Where         ( filter )
                                 .ToDictionary  ( property => property.Name );
            listeners  = new Dictionary < string, ChangeListener > ( properties.Count );
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
            if ( listeners.TryGetValue ( propertyName, out var listener ) )
            {
                listener.Dispose ( );

                listeners.Remove ( propertyName );
            }

            if ( ! properties.TryGetValue ( propertyName, out var property ) )
                return;

            var value = property.GetValue ( Instance, null );

            listener = CreateListener ( propertyName, value );

            if ( listener != null )
            {
                listener.PropertyChanging += RaisePropertyChanging;
                listener.PropertyChanged  += RaisePropertyChanged;

                listeners.Add ( propertyName, listener );

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