using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CamControl
{
    public partial class Form2 : Form
    {
        public Form1 main_form = null; 
        
        public Form2(Form callingForm)
        {
            main_form = callingForm as Form1;
            InitializeComponent();
        }

        public Form2()
        {
            InitializeComponent();
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            this.Location = main_form.Location;

        }

        private void click_small_focus_dec(object sender, EventArgs e)
        {
            if (numericUpDown2.Value - 20 > numericUpDown2.Minimum)
                numericUpDown2.Value -= 20;
        }

        private void click_small_focus_inc(object sender, EventArgs e)
        {
            if (numericUpDown2.Value + 20 < numericUpDown2.Maximum)
                numericUpDown2.Value += 20;
        }

        private void click_big_focus_inc(object sender, EventArgs e)
        {
            if (numericUpDown2.Value + 100 < numericUpDown2.Maximum)
                numericUpDown2.Value += 100;
        }

        private void click_big_focus_dec(object sender, EventArgs e)
        {
            if (numericUpDown2.Value - 100 > numericUpDown2.Minimum)
                numericUpDown2.Value -= 100;
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            main_form.numericUpDown2.Value = numericUpDown2.Value;
        }

        private void Form2_FormClosed(object sender, FormClosedEventArgs e)
        {
            main_form.Show();
        }
    }
}
