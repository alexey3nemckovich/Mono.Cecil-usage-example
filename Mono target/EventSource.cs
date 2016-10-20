using System;

namespace Mono_target
{

    public class EventSource
    {

        public event Action Enabled;

        public void CallEvent()
        {
            if(Enabled != null)
            {
                Enabled.Invoke();
            }
        }

    }

}