using System;

namespace RavenNest.BusinessLogic.ScriptParser
{
    public class ParserException : Exception
    {
        public ParserException(string message)
            : base(message)
        {
        }
    }
}
