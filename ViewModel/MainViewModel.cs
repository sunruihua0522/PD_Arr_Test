using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace PLC_Test_PD_Array.ViewModel
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
    public class MainViewModel : ViewModelBase
    {

        #region Definiations

        public enum SystemStatus
        {
            Idle,
            FindingInstruments,
            Sweeping
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            if (IsInDesignMode)
            {
                // Code runs in Blend --> create design time data.
                Debug.WriteLine("I'm running in Blend.");
            }
            else
            {
                // Code runs "for real"
                Debug.WriteLine("I'm running in real.");

                this.Messages = new ObservableCollection<string>();

                this.Controller = new Controller();
                this.Controller.OnSweepingProgressChanged += Controller_OnSweepingProgressChanged;
                this.Controller.OnMessagesUpdated += Controller_OnMessagesUpdated;
            }
        }

        #endregion

        #region Private Methods

        private void Controller_OnMessagesUpdated(object sender, string e)
        {
            var msg = $"{DateTime.Now.ToString("HH:mm:ss")} {e}";
            this.Messages.Add(msg);
        }

        private void Controller_OnSweepingProgressChanged(object sender, double e)
        {
            this.SweepingProgress = e;
            if (e == 1)
            {
                System.ComponentModel.ICollectionView col= Controller.PLC.Channels[0].CurveInsertionLoss;
                foreach (var it in Controller.PLC.Channels[0].InsertionLoss)
                {
                    Console.WriteLine(string.Format("{0:F3}      {1:F3}",it.X,it.Y));
                }
            }
        }

        #endregion

        #region Properties

        public Controller Controller { get; }

        /// <summary>
        /// Get the list of messages
        /// </summary>
        public ObservableCollection<string> Messages { get; }

        double _sweeping_progress = 0;
        public double SweepingProgress
        {
            get
            {
                return _sweeping_progress;
            }
            set
            {
                _sweeping_progress = value;
                RaisePropertyChanged();
            }
        }

        private SystemStatus _status = SystemStatus.Idle;
        public SystemStatus Status
        {
            get
            {
                return _status;
            }
            private set
            {
                _status = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #region Commands

        public RelayCommand FindInstruments
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    try
                    {
                        this.Status = SystemStatus.FindingInstruments;
                        await Controller.ConnectInstruments();
                    }

                    catch (AggregateException ae)
                    {
                        foreach (var ex in ae.Flatten().InnerExceptions)
                        {
                            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        this.Status = SystemStatus.Idle;
                    }
                });
            }
        }
         
        public RelayCommand SweepReference
        {
            get
            {
                return new RelayCommand(async ()=>
                {
                    try
                    {
                        this.Status = SystemStatus.Sweeping;
                        this.SweepingProgress = 0;
                        await this.Controller.StartSweepReference();
                        MessageBox.Show("Sweeping has finished.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch(Exception ex)
                    {
                        MessageBox.Show($"{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        this.Status = SystemStatus.Idle;
                        this.SweepingProgress = 0;
                    }
                });
            }
        }

        public RelayCommand SweepThroughPLC
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    try
                    {
                        this.Status = SystemStatus.Sweeping;
                        this.SweepingProgress = 0;
                        await this.Controller.StartSweepThroughPLC();
                        MessageBox.Show("Sweeping has finished.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {

                        this.Status = SystemStatus.Idle;
                        this.SweepingProgress = 0;

                    }
                });
            }
        }

        public RelayCommand StopSweeping
        {
            get
            {
                return new RelayCommand(() =>
                {
                    this.Controller.StopSweeping();
                });
            }
        }

        public RelayCommand SaveTestResult
        {
            get
            {
                return new RelayCommand(() =>
                {
                    if (this.Controller.PLC.Channels[0].InsertionLoss.Count != 0)
                    {
                        var w = new ChipIDInput();
                        w.ShowDialog();
                        this.Controller.SaveRawData(w.ChipID);
                        this.Controller.SaveTestResult(w.ChipID);
                    }
                    else
                    {
                        throw new Exception("There is no test data!");
                    }
                });
            }
        }
        #endregion
    }
}