using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

class Program
{
    // Absolute path to replace (constant)
    private const string AbsolutePathToReplace = @"C:\ndensan\framework\";

    // History file path
    private static readonly string HistoryFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "change_history.txt");

    static void Main()
    {
        // Get the root folder from the user
        Console.WriteLine("Enter the root folder path to search for project files:");
        string? rootFolder = Console.ReadLine();

        if (string.IsNullOrEmpty(rootFolder) || !Directory.Exists(rootFolder))
        {
            Console.WriteLine("Invalid folder. Please try again.");
            return;
        }

        // Variables to count the number of changed and unchanged files
        int changedCount = 0;
        int unchangedCount = 0;

        // Search for all .csproj and .vbproj files
        string[] projectFiles = Directory.GetFiles(rootFolder, "*.*proj", SearchOption.AllDirectories);

        using (StreamWriter historyWriter = new StreamWriter(HistoryFilePath, false))
        {
            foreach (string projectFile in projectFiles)
            {
                Console.WriteLine($"Processing file: {projectFile}");

                // Open and load the file content as XML
                XDocument xmlDoc = XDocument.Load(projectFile);
                bool updated = false;

                // Process HintPath references
                updated |= ProcessHintPaths(xmlDoc, projectFile);

                // Process ProjectReferences
                updated |= ProcessProjectReferences(xmlDoc, projectFile);

                // Save the file if there were changes and write to the history file
                if (updated)
                {
                    xmlDoc.Save(projectFile);
                    changedCount++;
                    historyWriter.WriteLine($"Changed: {projectFile}");
                    Console.WriteLine("Changes saved.");
                }
                else
                {
                    unchangedCount++;
                    historyWriter.WriteLine($"Unchanged: {projectFile}");
                    Console.WriteLine("No paths to replace.");
                }
            }

            // Write total changed and unchanged counts to the history file
            historyWriter.WriteLine();
            historyWriter.WriteLine($"Total files changed: {changedCount}");
            historyWriter.WriteLine($"Total files unchanged: {unchangedCount}");
        }

        Console.WriteLine("Process completed. Check the history file at: " + HistoryFilePath);
    }

    // Function to process HintPath elements
    static bool ProcessHintPaths(XDocument xmlDoc, string projectFilePath)
    {
        bool updated = false;

        foreach (var hintPathElement in xmlDoc.Descendants("HintPath"))
        {
            string originalPath = hintPathElement.Value;

            if (originalPath.Contains(AbsolutePathToReplace))
            {
                // Create relative path based on the project file location
                string relativePath = GetRelativePath(projectFilePath, originalPath);
                hintPathElement.Value = relativePath;
                updated = true;
                Console.WriteLine($"Replaced HintPath: {originalPath} => {relativePath}");
            }
        }

        return updated;
    }

    // Function to process ProjectReference elements
    static bool ProcessProjectReferences(XDocument xmlDoc, string projectFilePath)
    {
        bool updated = false;

        // Create a list to store new reference elements
        List<XElement> newReferences = new List<XElement>();

        foreach (var projectReferenceElement in xmlDoc.Descendants("ProjectReference"))
        {
            var referencedProjectPath = projectReferenceElement?.Attribute("Include")?.Value;

            if (!string.IsNullOrEmpty(referencedProjectPath))
            {
                // Ensure there are no unwanted characters in the path
                referencedProjectPath = referencedProjectPath.Trim();

                // Get the assembly name from the referenced project
                string assemblyName = GetAssemblyName(referencedProjectPath);

                // Determine the output type (DLL or EXE)
                string outputType = GetOutputType(referencedProjectPath);
                string outputExtension = outputType == "Exe" ? "exe" : "dll"; // Exe for console/apps, dll for libraries

                // Create the absolute path to the DLL/EXE based on the output directory
                string outputDir = Path.Combine(Path.GetDirectoryName(referencedProjectPath), "bin");
                string referencedOutputPath = Path.Combine(outputDir, $"{assemblyName}.{outputExtension}");

                // Get the relative path from the project file to the output path
                string relativePath = GetRelativePath(projectFilePath, referencedOutputPath);

                // Create a new reference element with the correct relative path
                var newReferenceElement = new XElement("Reference",
                    new XAttribute("Include", assemblyName),
                    new XAttribute("HintPath", relativePath));

                // Check if the HintPath needs to be updated
                var hintPathElement = projectReferenceElement?.Element("HintPath");
                if (hintPathElement == null || hintPathElement.Value != relativePath)
                {
                    newReferences.Add(newReferenceElement);
                    updated = true; // Mark as updated
                }
                else
                {
                    // If no update is needed, keep the existing element
                    newReferences.Add(projectReferenceElement);
                }
            }
        }

        // Clear existing ProjectReference elements and add new ones
        if (updated)
        {
            var projectReferencesElement = xmlDoc.Descendants("ProjectReference").FirstOrDefault();
            if (projectReferencesElement != null)
            {
                projectReferencesElement.Remove(); // Remove old elements
                xmlDoc?.Root?.Add(newReferences); // Add new elements
            }
        }

        return updated; // Return whether any updates were made
    }

    // Function to retrieve the assembly name from the <AssemblyName> tag
    static string GetAssemblyName(string projectFilePath)
    {
        XElement? assemblyNameElement;
        // Load the project file as XML
        using (Stream xmlFile = new FileStream(projectFilePath,FileMode.Open,FileAccess.Read))
        {
            XDocument xmlDoc = XDocument.Load(projectFilePath);

            // Find the AssemblyName element
            assemblyNameElement = xmlDoc.Descendants("AssemblyName").FirstOrDefault();

        }
        // Return the assembly name or the file name without extension if not found
        return assemblyNameElement != null ? assemblyNameElement.Value : Path.GetFileNameWithoutExtension(projectFilePath);
    }

    // Method to get the output type from the project file
    static string GetOutputType(string projectFilePath)
    {
        // Load the project file as XML
        XDocument xmlDoc = XDocument.Load(projectFilePath);

        // Find the OutputType element
        var outputTypeElement = xmlDoc.Descendants("OutputType").FirstOrDefault();

        // Return the output type or default to "Library"
        return outputTypeElement != null ? outputTypeElement.Value : "Library";
    }
    // Function to create a relative path
    static string GetRelativePath(string projectFilePath, string absolutePath)
    {
        Uri projectUri = new Uri(Path.GetDirectoryName(projectFilePath) + Path.DirectorySeparatorChar);
        Uri absoluteUri = new Uri(absolutePath);

        // Create the relative URI
        Uri relativeUri = projectUri.MakeRelativeUri(absoluteUri);
        return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
    }
}
