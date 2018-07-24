/*=====================================================================
  
  This file is part of the Autodesk Vault API Code Samples.

  Copyright (C) Autodesk Inc.  All rights reserved.

THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
PARTICULAR PURPOSE.

FURTHER EDITS BY JULIE EDWARDS for Archiving of data to specific rules
This Edit:      28-06-18
This Version:   0.0.1.1
This Status:    Development 
=====================================================================*/

using System;
using System.IO;
using System.Threading;
using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Currency;
using ADSK = Autodesk.Connectivity.WebServices;


namespace VaultMirror
{

    /// <summary>
    /// Summary description for FullMirrorCommand.
    /// </summary>
    sealed class FullMirrorCommand : Command
	{
        public FullMirrorCommand(ICommandReporter commandReporeter, string username, string password,
            string server, string vault, string outputFolder, bool useWorkingFolder, bool failOnError, CancellationToken ct, string sSubPathTest)
            : base(commandReporeter, username, password, server, vault, outputFolder, useWorkingFolder, failOnError, ct)
		{
		}

        public override void Execute_Impl()
        {
            ChangeStatusMessage("Starting Full Mirror...");
            // cycle through all of the files in the Vault and place them on disk if needed


            // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            // TESTING TO SEE IF WE CAN SPECIFY PATH
            // Folder root = Connection.WebServiceManager.DocumentService.GetFolderRoot();
            // Folder startPath = Connection.WebServiceManager.DocumentService.GetFolderRoot();
            Folder startPath = Connection.WebServiceManager.DocumentService.GetFolderByPath("$/Numeric Archive");
            // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!


            // cycle through all of the files on disk and make sure that they are in the Vault
            string localPath = OutputFolder;


            // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            // TESTING TO SEE IF WE CAN SPECIFY PATH
            // FullMirrorVaultFolder(root, localPath);
            FullMirrorVaultFolder(startPath, localPath, "80000");
            // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!


            // smithmat [2-20-2015]:
            //   https://jira.autodesk.com/browse/PDM-3725
            //   There is a defect here where the FullMirrorLocalFolder deletes all _V folders/files
            //   placed on disk.  This is because it looks for files that do not exist in Vault and deletes them.
            //   One attempt at eliminating this bug was to do FullMirroLocalFolder before FullMirroVaultFolder above.
            //   This fixes the problem on the first run, but on the next run, the _V files are deleted and not re-added because
            //   the code in FullMirroVaultFolder realizes that the files are up to date and doesn't re-download.
            // 

            // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            // TESTING TO SEE IF WE CAN SPECIFY PATH
            //FullMirrorLocalFolder(root, localPath);
            FullMirrorLocalFolder(startPath, localPath);
            // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        }

        private void FullMirrorVaultFolder(Folder folder, string localFolder, string sSubPathTest)
        {
            FullMirrorVaultFolderRecursive(folder, localFolder, sSubPathTest);
            DownloadFiles();
        }

        private void FullMirrorVaultFolderRecursive(Folder folder, string localFolder, string sSubPathTest)
        {
            ThrowIfCancellationRequested();

            if (folder.Cloaked)
                return;


            // Build a Target folder here.. not changing the local folder
            // but based upon it.

            if (!UseWorkingFolder && !Directory.Exists(localFolder))
                Directory.CreateDirectory(localFolder);

            ADSK.File[] files = Connection.WebServiceManager.DocumentService.GetLatestFilesByFolderId(
                folder.Id, true);
            if (files != null)
            {
                foreach (ADSK.File file in files)
                {
                    ThrowIfCancellationRequested();

                    if (file.Cloaked)
                        continue;


                    // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    // GET THE TC URL... 
                    // NOTE: MOve the folder check DOWN so this bit applies to ALL files, then can write it ut to a dafult CSV file...
                    // The TC link is here so we export the link to all the files.
                    // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

                    string sFilename = file.Name;
                    string sExt = sFilename.Substring(sFilename.Length - 3, 3);
                    sExt = sExt.ToUpper();

                    if (sExt == @"PDF" || sExt == @"DWF")
                    {
                        Uri serverUri = new Uri(Connection.WebServiceManager.InformationService.Url);
                        string[] ids = Connection.WebServiceManager.KnowledgeVaultService.GetPersistentIds("FILE", new long[] { file.Id }, EntPersistOpt.Latest);
                        string id = ids[0];
                        id = id.TrimEnd('=');
                        string url = string.Format("{0}://{1}/AutodeskTC/{1}/{2}#/Entity/Details?id=m{3}=&itemtype=File",
                            serverUri.Scheme, serverUri.Host, Connection.Vault, id);

                        // Convert the filename to a part number
                        sFilename = sFilename.Substring(0, sFilename.Length - 4);
                        sFilename = sFilename.ToLower();
                        if(sFilename.Contains(@"r") && sFilename.Contains(@"-"))
                        {
                            sFilename = sFilename.Substring(0, sFilename.IndexOf(@"r"));
                        }

                        // Now write to a file
                        // NOTE : NEED TO FIX HARD CODED PATH!
                        using (System.IO.StreamWriter fileCSV = new System.IO.StreamWriter( @"M:\DocumentURLs.csv", true))
                        {
                            fileCSV.WriteLine( sFilename + @"," + url);
                        }
                    }

                    // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    // TESTING WETHER THE FILE IS ONE TO BE MIRRORED HERE
                    // A SIMPLE TEST ATM - SUB PATH - Only copy files and folders if in this path...
                    //
                    // - Can then adjust the output folder according to rules 
                    // based on the filename - in that way the output archive need not match the 
                    // Vault structure leadigng the way for project based
                    // design data
                    // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

                    if (folder.FullName.Contains(sSubPathTest))
                    {
                        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                        //  CAN DO FILE TYPE TESTING HERE !!
                        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

                        if (UseWorkingFolder)
                            AddFileToDownload(file, null);
                        else
                        {
                            string filePath = Path.Combine(localFolder, file.Name);
                            if (System.IO.File.Exists(filePath))
                            {
                                if (file.CreateDate != System.IO.File.GetCreationTime(filePath))
                                    AddFileToDownload(file, filePath);
                            }
                            else
                                AddFileToDownload(file, filePath);
                        }
                    }
                }

            }

            Folder[] subFolders = Connection.WebServiceManager.DocumentService.GetFoldersByParentId(folder.Id, false);
            if (subFolders != null)
            {
                foreach (Folder subFolder in subFolders)
                {
                    if (!UseWorkingFolder)
                        FullMirrorVaultFolderRecursive(subFolder, Path.Combine(localFolder, subFolder.Name), sSubPathTest);
                    else
                        FullMirrorVaultFolderRecursive(subFolder, null, sSubPathTest);
                }
            }

         }

        private void FullMirrorLocalFolder(Folder folder, string localFolder)
        {
            ThrowIfCancellationRequested();
            if (folder.Cloaked)
                return;

            // delete any files on disk that are not in the vault
            string loc = localFolder;
            if (UseWorkingFolder)
            {
                FolderPathAbsolute folderPath = Connection.WorkingFoldersManager.GetWorkingFolder(folder.FullName);
                loc = folderPath.FullPath;
            }
            if (!Directory.Exists(loc))
                return;
            string[] localFiles = Directory.GetFiles(loc);

            ADSK.File[] vaultFiles = Connection.WebServiceManager.DocumentService.GetLatestFilesByFolderId(folder.Id, true);

            if (vaultFiles == null && localFiles != null)
            {
                foreach (string localFile in localFiles)
                {
                    ThrowIfCancellationRequested();
                    DeleteFile(localFile);
                }
            }
            else
            {
                foreach (string localFile in localFiles)
                {
                    ThrowIfCancellationRequested();
                    bool fileFound = false;
                    string filename = Path.GetFileName(localFile);
                    foreach (ADSK.File vaultFile in vaultFiles)
                    {
                        if (!vaultFile.Cloaked && vaultFile.Name == filename)
                        {
                            fileFound = true;
                            break;
                        }
                    }

                    if (!fileFound)
                        DeleteFile(localFile);
                }
            }

            // recurse the subdirectories and delete any folders not in the Vault
            if (UseWorkingFolder)
            {
                // working folders may not be in the same configuration on disk as in the Vault,
                // so we can't assume sub folders are tied to Vault.  Do folders on disk should be deleted.
                Folder[] vaultSubFolders = Connection.WebServiceManager.DocumentService.GetFoldersByParentId(folder.Id, false);

                if (vaultSubFolders == null)
                    return;

                foreach (Folder vaultSubFolder in vaultSubFolders)
                {
                    FullMirrorLocalFolder(vaultSubFolder, null);
                }

            }
            else
            {
                string[] localFullPaths = Directory.GetDirectories(localFolder);

                if (localFullPaths != null)
                {
                    foreach (string localFullPath in localFullPaths)
                    {
                        ThrowIfCancellationRequested();

                        string vaultPath = folder.FullName + "/" + Path.GetFileName(localFullPath);
                        Folder[] vaultSubFolder = Connection.WebServiceManager.DocumentService.FindFoldersByPaths(
                            new string[] { vaultPath });

                        if (vaultSubFolder[0].Id < 0)
                            DeleteFolder(localFullPath);
                        else
                            FullMirrorLocalFolder(vaultSubFolder[0], localFullPath);
                    }
                }
            }
        }

	}
}

