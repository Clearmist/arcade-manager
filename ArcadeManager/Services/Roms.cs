﻿using ArcadeManager.Actions;
using ElectronNET.API;
using System;
using System.IO;
using System.Linq;

namespace ArcadeManager.Services {

	/// <summary>
	/// Roms management
	/// </summary>
	public class Roms {

		/// <summary>
		/// Copies roms
		/// </summary>
		/// <param name="args">The arguments</param>
		public static void Add(Actions.RomsAdd args, BrowserWindow window) {
			Electron.IpcMain.Send(window, "progress", new Progress { label = "Copying roms", init = true, canCancel = true });

			try {
				// check files and folders
				if (!File.Exists(args.main)) { throw new FileNotFoundException("Unable to find main CSV file", args.main); }
				if (!Directory.Exists(args.romset)) { throw new DirectoryNotFoundException($"Unable to find romset folder {args.romset}"); }
				if (!Directory.Exists(args.selection)) { Directory.CreateDirectory(args.selection); }

				// read CSV file
				var content = Csv.ReadFile(args.main);

				var total = content.Count();
				var i = 0;
				var copied = 0;

				// copy each file found in CSV
				foreach (var f in content) {
					if (Behavior.MessageHandler.MustCancel) { break; }

					i++;

					// build vars
					var game = f.name;
					var zip = $"{game}.zip";
					var sourceRom = Path.Join(args.romset, zip);
					var destRom = Path.Join(args.selection, zip);

					if (File.Exists(sourceRom)) {
						var fi = new FileInfo(sourceRom);

						Electron.IpcMain.Send(window, "progress", new Progress { label = $"{game} ({HumanSize(fi.Length)})", total = total, current = i });

						if (!File.Exists(destRom) || args.overwrite) { copied++; }

						// copy rom
						File.Copy(sourceRom, destRom, args.overwrite);

						// try to copy chd if it can be found
						var sourceChd = Path.Join(args.romset, game);
						if (Directory.Exists(sourceChd)) {
							if (Behavior.MessageHandler.MustCancel) { break; }

							Electron.IpcMain.Send(window, "progress", new Progress { label = $"Copying {game} CHD ({HumanSize(DirectorySize(sourceChd))})", total = total, current = i });

							DirectoryCopy(sourceChd, Path.Join(args.selection, game), args.overwrite, false);
						}
					}
				}

				// display result
				if (Behavior.MessageHandler.MustCancel) {
					Electron.IpcMain.Send(window, "progress", new Progress { label = $"Operation cancelled! - Copied {copied} file(s)", end = true, cancelled = true });
				}
				else {
					Electron.IpcMain.Send(window, "progress", new Progress { label = $"Copied {copied} file(s)", end = true, folder = args.selection });
				}
			}
			catch (Exception ex) {
				Electron.IpcMain.Send(window, "progress", new Progress { label = $"An error has occurred: {ex.Message}", end = true });
			}
		}

		/// <summary>
		/// Copies a directory
		/// </summary>
		/// <param name="sourceDirName">The source directory name</param>
		/// <param name="destDirName">The destination directory name</param>
		/// <param name="overwrite">Wether to overwrite existing files</param>
		/// <param name="copySubDirs">Whether to copy the sub-directories</param>
		/// <remarks>From docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories</remarks>
		private static void DirectoryCopy(string sourceDirName, string destDirName, bool overwrite, bool copySubDirs) {
			// Get the subdirectories for the specified directory.
			DirectoryInfo dir = new DirectoryInfo(sourceDirName);

			if (!dir.Exists) {
				throw new DirectoryNotFoundException(
					"Source directory does not exist or could not be found: "
					+ sourceDirName);
			}

			DirectoryInfo[] dirs = dir.GetDirectories();

			// If the destination directory doesn't exist, create it.
			if (!Directory.Exists(destDirName)) {
				Directory.CreateDirectory(destDirName);
			}

			// Get the files in the directory and copy them to the new location.
			FileInfo[] files = dir.GetFiles();
			foreach (FileInfo file in files) {
				string tempPath = Path.Combine(destDirName, file.Name);
				file.CopyTo(tempPath, overwrite);
			}

			// If copying subdirectories, copy them and their contents to new location.
			if (copySubDirs) {
				foreach (DirectoryInfo subdir in dirs) {
					string tempPath = Path.Combine(destDirName, subdir.Name);
					DirectoryCopy(subdir.FullName, tempPath, overwrite, copySubDirs);
				}
			}
		}

		/// <summary>
		/// Computes a directory size
		/// </summary>
		/// <param name="directory">The path to the directory</param>
		/// <returns>The directory size</returns>
		/// <remarks>From stackoverflow.com/a/468131/6776</remarks>
		private static long DirectorySize(string directory) {
			var d = new DirectoryInfo(directory);

			long size = 0;
			// Add file sizes.
			FileInfo[] fis = d.GetFiles();
			foreach (FileInfo fi in fis) {
				size += fi.Length;
			}

			// Add subdirectory sizes.
			DirectoryInfo[] dis = d.GetDirectories();
			foreach (DirectoryInfo di in dis) {
				size += DirectorySize(di.FullName);
			}

			return size;
		}

		/// <summary>
		/// Makes a file size human-readable
		/// </summary>
		/// <param name="size">The source file size</param>
		/// <returns>The human-readable file size</returns>
		/// <remarks>From stackoverflow.com/a/4975942/6776</remarks>
		private static string HumanSize(long size) {
			string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
			if (size == 0)
				return $"0 {suf[0]}";
			long bytes = Math.Abs(size);
			int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
			double num = Math.Round(bytes / Math.Pow(1024, place), 1);
			return $"{Math.Sign(size) * num} {suf[place]}";
		}
	}
}
