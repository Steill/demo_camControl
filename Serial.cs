using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Ports;
using System.Windows.Forms;

namespace CamControl
{
    internal class Serial
    {
        public SerialPort port_ = null;

        public bool TryWrite(string command)
        {
            if (port_ == null)
                return false;

            bool result = false;

            try
            {
                port_.Write(command);
                result = true;
            }
            catch
            {
                MessageBox.Show("COM Device disconnected");
                port_.Close();
                port_ = null;
            }
            return result;
        }

        public bool TryWrite(Byte[] command)
        {
            if (port_ == null)
                return false;

            bool result = false;

            try
            {
                port_.Write(command, 0, command.Length);
                result = true;
            }
            catch
            {
                MessageBox.Show("COM Device disconnected");
                port_.Close();
                port_ = null;
            }
            return result;
        }

        public static Serial Open(string portName, int baudRate = 38400)
        {
            Serial port = new Serial(portName, baudRate);
            if (port.port_ == null) return null;

            return port;
        }

        private Serial(string portName, int baudRate)
        {
            port_ = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
            port_.ReadTimeout = 500;
            port_.WriteTimeout = 500;

            port_.DataReceived += new SerialDataReceivedEventHandler(DataRecieved);

            if (port_.IsOpen)
            {
                MessageBox.Show("COM Device busy");
                port_ = null;
                return;
            }
            port_.Open();
        }

        private void DataRecieved(object sender, SerialDataReceivedEventArgs e)
        {

        }
    }
}
