using AForge.Video;
using AForge.Video.DirectShow;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Tesseract;
using IronOcr;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MRZ
{
    public partial class Form1 : Form
    {
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        Bitmap image;
        Rectangle cropRect;
        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            //KAMERA LİSTESİ ALMA
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo filterInfo in videoDevices)
            {
                comboBox1.Items.Add(filterInfo.Name);
            }
            comboBox1.SelectedIndex = 0;

            try
            {
                if (videoSource != null)
                    videoSource.Stop();
            }
            catch
            {
                //işlendi işlenemedi
            }


            //KAMERA BAŞLATMA
            videoSource = new VideoCaptureDevice(videoDevices[2].MonikerString);
            videoSource.NewFrame += VideoSource_NewFrame;
            videoSource.Start();

            timer1.Enabled = true;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                if (videoSource != null)
                    videoSource.Stop();
            }
            catch 
            {
                //işlendi işlenemedi
            }


            //KAMERA BAŞLATMA
            videoSource = new VideoCaptureDevice(videoDevices[comboBox1.SelectedIndex].MonikerString);
            videoSource.NewFrame += VideoSource_NewFrame;
            videoSource.Start();
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            //HER FRAME'İ PİCTUREBOX'A YANSITMA
            image = (Bitmap)eventArgs.Frame.Clone();
            image.RotateFlip(RotateFlipType.RotateNoneFlipX); //HARFLERDE DÖNÜYOR
            pictureBox2.Image = image;

            //kırpılcak alanı belirleme ve yansıtma
            int startX = 50;
            int startY = 50;
            int width = 500;
            int height = 150;
            cropRect = new Rectangle(startX, startY, width, height);
            pictureBox2.Invalidate();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            ocr();
        }
        
        public void ocr()
        {
            #region resim işleme
            Bitmap croppedImage = CropImage(pictureBox2.Image as Bitmap, cropRect);
            croppedImage.RotateFlip(RotateFlipType.RotateNoneFlipX);
            Bitmap bnw = GrayScaleFilter(croppedImage);
            pictureBox3.Image = bnw;
            #endregion

            #region tesseract
            //METNİ İŞLEME
            if (image != null)
            {
                var ms = new MemoryStream();
                bnw.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                byte[] fileBytes = ms.ToArray();
                ms.Close();

                using (var engine = new TesseractEngine(@"./tessdata", "kimlik", EngineMode.Default))
                {
                    using (var img = Pix.LoadFromMemory(fileBytes))
                    {
                        using (var page = engine.Process(img))
                        {
                            richTextBox2.Text = page.GetText();
                            extractInformations(page.GetText());
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Önce bir görüntü yakalayın.");
            }
            #endregion
            #region ironocr
            //string filePath = "./image.png";
            //bnw.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
            //var ocrInput = new OcrInput();
            //ocrInput.LoadImage("./image.png");

            //string imageText = new IronTesseract().Read(ocrInput).Text;
            //richTextBox2.Text = imageText;
            #endregion
        }

        void ocrFromFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            string testImagePath = "";

            openFileDialog.Filter = "Image Files (*.jpg; *.jpeg; *.png; *.gif; *.bmp)|*.jpg; *.jpeg; *.png; *.gif; *.bmp";
            openFileDialog.Title = "Select an image file";

            DialogResult result = openFileDialog.ShowDialog();

            if (result == DialogResult.OK)
            {
                testImagePath = openFileDialog.FileName;
            }


            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.TesseractAndLstm))
            {
                using (var img = Pix.LoadFromFile(testImagePath))
                {
                    using (var page = engine.Process(img))
                    {
                        var text = page.GetText();
                        richTextBox2.Text = page.GetText();
                        MessageBox.Show(page.GetText());
                    }
                }
            }
        }

        public Bitmap GrayScaleFilter(Bitmap image)
        {
            Bitmap grayScale = new Bitmap(image.Width, image.Height);

            for (Int32 y = 0; y < grayScale.Height; y++)
                for (Int32 x = 0; x < grayScale.Width; x++)
                {
                    Color c = image.GetPixel(x, y);

                    Int32 gs = (Int32)(c.R * 0.3 + c.G * 0.59 + c.B * 0.11);

                    grayScale.SetPixel(x, y, Color.FromArgb(gs, gs, gs));
                }
            return grayScale;
        }

        void extractInformations(string mrzData)
        {
            if (mrzData.Length > 85)
            {
                string readedData;
                while (mrzData.Length > 0 && mrzData[0] != 'I')
                {
                    mrzData = mrzData.Substring(1);
                }

                if (mrzData.Length >= 2 && mrzData.StartsWith("I<", StringComparison.CurrentCultureIgnoreCase))
                {
                    timer1.Enabled = false;

                    //MessageBox.Show("Baştan 2 harfi 'I<' karakterine eşittir.");
                    readedData = "Belge Türü: Kimlik";
                    if (mrzData.Substring(2, 3).Equals("TUR"))
                    {
                        readedData = readedData + "\nUyruk: Türk";
                    }
                    else
                    {
                        readedData = readedData + "\nUyruk: Okunamadı";
                    }
                    readedData = readedData + "\nSeri Numarası: " + mrzData.Substring(5, 9);
                    readedData = readedData + "\nKimlik Numarası: " + mrzData.Substring(16, 11);

                    var birth = "";
                    var birthYear = mrzData.Substring(31, 2);
                    var birthMonth = mrzData.Substring(33, 2);
                    var birthDay = mrzData.Substring(35, 2);
                    if (Convert.ToInt32(birthYear) > 24)
                    {
                        birth = birthDay + "/" + birthMonth + "/" + "19" + birthYear;
                    }
                    else
                    {
                        birth = birthDay + "/" + birthMonth + "/" + "20" + birthYear;
                    }
                    readedData = readedData + "\nDoğum tarihi: " + birth;

                    if (mrzData.Substring(38, 1).Equals("M"))
                    {
                        readedData = readedData + "\nCinsiyet: Erkek";
                    }
                    else if (mrzData.Substring(38, 1).Equals("M"))
                    {
                        readedData = readedData + "\nCinsiyet: Kadın";
                    }
                    else
                    {
                        readedData = readedData + "\nCinsiyet: Okunamadı";
                    }

                    readedData = readedData + "\nSon geçerlilik tarihi: 20" + mrzData.Substring(39, 2) + "/" + mrzData.Substring(41, 2) + "/" + mrzData.Substring(43, 2);

                    //readedData = readedData + "\n son satır: " + mrzData.Substring(62, 30);

                    for (int i = 63; i < mrzData.Length; i++)
                    {
                        if (mrzData[i] == '<')
                        {
                            mrzData = mrzData.Remove(i, 1).Insert(i, " ");
                        }
                    }

                    // Boşluklardan böler ve isim ve soyisim değişkenlerine atar
                    string[] parcalar = mrzData.Substring(62).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string soyisim = parcalar.Length > 0 ? parcalar[0] : "";
                    string isim = parcalar.Length > 1 ? parcalar[1] : "";

                    readedData += "\nİsim: " + isim;
                    readedData += "\nSoyisim: " + soyisim;

                    richTextBox3.Text = readedData;

                    MessageBox.Show("Başarılı! Tamama tıklayınca okuma tekrar başlatılcak.");
                    timer1.Enabled = true;
                }
                else
                {
                    //MessageBox.Show("Baştan 2 harfi 'I<' karakterine eşit değildir.");
                }
            }
            else
            {
                
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            ocrFromFile();
        }

        private Bitmap CropImage(Bitmap source, Rectangle cropRect)
        {
            Bitmap croppedImage = new Bitmap(cropRect.Width, cropRect.Height);

            using (Graphics g = Graphics.FromImage(croppedImage))
            {
                g.DrawImage(source, new Rectangle(0, 0, croppedImage.Width, croppedImage.Height),
                    cropRect, GraphicsUnit.Pixel);
            }

            return croppedImage;
        }

        private void pictureBox2_Paint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(Color.Red, 2))
            {
                e.Graphics.DrawRectangle(pen, cropRect);
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            ocr();
        }
    }
}
