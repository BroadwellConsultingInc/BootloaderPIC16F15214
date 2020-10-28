using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

namespace WombatPanelWindowsForms
{
    public partial class SerialPortSelector : Form
    {
        public SerialPortSelector()
        {
            InitializeComponent();
        }
        public string SelectedPort = null;
        private void SerialPortSelector_Load(object sender, EventArgs e)
        {
            listBox1.Items.AddRange(SerialPort.GetPortNames());
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex != -1)
            {
                SelectedPort = (string)listBox1.SelectedItem;
                this.Close();
            }


        }

        private void button2_Click(object sender, EventArgs e)
        {
            SelectedPort = null;
            this.Close();
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex != -1)
            {
                SelectedPort = (string)listBox1.SelectedItem;
                this.Close();
            }

        }
    }
}
