﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace CCExtractorTester
{
	/// <summary>
	/// The class that does the heavy lifting in this application.
	/// </summary>
	public class Tester
	{
		/// <summary>
		/// A list of the TestEntry instances that will be processed.
		/// </summary>
		/// <value>The entries.</value>
		public List<TestEntry> Entries { get; private set; } 
		/// <summary>
		/// Gets or sets the progress reporter that will be used.
		/// </summary>
		/// <value>The progress reporter.</value>
		private IProgressReportable ProgressReporter { get; set; }
		/// <summary>
		/// Gets or sets the comparer that will be used by the tester.
		/// </summary>
		/// <value>The comparer.</value>
		private IFileComparable Comparer { get; set; }
		/// <summary>
		/// Gets or sets the configuration instance that will be used.
		/// </summary>
		/// <value>The config.</value>
		private ConfigurationSettings Config { get; set; }
		/// <summary>
		/// Gets or sets the logger.
		/// </summary>
		/// <value>The logger.</value>
		private ILogger Logger { get; set; }
		/// <summary>
		/// Gets or sets the performance logger.
		/// </summary>
		/// <value>The performance logger.</value>
		private IPerformanceLogger PerformanceLogger { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CCExtractorTester.Tester"/> class.
		/// </summary>
		/// <param name="cfg">The configuration that will be used.</param>
		/// <param name="logger">The logger that will be used.</param>
		public Tester(ConfigurationSettings cfg,ILogger logger){
			Entries = new List<TestEntry> ();
			ProgressReporter = NullProgressReporter.Instance;
			Config = cfg;
			Logger = logger;
			LoadPerformanceLogger ();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CCExtractorTester.Tester"/> class.
		/// </summary>
		/// <param name="cfg">The configuration that will be used.</param>
		/// <param name="logger">The logger that will be used.</param>
		/// <param name="xmlFile">The XML file containing all test entries.</param>
		public Tester (ConfigurationSettings cfg,ILogger logger,string xmlFile) : this(cfg,logger)
		{
			if (!String.IsNullOrEmpty(xmlFile)) {
				loadAndParseXML (xmlFile);
			}
		}

		/// <summary>
		/// Sets the comparer based on the "Comparer" config setting.
		/// </summary>
		void LoadComparer ()
		{
			switch (Config.GetAppSetting ("Comparer")) {
			case "diff":
				Comparer = new DiffLinuxComparer ();
				break;
			case "diffplexreduced":
				Comparer = new DiffToolComparer (true);
				break;
			case "diffplex":
				// Fall-through to default.
			default:
				Comparer = new DiffToolComparer (false);
				break;
			}

		}

		/// <summary>
		/// Loads the performance logger, based on the used platform.
		/// </summary>
		void LoadPerformanceLogger ()
		{
			switch (Environment.OSVersion.Platform) {
			case PlatformID.Win32NT:
				PerformanceLogger = new WindowsPerformanceCounters ();
				break;			
			default:
				PerformanceLogger = NullPerformanceLogger.Instance;
				break;
			}
			
		}

		/// <summary>
		/// Saves the entries to XML on a given fileName.
		/// </summary>
		/// <param name="fileName">The file name to save the XML to.</param>
		public void SaveEntriesToXML(string fileName){
			XmlDocument doc = new XmlDocument ();
			doc.LoadXml (@"<?xml version=""1.0"" encoding=""UTF-8""?><tests></tests>");
			XmlNode root = doc.DocumentElement;
			foreach (TestEntry te in Entries) {
				XmlNode t = doc.CreateElement ("test");

				XmlNode sample = doc.CreateElement ("sample");
				sample.InnerText = te.TestFile;
				t.AppendChild (sample);
				XmlNode cmd = doc.CreateElement ("cmd");
				cmd.InnerText = te.Command;
				t.AppendChild (cmd);
				XmlNode result = doc.CreateElement ("result");
				result.InnerText = te.ResultFile;
				t.AppendChild (result);
				root.AppendChild (t);
			}
			doc.Save (fileName);
		}


		/// <summary>
		/// Loads the given XML file and tries to parse it into XML.
		/// </summary>
		/// <param name="xmlFileName">The location of the XML file to load and parse.</param>
		void loadAndParseXML (string xmlFileName)
		{
			if (File.Exists (xmlFileName)) {
				ValidateXML (xmlFileName);
				XmlDocument doc = new XmlDocument ();
				using(FileStream fs = new FileStream(xmlFileName,FileMode.Open)){
					doc.Load (fs);
					foreach (XmlNode node in doc.SelectNodes("//test")) {
						XmlNode sampleFile = node.SelectSingleNode ("sample");
						XmlNode command = node.SelectSingleNode ("cmd");
						XmlNode resultFile = node.SelectSingleNode ("result");
						Entries.Add(new TestEntry(ConvertFolderDelimiters(sampleFile.InnerText),command.InnerText,ConvertFolderDelimiters(resultFile.InnerText)));
					}
				}
				return;
			}
			throw new InvalidDataException ("File does not exist");
		}

		/// <summary>
		/// Validates the given XML at the location against our scheme.
		/// </summary>
		/// <param name="xmlFileName">The location of the XML file to validate.</param>
		void ValidateXML (string xmlFileName)
		{
			using (StringReader sr = new StringReader (Resources.tests)) {
				XmlReader r = XmlReader.Create (sr);
				XmlReaderSettings settings = new XmlReaderSettings ();
				settings.Schemas.Add (null, r);
				settings.ValidationType = ValidationType.Schema;
				settings.ValidationEventHandler += new System.Xml.Schema.ValidationEventHandler (settings_ValidationEventHandler);
				using (FileStream fs = new FileStream (xmlFileName, FileMode.Open)) {
					var reader = XmlReader.Create (fs, settings);
				}
			}
		}

		/// <summary>
		/// Converts the folder delimiters between platforms to ensure no issues when running test xmls created on another platform.
		/// </summary>
		/// <returns>The converted path.</returns>
		/// <param name="path">The path to check and if necessary, convert.</param>
		string ConvertFolderDelimiters (string path)
		{
			char env = '\\';
			switch (Environment.OSVersion.Platform) {
			case PlatformID.Win32NT:
			case PlatformID.Win32S:
			case PlatformID.Win32Windows:
			case PlatformID.WinCE:
				env = '/';
					break;			
				default:					
					break;
			}
			return path.Replace (env, Path.DirectorySeparatorChar);
		}

		/// <summary>
		/// The validation event handler for the settings XSD scheme.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		void settings_ValidationEventHandler (object sender, System.Xml.Schema.ValidationEventArgs e)
		{
			throw new InvalidDataException ("XML File is not formatted correctly");
		}

		/// <summary>
		/// Runs the tests.
		/// </summary>
		public void RunTests(){
			String cce = Config.GetAppSetting ("CCExtractorLocation");
			if (!File.Exists (cce)) {
				throw new InvalidOperationException ("CCExtractor location ("+cce+") is not a valid file/executable");
			}
			String sourceFolder = Config.GetAppSetting ("SampleFolder");
			if (!Directory.Exists (sourceFolder)) {
				throw new InvalidOperationException ("Sample folder does not exist!");
			}

			LoadComparer ();

			String location = System.Reflection.Assembly.GetExecutingAssembly ().Location;
			location = location.Remove (location.LastIndexOf (Path.DirectorySeparatorChar));
			if(!Directory.Exists(Path.Combine(location,"tmpFiles"))){
				Directory.CreateDirectory (Path.Combine (location, "tmpFiles"));
			}

			bool useThreading = false;
			if (!String.IsNullOrEmpty(Config.GetAppSetting ("UseThreading")) && Config.GetAppSetting("UseThreading") == "true") {
				useThreading = true;
				Logger.Info ("Using threading");
			}

			int i = 1;
			SetUpTestEntryProcessing (cce, location,sourceFolder,Entries.Count);
			ManualResetEvent[] mres = new ManualResetEvent[Entries.Count];
			DateTime start = DateTime.Now;

			foreach (TestEntry te in Entries) {
				mres [i-1] = new ManualResetEvent (false);
				TestEntryProcessing tep = new TestEntryProcessing (te, i);
				tep.eventX = mres[i-1];

				if (useThreading) {
					ThreadPool.QueueUserWorkItem (new WaitCallback (tep.Process));
				} else {
					tep.Process (null);
				}

				i++;
			}
			if (useThreading) {
				WaitHandle.WaitAll (mres);
			}
			DateTime end = DateTime.Now;
			Logger.Info ("Runtime: "+(end.Subtract(start)).ToString());
			Comparer.SaveReport (Config.GetAppSetting ("ReportFolder"), new ResultData (){ CCExtractorVersion = cce + " " + DateTime.Now.ToShortDateString () });
			Comparer = null;
		}

		/// <summary>
		/// Sets up test entry processing.
		/// </summary>
		/// <param name="cce">The CCExtractor executable location.</param>
		/// <param name="location">The folder from where this program is executed.</param>
		/// <param name="sourceFolder">The folder with the samples in.</param>
		/// <param name="total">The number of test entries to process.</param>
		void SetUpTestEntryProcessing (string cce, string location,string sourceFolder, int total)
		{
			TestEntryProcessing.location = location;
			TestEntryProcessing.sourceFolder = sourceFolder;
			TestEntryProcessing.total = total;
			TestEntryProcessing.comparer = Comparer;
			TestEntryProcessing.config = Config;
			TestEntryProcessing.logger = Logger;
			TestEntryProcessing.progressReporter = ProgressReporter;
			TestEntryProcessing.runner = new Runner (cce, Logger, PerformanceLogger);
		}

		/// <summary>
		/// Sets the progress reporter.
		/// </summary>
		/// <param name="progressReporter">Progress reporter.</param>
		public void SetProgressReporter (IProgressReportable progressReporter)
		{
			ProgressReporter = progressReporter;
		}

		/// <summary>
		/// Internal class that processes a single entry.
		/// </summary>
		class TestEntryProcessing {
			public static Runner runner;
			public static string sourceFolder;
			public static string location;
			public static int total;
			public static ILogger logger;
			public static IFileComparable comparer;
			public static IProgressReportable progressReporter;
			public static ConfigurationSettings config;

			private TestEntry te;
			private int current;

			public ManualResetEvent eventX;

			/// <summary>
			/// Initializes a new instance of the <see cref="CCExtractorTester.Tester+TestEntryProcessing"/> class.
			/// </summary>
			/// <param name="testingEntry">Testing entry.</param>
			/// <param name="curr">Curr.</param>
			public TestEntryProcessing(TestEntry testingEntry,int curr){
				te = testingEntry;
				current = curr;
			}

			/// <summary>
			/// Process the current entry.
			/// </summary>
			/// <param name="state">State.</param>
			public void Process(object state){
				progressReporter.showProgressMessage (String.Format ("Starting with entry {0} of {1}", current, total));

				string sampleFile = Path.Combine (sourceFolder, te.TestFile);
				string producedFile = Path.Combine (location,"tmpFiles", te.ResultFile.Substring (te.ResultFile.LastIndexOf (Path.DirectorySeparatorChar) + 1));
				string expectedResultFile = Path.Combine (config.GetAppSetting ("CorrectResultFolder"), te.ResultFile);

				string command = te.Command + String.Format(@" --no_progress_bar -o ""{0}"" ""{1}""  ",producedFile,sampleFile);

				RunData rd = runner.Run (command,processError,processOutput);

				if (rd.ExitCode == 0) {
					try {
						comparer.CompareAndAddToResult (
							new CompareData(){ 
								ProducedFile = producedFile,
								CorrectFile = expectedResultFile,
								SampleFile = sampleFile,
								Command = te.Command,
								RunTime= rd.Runtime
							});
					} catch(Exception e){
						logger.Error (e);
					}
				}
				progressReporter.showProgressMessage (String.Format ("Finished entry {0} with exit code: {1}", current,rd.ExitCode));
				eventX.Set ();
			}

			/// <summary>
			/// Processes the output received from CCExtractor.
			/// </summary>
			/// <param name="sender">Sender.</param>
			/// <param name="e">E.</param>
			void processOutput (object sender, DataReceivedEventArgs e)
			{
				logger.Debug (e.Data);
			}

			/// <summary>
			/// Processes the errors received from CCExtractor.
			/// </summary>
			/// <param name="sender">Sender.</param>
			/// <param name="e">E.</param>
			void processError (object sender, DataReceivedEventArgs e)
			{
				if (!String.IsNullOrEmpty (e.Data)) {
					logger.Error (e.Data);
				}
			}
		}
	}
}