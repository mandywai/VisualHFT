using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualHFT.DataRetriever.TestingFramework.Core
{
    public class ErrorReporting
    {
        public string PluginName { get; set; }
        public string Message { get; set; }
        public ErrorMessageTypes MessageType { get; set; }
    }
}
