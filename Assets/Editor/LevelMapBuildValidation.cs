using System;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace CandyCruisers.Editor
{
    public sealed class LevelMapBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report)
        {
            try { LevelCatalog.Parse(File.ReadAllText(LevelCatalog.FilePath)); }
            catch (Exception error) { throw new BuildFailedException(error.Message); }
        }
    }
}
