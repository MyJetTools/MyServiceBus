namespace MyServiceBus.Domains
{
    public class GlobalVariables
    {
        public bool ShuttingDown { get; set; }

        public int PublishRequestsAmountAreBeingProcessed { get; set; }
    }
}