using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace MadsKristensen.AddAnyFile
{
    /// <summary>
    /// Represents the target location for creating a new item within a Visual Studio solution.
    /// </summary>
    public class NewItemTarget
    {
        /// <summary>
        /// Creates a new instance of <see cref="NewItemTarget"/> based on the current context in
        /// Visual Studio.
        /// </summary>
        /// <param name="dte">The DTE2 instance representing the Visual Studio environment.</param>
        /// <returns>A new instance of <see cref="NewItemTarget"/>.</returns>
        public static NewItemTarget Create(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            NewItemTarget item = null;

            // If a document is active, try to use the document's containing directory.
            if (dte.ActiveWindow is Window2 window && window.Type == vsWindowType.vsWindowTypeDocument)
            {
                item = CreateFromActiveDocument(dte);
            }

            // If no document was selected, or we could not get a selected item from the document,
            // then use the selected item in the Solution Explorer window.
            if (item == null)
            {
                item = CreateFromSolutionExplorerSelection(dte);
            }

            return item;
        }

        /// <summary>
        /// Creates a new instance of <see cref="NewItemTarget"/> based on the currently active document.
        /// </summary>
        /// <param name="dte">The DTE2 instance representing the Visual Studio environment.</param>
        /// <returns>
        /// A new instance of <see cref="NewItemTarget"/> or null if no valid target is found.
        /// </returns>
        private static NewItemTarget CreateFromActiveDocument(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string fileName = dte.ActiveDocument?.FullName;
            if (File.Exists(fileName))
            {
                ProjectItem docItem = dte.Solution.FindProjectItem(fileName);
                if (docItem != null)
                {
                    return CreateFromProjectItem(docItem);
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a new instance of <see cref="NewItemTarget"/> based on the selected item in the
        /// Solution Explorer.
        /// </summary>
        /// <param name="dte">The DTE2 instance representing the Visual Studio environment.</param>
        /// <returns>
        /// A new instance of <see cref="NewItemTarget"/> or null if no valid target is found.
        /// </returns>
        private static NewItemTarget CreateFromSolutionExplorerSelection(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Array items = (Array)dte.ToolWindows.SolutionExplorer.SelectedItems;

            if (items.Length == 1)
            {
                UIHierarchyItem selection = items.Cast<UIHierarchyItem>().First();

                if (selection.Object is Solution solution)
                {
                    return new NewItemTarget(Path.GetDirectoryName(solution.FullName), null, null, isSolutionOrSolutionFolder: true);
                }
                else if (selection.Object is Project project)
                {
                    if (project.IsKind(Constants.vsProjectKindSolutionItems))
                    {
                        return new NewItemTarget(GetSolutionFolderPath(project), project, null, isSolutionOrSolutionFolder: true);
                    }
                    else
                    {
                        return new NewItemTarget(project.GetRootFolder(), project, null, isSolutionOrSolutionFolder: false);
                    }
                }
                else if (selection.Object is ProjectItem projectItem)
                {
                    return CreateFromProjectItem(projectItem);
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a new instance of <see cref="NewItemTarget"/> based on the specified project item.
        /// </summary>
        /// <param name="projectItem">The project item to base the target on.</param>
        /// <returns>
        /// A new instance of <see cref="NewItemTarget"/> or null if no valid target is found.
        /// </returns>
        private static NewItemTarget CreateFromProjectItem(ProjectItem projectItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (projectItem.IsKind(Constants.vsProjectItemKindSolutionItems))
            {
                return new NewItemTarget(
                    GetSolutionFolderPath(projectItem.ContainingProject),
                    projectItem.ContainingProject,
                    null,
                    isSolutionOrSolutionFolder: true);
            }
            else
            {
                // The selected item needs a directory. This project item could be a virtual folder,
                // so resolve it to a physical file or folder.
                projectItem = ResolveToPhysicalProjectItem(projectItem);
                string fileName = projectItem?.GetFileName();

                if (string.IsNullOrEmpty(fileName))
                {
                    return null;
                }

                // If the file exists, then it must be a file and we can get the directory name from
                // it. If the file does not exist, then it must be a directory, and the directory
                // name is the file name.
                string directory = File.Exists(fileName) ? Path.GetDirectoryName(fileName) : fileName;
                return new NewItemTarget(directory, projectItem.ContainingProject, projectItem, isSolutionOrSolutionFolder: false);
            }
        }

        /// <summary>
        /// Resolves a virtual project item to a physical project item.
        /// </summary>
        /// <param name="projectItem">The project item to resolve.</param>
        /// <returns>The resolved physical project item or null if no physical item is found.</returns>
        private static ProjectItem ResolveToPhysicalProjectItem(ProjectItem projectItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (projectItem.IsKind(Constants.vsProjectItemKindVirtualFolder))
            {
                // Find the first descendant item that is not a virtual folder.
                return projectItem.ProjectItems
                    .Cast<ProjectItem>()
                    .Select(item => ResolveToPhysicalProjectItem(item))
                    .FirstOrDefault(item => item != null);
            }

            return projectItem;
        }

        /// <summary>
        /// Gets the full path to a solution folder.
        /// </summary>
        /// <param name="folder">The project representing the solution folder.</param>
        /// <returns>The full path to the solution folder.</returns>
        private static string GetSolutionFolderPath(Project folder)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string solutionDirectory = Path.GetDirectoryName(folder.DTE.Solution.FullName);
            List<string> segments = new List<string>();

            // Record the names of each folder up the hierarchy until we reach the solution.
            do
            {
                segments.Add(folder.Name);
                folder = folder.ParentProjectItem?.ContainingProject;
            } while (folder != null);

            // Because we walked up the hierarchy, the path segments are in reverse order.
            segments.Reverse();

            return Path.Combine(new[] { solutionDirectory }.Concat(segments).ToArray());
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NewItemTarget"/> class.
        /// </summary>
        /// <param name="directory">The target directory for the new item.</param>
        /// <param name="project">The project associated with the target.</param>
        /// <param name="projectItem">The project item associated with the target.</param>
        /// <param name="isSolutionOrSolutionFolder">
        /// A flag indicating if the target is a solution or solution folder.
        /// </param>
        private NewItemTarget(string directory, Project project, ProjectItem projectItem, bool isSolutionOrSolutionFolder)
        {
            Directory = directory;
            Project = project;
            ProjectItem = projectItem;
            IsSolutionOrSolutionFolder = isSolutionOrSolutionFolder;
        }

        /// <summary>
        /// Gets the target directory for the new item.
        /// </summary>
        public string Directory { get; }

        /// <summary>
        /// Gets the project associated with the target.
        /// </summary>
        public Project Project { get; }

        /// <summary>
        /// Gets the project item associated with the target.
        /// </summary>
        public ProjectItem ProjectItem { get; }

        /// <summary>
        /// Gets a value indicating whether the target is a solution or solution folder.
        /// </summary>
        public bool IsSolutionOrSolutionFolder { get; }

        /// <summary>
        /// Gets a value indicating whether the target is a solution.
        /// </summary>
        public bool IsSolution => IsSolutionOrSolutionFolder && Project == null;

        /// <summary>
        /// Gets a value indicating whether the target is a solution folder.
        /// </summary>
        public bool IsSolutionFolder => IsSolutionOrSolutionFolder && Project != null;
    }
}
