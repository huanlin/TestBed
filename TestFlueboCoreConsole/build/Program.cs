using FlubuCore.Scripting;
using System;
using System.Collections.Generic;
using System.IO;

namespace build
{
    class Program
    {
        static void Main(string[] args)
        {
            var engine = new FlubuEngine();
            engine.RunScript<BuildScript>(new string[] { "compile" });
        }
    }
}
