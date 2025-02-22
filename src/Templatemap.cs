using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace MadsKristensen.AddAnyFile
{
    internal static class TemplateMap
    {
        private const string _defaultExt = ".txt";
        private const string _templateDir = ".templates";
        private static readonly string _folder;
        private static readonly List<string> _templateFiles = new List<string>();

        static TemplateMap()
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var userProfile = Path.Combine(folder, ".vs", _templateDir);

            if (Directory.Exists(userProfile))
            {
                _templateFiles.AddRange(Directory.GetFiles(userProfile, "*" + _defaultExt, SearchOption.AllDirectories));
            }

            var assembly = Assembly.GetExecutingAssembly().Location;
            _folder = Path.Combine(Path.GetDirectoryName(assembly), "Templates");
            _templateFiles.AddRange(Directory.GetFiles(_folder, "*" + _defaultExt, SearchOption.AllDirectories));
        }

        /// <summary>
        /// Asynchronously gets the template file path based on the provided project and file name.
        /// </summary>
        /// <param name="project">The project in which the file is being created.</param>
        /// <param name="file">The name of the file for which a template is being sought.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the path to
        /// the matching template file, or null if no match is found.
        /// </returns>
        public static async Task<string> GetTemplateFilePathAsync(Project project, string file)
        {
            var name = Path.GetFileName(file);
            var safeName = name.StartsWith(".") ? name : Path.GetFileNameWithoutExtension(file);
            var relative = PackageUtilities.MakeRelative(project.GetRootFolder(), Path.GetDirectoryName(file) ?? "");

            var list = _templateFiles.ToList();

            AddTemplatesFromCurrentFolder(list, Path.GetDirectoryName(file));

            var templateFile = GetMatchingTemplateFromFileName(project, list, file);

            var template = await ReplaceTokensAsync(project, safeName, relative, templateFile);
            return NormalizeLineEndings(template);
        }

        private static void AddTemplatesFromCurrentFolder(List<string> list, string dir)
        {
            var current = new DirectoryInfo(dir);
            var dynaList = new List<string>();

            while (current != null)
            {
                var templateDirectory = Path.Combine(current.FullName, _templateDir);

                if (Directory.Exists(templateDirectory))
                {
                    dynaList.AddRange(Directory.GetFiles(templateDirectory, "*" + _defaultExt, SearchOption.AllDirectories));
                }

                current = current.Parent;
            }

            list.InsertRange(0, dynaList);
        }

        /// <summary>
        /// Gets the matching template file path based on the provided file name.
        /// </summary>
        /// <param name="project">The project in which the file is being created.</param>
        /// <param name="templateFilePaths">A list of available template file paths.</param>
        /// <param name="file">The name of the file for which a template is being sought.</param>
        /// <returns>The path to the matching template file, or null if no match is found.</returns>
        private static string GetMatchingTemplateFromFileName(Project project, List<string> templateFilePaths, string file)
        {
            var extension = Path.GetExtension(file).ToLowerInvariant();
            var name = Path.GetFileName(file);
            var safeName = name.StartsWith(".") ? name : Path.GetFileNameWithoutExtension(file);

            // Look for direct file name matches
            bool directFileMatchingPredicate(string path) => Path.GetFileName(path).Equals(name + _defaultExt, StringComparison.OrdinalIgnoreCase);

            if (templateFilePaths.Any(directFileMatchingPredicate))
            {
                var matchedTemplateFile = templateFilePaths.FirstOrDefault(directFileMatchingPredicate);
                return Path.Combine(Path.GetDirectoryName(matchedTemplateFile), name + _defaultExt);
            }

            // Look for convention matches
            bool conventionMatchingPredicate(string path) => (safeName + _defaultExt).EndsWith(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);
            if (templateFilePaths.Any(conventionMatchingPredicate))
            {
                return templateFilePaths.FirstOrDefault(conventionMatchingPredicate);
            }

            // Look for file extension matches
            bool extensionMatchingPredicate(string path) => Path.GetFileName(path).Equals(extension + _defaultExt, StringComparison.OrdinalIgnoreCase) && File.Exists(path);
            if (templateFilePaths.Any(extensionMatchingPredicate))
            {
                var templateFile = templateFilePaths.FirstOrDefault(extensionMatchingPredicate);
                var template = AdjustForSpecific(project, safeName, extension);
                return Path.Combine(Path.GetDirectoryName(templateFile), template + _defaultExt);
            }

            return null;
        }

        private static async Task<string> ReplaceTokensAsync(Project project, string name, string relative, string templateFile)
        {
            if (string.IsNullOrEmpty(templateFile))
            {
                return templateFile;
            }

            var rootNs = project.GetRootNamespace();
            var ns = string.IsNullOrEmpty(rootNs) ? "MyNamespace" : rootNs;

            var mvcProjectControllerNs = project.GetMVCNamespace() ?? "";

            if (!string.IsNullOrEmpty(relative))
            {
                ns += "." + ProjectHelpers.CleanNameSpace(relative);
            }

            using (var reader = new StreamReader(templateFile))
            {
                var content = await reader.ReadToEndAsync();

                return content.Replace("{namespace}", ns)
                           .Replace("{itemName}", name)
                           .Replace("{mvcProjectNamespace}", mvcProjectControllerNs);
            }
        }

        private static string NormalizeLineEndings(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return content;
            }

            return Regex.Replace(content, @"\r\n|\n\r|\n|\r", "\r\n");
        }

        private static string AdjustForSpecific(Project project, string safeName, string extension)
        {
            if (Regex.IsMatch(safeName, "^I[A-Z].*"))
            {
                return extension += "-interface";
            }
            else if (Regex.IsMatch(safeName, @".+Enum$"))
            {
                return extension += "-enum";
            }
            else if (Regex.IsMatch(safeName, @".+Controller$") && project.IsMVCProject())
            {
                return extension += "-controller";
            }
            else if (Regex.IsMatch(safeName, @".+Resource$"))
            {
                return extension += "resx";
            }

            return extension;
        }
    }
}
