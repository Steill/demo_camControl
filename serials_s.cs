using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Text;

namespace demowinformcs
{
    public partial class Form1
    {
        public SerialPort AstroPort_ = null;
        public SerialPort LightPort_ = null;

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox comboBox = (ComboBox)sender;
            AstroPort_ = new SerialPort(comboBox.SelectedItem.ToString(), 38400, Parity.None, 8, StopBits.One);
            AstroPort_.DataReceived += new SerialDataReceivedEventHandler(port_dataRecieved);
            AstroPort_.ReadTimeout = 500;
            AstroPort_.WriteTimeout = 500;

            if (AstroPort_.IsOpen)
            {
                MessageBox.Show("COM Device busy");
                LightPort_ = null;
                return;
            }
            AstroPort_.Open();

            if (!timer3.Enabled) timer3.Start();

            button6.Enabled = true;
            button7.Enabled = true;
            button10.Enabled = true;
            button11.Enabled = true;
            comboBox3.Enabled = true;
            numericUpDown2.Enabled = true;
        }

        private void port_dataRecieved(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort serialPort = (SerialPort)sender;
            string data = "<< " + serialPort.ReadExisting();
            listBox1.Invoke((MethodInvoker)delegate
            {
                listBox1.Items.Insert(0, data);
            });
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            if (AstroPort_ == null)
                return;

            int focusVal = (int)numericUpDown2.Value;

            if (focusVal == lastFocusVal) return;
            if ((uint)focusVal > 12000) return;
            if (AstroPort_.IsOpen)
            {
                string command = String.Format("M{0}#", focusVal);
                listBox1.Items.Insert(0, ">> " + command);
                AstroPort_.Write(command);
                lastFocusVal = focusVal;
            }
            else AstroPort_ = null;
        }


        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            //listBox1.Items.Add(comboBox3.SelectedIndex.ToString());

            if (AstroPort_ == null)
                return;

            try
            {
                string command = String.Format("A{0}#", comboBox3.SelectedIndex);
                AstroPort_.Write(command);
                listBox1.Items.Insert(0, ">> " + command);
            }
            catch
            {
                MessageBox.Show("AstroMech disconnected");
                AstroPort_.Close();
                AstroPort_ = null;
            }
        }

        private void comboBox4_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox comboBox = (ComboBox)sender;
            LightPort_ = new SerialPort(comboBox.SelectedItem.ToString(), 38400, Parity.None, 8, StopBits.One);
            LightPort_.DataReceived += new SerialDataReceivedEventHandler(port_dataRecieved);
            LightPort_.ReadTimeout = 500;
            LightPort_.WriteTimeout = 500;

            if (LightPort_.IsOpen)
            {
                MessageBox.Show("COM Device busy");
                LightPort_ = null;
                return;
            }

            timer4.Start();
            LightPort_.Open();

            if (comboBox5.SelectedIndex == -1)
                comboBox5.SelectedIndex = 0;

            button8.Enabled = true;
            comboBox5.Enabled = true;
            numericUpDown3.Enabled = true;
            trackBar16.Enabled = true;
            foreach (trackbarWithLastVal trackBar in TrackBars)
            {
                trackBar.bar.Enabled = true;
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            if (LightPort_ == null)
                return;

            if (comboBox5.SelectedIndex < 0)
                return;

            Encoding ascii = Encoding.ASCII;

            byte lbyte = (byte)(comboBox5.SelectedIndex);
            byte bbyte = (byte)numericUpDown3.Value;
            string command = String.Format("{0}\n{1}", lbyte, bbyte);
            Byte[] bytes = ascii.GetBytes(command);
            listBox1.Items.Insert(0, String.Format(">> {0} {1}// {2} bytes sent", lbyte, bbyte, command.Length));

            try
            {
                LightPort_.Write(bytes, 0, bytes.Length);
            }
            catch
            {
                MessageBox.Show("COM Device disconnected");
                LightPort_.Close();
                LightPort_ = null;
            }
        }

        private void RefreshComs()
        {
            string[] ports = SerialPort.GetPortNames();
            comboBox4.Items.Clear();
            comboBox2.Items.Clear();
            foreach (string port in ports)
            {
                comboBox4.Items.Add(port);
                comboBox2.Items.Add(port);
            }
            listBox1.Items.Insert(0, "-- COM Ports Refreshed --");
        }

        private void button12_Click(object sender, EventArgs e)
        {
            if (AstroPort_ == null)
                return;

            try
            {
                AstroPort_.Write("P#");
            }
            catch
            {
                MessageBox.Show("COM Device disconnected");
                AstroPort_.Close();
                AstroPort_ = null;
            }
        }

        private void timer4_Tick(object sender, EventArgs e)
        {
            if (LightPort_ == null)
                return;

            foreach (trackbarWithLastVal tbar in TrackBars)
            {
                if (tbar.bar.Value != tbar.lastValue)
                {
                    try
                    {
                        string tag = tbar.bar.Tag.ToString();
                        string newVal = tbar.bar.Value.ToString();
                        string command = String.Format("{0}\n{1}", tag, newVal);

                        tbar.lastValue = tbar.bar.Value;
                        listBox1.Items.Insert(0, String.Format(">> {0} {1}// {2} bytes sent", tag, newVal, command.Length));
                        LightPort_.Write(command);
                        return;
                    }
                    catch
                    {
                        MessageBox.Show("COM Device disconnected");
                        timer4.Stop();
                        foreach (trackbarWithLastVal trackBar in TrackBars)
                        {
                            trackBar.bar.Value = 50;
                            trackBar.bar.Enabled = false;
                        }
                        LightPort_.Close();
                        LightPort_ = null;
                    }
                }
            }
        }
    }
}
