using System;
using System.Collections.Generic;
using System.Text;

namespace RavenNest.Models
{
    public class StateParameters
    {
        public string ParametersName { get; set; }
        public string ParametersValue { get; set; }

        public StateParameters(string parametersName, string parametersValue)
        {
            ParametersName = parametersName;
            ParametersValue = parametersValue;
        }

    }
}
