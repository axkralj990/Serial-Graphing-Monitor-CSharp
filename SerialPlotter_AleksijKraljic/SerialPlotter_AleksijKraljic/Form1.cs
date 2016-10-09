﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using ZedGraph;
using System.Diagnostics;
using System.IO;

namespace SerialPlotter_AleksijKraljic
{
    public partial class Form1 : Form
    {
        Measurement measurement = new Measurement();
        List<Channel> channels = new List<Channel>();

        string fileName = "measured_data.txt";

        List<string> write_D = new List<string>();

        Color[] lineColors = { Color.Blue, Color.Red, Color.Green, Color.Black, Color.Purple, Color.Orange };

        // new GraphPane for plotting
        GraphPane akMonitor = new GraphPane();
        // range of X-Axis
        double t_range = 4;
        // Min and Max values for Y-Axis
        double Y_max = 5;
        double Y_min = 0;

        List<CheckBox> channelSelectBoxes = new List<CheckBox>();

        public Form1()
        {
            InitializeComponent();
            
            // initial form object states
            btn_connect.Enabled = false;
            btn_disconnect.Enabled = false;
            btn_start.Enabled = false;
            btn_stop.Enabled = false;

            // get and set serial ports
            getAndWritePorts();

            // available baud rates
            string[] bauds = { "300", "1200", "2400", "4800", "9600", "19200", "38400", "57600", "74880", "115200", "230400", "250000"};
            baudBox.DataSource = bauds;
            baudBox.SelectedIndex = 4;

            fileNameBox.Text = fileName;

            channelSelectBoxes.Add(checkCh1);
            channelSelectBoxes.Add(checkCh2);
            channelSelectBoxes.Add(checkCh3);
            channelSelectBoxes.Add(checkCh4);
            channelSelectBoxes.Add(checkCh5);
            channelSelectBoxes.Add(checkCh6);

            // initial form object states
            channelSelectBoxes.ForEach(c => c.Enabled = true);
            checkAutoY.Checked = true;
            numericUDmaxY.Enabled = false;
            numericUDminY.Enabled = false;
            numericUDtime.Enabled = true;

            // graph apearance settings
            akMonitor = zedGraphControl1.GraphPane;
            akMonitor.Title.Text = "";
            akMonitor.XAxis.Title.Text = "";
            akMonitor.YAxis.Title.Text = "";
            akMonitor.XAxis.MajorGrid.IsVisible = true;
            akMonitor.YAxis.MajorGrid.IsVisible = true;

            fileNameBox.Enabled = true;
        }

        private void btn_refreshCOM_Click(object sender, EventArgs e)
        {
            getAndWritePorts();
        }

        private void getAndWritePorts()
        {
            // get port names
            string[] avports = SerialPort.GetPortNames();

            // write them to select box
            comBox.DataSource = avports;
            if (comBox.Items.Count > 0)
            {
                // select the first comport 
                comBox.SelectedIndex = 0;
            }
        }

        private void btn_connect_Click(object sender, EventArgs e)
        {
            serialPort1.PortName = comBox.Text;
            serialPort1.BaudRate = int.Parse(baudBox.SelectedItem.ToString());

            // Construct objects for measurement
            for (int i = 0; i < 6; i++)
            {
                channels.Add(new Channel(i));
            }
            
            try
            {
                serialPort1.Open();
            }
            catch
            {
                MessageBox.Show("No devices found");
            }

            if (serialPort1.IsOpen)
            {
                btn_connect.Enabled = false;
                btn_disconnect.Enabled = true;
                btn_start.Enabled = true;
                btn_stop.Enabled = false;
                comBox.Enabled = false;
                baudBox.Enabled = false;
                btn_refreshCOM.Enabled = false;
                serialPort1.DataReceived += new SerialDataReceivedEventHandler(SerialPort1_DataReceived);
                serialPort1.Write("b");
                channelSelectBoxes.ForEach(c => c.Enabled = true);
            }
        }

        private void SerialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
                if (serialPort1.IsOpen)
                {
                    try { measurement.RxString += serialPort1.ReadLine(); }
                    catch { }
                }

                measurement.splitReceivedString();

                channels.ForEach(c => c.timeStamp = measurement.timeStamp);
                for (int i = 0; i < measurement.numOfDataReceived; i++)
                {
                    channels[i].measured_data = measurement.splittedData[i];
                }
              
                BeginInvoke(new EventHandler(toBuffer));

                if (saveCheckBox.Checked)
                {
                    measurement.recordData();
                }

                measurement.clearRxString(); 
        }

        private void btn_start_Click(object sender, EventArgs e)
        {
            serialPort1.Write("a");
            btn_start.Enabled = false;
            btn_stop.Enabled = true;
            btn_disconnect.Enabled = false;

            timer1.Start();
            measurement.start();

            akMonitor.CurveList.Clear();

            channels.ForEach(c => c.clearOnStart());
            measurement.clearOnStart();
            write_D.Clear();

            for (int i = 0; i < measurement.numOfDataReceived; i++)
            {
                channels[i].lineColor = lineColors[i];
                channels[i].curve = akMonitor.AddCurve(null, channels[i].ringBuffer, channels[i].lineColor, SymbolType.None);
                channels[i].setLineWidth(1);
            }

            for (int i = 0; i < 6; i++)
            {
                if (i < measurement.numOfDataReceived)
                    channelSelectBoxes[i].Enabled = true;
                else
                    channelSelectBoxes[i].Enabled = false;
            }
        }

        private void btn_stop_Click(object sender, EventArgs e)
        {
            serialPort1.Write("b");
            btn_stop.Enabled = false;
            btn_start.Enabled = true;
            btn_disconnect.Enabled = true;
            channelSelectBoxes.ForEach(c => c.Checked = false);

            timer1.Stop();
            measurement.stop();

            if (saveCheckBox.Checked)
            {
                save_measurements();
            }
        }

        private void save_measurements()
        {
            string folder_path = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
            
            //string fileName = DateTime.Now.ToString(@"MM\/dd\/yyyy h\:mm tt");

            string path = folder_path + "\\" + fileNameBox.Text;

            using (StreamWriter sw = File.CreateText(path))
            {
                sw.WriteLine("=====measurements=====");
                sw.WriteLine("|t|ch1|ch2|ch3|ch4|ch5|ch6|");

                for (int i=0;i<(measurement.recordedString.Count);i++)
                {
                    try
                    {
                        write_D.Add(measurement.recordedString[i]);
                    }
                    catch
                    {
                        write_D.Add("outOfRange");
                    }
                }

                foreach (string line in write_D)
                {
                    sw.WriteLine(line);
                }
            }
        }

        private void btn_disconnect_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
                System.Threading.Thread.Sleep(50);

                btn_connect.Enabled = true;
                btn_disconnect.Enabled = false;
                btn_start.Enabled = false;
                btn_stop.Enabled = false;
            }
            channelSelectBoxes.ForEach(c => c.Enabled = false);
            comBox.Enabled = true;
            baudBox.Enabled = true;
            btn_refreshCOM.Enabled = true;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (serialPort1.IsOpen) serialPort1.Write("b");
            System.Threading.Thread.Sleep(100);

            DialogResult dialogC = MessageBox.Show("Are you sure you want to exit?","Exit",MessageBoxButtons.YesNo);
            if (dialogC == DialogResult.Yes)
            {
                if (serialPort1.IsOpen) serialPort1.Close();
                Application.ExitThread();
            }
            else if (dialogC == DialogResult.No)
            {
                e.Cancel = true;
            }
        }

        
        private void comBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comBox.SelectedItem == null)
            {
                btn_connect.Enabled = false;
            }
            else
            {
                btn_connect.Enabled = true;
            }
        }

        private void toBuffer(object sender, EventArgs e)
        {
            measurement.setTimeStamp();
            try
            {
                for (int i = 0; i < measurement.numOfDataReceived; i++)
                {
                    if (channelSelectBoxes[i].Checked) { channels[i].addToBuffer(); }
                    else { channels[i].ringBuffer.Clear(); }
                }
            }
            catch { }
        }

        private void plot_data(object sender, EventArgs e)
        {
            zedGraphControl1.AxisChange();
            zedGraphControl1.Refresh();
            akMonitor.XAxis.Scale.Min = measurement.timeStamp / 1000 - t_range;
            akMonitor.XAxis.Scale.Max = measurement.timeStamp / 1000;

            if (!checkAutoY.Checked)
            {
                akMonitor.YAxis.Scale.Max = Y_max;
                akMonitor.YAxis.Scale.Min = Y_min;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            this.BeginInvoke(new EventHandler(plot_data));
        }
        
        private void checkAutoY_CheckedChanged(object sender, EventArgs e)
        {
            if (checkAutoY.Checked)
            {
                akMonitor.YAxis.Scale.MaxAuto = true;
                akMonitor.YAxis.Scale.MinAuto = true;
                numericUDmaxY.Enabled = false;
                numericUDminY.Enabled = false;
            }
            else
            {
                akMonitor.YAxis.Scale.MaxAuto = false;
                akMonitor.YAxis.Scale.MinAuto = false;
                numericUDmaxY.Enabled = true;
                numericUDminY.Enabled = true;
            }
        }

        private void numericUDtime_ValueChanged(object sender, EventArgs e)
        {
            t_range = Convert.ToDouble(numericUDtime.Value);
        }

        private void numericUDmaxY_ValueChanged(object sender, EventArgs e)
        {
            Y_max = Convert.ToDouble(numericUDmaxY.Value);
        }

        private void numericUDminY_ValueChanged(object sender, EventArgs e)
        {
            Y_min = Convert.ToDouble(numericUDminY.Value);
        }

        private void configDirectionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox1 aboutWindow = new AboutBox1();
            aboutWindow.Show();
        }

        private void saveCheckBox_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void numericUDlineWidth_ValueChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < measurement.numOfDataReceived; i++)
            {
                channels[i].setLineWidth(Convert.ToSingle(numericUDlineWidth.Value));
            }
        }
    }
}
