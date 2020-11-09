using System;
using System.Collections.Specialized;
using System.ComponentModel;

namespace EntityFrameworkCore.ChangeTracking.UserDefinedTypes
{
    public abstract class ChangeListener : INotifyPropertyChanging, INotifyPropertyChanged, IDisposable
    {
        public static ChangeListener? Create ( object? value ) => value switch
        {
            INotifyCollectionChanged collection => new CollectionChangeListener ( collection ),
            INotifyPropertyChanged   notify     => new PropertyChangeListener   ( notify     ),
            _                                   => null
        };

        private ChangeListener? parent;

        protected ChangeListener ( object instance )
        {
            if ( instance == null )
                throw new ArgumentNullException ( nameof ( instance ) );

            Instance = instance;
        }

        protected object  Instance     { get; }
        protected string? PropertyName { get; private set; }

        public abstract void Subscribe   ( );
        public abstract void Unsubscribe ( );

        protected ChangeListener? CreateListener ( string propertyName, object? value )
        {
            if ( IsListenedByParent ( value ) )
                return null;

            var listener = Create ( value );

            if ( listener != null )
            {
                listener.parent       = this;
                listener.PropertyName = propertyName;
            }

            return listener;
        }

        private bool IsListenedByParent ( object? value )
        {
            var current = (ChangeListener?) this;

            while ( current != null )
            {
                if ( ReferenceEquals ( current.Instance, value ) )
                    return true;

                current = current.parent;
            }

            return false;
        }

        public event PropertyChangingEventHandler? PropertyChanging;
        public event PropertyChangedEventHandler?  PropertyChanged;

        protected virtual void RaisePropertyChanging ( string propertyPath )
        {
            PropertyChanging?.Invoke ( Instance, new PropertyChangingEventArgs ( propertyPath ) );
        }

        protected virtual void RaisePropertyChanged ( string propertyPath )
        {
            PropertyChanged?.Invoke ( Instance, new PropertyChangedEventArgs ( propertyPath ) );
        }

        protected void RaisePropertyChanging ( object sender, PropertyChangingEventArgs e ) => RaisePropertyChanging ( BuildPropertyPath ( PropertyName, e.PropertyName ) );
        protected void RaisePropertyChanged  ( object sender, PropertyChangedEventArgs  e ) => RaisePropertyChanged  ( BuildPropertyPath ( PropertyName, e.PropertyName ) );

        protected static string? BuildPropertyPath ( string? propertyName, string? propertyPath )
        {
            if ( string.IsNullOrEmpty ( propertyName ) )
                return propertyPath;

            var isIndexer = propertyPath != null    &&
                            propertyPath.Length > 0 &&
                            propertyPath [ 0 ] == '[';
            if ( isIndexer )
                return propertyName + propertyPath;

            return propertyName + '.' + propertyPath;
        }

        protected virtual void Dispose ( bool disposing )
        {
            if ( disposing )
            {
                Unsubscribe ( );

                parent = null;

                PropertyChanging = null;
                PropertyChanged  = null;
            }
        }

        public void Dispose ( )
        {
            Dispose ( true );
            GC.SuppressFinalize ( this );
        }

        ~ChangeListener ( ) => Dispose ( false );
    }
}