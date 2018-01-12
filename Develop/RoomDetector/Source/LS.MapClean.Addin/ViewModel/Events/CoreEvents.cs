using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LS.MapClean.Addin.ViewModel.Events
{
    /// <summary>
    /// Timing
    /// </summary>
    public enum EventContextTiming
    {
        None,
        Before,
        After
    }

    /// <summary>
    /// Arguments
    /// </summary>
    public class CoreEventArgs : EventArgs
    {
        public bool Handled { get; set; }
        public object Sender { get; set; }
        public EventContextTiming Timing { get; set; }
    }

    /// <summary>
    /// Base Events class
    /// </summary>
    public abstract class CoreEvents
    {
#if DEBUG
        private Dictionary<string, uint> m_listenerTracking = null;
#endif

        protected CoreEvents()
        {
#if DEBUG
            m_listenerTracking = new Dictionary<string, uint>();
#endif
        }

#if DEBUG
        void AddListener(Delegate listener)
        {
            string sListener = string.Format(CultureInfo.InvariantCulture, "{0}::{1}", listener.Method.DeclaringType.FullName, listener.Method.Name);
            if (m_listenerTracking.ContainsKey(sListener))
            {
                m_listenerTracking[sListener] += 1;
            }
            else
            {
                m_listenerTracking[sListener] = 1;
            }
        }

        void RemoveListener(Delegate listener)
        {
            string sListener = string.Format(CultureInfo.InvariantCulture, "{0}::{1}", listener.Method.DeclaringType.FullName, listener.Method.Name);
            if (m_listenerTracking.ContainsKey(sListener))
            {
                m_listenerTracking[sListener] -= 1;
                if (m_listenerTracking[sListener] == 0)
                {
                    m_listenerTracking.Remove(sListener);
                }
            }
            else
            {
                System.Diagnostics.Debug.Assert(false, "The listener was already removed");
            }
        }
#endif

        protected System.Delegate IncrementListeners(System.Delegate existingListeners, System.Delegate newListener)
        {
#if DEBUG
            this.AddListener(newListener);
#endif
            // Add the new listener and if we still don't have a listener (don't know why) return...
            System.Delegate result = System.Delegate.Combine(existingListeners, newListener);
            if (null == result)
                return result;

            return result;
        }

        protected System.Delegate DecrementListeners(System.Delegate existingListeners, System.Delegate oldListener)
        {
#if DEBUG
            this.RemoveListener(oldListener);
#endif
            // If we never had listeners to begin with then return, this indicates a bug in the client code...
            if (null == existingListeners)
            {
                System.Diagnostics.Debug.Assert(false);
                return null;
            }

            // Remove the listener...
            System.Delegate result = System.Delegate.Remove(existingListeners, oldListener);
            return result;
        }

        public abstract string UniqueIdentifier { get; }
    }
}
