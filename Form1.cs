using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
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
        private void Form1_Load(object sender, EventArgs e)
        {
            string[] ports = SerialPort.GetPortNames();
            
          
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
                        dataToSend.Add(serialPort1.ReadLine());
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
                    rtbLog.AppendText(dds+Environment.NewLine);
                    byte[] xmlResponse = webClient.DownloadData(new Uri(dds));
                    string s = System.Text.Encoding.Default.GetString(xmlResponse, 0, xmlResponse.Length);

                    dataToSend.RemoveAt(0);
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
