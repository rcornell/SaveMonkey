using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.RightsManagement;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Documents;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace Saved_Game_Backup.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class MainViewModel : ViewModelBase {
        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>

        private Visibility _autoBackupEnabled;
        public Visibility AutoBackupEnabled {
            get { return _autoBackupEnabled; }
            set { _autoBackupEnabled = value; }
        }

        public ObservableCollection<string> HardDrives { get; set; } 
        public ObservableCollection<Game> GamesList { get; set; } 
        public ObservableCollection<Game> GamesToBackup { get; set; }
        public ObservableCollection<string> GameNames { get; set; } 

        private string _selectedHardDrive;
        public string SelectedHardDrive {
            get { return _selectedHardDrive; }
            set { _selectedHardDrive = value; }
        }
        private string _selectedGame;
        public string SelectedGame
        {
            get { return _selectedGame; }
            set { _selectedGame = value; }
        }
        private string _selectedBackupGame;
        public string SelectedBackupGame
        {
            get { return _selectedBackupGame; }
            set { _selectedBackupGame = value; }
        }
        private string _specifiedFolder;

        public RelayCommand MoveToBackupList
        {
            get
            {
                return new RelayCommand(() => ToBackupList());
            }
        }
        public RelayCommand MoveToGamesList
        {
            get
            {
                return new RelayCommand(() => ToGamesList());
            }
        }
        public RelayCommand BackupSaves {
            get { return new RelayCommand(() => ExecuteBackup());}
        }
        public RelayCommand BackupAndZip {
            get { return new RelayCommand(() => ExecuteBackupAndZip()); }
        }
        public RelayCommand ResetList
        {
            get { return new RelayCommand(() => ExecuteReset()); }
        }
        public RelayCommand SpecifyFolder {
            get {return new RelayCommand(() => ExecuteSpecifyFolder());}
        }
        public RelayCommand AutoBackup {
            get { return new RelayCommand(() => ExecuteAutoBackupToggle()); }
        }
        public RelayCommand DetectGames {
            get { return new RelayCommand(() => ExecuteDetectGames()); }
        }

        

        public MainViewModel() {
            _autoBackupEnabled = Visibility.Hidden;
            HardDrives = new ObservableCollection<string>();
            GamesList = DirectoryFinder.ReturnGamesList();
            GamesToBackup = new ObservableCollection<Game>();
                
            CreateHardDriveCollection();
        }

        private void ExecuteDetectGames() {
            if (_selectedHardDrive == null) {
                MessageBox.Show("Storage disk not selected. \r\nPlease select the drive where your \r\nsaved games are stored.");
                ExecuteReset();
                return;
            }
            
            GamesToBackup = DirectoryFinder.PollDirectories(_selectedHardDrive, GamesList);
            foreach (Game game in GamesToBackup)
                GamesList.Remove(game);

            RaisePropertyChanged(() => GamesToBackup);
            RaisePropertyChanged(() => GamesList);
        
        }

        private void ExecuteSpecifyFolder() {
            _specifiedFolder = DirectoryFinder.SpecifyFolder();
        }


        private void CreateHardDriveCollection() {
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo drive in drives) {
                HardDrives.Add(drive.RootDirectory.ToString());
            }
        }

        private void ExecuteAutoBackupToggle() {
            
            //First, toggle the visibility for the UI
            bool enabledBeforeClick;
            if (_autoBackupEnabled == Visibility.Visible) {
                enabledBeforeClick = true;
                _autoBackupEnabled = Visibility.Hidden;
            }
            else {

                //This is for turning **ON** AutoBackup
                _autoBackupEnabled = Visibility.Visible;
                enabledBeforeClick = false;
                _specifiedFolder = DirectoryFinder.SpecifyFolder();
            } 
                

            //Make Backup.cs listen for save modification events.
            
        }

        private void ToBackupList() {
            Game game = null;
            for (int i = 0; i < GamesList.Count(); i++) {
                if (_selectedGame == GamesList[i].Name) {
                    game = GamesList[i];
                    break;
                }
            }

            if (game != null) {
                GamesList.Remove(game);
                RaisePropertyChanged(() => GamesList);
                GamesToBackup.Add(game);
                RaisePropertyChanged(() => GamesToBackup);
            }
        }

        private void ToGamesList()
        {
            Game game = null;
            for (int i = 0; i < GamesToBackup.Count(); i++) {
                if (_selectedBackupGame == GamesToBackup[i].Name) {
                    game = GamesToBackup[i];
                    break;
                }
            }

            if (game != null) {
                GamesToBackup.Remove(game);
                RaisePropertyChanged(() => GamesToBackup);
                GamesList.Add(game);
                RaisePropertyChanged(() => GamesList);
            }

        }

        private void ExecuteBackup() {
            if (SelectedHardDrive == null) {
                MessageBox.Show("Storage disk not selected. \r\nPlease select the drive where your \r\nsaved games are stored.");
                ExecuteReset();
                return;
            }

            if (!GamesToBackup.Any()) {
                MessageBox.Show("No games selected. \n\rPlease select at least one game.");
                ExecuteReset();
                return;
            }

            if(Backup.BackupSaves(GamesToBackup, SelectedHardDrive, false, _specifiedFolder))
                MessageBox.Show("Saved games successfully backed up. \r\n");
            ExecuteReset();
        }
        
        private void ExecuteBackupAndZip() {
            if (SelectedHardDrive == null) {
                MessageBox.Show("Storage disk not selected. \r\nPlease select the drive where your \r\nsaved games are stored.");
                ExecuteReset();
                return;
            }

            if (!GamesToBackup.Any()) {
                MessageBox.Show("No games selected. \n\rPlease select at least one game.");
                ExecuteReset();
                return;
            }

            if(Backup.BackupAndZip(GamesToBackup, SelectedHardDrive, true, _specifiedFolder))
                MessageBox.Show("Saved games successfully backed up. \r\n");
            ExecuteReset();
        }

        private void ExecuteReset() {
            GamesList = DirectoryFinder.ReturnGamesList();
            GamesToBackup.Clear();
            _specifiedFolder = null;
            _selectedHardDrive = null;
            _selectedGame = null;
            _selectedBackupGame = null;

            RaisePropertyChanged(() => GamesList);
            RaisePropertyChanged(() => GamesToBackup);
            RaisePropertyChanged(() => SelectedHardDrive);
            RaisePropertyChanged(() => SelectedBackupGame);
            RaisePropertyChanged(() => SelectedGame);
        }

    }
}