using System;
using FlubuCore.Context;
using FlubuCore.Context.Attributes.BuildProperties;
using FlubuCore.IO;
using FlubuCore.Scripting;
using FlubuCore.Tasks.Attributes;
using FlubuCore.Tasks.Versioning;

namespace build
{
    public class BuildScript : DefaultBuildScript
    {
        public FullPath OutputDir => RootDirectory.CombineWith("output");

        [SolutionFileName]
        public string SolutionFileName => RootDirectory.CombineWith("source/ConsoleApp1.sln");

        [BuildConfiguration]
        public string BuildConfiguration { get; set; } = "Release"; // Debug or Release

        protected override void ConfigureBuildProperties(IBuildPropertiesContext context)
        {
            context.Properties.Set(BuildProps.ProductId, "ConsoleApp1");
            context.Properties.Set(BuildProps.ProductName, "ConsoleApp1");
        }

        protected override void ConfigureTargets(ITaskContext session)
        {
            Console.WriteLine($"RootDirectory: {RootDirectory}");
            Console.WriteLine($"OutputDir: {OutputDir}");

            var target = session.CreateTarget("compile")
                .SetDescription("Compile the solution.")
                .AddCoreTask(x =>
                    x.Build()
                    .Output(OutputDir));         
        }
    }
}
