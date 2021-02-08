using System;

namespace FlightRecorder.Client.Logics
{
    public class CurrentFrameChangedEventArgs : EventArgs
    {
        public CurrentFrameChangedEventArgs(int currentFrame)
        {
            CurrentFrame = currentFrame;
        }

        public int CurrentFrame { get; }
    }
}
