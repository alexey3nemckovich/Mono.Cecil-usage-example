using System;
using System.IO;

namespace Mono_target
{

    public class EventListener
    {

        private Delegate handler;
        
        [Weak]
        private Delegate Handler
        {
            get
            {
                return handler;
            }
            set
            {
                handler = value;
            }
        }
        private EventSource eventSource;

        public EventListener(EventSource eventSource)
        {
            this.eventSource = eventSource;
            Handler = (Action)EventHandler;
        }

        public void StartListeningEvent()
        {
            eventSource.Enabled += (Action)Handler;
        }

        public void StopListeningEvent()
        {
            eventSource.Enabled -= (Action)Handler;
        }

        private void EventHandler()
        {
            using (StreamWriter writer = new StreamWriter(new FileStream("EventsLog.txt", FileMode.Append, FileAccess.Write)))
            {
                writer.WriteLine(this.GetType() + " processed event[" + DateTime.Now + "].");
            }
        }

    }

}