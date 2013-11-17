﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Saved_Game_Backup
{
    public class Backup {

        public Backup() {
            
        }

        //Need to allow overwriting of previous saves? Maybe archive old ones?

        public static void BackupSaves(ObservableCollection<Game> gamesList, string harddrive, string specifiedfolder = null) {

            string destination = harddrive + "SaveBackups";
            Directory.CreateDirectory(destination);

            //If user chooses a specific place where they store their saves, this 
            //changes each game's path to that folder followed by the game name.
            if (specifiedfolder != null) {
                for (int i = 0; i <= gamesList.Count; i++) {
                    gamesList[i].Path = specifiedfolder + "\\" + gamesList[i].Name;
                }
            }

            //This backs up each game using BackupGame()
            foreach (Game g in gamesList) {
                BackupGame(g.Path, destination + "\\" + g.Name, false);
            }

            MessageBox.Show("Backup folders created in " + harddrive + "SaveBackups.");

        }

        private static void BackupGame(string sourceDirName, string destDirName, bool copySubDirs) {

            // Get the subdirectories for the specified directory.
            var dir = new DirectoryInfo(sourceDirName);
            var dirs = dir.GetDirectories();

            if (!dir.Exists) {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            // If the destination directory doesn't exist, create it. 
            if (!Directory.Exists(destDirName)) {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files) {
                var temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            } 
            
            foreach (DirectoryInfo subdir in dirs) {
                var temppath = Path.Combine(destDirName, subdir.Name);
                BackupGame(subdir.FullName, temppath, copySubDirs);
            }
            
        }

        public static void BackupAndZip(ObservableCollection<Game> gamesList, string harddrive, string specifiedfolder = null) {
            BackupSaves(gamesList, harddrive, specifiedfolder);

            var zipSource = harddrive + "SaveBackups";
            var zipResult = harddrive + "SaveBackups.zip";
            ZipFile.CreateFromDirectory(zipSource, zipResult);

            MessageBox.Show("SaveBackups.zip created in " + harddrive);

        }
        

        private static string CreateFolderPath() {
            string path;
            
            try
            {
                //username = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            catch (NullReferenceException ex)
            {
                MessageBox.Show("Cannot find the MyDocuments folder on your computer. \r\n" + ex);
                return null;
            }

            return path;
        }
    }
}
