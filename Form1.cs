using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;

namespace CamControl
{
    public partial class Form1 : Form
    {
        Toupcam cam_ = null;
        Bitmap bmp_ = null;

        Serial AstroPort_ = null;
        Serial LightPort_ = null;

        uint imgCount_ = 0;
        long lastTime = 0;
        int lastFocusVal = 0;


        public static Point GridSize = new Point(3, 3);
        public static List<labelWithRect> GridLabels = new List<labelWithRect>();

        public static Dictionary<TrackBar, int> TrackBars = new Dictionary<TrackBar, int>();

        [Serializable] class setting
        {
            public string Name;
            public int Value;

            public setting(string name_, int val_)
            {
                Name = name_;
                Value = val_;
            }
        }

        public class labelWithRect
        {
            public Label label {get;}
            public Point leftUpper;
            public Point rightLower;

            public int Vmin = 0;
            public int Vmax = 0;
            public int Vavg = 0;
            public int Vrms = 0;

            public labelWithRect(Label l_, Point lu, Point rl)
            {
                label = l_;
                leftUpper = lu;
                rightLower = rl;
            }
        }

        private void OnEventError()
        {
            cam_.Close();
            cam_ = null;

            button1.BackColor = Color.Tomato;
            button1.Text = "Disconnected";
            timer2.Start();
            //MessageBox.Show("Generic error.");
        }

        private void OnEventExposure()
        {
            uint nTime = 0;
            if (cam_.get_ExpoTime(out nTime))
            {
                float val = nTime / 1000;
                if ((int)val <= 0) return;
                trackBar1.Value = (int)val;
                numericUpDown1.Value = (decimal)val;
                //label1.Text = nTime.ToString();
            }
        }

        private void OnEventImage()
        {
            long time = DateTime.Now.Ticks;
            long frameTime = (time - lastTime) / 10000;
            //if (frameTime < 125) return;

            label4.Text = "Frame Time: " + frameTime.ToString();
            lastTime = time;


            if (bmp_ != null)
            {
                Toupcam.FrameInfoV3 info = new Toupcam.FrameInfoV3();
                bool bOK = false;
                try
                {
                    BitmapData bmpdata = bmp_.LockBits(new Rectangle(0, 0, bmp_.Width, bmp_.Height), ImageLockMode.WriteOnly, bmp_.PixelFormat);
                    try
                    {
                        bOK = cam_.PullImageV3(bmpdata.Scan0, 0, 24, bmpdata.Stride, out info); // check the return value
                    }
                    finally
                    {
                        bmp_.UnlockBits(bmpdata);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
                if (bOK)
                {
                    bmp_.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    pictureBox1.Image = bmp_;
                    EditLiveBmp();
                }
            }
        }

        private void OnEventStillImage()
        {
            Toupcam.FrameInfoV3 info = new Toupcam.FrameInfoV3();
            if (cam_.PullImageV3(IntPtr.Zero, 1, 24, 0, out info))   /* peek the width and height */
            {
                Bitmap sbmp = new Bitmap((int)info.width, (int)info.height, PixelFormat.Format24bppRgb);
                bool bOK = false;
                try
                {
                    BitmapData bmpdata = sbmp.LockBits(new Rectangle(0, 0, sbmp.Width, sbmp.Height), ImageLockMode.WriteOnly, sbmp.PixelFormat);
                    try
                    {
                        bOK = cam_.PullImageV3(bmpdata.Scan0, 1, 24, bmpdata.Stride, out info); // check the return value
                    }
                    finally
                    {
                        sbmp.UnlockBits(bmpdata);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
                if (bOK)
                    sbmp.Save(string.Format("IMG_{0}.jpg", ++imgCount_), ImageFormat.Jpeg);
                cam_.put_Option(Toupcam.eOPTION.OPTION_BINNING, 2);
            }
        }

        public Form1()
        {
            InitializeComponent();

            pictureBox1.Width = ClientRectangle.Right - pictureBox1.Left - button1.Top;
            pictureBox1.Height = ClientRectangle.Height - 2 * button1.Top;

            CreateLabelGrid(GridSize);
            PutLightTrackbarsToList();
        }

        private void Form_SizeChanged(object sender, EventArgs e)
        {
            pictureBox1.Width = ClientRectangle.Right - pictureBox1.Left - button1.Top;
            pictureBox1.Height = ClientRectangle.Height - 2 * button1.Top;
            ArrangeLabels(GridSize);
        }

        private void OnStart(object sender, EventArgs e)
        {
            if (cam_ != null)
                return;

            Toupcam.DeviceV2[] arr = Toupcam.EnumV2();
            if (arr.Length <= 0)
            {
                MessageBox.Show("No camera found.");
                return;
            }

            if (1 == arr.Length)
                startDevice(arr[0].id);
            else
            {
                ContextMenuStrip ctxmenu = new ContextMenuStrip();
                ctxmenu.ItemClicked += (nsender, ne) =>
                {
                    startDevice((string)(ne.ClickedItem.Tag));
                };
                for (int i = 0; i < arr.Length; ++i)
                    ctxmenu.Items.Add(arr[i].displayname).Tag = arr[i].id;
                ctxmenu.Show(button1, 0, 0);
            }
            
            button1.BackColor = Color.Green;
            button1.Text = "Connected";
            button1.Enabled = false;
        }

        private void startDevice(string id)
        {
            cam_ = Toupcam.Open(id);
            if (cam_ != null)
            {
                AutoExpoCheckBox.Enabled = true;
                comboBox1.Enabled = true;
                button2.Enabled = true;
                InitExpoTimeRange();
                if (cam_.MonoMode)
                {
                    trackBar2.Enabled = false;
                    trackBar3.Enabled = false;
                    button3.Enabled = false;
                }
                else
                {
                    trackBar2.Enabled = true;
                    trackBar3.Enabled = true;
                    button3.Enabled = true;
                    OnEventTempTint();
                }

                uint resnum = cam_.ResolutionNumber;
                uint eSize = 0;
                if (cam_.get_eSize(out eSize))
                {
                    for (uint i = 0; i < resnum; ++i)
                    {
                        int w = 0, h = 0;
                        if (cam_.get_Resolution(i, out w, out h))
                            comboBox1.Items.Add(w.ToString() + "*" + h.ToString());
                    }
                    comboBox1.SelectedIndex = (int)eSize;

                    int width = 0, height = 0;
                    if (cam_.get_Size(out width, out height))
                    {
                        /* The backend of Winform is GDI, which is different from WPF/UWP/WinUI's backend Direct3D/Direct2D.
                         * We use their respective native formats, Bgr24 in Winform, and Bgr32 in WPF/UWP/WinUI
                         */
                        bmp_ = new Bitmap(width / 2, height / 2, PixelFormat.Format24bppRgb);
                        if (!cam_.StartPullModeWithCallback(new Toupcam.DelegateEventCallback(DelegateOnEventCallback)))
                            MessageBox.Show("Failed to start camera.");
                        else
                        {
                            bool autoexpo = AutoExpoCheckBox.Checked;
                            cam_.put_AutoExpoEnable(autoexpo);
                            //cam_.get_AutoExpoEnable(out autoexpo);
                            //checkBox1.Checked = autoexpo;
                            trackBar1.Enabled = !autoexpo;
                            PushSettings();
                        }
                    }
                }

                //timer1.Start();
            }
        }

        private void InitExpoTimeRange()
        {
            uint nMin = 0, nMax = 0, nDef = 0;
            if (cam_.get_ExpTimeRange(out nMin, out nMax, out nDef))
            {
                int max = (int)(nMax / 1000);
                trackBar1.SetRange(1, max);
                numericUpDown1.Minimum = 1;
                numericUpDown1.Maximum = max;
            }

                OnEventExposure();
        }

        private void OnSnap(object sender, EventArgs e)
        {
            cam_.put_Option(Toupcam.eOPTION.OPTION_BINNING, 1);
            if (cam_ != null)
            {
                if (cam_.StillResolutionNumber <= 0)
                    bmp_?.Save(string.Format("IMG_{0}.jpg", ++imgCount_), ImageFormat.Jpeg);
                else
                {
                    ContextMenuStrip ctxmenu = new ContextMenuStrip();
                    ctxmenu.ItemClicked += (nsender, ne) =>
                    {
                        uint k = (uint)(ne.ClickedItem.Tag); //unbox
                        if (k < cam_.StillResolutionNumber)
                            cam_.Snap(k);
                        //cam_.put_Option(Toupcam.eOPTION.OPTION_BINNING, 2);
                    };
                    for (uint i = 0; i < cam_.ResolutionNumber; ++i)
                    {
                        int w = 0, h = 0;
                        cam_.get_Resolution(i, out w, out h);
                        ctxmenu.Items.Add(string.Format("{0} * {1}", w, h)).Tag = i; // box
                    }
                    ctxmenu.Show(button2, 0, 0);
                }
            }
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            cam_?.Close();
            cam_ = null;
        }

        private void OnSelectResolution(object sender, EventArgs e)
        {
            if (cam_ != null)
            {
                uint eSize = 0;
                if (cam_.get_eSize(out eSize))
                {
                    if (eSize != comboBox1.SelectedIndex)
                    {
                        cam_.Stop();
                        cam_.put_eSize((uint)comboBox1.SelectedIndex);

                        InitExpoTimeRange();
                        OnEventTempTint();

                        int width = 0, height = 0;
                        if (cam_.get_Size(out width, out height))
                        {
                            bmp_ = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                            cam_.StartPullModeWithCallback(new Toupcam.DelegateEventCallback(DelegateOnEventCallback));
                        }
                    }
                }
            }
        }

        private void ToggleAutoExposure(object sender, EventArgs e)
        {
            cam_?.put_AutoExpoEnable(AutoExpoCheckBox.Checked);
            trackBar1.Enabled = !AutoExpoCheckBox.Checked;
            numericUpDown1.Enabled = !AutoExpoCheckBox.Checked;
        }

        private void OnExpoValueChange(object sender, EventArgs e)
        {
            if ((!AutoExpoCheckBox.Checked) && (cam_ != null))
            {
                uint n = (uint)trackBar1.Value;
                numericUpDown1.Value = n;
                cam_.put_ExpoTime(n * 1000);
                //label1.Text = n.ToString();
            }
        }

        private void OnEventTempTint()
        {
            int nTemp = 0, nTint = 0;
            if (cam_.get_TempTint(out nTemp, out nTint))
            {
                label2.Text = nTemp.ToString();
                label3.Text = nTint.ToString();
                trackBar2.Value = nTemp;
                trackBar3.Value = nTint;
            }
        }

        private void OnWhiteBalanceOnce(object sender, EventArgs e)
        {
            cam_?.AwbOnce();
        }

        private void OnTempTintChanged(object sender, EventArgs e)
        {
            cam_?.put_TempTint(trackBar2.Value, trackBar3.Value);
            label2.Text = trackBar2.Value.ToString();
            label3.Text = trackBar3.Value.ToString();
        }

        private void OnTimer1(object sender, EventArgs e)
        {
            if (cam_ != null)
            {
                uint nFrame = 0, nTime = 0, nTotalFrame = 0;
                if (cam_.get_FrameRate(out nFrame, out nTime, out nTotalFrame) && (nTime > 0))
                    label4.Text = String.Format("Frame Time: {0}", nTime.ToString());
                    //label4.Text = string.Format("{0}; fps = {1:#.0}", nTotalFrame, ((double)nFrame) * 1000.0 / (double)nTime);
            }
        }

        private void CreateLabelGrid(Point size)
        {
            int stepX = pictureBox1.Width / size.X;
            int stepY = pictureBox1.Height / size.Y;

            int pboxX = stepX / 2;
            int pboxY = stepY / 2;

            for (int i = 0; i < size.Y; i++)
            {
                for (int j = 0; j < size.X; j++)
                {
                    Label l = new Label();
                    l.AutoSize = true;
                    Point lPos = new Point(pboxX + j * (stepX - l.Size.Width / 5), pboxY + i * stepY);
                    l.Location = lPos;
                    l.Text = string.Format("{0} - {1}", l.Location.X, l.Location.Y);
                    pictureBox1.Controls.Add(l);
                    GridLabels.Add(new labelWithRect(
                        l, 
                        new Point(lPos.X - pboxX, lPos.Y - pboxY), 
                        new Point(lPos.X + pboxX, lPos.Y + pboxY)));
                }
            }
        }

        private void ArrangeLabels(Point size)
        {
            int stepX = pictureBox1.Width / size.X;
            int stepY = pictureBox1.Height / size.Y;

            int pboxX = stepX / 2;
            int pboxY = stepY / 2;

            int labelCounter = 0;
            for (int i = 0; i < size.Y; i++)
            {
                for (int j = 0; j < size.X; j++)
                {
                    Label l = GridLabels[labelCounter].label;
                    l.Location = new Point(pboxX + j * (stepX - l.Size.Width / 5), pboxY + i * stepY);
                    l.Text = string.Format("{0} - {1}", l.Location.X, l.Location.Y);
                    labelCounter++;
                }
            }
        }

        private async void EditLiveBmp()
        {
            //bmp_.RotateFlip(RotateFlipType.RotateNoneFlipX);
            //CalculateDeltaL(GridLabels[0]);
            int dLavg_min = 100, dLavg_max = 0, 
                dLmin = 100, dLmax = 0,
                dLrms_min = 100, dLrms_max = 0;

            if (!GridLabels[0].label.Visible) return;

            foreach (labelWithRect lwR in GridLabels)
            {
                CalculateDeltaL(lwR);
                dLavg_min = Math.Min(lwR.Vavg, dLavg_min);
                dLavg_max = Math.Max(lwR.Vavg, dLavg_max);

                dLmin = Math.Min(lwR.Vmin, dLmin);
                dLmax = Math.Max(lwR.Vmax, dLmax);

                dLrms_min = Math.Min(lwR.Vrms, dLrms_min);
                dLrms_max = Math.Max(lwR.Vrms, dLrms_max);
            }
            label14.Text = String.Format("ΔL:    {0}\nΔLavg: {1}\nΔLrms: {2}", dLmax - dLmin, dLavg_max - dLavg_min, dLrms_max - dLrms_min);

            /*if (cloneRequested)
            {
                try
                {
                    clone_ = (Bitmap)bmp_.Clone();
                }
                catch
                {
                    clone_ = null;
                }
                await Task.Run(() => parseClone(0, 0));
            }*/

            Graphics g = Graphics.FromImage(bmp_);
            Pen pen = new Pen(SystemColors.Control, 10);

            //Horizontal
            int gHeight = bmp_.Height / 3;
            g.DrawLine(pen, 0, gHeight, bmp_.Width, gHeight);
            g.DrawLine(pen, 0, gHeight * 2, bmp_.Width, gHeight * 2);

            //Vertical
            int gWidth = bmp_.Width / 3;
            g.DrawLine(pen, gWidth, 0, gWidth, bmp_.Height);
            g.DrawLine(pen, gWidth * 2, 0, gWidth * 2, bmp_.Height);
            
        }

        private void CalculateDeltaL(labelWithRect lwR, int resX = 5, int resY = 5)
        {
            int pWidth = pictureBox1.Width;

            if (pWidth == 0) return;
            float scale = pictureBox1.Image.Width / pWidth;
            int stepX = (lwR.rightLower.X - lwR.leftUpper.X) / resX;
            int stepY = (lwR.rightLower.Y - lwR.leftUpper.Y) / resY;

            int Vmin = 100;
            int Vmax = 0;

            int sum = 0;
            long sqr_sum = 0;
            int count = 0;

            for (int i = lwR.leftUpper.Y + stepY; i < lwR.rightLower.Y; i += stepY)
            {
                for (int j = lwR.leftUpper.X + stepX; j < lwR.rightLower.X; j += stepX)
                {
                    Color c = bmp_.GetPixel((int)(j * scale), (int)(i * scale));
                    int pixelVal = getPixelValue(c);

                    if (pixelVal > Vmax) Vmax = pixelVal;
                    if (pixelVal < Vmin) Vmin = pixelVal;

                    sum += pixelVal;
                    sqr_sum += pixelVal * pixelVal;
                    count++;
                }
            }
            int Vavg = sum / count;
            int Vrms = (int)(Math.Sqrt(sqr_sum) / resX);
            lwR.label.Text = String.Format("MAX {0}\nMIN {1}\nAVG {2}\nRMS {3}", Vmax, Vmin, Vavg, Vrms);
            lwR.Vavg = Vavg;
            lwR.Vmax = Vmax;
            lwR.Vmin = Vmin;
            lwR.Vrms = Vrms;            
        }

        void PushSettings()
        {
            cam_.put_Option(Toupcam.eOPTION.OPTION_BITDEPTH, 0);
            cam_.put_Option(Toupcam.eOPTION.OPTION_FRAMERATE, 8);
            //cam_.put_Option(Toupcam.eOPTION.OPTION_UPSIDE_DOWN, 1);
            //cam_.put_Option(Toupcam.eOPTION.OPTION_ROTATE, 2);
            cam_.put_Option(Toupcam.eOPTION.OPTION_BINNING, 2);
        }

        private int getPixelValue(Color c)
        {
            int v = Math.Max(c.R, Math.Max(c.G, c.B));
            return (int)(v / 2.55f);
        }

        private void ToggleDeltaMeasure(object sender, EventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            foreach (labelWithRect lwR in GridLabels)
                lwR.label.Visible = checkBox.Checked;
            Refresh();
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            int n = (int)((NumericUpDown)sender).Value;
            trackBar1.Value = n;
        }

        private void AutoReconnect(object sender, EventArgs e)
        {
            if (cam_ != null)
                return;

            Toupcam.DeviceV2[] arr = Toupcam.EnumV2();
            if (arr.Length <= 0) 
                return;
            uint lastExpo = (uint)trackBar1.Value;
            if (1 == arr.Length)
                startDevice(arr[0].id);

            trackBar1.Value = (int)lastExpo;
            button1.BackColor = Color.Green;
            button1.Text = "Connected";
            timer2.Stop();
        }
        /*
        private void parseClone(int f, int l)
        {
            if (clone_ == null) return;

            int imgCenterLine = clone_.Height / 2;

            int firstBlackPixel = 0;
            int lastBlackPixel = 0;
            int countedLength = 0;
            for (int i = 0; i < clone_.Width; i++)
            {
                Color c = clone_.GetPixel(i, imgCenterLine);
                int val = getPixelValue(c);

                if (val > 15)
                {
                    if (firstBlackPixel > 0) break;
                    else continue;
                }

                if (firstBlackPixel == 0) firstBlackPixel = i;
                countedLength++;
                lastBlackPixel = i;
            }
            if (countedLength > 0)
            {
                debugBox.Invoke((MethodInvoker)delegate
                {
                    float pixsize = 2.32f;
                    float h = (f * l * pixsize / countedLength) * 100;
                    string s = String.Format("f{0} w{1} h{2}", firstBlackPixel, countedLength, (int)h);
                    debugBox.Items.Insert(0, s);
                });
            }
            cloneRequested = false;
        }*/

        private void PutLightTrackbarsToList()
        {
            foreach (TrackBar trackBar in tabControl1.TabPages[1].Controls.OfType<TrackBar>().ToList())
            {
                if (trackBar.Tag != null)
                    TrackBars.Add(trackBar, trackBar.Value);
            }
        }

        private void setGlobalLightValue(object sender, EventArgs e)
        {
            TrackBar trackBar = (TrackBar)sender;
            button5.Text = trackBar.Value.ToString();
            foreach (var tbar in TrackBars.Keys)
            {
                tbar.Value = trackBar.Value;
            }
        }

        private void saveLoadPreset(object sender, EventArgs e)
        {
            Button button = (Button)sender;
            ContextMenuStrip ctxmenu = new ContextMenuStrip();
            ctxmenu.ItemClicked += (nsender, ne) =>
            {
                string tag = (ne.ClickedItem.Tag.ToString());
                List<setting> settings = new List<setting>();

                if (tag == "3")
                {
                    foreach (var tbar in TrackBars.Keys)
                    {
                        tbar.Value = 50;
                    }
                    return;
                }

                if (tag == "1")
                {
                    foreach (var tbar in TrackBars.Keys)
                    {
                        settings.Add(new setting(tbar.Name, tbar.Value));
                    }
                    using (MemoryStream ms = new MemoryStream())
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        bf.Serialize(ms, settings);
                        ms.Position = 0;
                        byte[] buffer = new byte[(int)ms.Length];
                        ms.Read(buffer, 0, buffer.Length);
                        Properties.Settings.Default.lightPreset = Convert.ToBase64String(buffer);
                        Properties.Settings.Default.Save();
                    }
                }
                else
                {
                    using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(Properties.Settings.Default.lightPreset)))
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        settings = (List<setting>)bf.Deserialize(ms);
                    }
                    if (settings.Count == 0) return;
                    foreach (var tbar in TrackBars.Keys)
                    {
                        foreach (setting setting in settings)
                            if (tbar.Name == setting.Name)
                                tbar.Value = setting.Value;
                    }
                }
            };
            ctxmenu.Items.Add("Save Preset").Tag = 1;
            ctxmenu.Items.Add("Load Preset").Tag = 2;
            ctxmenu.Items.Add("Defaults").Tag = 3;
            ctxmenu.Show(button, 0, 0);
        }

        private void onRaw(object sender, EventArgs e)
        {
            //Placeholder
        }

        private void showCompactForm(object sender, EventArgs e)
        {
            Form2 compact_form = new Form2(this);
            compact_form.Show();
            Hide();
        }

        private void RefreshComs(object sender, EventArgs e)
        {
            ComboBox cb = (ComboBox)sender;
            cb.Items.Clear();

            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
                cb.Items.Add(port);

            Log("-- COM Ports Refreshed --");
        }

        private void connectToAstroMech(object sender, EventArgs e)
        {
            ComboBox comboBox = (ComboBox)sender;
            AstroPort_ = Serial.Open(comboBox.SelectedItem.ToString());
            //AstroPort_.Response += new PortResponseEventHandler(port_dataRecieved);

            if (AstroPort_ == null) return;

            button6.Enabled = true;
            button7.Enabled = true;
            button10.Enabled = true;
            button11.Enabled = true;
            comboBox3.Enabled = true;
            numericUpDown2.Enabled = true;
        }

        private void connectToLights(object sender, EventArgs e)
        {
            ComboBox comboBox = (ComboBox)sender;
            LightPort_ = Serial.Open(comboBox.SelectedItem.ToString());

            //LightPort_.Response += new SerialDataReceivedEventHandler(port_dataRecieved);

            if (AstroPort_ == null) return;

            if (comboBox5.SelectedIndex == -1)
                comboBox5.SelectedIndex = 0;

            button8.Enabled = true;
            comboBox5.Enabled = true;
            numericUpDown3.Enabled = true;
            trackBar16.Enabled = true;

            foreach (var tbar in TrackBars.Keys)
            {
                tbar.Enabled = true;
            }
        }

        private void GetAstroMechPos(object sender, EventArgs e)
        {
            if (AstroPort_ == null)
                return;

            AstroPort_.TryWrite("P#");
        }

        private void SetAstroMechPos(object sender, EventArgs e)
        {
            if (AstroPort_ == null)
                return;

            int focusVal = (int)numericUpDown2.Value;

            if (focusVal == lastFocusVal) return;
            if ((uint)focusVal > 12000) return;

            string command = String.Format("M{0}#", focusVal);
            Log(">> " + command);

            AstroPort_.TryWrite(command);

            lastFocusVal = focusVal;
        }

        private void SwitchAstroAperture(object sender, EventArgs e)
        {
            if (AstroPort_ == null)
                return;

            string command = String.Format("A{0}#", comboBox3.SelectedIndex);

            AstroPort_.TryWrite(command);
            Log(">> " + command);
        }

        private void Log(string str)
        {
            debugBox.Items.Insert(0, str);
        }
    }
}