﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CommandLine;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DocGenerator
{
	public static class Program
	{
		static Program()
		{
			var root = new DirectoryInfo(Directory.GetCurrentDirectory());

			do
			{
				if (File.Exists(Path.Combine(root.FullName, "global.json")))
					break;
				root = root.Parent;
			} while (root != null && root.Parent != root.Root);

			if (root == null || root.Parent == root.Root)
				throw new Exception("Expected to find a global.json in a parent folder");


			var r = root.FullName;
			var globalJson = Path.Combine(r, "global.json");
			InputDirPath = Path.Combine(r, "src");
			OutputDirPath = Path.Combine(r, "docs");
			BuildOutputPath = Path.Combine(r, "build", "output");

			var jObject = JObject.Parse(File.ReadAllText(globalJson));

			DocVersion = string.Join(".", jObject["doc_current"]
				.Value<string>()
				.Split(".")
				.Take(2));

			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					UseShellExecute = false,
					RedirectStandardOutput = true,
					FileName = "git.exe",
					CreateNoWindow = true,
					WorkingDirectory = Environment.CurrentDirectory,
					Arguments = "rev-parse --abbrev-ref HEAD"
				}
			};

			try
			{
				process.Start();
				BranchName = process.StandardOutput.ReadToEnd().Trim();
				process.WaitForExit();
			}
			catch (Exception)
			{
				BranchName = "master";
			}
			finally
			{
				process.Dispose();
			}
		}

		/// <summary>
		/// The branch name to include in generated docs to link back to the original source file
		/// </summary>
		public static string BranchName { get; set; }

		public static string BuildOutputPath { get; }

		/// <summary>
		/// The Elasticsearch documentation version to link to
		/// </summary>
		public static string DocVersion { get; set; }

		public static string InputDirPath { get; }

		public static string OutputDirPath { get; }

		private static int Main(string[] args) =>
			Parser.Default.ParseArguments<DocGeneratorOptions>(args)
				.MapResult(
					opts =>
					{
						try
						{
							if (!string.IsNullOrEmpty(opts.BranchName))
								BranchName = opts.BranchName;

							if (!string.IsNullOrEmpty(opts.DocVersion))
								DocVersion = opts.DocVersion;

							Console.WriteLine($"Using branch name {BranchName} in documentation");
							Console.WriteLine($"Using doc reference version {DocVersion} in documentation");

							LitUp.GoAsync(args).Wait();
							return 0;
						}
						catch (AggregateException ae)
						{
							var ex = ae.InnerException ?? ae;
							Console.WriteLine(ex.Message);
							return 1;
						}
					},
					errs => 1);
	}

	public class DocGeneratorOptions
	{
		[Option('b', "branch", Required = false, HelpText = "The version that appears in generated from source link")]
		public string BranchName { get; set; }

		[Option('d', "docs", Required = false, HelpText = "The version that links to the Elasticsearch reference documentation")]
		public string DocVersion { get; set; }
	}
}
