﻿using System;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.IO;

namespace CCExtractorTester
{
	/// <summary>
	/// Differences comparer using the built-in linux diff command.
	/// </summary>
	public class DiffLinuxComparer : IFileComparable
	{
		/// <summary>
		/// Gets or sets the stringbuilder.
		/// </summary>
		/// <value>The builder.</value>
		private StringBuilder Builder { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CCExtractorTester.DiffLinuxComparer"/> class.
		/// </summary>
		public DiffLinuxComparer ()
		{
			Builder = new StringBuilder ();
		}

		#region IFileComparable implementation
		/// <summary>
		/// Compares the files provided in the data and add to an internal result.
		/// </summary>
		/// <param name="data">The data for this entry.</param>
		public void CompareAndAddToResult (CompareData data)
		{
			Builder.AppendLine ("Time needed for this entry: "+data.RunTime.ToString());
			Builder.AppendLine ("Used command: " + data.Command);
			Builder.AppendLine ("Sample file: " + data.SampleFile);
			ProcessStartInfo psi = new ProcessStartInfo("diff");
			psi.UseShellExecute = false;
			psi.RedirectStandardError = true;
			psi.RedirectStandardOutput = true;
			psi.CreateNoWindow = true;

			psi.Arguments = String.Format(@"-y ""{0}"" ""{1}""",data.CorrectFile,data.ProducedFile);
			Process p = new Process ();
			p.StartInfo = psi;
			p.ErrorDataReceived += processError;
			p.OutputDataReceived += processOutput;
			p.Start ();
			p.BeginOutputReadLine ();
			p.BeginErrorReadLine ();
			while (!p.HasExited) {
				Thread.Sleep (1000);
			}
		}
		/// <summary>
		/// Gets the name of the report file.
		/// </summary>
		/// <returns>The report file name.</returns>
		public string GetReportFileName ()
		{
			return "Report_" + DateTime.Now.ToFileTime () + ".txt";
		}
		/// <summary>
		/// Saves the report to a given file, with some extra data provided.
		/// </summary>
		/// <param name="pathToFolder">Path to folder to save the report in</param>
		/// <param name="data">The extra result data that should be in the report.</param>
		public void SaveReport (string pathToFolder, ResultData data)
		{
			using (StreamWriter sw = new StreamWriter(Path.Combine(pathToFolder,GetReportFileName()))) {
				sw.WriteLine ("Report generated for version " + data.CCExtractorVersion);
				sw.WriteLine (Builder.ToString ());
			}
		}

		#endregion
		/// <summary>
		/// Processes an error received by the diff command
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		void processError (object sender, DataReceivedEventArgs e)
		{
			Builder.AppendLine (e.Data);
		}
		/// <summary>
		/// Processes the output from the diff command.
		/// </summary>
		/// <param name="sender">Sender.</param>
		/// <param name="e">E.</param>
		void processOutput (object sender, DataReceivedEventArgs e)
		{
			Builder.AppendLine (e.Data);
		}
	}
}