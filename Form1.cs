using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rfid2Lan
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        List<String> dataToSend = new List<string>();
        List<COMPortInfo> comports = new List<COMPortInfo>();
        List<USBDeviceInfo> usbDevices = new List<USBDeviceInfo>();

        private void Form1_Load(object sender, EventArgs e)
        {

            //var usbDevices = USBDeviceInfo.GetUSBDevices();

            //foreach (var usbDevice in usbDevices)
            //{
            //    Console.WriteLine("Device ID: {0}, PNP Device ID: {1}, Description: {2}",
            //        usbDevice.DeviceID, usbDevice.PnpDeviceID, usbDevice.Description);
            //}


            string[] ports = SerialPort.GetPortNames();

            comports = COMPortInfo.GetCOMPortsInfo();

            //foreach (COMPortInfo port in comports)
            //{
            //    cbSerialPort.Items.Add(port.Name);
            //};


            foreach (string port in ports)
            {
                cbSerialPort.Items.Add(port);
            };


            label5.Text = GetMacAddress();
            Console.WriteLine(label5.Text);

            tbServerIP.Text = Properties.Settings.Default["cfgIP"].ToString();
            tbPort.Text = Properties.Settings.Default["cfgPort"].ToString();
            tbResource.Text = Properties.Settings.Default["cfgRes"].ToString();
        }


        public class COMPortInfo
        {
            internal class ProcessConnection
            {
                public static ConnectionOptions ProcessConnectionOptions()
                {
                    ConnectionOptions options = new ConnectionOptions();
                    options.Impersonation = ImpersonationLevel.Impersonate;
                    options.Authentication = AuthenticationLevel.Default;
                    options.EnablePrivileges = true;
                    return options;
                }

                public static ManagementScope ConnectionScope(string machineName, ConnectionOptions options, string path)
                {
                    ManagementScope connectScope = new ManagementScope();
                    connectScope.Path = new ManagementPath(@"\\" + machineName + path);
                    connectScope.Options = options;
                    connectScope.Connect();
                    return connectScope;
                }
            }

            public string Name { get; set; }

            public string Description { get; set; }

            public COMPortInfo() { }

            public static List<COMPortInfo> GetCOMPortsInfo()
                {

                    List<COMPortInfo> comPortInfoList = new List<COMPortInfo>();
                    ConnectionOptions options = ProcessConnection.ProcessConnectionOptions();
                    ManagementScope connectionScope = ProcessConnection.ConnectionScope(Environment.MachineName, options, @"\root\CIMV2");
                    ObjectQuery objectQuery = new ObjectQuery("SELECT * FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0");
                    ManagementObjectSearcher comPortSearcher = new ManagementObjectSearcher(connectionScope, objectQuery);
                    using (comPortSearcher)
                    {

                        string caption = null;
                        foreach (ManagementObject obj in comPortSearcher.Get())
                        {
                            if (obj != null)
                            {
                                object captionObj = obj["Caption"];
                         
                                if (captionObj != null)
                                {
                                    caption = captionObj.ToString();
                                    Console.WriteLine(caption);

                                    if (caption.Contains("RFID"))
                                    {
                                        COMPortInfo comPortInfo = new COMPortInfo();
                                        comPortInfo.Name = caption.Substring(caption.LastIndexOf("(COM")).Replace("(", string.Empty).Replace(")", string.Empty);
                                        comPortInfo.Description = caption;
                                        comPortInfoList.Add(comPortInfo);
                                    }
                                }
                            }
                        }
                    }
                    return comPortInfoList;
                }
          }

         public class USBDeviceInfo
        {
            public USBDeviceInfo(string deviceID, string pnpDeviceID, string description)
            {
                this.DeviceID = deviceID;
                this.PnpDeviceID = pnpDeviceID;
                this.Description = description;
            }
            public string DeviceID { get; private set; }
            public string PnpDeviceID { get; private set; }
            public string Description { get; private set; }


            public static List<USBDeviceInfo> GetUSBDevices()
            {
                List<USBDeviceInfo> devices = new List<USBDeviceInfo>();

                ManagementObjectCollection collection;
                using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_USBHub")) //Win32_PnPEntity
                    collection = searcher.Get();

                foreach (var device in collection)
                {
                    devices.Add(new USBDeviceInfo(
                    (string)device.GetPropertyValue("DeviceID"),
                    (string)device.GetPropertyValue("PNPDeviceID"),
                    (string)device.GetPropertyValue("Description")
                    ));
                }

                collection.Dispose();
                return devices;
            }
        }


        /// <summary>
        /// Finds the MAC address of the NIC with maximum speed.
        /// </summary>
        /// <returns>The MAC address.</returns>
        private string GetMacAddress()
        {
            const int MIN_MAC_ADDR_LENGTH = 12;
            string macAddress = string.Empty;
            long maxSpeed = -1;

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                string tempMac = nic.GetPhysicalAddress().ToString();
                if (nic.Speed > maxSpeed &&
                    !string.IsNullOrEmpty(tempMac) &&
                    tempMac.Length >= MIN_MAC_ADDR_LENGTH)
                {
                    maxSpeed = nic.Speed;
                    macAddress = tempMac;
                }
            }
            return macAddress;
        }


        private void cbSerialPort_SelectedIndexChanged(object sender, EventArgs e)
        {
            serialPort1.PortName = cbSerialPort.SelectedItem.ToString();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
           
            try
            {
                serialPort1.Open();
                BackgroundWorker bw = new BackgroundWorker();
                bw.DoWork += Bw_DoWork;
                bw.RunWorkerAsync();

                rtbLog.AppendText("Port opened, waiting for data.. " + Environment.NewLine);
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
          
        }

        private void Bw_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                while (true)
                {
                    if (serialPort1.IsOpen)
                    {
                        //OK solo dalla seconda lettura del badge (alla prima lettura dataToSend[0]="")
                        dataToSend.Add(serialPort1.ReadLine());
                        var content = dataToSend;
                       // rtbLog.AppendText(dataToSend[0] + Environment.NewLine);
                    };
                    System.Threading.Thread.Sleep(500);
                };
            }
            catch(Exception exc)
            {
                Console.WriteLine(exc.Message);
            };
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            SendHttp(tbServerIP.Text, "admin", "admin");

        }


        public class WebClientWithTimeout : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest wr = base.GetWebRequest(address);
                wr.Timeout = 2000; // timeout in milliseconds (ms)
                return wr;
            }
        }

        public int SendHttp(string IP, string USER, string PASS)
        {

            if (dataToSend.Count() > 0)
            {
                WebClient webClient = new WebClientWithTimeout();
                try
                {
                    webClient.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(USER + ":" + PASS)));
                    webClient.Credentials = new NetworkCredential(USER, PASS);

                    string dds = string.Format("http://{0}:{1}/{2}?mac={3}&id={4}", tbServerIP.Text, tbPort.Text, tbResource.Text, GetMacAddress(), dataToSend[0].ToString().Replace("\n","").Replace("\r",""));
                    //string dds = string.Format("http://{0}:{1}/{2}?mac={3}&id={4}", tbServerIP.Text, tbPort.Text, tbResource.Text, GetMacAddress(), dataToSend[0].ToString().Replace("\n", "").Replace("\r", "").Replace("-", ""));
                    rtbLog.AppendText(dds+Environment.NewLine);
                    //Catturo la response del servizio
                    //byte[] xmlResponse = webClient.DownloadData(new Uri(dds));
                    //string s = System.Text.Encoding.Default.GetString(xmlResponse, 0, xmlResponse.Length);

                    dataToSend.Clear();

                    //dataToSend.RemoveAt(0);
                }
                catch (Exception exc)
                {
                    Console.WriteLine(exc.Message);
                    webClient.Dispose();
                    System.Threading.Thread.Sleep(1000);
                };
            };
            return 0;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default["cfgIP"] = tbServerIP.Text;
            Properties.Settings.Default["cfgPort"] = tbPort.Text;
            Properties.Settings.Default["cfgRes"] = tbResource.Text;
            Properties.Settings.Default.Save();
        }
    }
}
