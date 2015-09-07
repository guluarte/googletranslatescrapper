using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Windows.Forms;
using Gecko;
using Gecko.Events;
using HtmlAgilityPack;

namespace PinterestBotC
{
    public partial class Form1 : Form
    {
        private const string GOOGLE_URL = "https://translate.google.com/#{0}/{1}/{2}";
        private const string AppName = "PINTEREST_BOT";
        private string xulPath = "";
        private string profileDirectory = "";
        private int scrollNum = 0;
        private string localAppDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\"+AppName+@"\";


        private Queue<string> wordsToScrap = new Queue<string>();
        private List<string> listWordScrapped = new List<string>(); 

        public Form1()
        {
            InitializeComponent();

            xulPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            xulPath = xulPath.Substring(0, xulPath.LastIndexOf(@"\") + 1) + @"xulrunner-sdk\bin";

            Random random = new Random();
            profileDirectory = ProfileDirectory("DefaultProfile" + 3);

            Xpcom.ProfileDirectory = profileDirectory;

            Xpcom.Initialize(xulPath);

           
            
        }

        private void Form1_Load(object sender, EventArgs e)
        {

           toolStripStatusLabel.Text = "New profile directory = " + profileDirectory;

        }

        private void geckoWebBrowser_DocumentCompleted(object sender, GeckoDocumentCompletedEventArgs e)
        {
            ScrapeElements();
        }

        private void ScrapeElements()
        {
            GeckoHtmlElement element;
            var geckoDomElement = geckoWebBrowser.Document.DocumentElement;
            if (geckoDomElement is GeckoHtmlElement)
            {
                element = (GeckoHtmlElement)geckoDomElement;
                var innerHtml = element.InnerHtml;

                HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(innerHtml);

                var translationCollection =
                    doc.DocumentNode.Descendants("span")
                        .Where(
                            d =>
                                d.Attributes.Contains("id") &&
                                d.Attributes["id"].Value.Contains("result_box"));

                var translation = translationCollection.FirstOrDefault();

                var otherTranslations =
                    doc.DocumentNode.Descendants("div")
                        .Where(
                            d =>
                                d.Attributes.Contains("class") &&
                                d.Attributes["class"].Value.Contains("gt-baf-word-clickable"));

                
                var moreTranslations =
                    doc.DocumentNode.Descendants("span")
                        .Where(
                            d =>
                                d.Attributes.Contains("class") &&
                                d.Attributes["class"].Value.Contains("gt-baf-back"));
               
                var seeAlso =
                   doc.DocumentNode.Descendants("span")
                       .Where(
                           d =>
                               d.Attributes.Contains("class") &&
                               d.Attributes["class"].Value.Contains("gt-cd-cl"));

                var definitions =
                   doc.DocumentNode.Descendants("div")
                       .Where(
                           d =>
                               d.Attributes.Contains("class") &&
                               d.Attributes["class"].Value.Contains("gt-def-row"));

                var examples =
                   doc.DocumentNode.Descendants("div")
                       .Where(
                           d =>
                               d.Attributes.Contains("class") &&
                               d.Attributes["class"].Value.Contains("gt-def-example"));

                pushLine(currentWord, translation.InnerText, otherTranslations, moreTranslations,  seeAlso, definitions, examples);

            }

            isGeckoFetching = false;
        }


        private void writeNode(IEnumerable<HtmlNode> examples)
        {
            foreach (var htmlNode in examples)
            {
                Console.WriteLine(htmlNode.InnerText);
            }
        }

        private string getCsvField(IEnumerable<HtmlNode> examples, string separator)
        {
            var field = "\"";
            foreach (var htmlNode in examples)
            {
                field += htmlNode.InnerText.Replace("\"", "") + separator;
            }

            field = field.TrimEnd(' ').TrimEnd(',');
            field += "\"";
            return field;
        }

        private StreamWriter textStream = new StreamWriter(@"output.txt");

        private void pushLine(string word, string trasnlation, IEnumerable<HtmlNode> otherTranslations, IEnumerable<HtmlNode> moreTranslations, IEnumerable<HtmlNode> seeAlso, IEnumerable<HtmlNode> definitions,
            IEnumerable<HtmlNode> examples)
        {
            var field = "";

            field += "\"" + word.Replace("\"", "") + "\",";
            field += "\"" + trasnlation.Replace("\"", "") + "\",";
            
            field += getCsvField(otherTranslations, ", ") + ",";
            field += getCsvField(moreTranslations, ", ") + ",";
            field += getCsvField(seeAlso, ", ") + ",";
            field += getCsvField(definitions, "<br />") + ",";
            field += getCsvField(examples, "<br />");


            textStream.WriteLine(field);
            textStream.Flush();
        }

        private string ProfileDirectory(string profileName)
        {
            string profileDirectory = localAppDirectory + profileName;

            if (!Directory.Exists(profileDirectory))
            {
                Directory.CreateDirectory(profileDirectory);
            }
            return profileDirectory;
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {

        }

        private bool isGeckoFetching = false;
        private string currentWord = String.Empty;
        private void btnStart_Click(object sender, EventArgs e)
        {
            using (StringReader sr = new StringReader(txtText.Text))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.ToLower();

                    if (!wordsToScrap.Contains(line) && !string.IsNullOrWhiteSpace(line))
                    {
                        wordsToScrap.Enqueue(line);

                        var words = GetWords(line);

                        foreach (var word in words)
                        {
                            var normalizedWord = word.ToLower().Replace("qqoottee", "'");
                            if (!wordsToScrap.Contains(normalizedWord))
                            {
                                wordsToScrap.Enqueue(normalizedWord);
                            }
                        }

                    }
                }//while

            }//using

            while (wordsToScrap.Count > 0)
            {
                currentWord = wordsToScrap.Peek();
                toolStripStatusLabel.Text = String.Format("Fetching {0}", currentWord);
                isGeckoFetching = true;
                geckoWebBrowser = null;
                geckoWebBrowser = new GeckoWebBrowser();
                geckoWebBrowser.DocumentCompleted += geckoWebBrowser_DocumentCompleted;
                var url = String.Format(GOOGLE_URL, "fr", "en", HttpUtility.UrlEncode(currentWord));
                geckoWebBrowser.Navigate(url);

                while (isGeckoFetching)
                {
                    Application.DoEvents();
                }

                wordsToScrap.Dequeue();
            }

            MessageBox.Show("Done");

        }

        static string[] GetWords(string input)
        {
            input = input.Replace("'", "qqoottee");
            input = input.Replace("’", "qqoottee");
            
            MatchCollection matches = Regex.Matches(input, @"\b[\w']*\b");

            var words = from m in matches.Cast<Match>()
                        where !string.IsNullOrEmpty(m.Value)
                        select TrimSuffix(m.Value);

            return words.ToArray();
        }

        static string TrimSuffix(string word)
        {
            int apostropheLocation = word.IndexOf('\'');
            if (apostropheLocation != -1)
            {
                word = word.Substring(0, apostropheLocation);
            }

            return word;
        }
        private void btnCapture_Click(object sender, EventArgs e)
        {
            using (StringReader sr = new StringReader(txtText.Text))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.ToLower();

                    if (!wordsToScrap.Contains(line) && !string.IsNullOrWhiteSpace(line))
                    {
                        wordsToScrap.Enqueue(line);

                        if (chkWords.Checked)
                        {
                            var words = GetWords(line);

                            foreach (var word in words)
                            {
                                var normalizedWord = word.ToLower().Replace("qqoottee", "'");
                                if (!wordsToScrap.Contains(normalizedWord))
                                {
                                    wordsToScrap.Enqueue(normalizedWord);
                                }
                            }
                        }

                    }
                }//while

            }//using

            while (wordsToScrap.Count > 0)
            {
                currentWord = wordsToScrap.Peek();
                toolStripStatusLabel.Text = String.Format("Fetching {0}", currentWord);
                isGeckoFetching = true;
                
                geckoWebBrowser.DocumentCompleted += geckoBrowser_GoogleImages;
                var url = String.Format("https://www.google.fr/search?q={0}&biw=1280&bih=911&source=lnms&tbm=isch&sa=X", HttpUtility.UrlEncode(currentWord));
                geckoWebBrowser.Navigate(url);

                while (isGeckoFetching)
                {
                    Application.DoEvents();
                }

                wordsToScrap.Dequeue();
            }

            MessageBox.Show("Done");
        }

        private void geckoBrowser_GoogleImages(object sender, GeckoDocumentCompletedEventArgs e)
        {
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(geckoWebBrowser.Width, geckoWebBrowser.Height);
            geckoWebBrowser.DrawToBitmap(bmp, geckoWebBrowser.Bounds);

            bmp.Save(string.Format("Images/{0}.png", currentWord));

            isGeckoFetching = false;
        }
    }
}
