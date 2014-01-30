﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Timers.Timer;


namespace Saved_Game_Backup
{
    public class Backup {

        private static ObservableCollection<Game> _gamesToAutoBackup = new ObservableCollection<Game>();
        private static List<FileSystemWatcher> _fileWatcherList;
        private static string _hardDrive = Path.GetPathRoot(Environment.SystemDirectory);
        private static string _myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private static string _userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private static string _specifiedAutoBackupFolder;
        private static bool _autoBackupAllowed;
        private static Timer _timer;
        
        public Backup() {
            
        }

        public static bool StartBackup(ObservableCollection<Game> games, BackupType backupType, bool backupEnabled) {
            bool success;
            var gamesToBackup = ModifyGamePaths(games);
            switch (backupType) {
                case BackupType.ToZip:
                    success = BackupAndZip(gamesToBackup);
                    break;
                case BackupType.ToFolder:
                    success = BackupSaves(gamesToBackup);
                    break;
                case BackupType.Autobackup:
                    success = ToggleAutoBackup(gamesToBackup, backupEnabled);
                    break;
                default:
                    success = false;
                    break;
            }
            return success;
        }

        //Doesn't know if you cancel out of a dialog.
        //Needs threading when it is processing lots of files. Progress bar? Progress animation?
        public static bool BackupSaves(ObservableCollection<Game> gamesList, string specifiedfolder = null) {
            string destination;

            var fd = new FolderBrowserDialog() { SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer), 
                    Description = "Select the folder where this utility will create the SaveBackups folder.",
                    ShowNewFolderButton = true };

            if (fd.ShowDialog() == DialogResult.OK)
                destination = fd.SelectedPath;
            else {
                return false;
            }

            if (!Directory.Exists(destination) && !string.IsNullOrWhiteSpace(destination))
                Directory.CreateDirectory(destination);

            #region Likely not necessary anymore
            ////If user chooses a specific place where they store their saves, this 
            ////changes each game's path to that folder followed by the game name.
            //if (specifiedfolder != null) {
            //    for (int i = 0; i <= gamesList.Count; i++) {
            //        gamesList[i].Path = specifiedfolder + "\\" + gamesList[i].Name;
            //    }
            //}
            #endregion

            //This backs up each game using BackupGame()

            foreach (var game in gamesList) {
                BackupGame(game.Path, destination + "\\" + game.Name);
            }

            return true;
        }

        private static void BackupGame(string sourceDirName, string destDirName) {

            // Get the subdirectories for the specified directory.
            var dir = new DirectoryInfo(sourceDirName);
            var dirs = dir.GetDirectories();

            if (!dir.Exists) {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            // If the destination directory doesn't exist, create it. 
            if (Directory.Exists(destDirName)) {
                DeleteDirectory(destDirName);
            } 
                
            Directory.CreateDirectory(destDirName);
  

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files) {
                var temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            } 
            
            foreach (DirectoryInfo subdir in dirs) {
                var temppath = Path.Combine(destDirName, subdir.Name);
                BackupGame(subdir.FullName, temppath);
            }
        }

        public static bool BackupAndZip(ObservableCollection<Game> gamesList, string specifiedfolder = null) {
            string zipSource;
            FileInfo zipDestination;

            var fd = new SaveFileDialog() {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer),
                FileName = "SaveBackups.zip",
                Filter = @"Zip files (*.zip) | *.zip",
                Title = @"Select the root folder where this utility will create the SaveBackups folder.",
                CheckFileExists = false,
                OverwritePrompt = true
            };

            if (fd.ShowDialog() == DialogResult.OK) {
                zipDestination = new FileInfo(fd.FileName);
                zipSource = zipDestination.DirectoryName + "\\Temp";
            }
            else {
                return false;
            }

            if (!Directory.Exists(zipSource))
                Directory.CreateDirectory(zipSource);

            //Creates temporary directory at ZipSource + the game's name
            //To act as the source folder for the ZipFile class.
            foreach (var game in gamesList) {
                BackupGame(game.Path, zipSource + "\\" + game.Name);
            }

            //Delete existing zip file if one exists.
            if(zipDestination.Exists)
                zipDestination.Delete();
            
            ZipFile.CreateFromDirectory(zipSource, zipDestination.FullName);

            //Delete temporary folder that held save files.
            DeleteDirectory(zipSource);
            Directory.Delete(zipSource);

            return true;
        }

        /// <summary>
        /// Returns false if turning off autobackup. Returns true if activating.
        /// </summary>
        /// <param name="gamesToBackup"></param>
        /// <param name="harddrive"></param>
        /// <param name="backupEnabled"></param>
        /// <param name="specifiedFolder"></param>
        /// <returns></returns>
        public static bool ToggleAutoBackup(ObservableCollection<Game> gamesToBackup, bool backupEnabled, string specifiedFolder = null) {
            if (backupEnabled){
                DeactivateAutoBackup();
                return false;
            }
            
            ActivateAutoBackup(gamesToBackup, specifiedFolder);
            return true;
        }

        
        public static void ActivateAutoBackup(ObservableCollection<Game> gamesToBackup, string specifiedFolder = null) {
            _timer = new Timer { Interval = 10000 };
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start();
            
            _fileWatcherList = new List<FileSystemWatcher>();
            _gamesToAutoBackup = gamesToBackup;

            //_specifiedAutoBackupFolder = specifiedFolder;

            if (_specifiedAutoBackupFolder == null) {
                var fb = new FolderBrowserDialog() {SelectedPath = _hardDrive, ShowNewFolderButton = true};
                if (fb.ShowDialog() == DialogResult.OK)
                    _specifiedAutoBackupFolder = fb.SelectedPath;
                //_specifiedAutoBackupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\Save Backups\\";
            }
            

            int watcherNumber = 0;
            foreach (Game game in gamesToBackup) {
                _fileWatcherList.Add(new FileSystemWatcher(game.Path));
                _fileWatcherList[watcherNumber].Changed += OnChanged;
                _fileWatcherList[watcherNumber].EnableRaisingEvents = true;
                watcherNumber++;
            }

            
        }

        public static void DeactivateAutoBackup() {
            foreach (FileSystemWatcher f in _fileWatcherList) {
                f.EnableRaisingEvents = false;
            }
            _fileWatcherList.Clear();
        }

        private static void DeleteDirectory(string deleteDirName) {

                    var dir = new DirectoryInfo(deleteDirName);
                    var dirs = dir.GetDirectories();

                    // Get the files in the directory and copy them to the new location.
                    FileInfo[] files = dir.GetFiles();
                    foreach (FileInfo file in files)
                    {
                        File.Delete(file.FullName);
                    }

                    foreach (DirectoryInfo subdir in dirs)
                    {
                        var temppath = Path.Combine(deleteDirName, subdir.Name);
                        DeleteDirectory(temppath);
                        Directory.Delete(subdir.FullName);
                    }


                }

        public static bool CanBackup(ObservableCollection<Game> gamesToBackup) {
            if (_hardDrive == null) {
                MessageBox.Show("Cannot find OS drive. \r\nPlease add each game using \r\nthe 'Add Game to List' button.");
                return false;
            }

            if (gamesToBackup.Any()) return true;
            MessageBox.Show("No games selected. \n\rPlease select at least one game.");
            return false;
        }

        private static void _timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            _autoBackupAllowed = true;
        }

        private static void OnChanged(object source, FileSystemEventArgs e) {
            
            Console.WriteLine(e.FullPath);
            Console.WriteLine(e.Name);
            Console.WriteLine(e.ChangeType);
            var file = new FileInfo(e.FullPath);
            Console.WriteLine(file.Name);

            if(_autoBackupAllowed)
                foreach (Game game in _gamesToAutoBackup) {
                    if (e.FullPath.Contains(game.Path))
                        BackupGame(game.Path, _specifiedAutoBackupFolder + "\\" + game.Name + "\\");
                }
            _autoBackupAllowed = false;

            //BackupFile(e);
        }

        private static void BackupFile(FileSystemEventArgs e) {
            
        }

        /// <summary>
        /// Edits the truncated paths in the Games.json file and inserts the 
        /// user's path before the \\Documents\\ or \\AppData\\ folder path.
        /// </summary>
        /// <param name="gamesToBackup"></param>
        private static ObservableCollection<Game> ModifyGamePaths(ICollection<Game> gamesToBackup) {
            var editedList = new ObservableCollection<Game>();
            foreach (var game in gamesToBackup) {
                if (game.Path.Contains("Documents"))
                    editedList.Add(new Game(game.Name, _userPath+game.Path));
            }
            gamesToBackup.Clear();
            return editedList;
        }
        
    }
}
