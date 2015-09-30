﻿using KVLib;
using MetroFramework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Dota2ModKit.Features {
	public class KVFeatures {
		MainForm mainForm;

		public KVFeatures(MainForm mainForm) {
			this.mainForm = mainForm;
		}

		internal void combine() {
			string[] items = { "heroes", "units", "items", "abilities" };
			for (int i = 0; i < items.Length; i++) {
				string item = items[i];
				string foldPath = Path.Combine(mainForm.currAddon.gamePath, "scripts", "npc", item);
				string foldName = foldPath.Substring(foldPath.LastIndexOf('\\') + 1);
				string foldParent = foldPath.Substring(0, foldPath.LastIndexOf('\\'));
				string bigKVPath = Path.Combine(foldParent, "npc_" + foldName + "_custom.txt");

				//if (!File.Exists(bigKVPath)) {
				//	continue;
				//}

				bool doBreakUp = false;
				if (File.Exists(bigKVPath)) {
					if (!Directory.Exists(foldPath)) {
						DialogResult dr = MetroMessageBox.Show(mainForm, "npc_" + item + "_custom.txt " + strings.HasNotBeenBrokenUpMsg,
							strings.HasNotBeenBrokenUpCaption,
							MessageBoxButtons.OKCancel,
							MessageBoxIcon.Information);

						if (dr != DialogResult.OK) {
							// nothings been broken up, so nothing to combine
							continue;
						} else {
							doBreakUp = true;
						}
					} else {
						if (mainForm.currAddon.askToBreakUp) {
							DialogResult dr = MetroMessageBox.Show(mainForm, strings.AskToBreakUpMsg + " npc_" + item + "_custom.txt?",
								strings.HasNotBeenBrokenUpCaption,
								MessageBoxButtons.YesNo,
								MessageBoxIcon.Information);
							if (dr == DialogResult.Yes) {
								doBreakUp = true;
							}
						}
					}

					if (doBreakUp) {
						breakUp(item);
					}
				}

				string currText = File.ReadAllText(bigKVPath);

				// so now we have the big KV file created and ready to be populated.
				string[] files = Directory.GetFiles(foldPath, "*.txt", SearchOption.AllDirectories);
				StringBuilder text = new StringBuilder("\"DOTAAbilities\"" + "\n{\n");
				if (foldName == "heroes") {
					text = new StringBuilder("\"DOTAHeroes\"" + "\n{\n");
				} else if (foldName == "units") {
					text = new StringBuilder("\"DOTAUnits\"" + "\n{\n");
				}

				foreach (string file in files) {

					bool addTab = false;
					string[] lines = File.ReadAllLines(file);
					for (int j = 0; j < lines.Length; j++) {
						string line = lines[j];
						if (j == 0 && line.StartsWith("\t") == false && line.StartsWith("  ") == false) {
							addTab = true;
						}
						string newLine = line;
						if (addTab) {
							newLine = "\t" + line;
						}
						text.AppendLine(newLine);
					}
				}

				text.Append("}");

				// check if they're different before writing
				string txt = text.ToString();
				if (txt.Trim() != currText.Trim()) {
					File.WriteAllText(bigKVPath, txt);
				} else {
					Debug.WriteLine("Not overwriting.");
				}
			}

			mainForm.text_notification(strings.CombineKVSuccess, MetroColorStyle.Green, 1500);
		}

		public void breakUp(string itemStr) {
			string file = Path.Combine(mainForm.currAddon.gamePath, "scripts", "npc", "npc_" + itemStr + "_custom.txt");
			string foldPath = Path.Combine(mainForm.currAddon.gamePath, "scripts", "npc", itemStr);

			// Ensure the npc_ file exists.
			if (!File.Exists(file)) {
				return;
			}

			string foldName = file.Substring(file.LastIndexOf('\\') + 1);
			// get rid of extension.
			foldName = foldName.Substring(0, foldName.LastIndexOf('.'));

			if (Directory.Exists(foldPath)) {
				Directory.Delete(foldPath, true);
			}
			Directory.CreateDirectory(foldPath);

			string allText = File.ReadAllText(file);
			allText = allText.Replace("\r", "");
			KeyValue[] kvs = KVLib.KVParser.KV1.ParseAll(allText);
			foreach (KeyValue kv in kvs) {
				if (kv.Key == "DOTAAbilities" || kv.Key == "DOTAHeroes" || kv.Key == "DOTAUnits") {
					// skip this first nextKey, go straight to children.
					if (kv.HasChildren) {
						IEnumerable<KeyValue> kvs2 = kv.Children;
						KeyValue[] kvArr = kvs2.ToArray();

						if (kvArr.Length == 0) {
							// This kv file is screwed up.
							break;
						}

						// record start line number and end line number of each Key-Value block
						int[] startLineNumber = new int[kvArr.Length];
						int[] endLineNumber = new int[kvArr.Length];

						// catch the start pointer, ignore all "Version"s
						int ptr = 0;
						while (kvArr[ptr].Key == "Version" && ptr < kvArr.Length)
							ptr++;

						// store the start pointer
						int startPtr = ptr;

						// init the first key
						string key = kvArr[ptr].Key;

						// loop over all lines to record the start/end of all kvs
						string[] lines = allText.Split('\n');
						for (int index = 0; index < lines.Length; index++) {
							string line = lines[index];
							if (line.Trim().StartsWith("\"" + key)) {
								int ind = index - 1;
								// go back to add all comments/empty lines to this block
								while ((lines[ind].Trim() == "" || lines[ind].Trim().StartsWith("//")) && (ind > 0))
									ind--;
								startLineNumber[ptr] = ind + 1;
								// record the end of the block for last pointer
								if (ptr > 0)
									endLineNumber[ptr - 1] = ind;
								if (ptr < kvArr.Length - 1) {
									ptr++;
									key = kvArr[ptr].Key;
								}
							}
						}
						// deal with very last pointer
						int lastInd = lines.Length - 1;
						while (lastInd > 0 && lines[lastInd].Contains("}") && (lines[lastInd].IndexOf("//") > lines[lastInd].IndexOf("}")))
							lastInd--;
						endLineNumber[kvArr.Length - 1] = lastInd;

						// generate break-down kv files and write text
						for (int p = startPtr; p < kvArr.Length; p++) {
							string filePath = Path.Combine(foldPath, kvArr[p].Key + ".txt");
							File.Create(filePath).Close();
							StringBuilder sb = new StringBuilder();

							for (int p1 = startLineNumber[p]; p1 <= endLineNumber[p]; p1++) {
								string line = lines[p1];
								// remove first tab
								if (line.StartsWith("\t")) {
									line = line.Substring(1);
								}
								sb.AppendLine(line);
							}

							string output = sb.ToString();
							// remove beginning newline
							output = output.TrimStart();

							// if last file, we need to remove the last }
							if (p == kvArr.Length - 1) {
								output = output.TrimEnd();
								if (output.EndsWith("}")) {
									output = output.Substring(0, output.Length - 1);
								}
							}
							File.WriteAllText(filePath, output);
						}
					}
				}
			}
		}

	}
}
