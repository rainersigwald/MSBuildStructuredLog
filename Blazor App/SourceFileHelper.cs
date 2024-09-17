using StructuredLogViewerWASM.Pages;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer;
using System.ComponentModel;
using Microsoft.Language.Xml;
using System.Linq;

namespace StructuredLogViewerWASM
{
    public class SourceFileHelper
    {
        /// <summary>
        /// Determines the SourceFile (text, name, line number) from the tree node
        /// </summary>
        /// <param name="fileResolver"> Either the Source or Archive File Resolver to read file from </param>
        /// <param name="node">BaseNode to be reading file from</param>
        public static object[] SourceFileText(ISourceFileResolver fileResolver, BaseNode node)
        {
            string path = "";
            string sourceFileText = null;
            string sourceFileName = "";
            int sourceFileLineNumber = -1;

            if (node is AbstractDiagnostic diagNode)
            {
                path = diagNode.ProjectFile;
                if (diagNode.IsTextShortened)
                {
                    sourceFileText = diagNode.Text;
                    sourceFileName = diagNode.ShortenedText;
                }
                else
                {
                    sourceFileText = fileResolver.GetSourceFileText(path).Text;
                    sourceFileName = diagNode.Text;
                }
                sourceFileLineNumber = diagNode.LineNumber;

            }
            else if (node is Project projectNode)
            {
                path = projectNode.SourceFilePath;
                sourceFileName = projectNode.Name;
                sourceFileText = fileResolver.GetSourceFileText(path).Text;
            }
            else if (node is Target targetNode)
            {
                path = targetNode.SourceFilePath;
                sourceFileName = targetNode.Name;
                sourceFileText = fileResolver.GetSourceFileText(path).Text;
                sourceFileLineNumber = TargetLineNumber(fileResolver.GetSourceFileText(path), sourceFileName);
            }
            else if (node is Microsoft.Build.Logging.StructuredLogger.Task taskNode)
            {
                path = taskNode.SourceFilePath;
                sourceFileName = taskNode.Name;
                sourceFileText = fileResolver.GetSourceFileText(path).Text;
                sourceFileLineNumber = TaskLineNumber(fileResolver.GetSourceFileText(path), taskNode.Parent, sourceFileName);
            }
            else if (node is IHasSourceFile && ((IHasSourceFile)node).SourceFilePath != null)
            {
                path = ((IHasSourceFile)node).SourceFilePath;
                sourceFileName = ((IHasSourceFile)node).SourceFilePath;
                sourceFileText = fileResolver.GetSourceFileText(path).Text;
            }
            else if (node is SourceFileLine && ((SourceFileLine)node).Parent is Microsoft.Build.Logging.StructuredLogger.SourceFile
            && ((Microsoft.Build.Logging.StructuredLogger.SourceFile)((SourceFileLine)node).Parent).SourceFilePath != null)
            {
                path = ((Microsoft.Build.Logging.StructuredLogger.SourceFile)((SourceFileLine)node).Parent).SourceFilePath;
                sourceFileName = ((Microsoft.Build.Logging.StructuredLogger.SourceFile)((SourceFileLine)node).Parent).Name;
                sourceFileText = fileResolver.GetSourceFileText(path).Text;
                sourceFileLineNumber = ((SourceFileLine)node).LineNumber;
            }
            else if (node is NameValueNode nameValueNode && nameValueNode.IsValueShortened)
            {
                sourceFileText = nameValueNode.Value;
                sourceFileName = nameValueNode.Name;
            }
            else if (node is TextNode textNode && textNode.IsTextShortened)
            {
                sourceFileText = textNode.Text;
                sourceFileName = textNode.ShortenedText;
            }

            if (sourceFileText is null)
            {
                sourceFileText = "No file to display";
            }

            string[] fileParts = path.Split(".");
            string fileExtension = fileParts[fileParts.Length - 1];
            if (fileExtension.Equals("csproj") || fileExtension.Equals("metaproj") || fileExtension.Equals("targets") || fileExtension.Equals("props"))
            {
                fileExtension = "xml";
            }
            object[] sourceFileResults = new object[4];
            sourceFileResults[0] = sourceFileName;
            sourceFileResults[1] = sourceFileText;
            sourceFileResults[2] = sourceFileLineNumber;
            sourceFileResults[3] = fileExtension;
            return sourceFileResults;
        }

        /// <summary>
        /// Finds the line number for a Task
        /// </summary>
        /// <param name="text"> The file information for the target node </param>
        /// <param name="parent"> Target the task should reside in </param>
        /// <param name="name"> Name of the task to highlight </param>
        /// <returns> Line number to highlight</returns>
        public static int  TaskLineNumber(SourceText text, TreeNode parent, string name)
        {
            Target target = parent as Target;
            if (target == null)
            {
                return -1;
            }
            return TargetLineNumber(text, target.Name, name);
        }

        /// <summary>
        /// Finds the line number for a Target
        /// </summary>
        /// <param name="text"> The file information for the target node </param>
        /// <param name="targetName"> Name of the target to find in the file </param>
        /// <param name="taskName"> Name of the task to find in the file </param>
        /// <returns> Line number to highlight</returns>
        public static int TargetLineNumber(SourceText text, string targetName, string taskName = null)
        {
            var xml = text.XmlRoot;
            IXmlElement root = xml.Root;
            int startPosition = 0;
            int line = 0;

            // work around a bug in Xml Parser where a virtual parent is created around the root element
            // when the root element is preceded by trivia (comment)
            if (root.Name == null && root.Elements.FirstOrDefault() is IXmlElement firstElement && firstElement.Name == "Project")
            {
                root = firstElement;
            }

            foreach (var element in root.Elements)
            {
                if (element.Name == "Target" && element.Attributes != null)
                {
                    var nameAttribute = element.AsSyntaxElement.Attributes.FirstOrDefault(a => a.Name == "Name" && a.Value == targetName);
                    if (nameAttribute != null)
                    {
                        startPosition = nameAttribute.ValueNode.Start;

                        if (taskName != null)
                        {
                            var tasks = element.Elements.Where(e => e.Name == taskName).ToArray();
                            if (tasks.Length == 1)
                            {
                                startPosition = tasks[0].AsSyntaxElement.NameNode.Start;
                            }
                        }

                        break;
                    }
                }
            }

            if (startPosition > 0)
            {
                line = text.GetLineNumberFromPosition(startPosition);
            }

            return  line + 1;
        }
    }
}
