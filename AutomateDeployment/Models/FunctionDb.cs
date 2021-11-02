using System;
using System.Collections.Generic;
using System.Text;

namespace AutomateDeployment.Models
{
   public class FunctionDb
    {
        public string DBName { get; set; }
        public string ScriptName { get; set; }
        public string ConnectionServiceName { get; set; }
    }
}
