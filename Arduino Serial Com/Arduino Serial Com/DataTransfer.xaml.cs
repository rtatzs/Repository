using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Arduino_Serial_Com
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DataTransfer : Page, INotifyPropertyChanged
    {
        private String _subTitle;
        private SerialDevice serialPort = null;
        DataWriter dataWriteObject = null;
        DataReader dataReaderObject = null;
        private ObservableCollection<DeviceInformation> listOfDevices;
        private CancellationTokenSource ReadCancellationTokenSource;


        public event PropertyChangedEventHandler PropertyChanged;



        public String SubTitle
        {
            get { return _subTitle; }
            set
            {
                _subTitle = value;
                OnPropertyChanged(nameof(SubTitle));
            }
        }

        public DataTransfer()
        {
            this.InitializeComponent();
            DataContext = this;
            listOfDevices = new ObservableCollection<DeviceInformation>();
            ListAvailablePorts();
            ConnectionList.SelectedIndex = 0;
        }

        private void EON_MainList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView listView = (ListView)sender;                                      //Gets current sender  
            if (listView.SelectedIndex == -1) { return; }                              //Trap out of bounds

            //Can be properly implemented if using binding
            switch (listView.SelectedIndex)
            {
                case 0:
                    SubTitle = "Serial (RS232)";
                    break;
                case 1:
                    SubTitle = "ESP8266";
                    break;
                case 2:
                    SubTitle = "Bluetooth";
                    break;
                case 3:
                    SubTitle = "Wifi";
                    break;
                case 4:
                    SubTitle = "RG45";
                    break;
                default:
                    SubTitle = "";
                    break;
            }

            mainSplitView.IsPaneOpen = false;
        }
        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            mainSplitView.IsPaneOpen = !mainSplitView.IsPaneOpen;
        }
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ConnectClick(object sender, RoutedEventArgs e)
        {
            var buttonClicked = sender as Button;
            switch (buttonClicked.Name)
            {
                case "btnSerialConnect":
                    SerialPortConfiguration();
                    break;
                case "btnDisconnect":
                    SerialPortDisconnect();
                    break;
                case "btnClear":
                    txtDataRecieved.Text = String.Empty;
                    txtResult.Text = String.Empty;
                    break;
            }
        }

        private async void SerialPortConfiguration()
        {
            var selection = lstSerialDevices.SelectedItems;
            if (selection.Count <= 0)
            {
                txtDetail.Text = "Select an object for the serial connection.";
                return;
            }
            DeviceInformation entry = (DeviceInformation)selection[0];
            try
            {
                serialPort = await SerialDevice.FromIdAsync(entry.Id);
                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(500); //changed from 1000
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(250);
                serialPort.BaudRate = 500000;
                serialPort.Parity = SerialParity.None;
                serialPort.StopBits = SerialStopBitCount.One;
                serialPort.DataBits = 8;
                serialPort.Handshake = SerialHandshake.None;
                txtDetail.Text = entry.Id.ToString().Substring(0, 20) + "...";
                txtResult.Text = "Serial  Port Configured Properly.";
                ReadCancellationTokenSource = new CancellationTokenSource();
                Listen();
            }
            catch (Exception ex)
            {
                txtDetail.Text = ex.Message;
            }
        }

        private void SerialPortDisconnect()
        {
            try
            {
                CancelReadTask();
                CloseDevice();
                ListAvailablePorts();
            }
            catch (Exception ex)
            {
                txtDetail.Text = ex.Message;
            }
        }

        private async void ListAvailablePorts()
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);
                for (int i = 0; i < dis.Count; i++)
                {
                    listOfDevices.Add(dis[i]);
                }
                lstSerialDevices.ItemsSource = listOfDevices;
                lstSerialDevices.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                txtDetail.Text = ex.Message;
            }
        }

        private async Task ManageLed(string value)
        {
            var accendiLed = value;
            Task<UInt32> storeAsyncTask;
            if (accendiLed.Length != 0)
            {
                dataWriteObject.WriteString(accendiLed);
                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();
                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    txtDataRecieved.Text = "Value sent correctly";
                }
            }
            else
            {
                txtResult.Text = "No value sent";
            }
        }

        private async void Listen()
        {
            try
            {
                if (serialPort != null)
                {
                    dataReaderObject = new DataReader(serialPort.InputStream);
                    dataReaderObject.UnicodeEncoding = UnicodeEncoding.Utf8;
                    dataReaderObject.ByteOrder = ByteOrder.LittleEndian;
                    while (true) { await ReadData(ReadCancellationTokenSource.Token); }
                }
            }
            catch (Exception ex)
            {
                txtDetail.Text = ex.Message;
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    CloseDevice();
                }
                else
                {
                    txtResult.Text = "Task canceled";
                }
            }
            finally
            {
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }

        private async Task ReadData(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;
            //uint ReadBufferLength = 1024;
            uint ReadBufferLength = 11;
            cancellationToken.ThrowIfCancellationRequested();
            dataReaderObject.InputStreamOptions = InputStreamOptions.ReadAhead;
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);
            UInt32 bytesRead = await loadAsyncTask;
            if (bytesRead > 0)
            {
                String tmp = txtDataRecieved.Text;
                //tbkStatusLed.Text = tmp+"\n"+dataReaderObject.ReadString(bytesRead);
                String readValue = dataReaderObject.ReadString(dataReaderObject.UnconsumedBufferLength);
                if (readValue.Length >= 11)
                {
                    txtDataRecieved.Text = readValue;
                    txtDataRecieved.SelectionStart = txtDataRecieved.Text.Length;
                }
            }
        }

        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }

        private void CloseDevice()
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
            }
            serialPort = null;
            listOfDevices.Clear();
        }
    }
}
