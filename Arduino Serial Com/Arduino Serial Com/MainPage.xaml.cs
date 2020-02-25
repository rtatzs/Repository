using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Arduino_Serial_Com
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // <summary>   
        /// Private variables   
        /// </summary>   
        private SerialDevice serialPort = null;
        DataWriter dataWriteObject = null;
        DataReader dataReaderObject = null;
        private ObservableCollection<DeviceInformation> listOfDevices;
        private CancellationTokenSource ReadCancellationTokenSource;

        public MainPage()
        {
            this.InitializeComponent();
            btnAccendiled.IsEnabled = false;
            btnSpegniled.IsEnabled = false;
            listOfDevices = new ObservableCollection<DeviceInformation>();
            ListAvailablePorts();
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
                btnAccendiled.IsEnabled = true;
                btnSpegniled.IsEnabled = true;
                lstSerialDevices.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                tbkAllarmi.Text = ex.Message;
            }
        }

        private async void ButtonClick(object sender, RoutedEventArgs e)
        {
            var buttonClicked = sender as Button;
            switch (buttonClicked.Name)
            {
                case "btnSerialConnect":
                    SerialPortConfiguration();
                    break;
                case "btnSerialDisconnect":
                    SerialPortDisconnect();
                    break;
                case "btnAccendiled":
                    if (serialPort != null)
                    {
                        dataWriteObject = new DataWriter(serialPort.OutputStream);
                        await ManageLed("2");
                    }
                    if (dataWriteObject != null)
                    {
                        dataWriteObject.DetachStream();
                        dataWriteObject = null;
                    }
                    break;
                case "btnSpegniled":
                    if (serialPort != null)
                    {
                        dataWriteObject = new DataWriter(serialPort.OutputStream);
                        await ManageLed("1");
                    }
                    if (dataWriteObject != null)
                    {
                        dataWriteObject.DetachStream();
                        dataWriteObject = null;
                    }
                    break;
                case "btnPulse1000ms":
                    tbkStatusLed.Text = String.Empty;
                    //if (serialPort != null)
                    //{
                    //    dataWriteObject = new DataWriter(serialPort.OutputStream);
                    //    await ManageLed("3");
                    //}
                    //if (dataWriteObject != null)
                    //{
                    //    dataWriteObject.DetachStream();
                    //    dataWriteObject = null;
                    //}
                    break;
                case "btnPulse2000ms":
                    tbkStatusLed.Text = String.Empty;
                    //if (serialPort != null)
                    //{
                    //    dataWriteObject = new DataWriter(serialPort.OutputStream);
                    //    await ManageLed("4");
                    //}
                    //if (dataWriteObject != null)
                    //{
                    //    dataWriteObject.DetachStream();
                    //    dataWriteObject = null;
                    //}
                    break;
            }
        }

        private async void SerialPortConfiguration()
        {
            var selection = lstSerialDevices.SelectedItems;
            if (selection.Count <= 0)
            {
                tbkAllarmi.Text = "Select an object for the serial connection!";
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
                tbkAllarmi.Text = "Serial  Port Configured Properly!";
                ReadCancellationTokenSource = new CancellationTokenSource();
                Listen();
            }
            catch (Exception ex)
            {
                tbkAllarmi.Text = ex.Message;
                btnAccendiled.IsEnabled = false;
                btnSpegniled.IsEnabled = false;
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
                tbkAllarmi.Text = ex.Message;
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
                    tbkAllarmi.Text = "Value sent correctly";
                }
            }
            else
            {
                tbkAllarmi.Text = "No value sent";
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
                tbkAllarmi.Text = ex.Message;
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    CloseDevice();
                }
                else
                {
                    tbkAllarmi.Text = "Task canceled";
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
                String tmp = tbkStatusLed.Text;
                //tbkStatusLed.Text = tmp+"\n"+dataReaderObject.ReadString(bytesRead);
                String readValue = dataReaderObject.ReadString(dataReaderObject.UnconsumedBufferLength);
                if (readValue.Length >= 11)
                {
                    tbkStatusLed.Text = readValue;
                    tbkStatusLed.SelectionStart = tbkStatusLed.Text.Length;
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
            btnAccendiled.IsEnabled = false;
            btnSpegniled.IsEnabled = false;
            listOfDevices.Clear();
        }
    }
}
