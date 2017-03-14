using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml;

// srt format: http://www.visualsubsync.org/help/srt
namespace SmiToSrt
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private int number;
        private double seek;
        private string openFile;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnSelected(object sender, RoutedEventArgs e)
        {
            ListBoxItem item = sender as ListBoxItem;
            String fullpath = item.Tag as String;

            openFile = fullpath;

            LoadSubtitle(fullpath);
        }

        private void LoadSubtitle(string filename)
        {
            String content = String.Empty;

            seek = Double.Parse(tbSeconds.Text) * 1000;

            using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Encoding encoding = null;

                switch (cbEncoding.SelectedIndex)
                {
                    case 0:
                        encoding = new UnicodeEncoding();
                        break;
                    case 1:
                        encoding = new UTF8Encoding();
                        break;
                    default:
                        encoding = Encoding.Default;
                        break;
                }

                using (StreamReader reader = new StreamReader(stream, encoding))
                {
                    content = reader.ReadToEnd();
                }
            }

            try
            {
                FillToListView(ConvertToSrt(content));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "안내", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void FillToListView(Stream stream)
        {
            SubtitleObject subtitleObject = new SubtitleObject();

            List<List<String>> sections = new List<List<String>>();

            int number = 1;
            string strNumber = number.ToString();

            stream.Seek(0, SeekOrigin.Begin);

            using (StreamReader reader = new StreamReader(stream, Encoding.Unicode))
            {
                List<String> section = null;

                while (reader.EndOfStream == false)
                {
                    string line = reader.ReadLine();

                    if (strNumber == line)
                    {
                        section = new List<String>();
                        sections.Add(section);

                        number++;
                        strNumber = number.ToString();
                    }
                    section.Add(line);
                }
            }

            lvSubtitle.Items.Clear();

            foreach (var section in sections)
            {
                if (section[section.Count - 1] == "")
                {
                    section.RemoveAt(section.Count - 1);
                }

                Section currentSection = Section.INDEX;
                StringBuilder text = new StringBuilder();

                foreach (string line in section)
                {
                    if (currentSection == Section.INDEX)
                    {
                        subtitleObject.Index = Int32.Parse(line);

                        currentSection = Section.TIME;
                    }
                    else if (currentSection == Section.TIME)
                    {
                        string[] fields = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        subtitleObject.Start = TimeSpan.Parse(fields[0].Replace(',', '.'));
                        subtitleObject.Stop = TimeSpan.Parse(fields[2].Replace(',', '.'));

                        currentSection = Section.TEXT;
                    }
                    else if (currentSection == Section.TEXT)
                    {
                        if (text.Length != 0)
                        {
                            text.AppendLine();
                        }
                        text.Append(line);
                    }
                }

                subtitleObject.Text = text.ToString();
                lvSubtitle.Items.Add(subtitleObject);

                subtitleObject = new SubtitleObject();
            }
        }

        private void OnSelectFolderClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();

            ofd.Filter = "Sami file (*.smi)|*.smi";

            if (ofd.ShowDialog() == true)
            {
                SelectFolder(Path.GetDirectoryName(ofd.FileName), Path.GetFileName(ofd.FileName));
            }
        }

        private void SelectFolder(string path, string selectFile)
        {
            int index = -1;
            int selected = -1;

            tbPath.Text = path;
            lbFiles.Items.Clear();

            foreach (String fullpath in Directory.GetFiles(path, "*.smi"))
            {
                string filename = Path.GetFileName(fullpath);

                ListBoxItem item = new ListBoxItem() { Content = Path.GetFileName(filename), Tag = fullpath };
                lbFiles.Items.Add(item);

                index++;

                if (filename == selectFile)
                {
                    selected = index;
                }
            }

            lbFiles.UpdateLayout();
            lbFiles.SelectedIndex = selected;
        }

        private XmlNode GetParent(XmlNode currentNode, String name)
        {
            name = name.ToLower();

            XmlNode orignal = currentNode;
            currentNode = currentNode.ParentNode;

            while (currentNode.Name.ToLower() != name)
            {
                currentNode = currentNode.ParentNode;

                if (currentNode == null)
                {
                    break;
                }
            }

            if (currentNode != null)
            {
                return currentNode;
            }
            if (orignal.Name.ToLower() == name)
            {
                return orignal;
            }
            return null;
        }

        private XmlNode InsertTag(XmlDocument doc, XmlNode currentNode, String tag)
        {
            string[] segments = tag.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string tagname = segments[0].ToUpper();

            if (tagname == "SYNC")
            {
                XmlNode parentNode = GetParent(currentNode, tagname);

                if (parentNode != null)
                {
                    currentNode = parentNode.ParentNode;
                }
            }
            if (tagname.StartsWith("!--"))
            {
                return currentNode;
            }

            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];

                if (i != 0)
                {
                    string[] attribute = segment.Split('=');
                    string name = attribute[0];
                    string value = attribute.Length == 2 ? attribute[1] : String.Empty;

                    if (value.Length > 0 && value[0] == '"')
                    {
                        currentNode.Attributes.Append(doc.CreateAttribute(name)).Value = value.Substring(1, value.Length - 2);
                    }
                    else if (name != "/") // <br /> 넘기기
                    {
                        currentNode.Attributes.Append(doc.CreateAttribute(name)).Value = value;
                    }
                }
                else if (segment[0] == '/')
                {
                    string parent = segment.Substring(1);
                    XmlNode parentNode = GetParent(currentNode, parent);

                    if (parentNode != null)
                    {
                        currentNode = currentNode.ParentNode;
                    }
                }
                else
                {
                    if (tagname == "BR")
                    {
                        currentNode.AppendChild(doc.CreateElement(tagname));
                    }
                    else
                    {
                        currentNode = currentNode.AppendChild(doc.CreateElement(tagname));
                    }
                }
            }

            if (tagname == "SYNC")
            {
                currentNode = GetParent(currentNode, tagname);
            }
            return currentNode;
        }

        private void InsertContent(XmlDocument doc, XmlNode currentNode, String content)
        {
            content = content.Trim();

            if (content.Length == 0)
            {
                return;
            }
            currentNode.AppendChild(doc.CreateTextNode(content));
        }

        private void WriteChildNodeToSami(XmlNode current, StreamWriter writer, int indent)
        {
            if (current is XmlText)
            {
                writer.Write(current.Value);
            }
            else
            {
                string name = current.Name;

                bool isP = name.Equals("p", StringComparison.InvariantCultureIgnoreCase);
                bool isBr = name.Equals("br", StringComparison.InvariantCultureIgnoreCase);
                bool isSami = name.Equals("sami", StringComparison.InvariantCultureIgnoreCase);
                bool isBody = name.Equals("body", StringComparison.InvariantCultureIgnoreCase);
                bool isSync = name.Equals("sync", StringComparison.InvariantCultureIgnoreCase);

                if (isP && indent != 0)
                {
                    writer.Write(new String(' ', indent * 2));
                }

                if (current.Attributes.Count == 0)
                {
                    writer.Write(String.Format("<{0}>", current.Name));
                }
                else
                {
                    writer.Write("<");
                    writer.Write(current.Name);

                    for (int i = 0; i < current.Attributes.Count; i++)
                    {
                        string attrname = current.Attributes[i].Name;
                        attrname = attrname.Substring(0, 1).ToUpper() + attrname.Substring(1).ToLower();

                        writer.Write(" ");
                        writer.Write(attrname);
                        writer.Write("=");

                        if (isSync && attrname == "Start")
                        {
                            current.Attributes[i].Value = String.Format("{0:F0}", Int32.Parse(current.Attributes[i].Value) + seek);
                        }

                        if (isP || isSync)
                        {
                            writer.Write(current.Attributes[i].Value);
                        }
                        else
                        {
                            writer.Write('"');
                            writer.Write(current.Attributes[i].Value);
                            writer.Write('"');
                        }
                    }

                    writer.Write(">");
                }

                if (isSync || isSami || isBody)
                {
                    writer.WriteLine();
                }

                int nextIndent = indent;

                if (isSync)
                {
                    nextIndent++;
                }

                foreach (XmlNode xmlNode in current.ChildNodes)
                {
                    WriteChildNodeToSami(xmlNode, writer, nextIndent);
                }

                if (isBr == false)
                {
                    writer.Write(String.Format("</{0}>", current.Name));
                }

                if (isSync || isBody || isP)
                {
                    writer.WriteLine();
                }
                if (isSync)
                {
                    writer.WriteLine();
                }
            }
        }

        private double GetStart(XmlNode node)
        {
            foreach (XmlAttribute attribute in node.Attributes)
            {
                if (attribute.Name.Equals("start", StringComparison.InvariantCultureIgnoreCase))
                {
                    return double.Parse(attribute.Value);
                }
            }
            throw new KeyNotFoundException("start 속성이 없습니다.");
        }

        private bool IsWhiteSpace(XmlNode node)
        {
            if (node.ChildNodes.Count != 1)
            {
                return false;
            }
            if (node.ChildNodes.Count == 1)
            {
                XmlNode child = node.ChildNodes[0];

                if (child is XmlText)
                {
                    return String.IsNullOrWhiteSpace(child.Value) || child.Value.Equals("&nbsp;", StringComparison.InvariantCultureIgnoreCase);
                }
                if (child.ChildNodes.Count == 1)
                {
                    child = child.ChildNodes[0];

                    if (child is XmlText)
                    {
                        return String.IsNullOrWhiteSpace(child.Value) || child.Value.Equals("&nbsp;", StringComparison.InvariantCultureIgnoreCase);
                    }
                }
                return false;
            }
            return true;
        }

        private void WriteChildNodeToSrt(XmlNode current, StreamWriter writer, int indent)
        {
            if (current is XmlText)
            {
                writer.Write(current.Value);
            }
            else
            {
                string name = current.Name.ToLower();

                bool isU = name.Equals("u");
                bool isI = name.Equals("i");
                bool isB = name.Equals("b");
                bool isBr = name.Equals("br");
                bool isFont = name.Equals("font");
                bool isSync = name.Equals("sync");
                bool isTitle = name.Equals("title");

                if (isTitle)
                {
                    return;
                }

                if (isBr)
                {
                    writer.WriteLine();
                }

                if (isSync)
                {
                    if (IsWhiteSpace(current))
                    {
                        return;
                    }
                    number++;

                    XmlNode nextNode = current.NextSibling;
                    TimeSpan begin = TimeSpan.FromMilliseconds(GetStart(current) + seek);
                    TimeSpan end;

                    if (nextNode != null)
                    {
                        end = TimeSpan.FromMilliseconds(GetStart(nextNode) + seek);
                    }
                    else
                    {
                        end = begin.Add(TimeSpan.FromSeconds(5));
                    }

                    writer.WriteLine(number.ToString());
                    writer.WriteLine(String.Format("{0} --> {1}", begin.ToString(@"hh\:mm\:ss\,fff"), end.ToString(@"hh\:mm\:ss\,fff")));
                }

                if (isI || isU || isFont)
                {
                    if (current.Attributes.Count == 0)
                    {
                        writer.Write(String.Format("<{0}>", name));
                    }
                    else
                    {
                        writer.Write("<");
                        writer.Write(name);

                        for (int i = 0; i < current.Attributes.Count; i++)
                        {
                            string attrname = current.Attributes[i].Name;
                            string attrvalue = current.Attributes[i].Value;

                            attrname = attrname.ToLower();

                            writer.Write(" ");
                            writer.Write(attrname);
                            writer.Write("=");
                            writer.Write('"');
                            writer.Write(attrvalue);
                            writer.Write('"');
                        }

                        writer.Write(">");
                    }
                }

                foreach (XmlNode node in current.ChildNodes)
                {
                    WriteChildNodeToSrt(node, writer, indent);
                }

                if (isI || isU || isFont)
                {
                    writer.Write(String.Format("</{0}>", name));
                }

                if (isSync)
                {
                    writer.WriteLine();
                    writer.WriteLine();
                }
            }
        }

        private Stream ConvertToSrt(string content)
        {
            StringBuilder innerText = new StringBuilder();

            XmlDocument xmlDoc = new XmlDocument();
            XmlNode current = xmlDoc.AppendChild(xmlDoc.CreateElement("SAMI"));

            current = current.AppendChild(xmlDoc.CreateElement("BODY"));

            int offset = 0;
            int length = content.Length;

            for (int i = 0; i < length; i++)
            {
                char ch = content[i];

                if (ch == '<' && length > (i + 5))
                {
                    if (content[i + 1] != 'S' && content[i + 1] != 's')
                    {
                        continue;
                    }
                    if (content[i + 2] != 'Y' && content[i + 2] != 'y')
                    {
                        continue;
                    }
                    if (content[i + 3] != 'N' && content[i + 3] != 'n')
                    {
                        continue;
                    }
                    if (content[i + 4] != 'C' && content[i + 4] != 'c')
                    {
                        continue;
                    }
                    offset = i;

                    break;
                }
            }

            for (int i = offset; i < length; i++)
            {
                char ch = content[i];

                if (ch == '<')
                {
                    if (innerText.Length > 0)
                    {
                        InsertContent(xmlDoc, current, innerText.ToString());

                        innerText.Clear();
                    }

                    int closeIdx = content.IndexOf('>', i);
                    if (closeIdx == -1)
                    {
                        return null;
                    }

                    int sublen = closeIdx - i - 1;

                    string tag = content.Substring(i + 1, sublen);

                    i = closeIdx;

                    current = InsertTag(xmlDoc, current, tag);
                }
                else
                {
                    innerText.Append(ch);
                }
            }

            if (innerText.Length > 0)
            {
                InsertContent(xmlDoc, current, innerText.ToString());

                innerText.Clear();
            }

            current = xmlDoc.DocumentElement;

            XmlNodeList syncNodes = xmlDoc.SelectNodes("/SAMI/BODY/SYNC");

            using (MemoryStream stream = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(stream, new UnicodeEncoding()))
                {
                    number = 0;

                    WriteChildNodeToSrt(current, writer, 0);
                }
                return new MemoryStream(stream.ToArray());
            }
        }

        private void OnReloadClick(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadSubtitle(openFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "안내", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            string location = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string filename = Path.GetFileNameWithoutExtension(openFile);
            string savepath = Path.Combine(location, "output");
            string fullpath = Path.Combine(savepath, filename + ".srt");

            if (Directory.Exists(savepath) == false)
            {
                Directory.CreateDirectory(savepath);
            }

            using (FileStream stream = new FileStream(fullpath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                using (StreamWriter writer = new StreamWriter(stream, Encoding.Unicode))
                {
                    for (int i = 0; i < lvSubtitle.Items.Count; i++)
                    {
                        SubtitleObject subtitleObject = lvSubtitle.Items[i] as SubtitleObject;

                        writer.WriteLine(subtitleObject.Index.ToString());
                        writer.WriteLine(String.Format("{0} --> {1}", subtitleObject.Start.ToString(@"hh\:mm\:ss\,fff"), subtitleObject.Stop.ToString(@"hh\:mm\:ss\,fff")));
                        writer.WriteLine(subtitleObject.Text);
                        writer.WriteLine();
                    }
                }
            }
        }
    }
}
