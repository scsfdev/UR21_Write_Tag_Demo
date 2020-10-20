using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows.Input;
using System.Windows.Threading;
using UR21_Write_Tag_Demo.Model;

namespace UR21_Write_Tag_Demo.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IDataService _dataService;

        DispatcherTimer dTimer;
        Ur21 ur = new Ur21();

        public ICommand CmdClear { get; private set; }
        public ICommand CmdExit { get; private set; }
        public ICommand CmdScan { get; private set; }
        public ICommand CmdWrite { get; private set; }
        public ICommand CmdConnect { get; private set; }


        public MainViewModel(IDataService dataService)
        {
            _dataService = dataService;
            _dataService.GetData(
                (item, error) =>
                {
                    if (error != null)
                    {
                        // Report error here
                        return;
                    }

                    
                });

            Version = General.gGetVersion();

            Messenger.Default.Register<string>(this, MsgType.MAIN_VM, ShowMsg);

            DefaultMsg = "UR21 Write Demo. Created by Tin Maung Htay © 2018 DIAS.";

            CmdConnect = new RelayCommand(ConnectAction);
            CmdClear = new RelayCommand(ClearAction);
            CmdScan= new RelayCommand(ScanTagAction);
            CmdWrite= new RelayCommand(WriteTagAction);
            CmdExit = new RelayCommand(ExitForm);

            Ur21.OnTagRead += Ur_OnTagRead;

            dTimer = new DispatcherTimer();
            dTimer.Interval = TimeSpan.FromSeconds(2);
            dTimer.Tick += DTimer_Tick;

            Connect_Text = MyConst.CONNECT;

            Connected = false;

            ClearAction();

            dTimer.Start();
        }


        public override void Cleanup()
        {
            // Clean up if needed
            base.Cleanup();

            if (Connected)
            {
                Connected = false;
                // Disconnect UR21.
                ur.DisconnectUR21();
            }
        }


        #region Getter / Setter

        private string connect_Text;
        public string Connect_Text
        {
            get { return connect_Text; }
            set { Set(ref connect_Text, value); }
        }


        private string defaultMsg;
        public string DefaultMsg
        {
            get { return defaultMsg; }
            set { Set(ref defaultMsg, value); }
        }



        private string version;
        public string Version
        {
            get { return version; }
            set { Set(ref version, value); }
        }



        private string statusMsg;
        public string StatusMsg
        {
            get { return statusMsg; }
            set { Set(ref statusMsg, value); }
        }



        private bool connected;
        public bool Connected
        {
            get { return connected; }
            set { Set(ref connected, value); }
        }



        private string comPort;
        public string ComPort
        {
            get { return comPort; }
            set { Set(ref comPort, value); }
        }



        private string scanTag;
        public string ScanTag
        {
            get { return scanTag; }
            set { Set(ref scanTag, value); }
        }



        private string writeTag;
        public string WriteTag
        {
            get { return writeTag; }
            set { Set(ref writeTag, value); }
        }


        private bool connectReady;
        public bool ConnectReady
        {
            get { return connectReady; }
            set { Set(ref connectReady, value); }
        }


        #endregion


        #region Custom Functions

        private void ShowMsg(string strMsg)
        {
            StatusMsg = strMsg.Replace(MyConst.ERROR, "Error:").Replace(Environment.NewLine, " ").Replace("Error Details", "Details");
            Messenger.Default.Send(strMsg, MsgType.MAIN_V);
            //Connect_Text = MyConst.DISCONNECT;
            //if(!strMsg.Contains(MyConst.INFO))
            //    ConnectAction();
        }


        private void ExitForm()
        {
            dTimer.Stop();
            Messenger.Default.Send(new NotificationMessage(this, MyConst.EXIT));
        }


        private void ConnectAction()
        {
            // Check COM port, if ok, connect to it.
            if (comPort == "")
            {
                ShowMsg(MyConst.WARNING + Environment.NewLine + "COM port empty!");
                return;
            }

            byte bPort = byte.Parse(ComPort);

            if (connect_Text == MyConst.CONNECT)
            {
                if (dTimer.IsEnabled)
                    dTimer.Stop();

                // Start RFID reading.
                if (ur.ConnectUR21(bPort))
                {
                    // Change btn text to DISCONNECT.
                    Connect_Text = MyConst.DISCONNECT;
                    Connected = true;
                }                
            }
            else
            {
                // Disconnect from UR21.
                ur.DisconnectUR21();

                Connected = false;
                dTimer.Start();

                // Change btn text to CONNECT.
                Connect_Text = MyConst.CONNECT;
            }
        }


        private void ScanTagAction()
        {
            ScanTag = "";

            Tag tIn = new Tag();
            if (ur.ReadOneTag(ref tIn))
            {
                // Display read tag.
                ScanTag = tIn.Uii;
                StatusMsg = "Read Tag Data: " + scanTag;
            }
        }

        private void WriteTagAction()
        {
            if (scanTag == "")
                ShowMsg(MyConst.WARNING + Environment.NewLine + "Please scan the tag that you want to write data to.");
            else
            {
                // Write tag data.
                if (ur.WriteOneTag(scanTag, writeTag))
                {
                    ShowMsg(MyConst.INFO + Environment.NewLine + "New data has been written to the tag.");
                    ClearAction();
                }
            }
        }



        private void RefreshAction()
        {
            Auto_Get_COM_Ports();
        }


        private void ClearAction()
        {
            StatusMsg = defaultMsg;
            ScanTag = "";
            WriteTag = "";
        }


        private void Auto_Get_COM_Ports(bool bSilent = false)
        {
            try
            {
                ConnectReady = false;
                string strPORT = "";
                int iCount = 0;

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_SerialPort"))
                {
                    foreach (ManagementObject queryObj in searcher.Get())
                    {
                        if (queryObj["Caption"] != null && queryObj["Caption"].ToString().Trim().ToUpper().Contains("DENSO WAVE"))
                        {
                            if (!queryObj["Caption"].ToString().Trim().ToUpper().Contains("DISCONNECTED") && queryObj["DeviceID"] != null)
                            {
                                strPORT = queryObj["DeviceID"].ToString().ToUpper().Replace("COM", "");
                                iCount += 1;
                            }
                        }
                    }
                }

                if (iCount > 1)
                {
                    StatusMsg = "Warning: More than one DENSO WAVE USB-COM devices are detected. Disconnect the one you don't need.";

                    if (!bSilent)
                        Messenger.Default.Send(MyConst.WARNING + Environment.NewLine + "More than one DENSO WAVE USB-COM devices are detected. Disconnect the one you don't need",
                                            MsgType.MAIN_V);
                }
                else
                {
                    if (string.IsNullOrEmpty(strPORT))
                    {
                        StatusMsg = "Warning: No DENSO WAVE USB-COM device is connected to this PC!";

                        ComPort = "";
                        if (!bSilent)
                            Messenger.Default.Send(MyConst.WARNING + Environment.NewLine + "No DENSO WAVE USB-COM device is connected to this PC!", MsgType.MAIN_V);
                    }
                    else
                    {
                        ComPort = strPORT;      // Assign to variable.
                        ConnectReady = true;
                        StatusMsg = DefaultMsg;
                    }
                }
            }
            catch (ManagementException e)
            {
                dTimer.Stop();
                StatusMsg = "Error: Process failed while trying to retrieve COM port. Details: " + e.Message;
                Messenger.Default.Send(MyConst.ERROR + Environment.NewLine + "An error occurred while trying to retrieve COM port." + Environment.NewLine +
                                        Environment.NewLine + "Error detail: " + e.Message, MsgType.MAIN_V);
            }
        }




        private void DTimer_Tick(object sender, EventArgs e)
        {
            Auto_Get_COM_Ports(true);
        }


       

        private void Ur_OnTagRead(object sender, TagArgs e)
        {
            if (e != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(delegate
                {
                    int iCount = 0;
                    int iQty = 1;

                    //if (TagList == null)
                    //    TagList = new ObservableCollection<Tag>();
                    //else
                    //    iCount = TagList.Count;

                    //bool bExist = false;

                    //if (TagList.Count > 0)
                    //{
                    //    List<Tag> lst = TagList.ToList();

                    //    foreach (Tag ta in lst)
                    //    {
                    //        if (ta.Uii == e.Uii)
                    //            bExist = true;
                    //    }

                    //    if (bExist)
                    //    {
                    //        TagList.Clear();
                    //        TagList = new ObservableCollection<Tag>(lst);
                    //    }
                    //}

                    //if (!bExist)
                    //{
                    //    iCount++;

                    //    Tag t = new Tag();
                    //    t.Uii = e.Uii;
                    //    t.No = iCount;
                    //    t.Qty = iQty;
                    //    t.ReadDate = DateTime.Now.ToString("yyyy-MM-dd");
                    //    t.ReadTime = DateTime.Now.ToString("hh:mm:ss tt");

                    //    TagList.Add(t);
                    //}
                });
            }
        }




        #endregion
    }
}