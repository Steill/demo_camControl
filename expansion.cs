using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CamControl
{
    public partial class Form1
    {

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
        private void Form1_Load(object sender, EventArgs e)
        {
            button2.Enabled = false;
            button3.Enabled = false;
            trackBar1.Enabled = false;
            trackBar2.Enabled = false;
            trackBar3.Enabled = false;
            AutoExpoCheckBox.Enabled = false;
            comboBox1.Enabled = false;
            trackBar2.SetRange(Toupcam.TEMP_MIN, Toupcam.TEMP_MAX);
            trackBar3.SetRange(Toupcam.TINT_MIN, Toupcam.TINT_MAX);
        }

        private void DelegateOnEventCallback(Toupcam.eEVENT evt)
        {
            /* this is call by internal thread of toupcam.dll which is NOT the same of UI thread.
             * Why we use BeginInvoke, Please see:
             * http://msdn.microsoft.com/en-us/magazine/cc300429.aspx
             * http://msdn.microsoft.com/en-us/magazine/cc188732.aspx
             * http://stackoverflow.com/questions/1364116/avoiding-the-woes-of-invoke-begininvoke-in-cross-thread-winform-event-handling
             */
            BeginInvoke((Action)(() =>
            {
                /* this run in the UI thread */
                if (cam_ != null)
                {
                    switch (evt)
                    {
                        case Toupcam.eEVENT.EVENT_ERROR:
                            OnEventError();
                            break;
                        case Toupcam.eEVENT.EVENT_DISCONNECTED:
                            OnEventError();
                            break;
                        case Toupcam.eEVENT.EVENT_EXPOSURE:
                            OnEventExposure();
                            break;
                        case Toupcam.eEVENT.EVENT_IMAGE:
                            OnEventImage();
                            break;
                        case Toupcam.eEVENT.EVENT_STILLIMAGE:
                            OnEventStillImage();
                            break;
                        case Toupcam.eEVENT.EVENT_TEMPTINT:
                            OnEventTempTint();
                            break;
                        default:
                            break;
                    }
                }
            }));
        }
    }

    public partial class Form2
    {

    }
}