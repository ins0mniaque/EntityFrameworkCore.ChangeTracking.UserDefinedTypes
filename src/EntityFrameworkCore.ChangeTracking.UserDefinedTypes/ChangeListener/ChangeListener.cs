using System;
using System.Collections.Specialized;
using System.ComponentModel;

namespace EntityFrameworkCore.ChangeTracking.UserDefinedTypes
{
    public abstract class ChangeListener : INotifyPropertyChanging, INotifyPropertyChanged, IDisposable
    {
        public static ChangeListener? Create ( object? instance ) => instance switch
        {
            INotifyCollectionChanged collection => new CollectionChangeListener ( collection ),
            INotifyPropertyChanged   notify     => new PropertyChangeListener   ( notify     ),
            _                                   => null
        };

        public static ChangeListener? Create ( object? instance, Func < ChangeListener, bool > filter ) => instance switch
        {
            INotifyCollectionChanged collection => new CollectionChangeListener ( collection ) { filter = filter },
            INotifyPropertyChanged   notify     => new PropertyChangeListener   ( notify     ) { filter = filter },
            _                                   => null
        };

        private Func < ChangeListener, bool >? filter;

        protected ChangeListener ( object instance )
        {
            if ( instance == null )
                throw new ArgumentNullException ( nameof ( instance ) );

            Instance = instance;
        }

        public ChangeListener? Parent       { get; private set; }
        public int             Depth        { get; private set; }
        public object          Instance     { get; }
        public string?         PropertyName { get; private set; }

        public abstract void Subscribe   ( );
        public abstract void Unsubscribe ( );

        protected ChangeListener? CreateListener ( string propertyName, object? instance )
        {
            if ( IsListenedByParent ( instance ) )
                return null;

            var listener = filter != null ? Create ( instance, filter ) :
                                            Create ( instance );

            if ( listener != null )
            {
                listener.Parent       = this;
                listener.Depth        = Depth + 1;
                listener.PropertyName = propertyName;

                if ( filter != null && ! filter ( listener ) )
                    listener = null;
            }

            return listener;
        }

        private bool IsListenedByParent ( object? instance )
        {
            var current = (ChangeListener?) this;

            while ( current != null )
            {
                if ( ReferenceEquals ( current.Instance, instance ) )
                    return true;

                current = current.Parent;
            }

            return false;
        }

        public event PropertyChangingEventHandler? PropertyChanging;
        public event PropertyChangedEventHandler?  PropertyChanged;

        protected virtual void RaisePropertyChanging ( string propertyPath )
        {
            PropertyChanging?.Invoke ( this, new PropertyChangingEventArgs ( propertyPath ) );
        }

        protected virtual void RaisePropertyChanged ( string propertyPath )
        {
            PropertyChanged?.Invoke ( this, new PropertyChangedEventArgs ( propertyPath ) );
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

                Parent = null;

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