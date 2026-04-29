using System;

namespace VisualHFT.DataRetriever.TestingFramework.Core
{
    /// <summary>
    /// Indicates that a plugin test could not be exercised in the current environment and should be reported as skipped.
    /// </summary>
    public sealed class SkippedPluginTestException : Exception
    {
        public SkippedPluginTestException(string message)
            : base(message)
        {
        }
    }
}
