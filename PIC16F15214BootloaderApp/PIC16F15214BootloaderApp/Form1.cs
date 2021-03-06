﻿using System;
using System.IO.Ports;
using System.Windows.Forms;
using WombatPanelWindowsForms;
using IntelHex;

/*
MIT License

Copyright (c) 2020 Broadwell Consulting Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

namespace PIC16F15214BootloaderApp
{
    public partial class Form1 : Form
    {
        SerialPort _port = null;
        string _filename = null;
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SerialPortSelector sps = new SerialPortSelector();
            sps.ShowDialog();
            if (sps.SelectedPort != null)
            {
                try
                {
                    _port = new SerialPort(sps.SelectedPort, 115200, Parity.None, 8, StopBits.One);

                    bSelectSerial.Enabled = false;
                    bDownload.Enabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void bDownload_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (_filename == null)
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _filename = ofd.FileName;
                    tbFilename.Text = _filename;
                }
            }

            if (_filename != null)

            {
           //     try
                {
                    DownloadHex(_filename);
                }
               /* catch (TimeoutException)
                {
                    lState.Text = "Timeout";
                    _port.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    lState.Text = "Exception Thrown";
                    _port.Close();
                }
               */
            }

        }

        private bool DownloadHex(string filename)
        {
            HexData data = new HexData(filename, true);
            data.Crop(0x140 * 2, 0x1000 * 2);
            data.Fill16(0x140 * 2, 0x1000 * 2, 0x3FFF);
            progressBar1.Value = 0x300;
            _port.Open();
            _port.ReadTimeout = 2000;
            lState.Text = "Initiating...";
            this.Refresh();
            InitiateDownload();
            lState.Text = "Erasing...";
            WaitForEraseCompletion();
            lState.Text = "Writing...";
            SendHex(data, 64);
            Verify(data);
            _port.Close();
            return true;
        }

        private bool InitiateDownload()
        {
            byte[] startSequence = { 0x52, 0xA3, 0x4D, 0xF6 };
            // byte[] startSequence = { 0x55 , 0xCC, 0x44, 0x80 };

            _port.DiscardInBuffer();
            _port.Write(startSequence, 0, 4);
            int priorTimout = _port.ReadTimeout;
            _port.ReadTimeout = 50; // 50 ms
            try
            {
                byte b = (byte)_port.ReadByte();
                _port.ReadTimeout = priorTimout;
                return (b == 'e');
            }
            catch
            {
                _port.ReadTimeout = priorTimout;
                return (false);
            }

        }

        private bool WaitForEraseCompletion()
        {

            try
            {
                byte b = (byte)_port.ReadByte();
                return (b == 'W');
            }
            catch
            {
                return (false);
            }
        }

        private bool SendHex(HexData data, uint pagesize)
        {
            for (uint i = data.LowestAddress; i < data.HighestAddress; i += pagesize)
            {
                _port.Write(data.Subarray(i, pagesize), 0, (int)pagesize);
                lState.Text = $"Writing... 0x{i:X2}";
                progressBar1.Value = (int)i;
                this.Refresh();

                int b = _port.ReadByte();
                if (b != (int)'W')
                {
                    return false;
                }

            }

            return (true);
        }

        private bool Verify(HexData data)
        {
            uint highestAddress = data.HighestAddress;
            uint lowestAddress = data.LowestAddress;
            uint length = (highestAddress - lowestAddress + 1);

            byte[] incomingdata = new byte[length];
            int count = 0;
            byte Rbyte = (byte)_port.ReadByte();
            progressBar1.Minimum = 0; progressBar1.Maximum = int.MaxValue;
            progressBar1.Value = (int)lowestAddress;
            progressBar1.Minimum = (int)lowestAddress;
            progressBar1.Maximum = (int)highestAddress + 1;
            while (count < length)
            {
                if (_port.BytesToRead > 0)
                {
                    incomingdata[count] = (byte)_port.ReadByte();
                    ++count;
                    if ((count % 128) == 0 )
                    {
                        lState.Text = $"Verifying... 0x{count:X2}";
                        progressBar1.Value = (int)(count + lowestAddress);
                        this.Refresh();
                    }
                }
            }

            for (count = 0; count < length; ++count)
            {
                byte m = (byte)data.Memory[(uint)(count + lowestAddress)];
                byte ic = incomingdata[count];
                int address = (int)(count + lowestAddress) / 2;
                if (m != ic)
                {
                    lState.Text = $"Verify failed at byte 0x{count + lowestAddress:X2}, expected 0x{m:X2}, got 0x{ic:X2}";
                    progressBar1.Value = 0x300;
                    return (false);
                }
            }

            lState.Text = "Download Complete";
            return (true);
        }
    }
}
