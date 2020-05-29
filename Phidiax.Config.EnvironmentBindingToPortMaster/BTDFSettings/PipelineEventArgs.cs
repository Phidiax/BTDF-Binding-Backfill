using System;

namespace Phidiax.Config.EnvironmentBindingToPortMaster
{
    public class PipelineEventArgs : EventArgs
    {
        public PipelineEventArgs(int number, string message)
        {
            Number = number;
            Message = message;
        }

        public int Number { get; set; }
        public string Message { get; set; }
    }
}
