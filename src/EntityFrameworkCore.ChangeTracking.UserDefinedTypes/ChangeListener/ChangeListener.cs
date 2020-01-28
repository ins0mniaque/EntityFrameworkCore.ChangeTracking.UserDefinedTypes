using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EntityFrameworkCore.ChangeTracking.UserDefinedTypes
{
    public abstract class ChangeListener : INotifyPropertyChanging, INotifyPropertyChanged, IDisposable
    {
        public static ChangeListener? Create ( object value ) => CreateIf ( value, _ => true );

        private static ChangeListener? CreateIf ( object value, Func < object, bool > predicate )
        {
            if ( value is INotifyCollectionChanged collection )
                return predicate ( collection ) ? new CollectionChangeListener ( collection ) : null;

            if ( value is INotifyPropertyChanged notify )
                return predicate ( notify ) ? new PropertyChangeListener ( notify ) : null;

            return null;
        }

        private ChangeListener      root;
        private HashSet < object >? visited;

        protected ChangeListener ( object instance )
        {
            if ( instance == null )
                throw new ArgumentNullException ( nameof ( instance ) );

            Instance = instance;
            root     = this;
        }

        protected object  Instance     { get; }
        protected string? PropertyName { get; private set; }

        public abstract void Subscribe   ( );
        public abstract void Unsubscribe ( );

        private HashSet < object > EnsureVisited ( )
        {
            return visited ?? ( visited = new HashSet < object > ( ReferenceEqualityComparer.Default ) );
        }

        protected ChangeListener? CreateListener ( string propertyName, object? value )
        {
            var listener = CreateIf ( value, root.EnsureVisited ( ).Add );

            if ( listener != null )
            {
                listener.root         = root;
                listener.PropertyName = propertyName;
            }

            return listener;
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

                root.visited?.Remove ( Instance );

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

        protected sealed class ReferenceEqualityComparer : IEqualityComparer < object >
        {
            public static ReferenceEqualityComparer Default { get; } = new ReferenceEqualityComparer ( );

            public new bool Equals      ( object x, object y ) => x == y;
            public     int  GetHashCode ( object value       ) => RuntimeHelpers.GetHashCode ( value );
        }
    }
}