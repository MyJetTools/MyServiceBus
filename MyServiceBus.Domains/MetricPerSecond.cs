namespace MyServiceBus.Domains
{
    public class MetricPerSecond
    {
        public int Value { get; private set; }

        private int _counter;


        public void EventHappened()
        {
            _counter++;
        }

        public void EventsHappened(int delta)
        {
            _counter += delta;
        }

        public void OneSecondTimer()
        {
            Value = _counter;
            _counter = 0;
        }
    }
}