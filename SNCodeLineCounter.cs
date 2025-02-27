using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A Unity Editor tool that analyzes and counts code lines in the project.
/// Provides statistics about code, comment, and empty lines for various file types.
/// </summary>
public class SNCodeLineCounter : EditorWindow
{
    [Serializable]
    public class FileStatistics
    {
        /// <summary>The relative path to the file within the project.</summary>
        public string filePath;

        /// <summary>The name of the file with extension.</summary>
        public string fileName;

        /// <summary>The file extension (e.g. ".cs", ".js").</summary>
        public string extension;

        /// <summary>Total number of lines in the file.</summary>
        public int totalLines;

        /// <summary>Number of code lines (non-comment, non-empty).</summary>
        public int codeLines;

        /// <summary>Number of comment lines.</summary>
        public int commentLines;

        /// <summary>Number of empty lines.</summary>
        public int emptyLines;
    }

    // Settings
    private string rootFolder = "Assets/Scripts";
    private List<string> extensionsToCheck = new List<string> { ".cs" };
    private bool excludeThirdParty = true;

    // UI State
    private bool showSettings = false;
    private bool isAnalyzing = false;

    // Analysis results
    private List<FileStatistics> fileStatsList = new List<FileStatistics>();
    private int totalFiles = 0;
    private int totalCodeLines = 0;
    private int totalCommentLines = 0;
    private int totalEmptyLines = 0;

    // Editor preferences
    private const string EditorPrefRootFolder = "SN_CodeLineCounter_RootFolder";
    private const string EditorPrefExcludeThirdParty = "SN_CodeLineCounter_ExcludeThirdParty";
    private GUIStyle headerStyle;


    /// <summary>
    /// Adds a menu item to open the SN Code Line Counter window.
    /// </summary>
    [MenuItem("Tools/SN Code Line Counter")]
    public static void ShowWindow()
    {
        GetWindow<SNCodeLineCounter>("SN Code Line Counter");
    }


    /// <summary>
    /// Initializes the window when it's enabled.
    /// </summary>
    private void OnEnable()
    {
        // Load saved settings
        rootFolder = EditorPrefs.GetString(EditorPrefRootFolder, "Assets/Scripts");
        excludeThirdParty = EditorPrefs.GetBool(EditorPrefExcludeThirdParty, true);

        // Prepare UI styles
        headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 14;
    }

    /// <summary>
    /// Draws the Editor GUI.
    /// </summary>
    private void OnGUI()
    {
        DrawHeader();

        // If analysis is running, don't allow interaction
        EditorGUI.BeginDisabledGroup(isAnalyzing);

        DrawFolderSelection();
        DrawSettings();

        GUILayout.Space(10);
        if (GUILayout.Button("Analyze", GUILayout.Height(28)))
        {
            StartAnalysis();
        }

        EditorGUI.EndDisabledGroup();

        if (fileStatsList.Count > 0)
        {
            DrawResults();
        }

        // Show analyzing message if needed
        if (isAnalyzing)
        {
            EditorGUILayout.HelpBox("Analyzing files... Please wait.", MessageType.Info);
        }
    }


    /// <summary>
    /// Draws the header section of the window.
    /// </summary>
    private void DrawHeader()
    {
        GUILayout.Label("Unity Code Statistics Tool", headerStyle);
        GUILayout.Space(5);
        EditorGUILayout.HelpBox("This tool analyzes the line count of code files in the specified folder.", MessageType.Info);
        GUILayout.Space(10);
    }

    /// <summary>
    /// Draws the folder selection controls.
    /// </summary>
    private void DrawFolderSelection()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Root Folder:");

        // Folder field and save settings
        EditorGUI.BeginChangeCheck();
        rootFolder = EditorGUILayout.TextField(rootFolder);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetString(EditorPrefRootFolder, rootFolder);
        }

        // Browse button
        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Root Folder", Application.dataPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                // Always use this selected path - we'll validate it in the analysis phase
                rootFolder = selectedPath;
                EditorPrefs.SetString(EditorPrefRootFolder, rootFolder);
            }
        }

        // Project root button
        if (GUILayout.Button("Project Root", GUILayout.Width(90)))
        {
            rootFolder = Path.GetDirectoryName(Application.dataPath);
            EditorPrefs.SetString(EditorPrefRootFolder, rootFolder);
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draws the settings section of the window.
    /// </summary>
    private void DrawSettings()
    {
        showSettings = EditorGUILayout.Foldout(showSettings, "Settings", true);
        if (showSettings)
        {
            EditorGUI.indentLevel++;

            // Third-party code exclusion option
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Exclude Third-Party Code", GUILayout.Width(165));
            excludeThirdParty = EditorGUILayout.Toggle("", excludeThirdParty);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(EditorPrefExcludeThirdParty, excludeThirdParty);
            }

            EditorGUILayout.LabelField("File Extensions to Include:", GUILayout.Width(170));
            for (int i = 0; i < extensionsToCheck.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                extensionsToCheck[i] = EditorGUILayout.TextField(extensionsToCheck[i]);

                GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    extensionsToCheck.RemoveAt(i);
                    GUIUtility.ExitGUI(); // Prevent UI update issues
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
            if (GUILayout.Button("Add Extension", GUILayout.Width(120)))
            {
                extensionsToCheck.Add(".txt");
            }
            GUI.backgroundColor = Color.white;

            EditorGUI.indentLevel--;
        }
    }


    /// <summary>
    /// Draws the results section of the window.
    /// </summary>
    private void DrawResults()
    {
        GUILayout.Space(15);
        GUILayout.Label("Results", headerStyle);

        // Summary
        EditorGUILayout.BeginVertical("box");
        int totalAllLines = totalCodeLines + totalCommentLines + totalEmptyLines;

        if (totalAllLines > 0)
        {
            DrawResultRow("Total Files:", totalFiles.ToString());
            DrawResultRow("Total Lines:", totalAllLines.ToString());
            DrawResultRow("Code Lines:", $"{totalCodeLines} ({(float)totalCodeLines / totalAllLines:P1})");
            DrawResultRow("Comment Lines:", $"{totalCommentLines} ({(float)totalCommentLines / totalAllLines:P1})");
            DrawResultRow("Empty Lines:", $"{totalEmptyLines} ({(float)totalEmptyLines / totalAllLines:P1})");
        }
        else
        {
            DrawResultRow("Total Files:", totalFiles.ToString());
            DrawResultRow("Total Lines:", "0");
            DrawResultRow("Code Lines:", "0");
            DrawResultRow("Comment Lines:", "0");
            DrawResultRow("Empty Lines:", "0");
        }

        EditorGUILayout.EndVertical();

        // Export option
        GUILayout.Space(10);
        GUI.backgroundColor = new Color(0.7f, 0.85f, 1f);
        if (GUILayout.Button("Export as CSV"))
        {
            string exportPath = EditorUtility.SaveFilePanel("Save Statistics", "", "CodeStatistics", "csv");
            if (!string.IsNullOrEmpty(exportPath))
            {
                ExportCSV(exportPath);
            }
        }
        GUI.backgroundColor = Color.white;
    }


    /// <summary>
    /// Draws a single row in the results summary.
    /// </summary>
    /// <param name="label">The label for the row.</param>
    /// <param name="value">The value to display.</param>
    private void DrawResultRow(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(120));
        EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();
    }


    /// <summary>
    /// Initiates the analysis process.
    /// </summary>
    private void StartAnalysis()
    {
        // Reset previous data
        fileStatsList.Clear();
        totalFiles = 0;
        totalCodeLines = 0;
        totalCommentLines = 0;
        totalEmptyLines = 0;
        isAnalyzing = true;

        // Check folder path
        if (!ValidateFolder())
        {
            isAnalyzing = false;
            return;
        }

        // Start the async analysis
        EditorApplication.update += AnalysisUpdateLoop;
        analysisState = new AnalysisState();
    }


    /// <summary>
    /// Holds the state for the ongoing analysis process.
    /// </summary>
    private class AnalysisState
    {
        public string[] allFiles;
        public int currentIndex = 0;
        public int processedFiles = 0;
        public int totalFilesToProcess = 0;
        public bool initialized = false;
    }

    private AnalysisState analysisState;
    private const int FILES_PER_FRAME = 10; // Process this many files per frame


    /// <summary>
    /// Editor update loop used for incremental file analysis.
    /// This prevents the editor from becoming unresponsive during analysis.
    /// </summary>
    private void AnalysisUpdateLoop()
    {
        if (!isAnalyzing || analysisState == null)
        {
            EditorApplication.update -= AnalysisUpdateLoop;
            return;
        }

        try
        {
            if (!analysisState.initialized)
            {
                InitializeAnalysis();
                analysisState.initialized = true;
            }

            if (analysisState.currentIndex >= analysisState.allFiles.Length)
            {
                FinishAnalysis();
                return;
            }

            // Process a batch of files
            int filesToProcess = Math.Min(FILES_PER_FRAME, analysisState.allFiles.Length - analysisState.currentIndex);
            for (int i = 0; i < filesToProcess; i++)
            {
                string filePath = analysisState.allFiles[analysisState.currentIndex++];
                FileStatistics stats = AnalyzeFile(filePath);
                fileStatsList.Add(stats);

                totalFiles++;
                totalCodeLines += stats.codeLines;
                totalCommentLines += stats.commentLines;
                totalEmptyLines += stats.emptyLines;

                analysisState.processedFiles++;
            }

            // Update progress bar
            float progress = (float)analysisState.processedFiles / analysisState.totalFilesToProcess;
            bool canceled = EditorUtility.DisplayCancelableProgressBar("Analyzing Files",
                $"Analyzed {analysisState.processedFiles} of {analysisState.totalFilesToProcess} files...", progress);

            if (canceled)
            {
                FinishAnalysis();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error analyzing files: {ex.Message}");
            EditorUtility.DisplayDialog("Error", $"Error analyzing files: {ex.Message}", "OK");
            FinishAnalysis();
        }
    }

    /// <summary>
    /// Initializes the analysis process by finding all files to be analyzed.
    /// </summary>
    private void InitializeAnalysis()
    {
        // Find all files that match the extensions
        try
        {
            var allFilesQuery = Directory.GetFiles(rootFolder, "*.*", SearchOption.AllDirectories)
                .Where(file => extensionsToCheck.Contains(Path.GetExtension(file).ToLower()));

            if (excludeThirdParty)
            {
                allFilesQuery = allFilesQuery.Where(file => !ShouldExcludeFile(file));
            }

            analysisState.allFiles = allFilesQuery.ToArray();
            analysisState.totalFilesToProcess = analysisState.allFiles.Length;

            if (analysisState.totalFilesToProcess == 0)
            {
                EditorUtility.DisplayDialog("Info", "No matching files found in the selected folder.", "OK");
                FinishAnalysis();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error finding files: {ex.Message}");
            EditorUtility.DisplayDialog("Error", $"Error finding files: {ex.Message}", "OK");
            FinishAnalysis();
        }
    }


    /// <summary>
    /// Completes the analysis process and cleans up.
    /// </summary>
    private void FinishAnalysis()
    {
        // Sort by code line count
        fileStatsList = fileStatsList.OrderByDescending(f => f.codeLines).ToList();

        EditorUtility.ClearProgressBar();
        isAnalyzing = false;
        EditorApplication.update -= AnalysisUpdateLoop;
        Repaint(); // Update the UI
    }


    /// <summary>
    /// Determines if a file should be excluded from analysis.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>True if the file should be excluded, false otherwise.</returns>
    private bool ShouldExcludeFile(string filePath)
    {
        // Common third-party directories
        string[] thirdPartyDirs = new string[]
        {
            "ThirdParty", "Plugins", "External", "Vendor",
            "Packages", "Library", "node_modules"
        };

        foreach (var dir in thirdPartyDirs)
        {
            if (filePath.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar) ||
                filePath.StartsWith(dir + Path.DirectorySeparatorChar))
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>
    /// Validates the selected folder to ensure it exists and is within the project.
    /// </summary>
    /// <returns>True if the folder is valid, false otherwise.</returns>
    private bool ValidateFolder()
    {
        // Check if the directory exists directly
        if (Directory.Exists(rootFolder))
        {
            // Check if the path is within the project
            string projectPath = Path.GetDirectoryName(Application.dataPath);

            // Normalize paths to ensure consistent comparison
            string normalizedRootFolder = Path.GetFullPath(rootFolder).TrimEnd('/', '\\');
            string normalizedProjectPath = Path.GetFullPath(projectPath).TrimEnd('/', '\\');

            // Check if the normalized root folder starts with the normalized project path
            if (!normalizedRootFolder.StartsWith(normalizedProjectPath, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"Selected folder is outside the current project: {rootFolder}");
                EditorUtility.DisplayDialog("Error", "Only folders within the current project can be analyzed.", "OK");
                rootFolder = projectPath; // Reset to project root
                return false;
            }

            return true;
        }

        // Project root path
        string fullProjectPath = Path.GetDirectoryName(Application.dataPath);

        // Try as a project-relative path
        string fullPath = Path.Combine(fullProjectPath, rootFolder);
        if (Directory.Exists(fullPath))
        {
            rootFolder = fullPath;
            return true;
        }

        // Try within Assets
        if (Directory.Exists(Path.Combine(Application.dataPath, rootFolder)))
        {
            rootFolder = Path.Combine(Application.dataPath, rootFolder);
            return true;
        }

        Debug.LogError($"Directory not found: {rootFolder}");
        EditorUtility.DisplayDialog("Error", $"Directory not found: {rootFolder}", "OK");
        return false;
    }

    /// <summary>
    /// Analyzes a single file to count its lines of code, comments, and empty lines.
    /// </summary>
    /// <param name="filePath">The path to the file to analyze.</param>
    /// <returns>A FileStatistics object containing the analysis results.</returns>
    private FileStatistics AnalyzeFile(string filePath)
    {
        FileStatistics stats = new FileStatistics
        {
            filePath = GetProjectRelativePath(filePath),
            fileName = Path.GetFileName(filePath),
            extension = Path.GetExtension(filePath).ToLower()
        };

        try
        {
            string[] lines = File.ReadAllLines(filePath);
            stats.totalLines = lines.Length;

            bool inBlockComment = false;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                // Check for empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    stats.emptyLines++;
                    continue;
                }

                // Process multi-line comments
                if (inBlockComment)
                {
                    stats.commentLines++;
                    if (trimmedLine.Contains("*/"))
                    {
                        inBlockComment = false;
                    }
                    continue;
                }

                // Comment start
                if (trimmedLine.StartsWith("/*"))
                {
                    stats.commentLines++;
                    if (!trimmedLine.Contains("*/"))
                    {
                        inBlockComment = true;
                    }
                    continue;
                }

                // Single line comment
                if (trimmedLine.StartsWith("//"))
                {
                    stats.commentLines++;
                    continue;
                }

                // Code line with comment
                if (trimmedLine.Contains("//"))
                {
                    stats.codeLines++;
                    continue;
                }

                // Regular code line
                stats.codeLines++;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error analyzing file: {filePath} - {ex.Message}");
            stats.totalLines = 0;
        }

        return stats;
    }


    /// <summary>
    /// Exports the analysis results to a CSV file.
    /// </summary>
    /// <param name="path">The path where the CSV file will be saved.</param>
    private void ExportCSV(string path)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(path))
            {
                // Write header
                writer.WriteLine("File,Extension,Total Lines,Code Lines,Comment Lines,Empty Lines");

                // Write summary row
                writer.WriteLine($"TOTAL,{totalFiles} Files,{totalCodeLines + totalCommentLines + totalEmptyLines},{totalCodeLines},{totalCommentLines},{totalEmptyLines}");

                // Write file details
                foreach (var stats in fileStatsList)
                {
                    writer.WriteLine($"\"{stats.fileName}\",{stats.extension},{stats.totalLines},{stats.codeLines},{stats.commentLines},{stats.emptyLines}");
                }
            }

            Debug.Log($"Statistics exported to: {path}");

            // Open in default application
            Application.OpenURL("file://" + path);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error exporting CSV: {ex.Message}");
            EditorUtility.DisplayDialog("Export Error", ex.Message, "OK");
        }
    }


    /// <summary>
    /// Converts an absolute file path to a project-relative path.
    /// </summary>
    /// <param name="absolutePath">The absolute path to convert.</param>
    /// <returns>A project-relative path.</returns>
    private string GetProjectRelativePath(string absolutePath)
    {
        if (absolutePath.StartsWith(Application.dataPath))
        {
            return "Assets" + absolutePath.Substring(Application.dataPath.Length);
        }

        // Find project root directory
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        if (absolutePath.StartsWith(projectRoot))
        {
            return absolutePath.Substring(projectRoot.Length + 1); // +1 for the separator
        }

        return absolutePath;
    }
}
