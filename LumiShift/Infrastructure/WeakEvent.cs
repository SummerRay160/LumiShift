using System;
using System.Collections.Generic;

namespace LumiShift.Infrastructure
{
    public class WeakEvent<TEventArgs> where TEventArgs : EventArgs
    {
        private readonly List<WeakReference> _handlers = new List<WeakReference>();

        public void Subscribe(EventHandler<TEventArgs> handler)
        {
            if (handler == null) return;
            _handlers.RemoveAll(wr => !wr.IsAlive || (object)wr.Target == (object)handler);
            _handlers.Add(new WeakReference(handler));
        }

        public void Unsubscribe(EventHandler<TEventArgs> handler)
        {
            if (handler == null) return;
            _handlers.RemoveAll(wr => !wr.IsAlive || (object)wr.Target == (object)handler);
        }

        public void Raise(object sender, TEventArgs args)
        {
            var toRemove = new List<int>();
            for (int i = 0; i < _handlers.Count; i++)
            {
                if (_handlers[i].Target is EventHandler<TEventArgs> handler && _handlers[i].IsAlive)
                    handler(sender, args);
                else
                    toRemove.Add(i);
            }
            for (int i = toRemove.Count - 1; i >= 0; i--)
                _handlers.RemoveAt(toRemove[i]);
        }
    }
}
