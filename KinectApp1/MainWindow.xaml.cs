using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.ComponentModel;

using Microsoft.Kinect;

namespace KinectApp1
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        #region field
        private KinectSensor kinectDevice;

        private WriteableBitmap _human_image2_bitmap;
        private WriteableBitmap _room_bitmap;
        private WriteableBitmap _human_image1_bitmap;

        
        private Int32Rect _screenImageRect;
        private short[] _depthPixelData;
        private byte[] _colorPixelData;

        #endregion


        #region プロパティ

        private double _target_depth;
        public double Target_Depth
        {
            get { return _target_depth; }
            set
            {
                _target_depth = value;
                OnPropertyChanged("Target_Depth");
            }
        }

        private string _depthMessage;
        public string DepthMessage
        {
            get { return _depthMessage; }
            set
            {
                _depthMessage = value;
                OnPropertyChanged("DepthMessage");
            }
        }

        
        public WriteableBitmap Human_image1_bitmap { get { return _human_image1_bitmap; } }
        public WriteableBitmap Human_image2_bitmap { get { return _human_image2_bitmap; } }
        public WriteableBitmap Room_Bitmap { get { return _room_bitmap; } }


        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }


        #endregion


        public MainWindow()
        {
            InitializeComponent();

            Target_Depth = 2400;

            //kinect初期化
            if (init_kinect()==false) Close();

            //レンダリング時にKinectデータを取得し描画
            CompositionTarget.Rendering += compositionTarget_rendering;

            //Bind用
            this.DataContext = this;
        }


        /// <summary>
        /// レンダリング時にKinectデータを取得し描画
        /// </summary>
        private void compositionTarget_rendering(object sender, EventArgs e)
        {
            using (ColorImageFrame colorFrame = this.kinectDevice.ColorStream.OpenNextFrame(100))//100ミリ秒
            {
                using (DepthImageFrame depthFrame = this.kinectDevice.DepthStream.OpenNextFrame(100))
                {
                    RenderScreen(colorFrame, depthFrame);
                }
            }
        }

        /// <summary>
        /// kinect初期化
        /// </summary>
        private bool init_kinect()
        {
            try
            {
                // Kinectデバイスを取得する
                kinectDevice = KinectSensor.KinectSensors.FirstOrDefault(x => x.Status == KinectStatus.Connected);
                if (kinectDevice == null) return false;
                kinectDevice.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                kinectDevice.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                kinectDevice.SkeletonStream.Enable();

                //ビットマップデータ初期化
                var depthStream = kinectDevice.DepthStream;
                _depthPixelData = new short[kinectDevice.DepthStream.FramePixelDataLength];
                _colorPixelData = new byte[kinectDevice.ColorStream.FramePixelDataLength];


                //部屋の画層初期化
                _room_bitmap = new WriteableBitmap(depthStream.FrameWidth, depthStream.FrameHeight, 96, 96, PixelFormats.Bgra32, null);

                //人の画層初期化
                _human_image1_bitmap = new WriteableBitmap(depthStream.FrameWidth, depthStream.FrameHeight, 96, 96, PixelFormats.Bgra32, null);
                _human_image2_bitmap = new WriteableBitmap(depthStream.FrameWidth, depthStream.FrameHeight, 96, 96, PixelFormats.Bgra32, null);

                _screenImageRect = new Int32Rect(0, 0, (int)Math.Ceiling(_human_image2_bitmap.Width), (int)Math.Ceiling(_human_image2_bitmap.Height));
                kinectDevice.Start();

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// キネクトの画像をビットマップデータに書き出す
        /// </summary>
        /// <param name="kinectDevice"></param>
        /// <param name="colorFrame"></param>
        /// <param name="depthFrame"></param>
        private void RenderScreen(ColorImageFrame colorFrame, DepthImageFrame depthFrame)
        {
            if (kinectDevice == null || depthFrame == null || colorFrame == null) return;

            int depth = 0;//深度計算用
            int depthPixelIndex; //深度ピクセル情報を得るためのインデックス
            int playerIndex;//人ID
            int colorPixelIndex;//色ピクセル情報を得るためのインデックス
            ColorImagePoint colorPoint;
            int colorStride = colorFrame.BytesPerPixel * colorFrame.Width; //4×画像幅
            int screenImageStride = kinectDevice.DepthStream.FrameWidth * colorFrame.BytesPerPixel;

            int ImageIndex = 0;
            byte[] bytePlayer2 = new byte[depthFrame.Height * screenImageStride];
            byte[] bytePlayer1 = new byte[depthFrame.Height * screenImageStride];
            byte[] byteRoom = new byte[depthFrame.Height * screenImageStride];
           
            depthFrame.CopyPixelDataTo(_depthPixelData);
            colorFrame.CopyPixelDataTo(_colorPixelData);

            for (int depthY = 0; depthY < depthFrame.Height; depthY++)
            {
                for (int depthX = 0; depthX < depthFrame.Width; depthX++, ImageIndex += colorFrame.BytesPerPixel)
                {
                    depthPixelIndex = depthX + (depthY * depthFrame.Width);
                    playerIndex = _depthPixelData[depthPixelIndex] & DepthImageFrame.PlayerIndexBitmask; //人のID取得
                    colorPoint = kinectDevice.MapDepthToColorImagePoint(depthFrame.Format, depthX, depthY, _depthPixelData[depthPixelIndex], colorFrame.Format);
                    colorPixelIndex = (colorPoint.X * colorFrame.BytesPerPixel) + (colorPoint.Y * colorStride); 

                    if (playerIndex != 0)
                    {
                        //ピクセル深度を取得
                        depth = _depthPixelData[depthPixelIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth;

                        //ターゲット深度に応じて書き込み先を変更
                        if (depth > _target_depth)
                        {
                            //奥側へ描画
                            bytePlayer1[ImageIndex] = _colorPixelData[colorPixelIndex];           //Blue    
                            bytePlayer1[ImageIndex + 1] = _colorPixelData[colorPixelIndex + 1];   //Green
                            bytePlayer1[ImageIndex + 2] = _colorPixelData[colorPixelIndex + 2];   //Red
                            bytePlayer1[ImageIndex + 3] = 0xFF;                             //Alpha
                        }
                        else
                        {
                            //手前へ描画
                            bytePlayer2[ImageIndex] = _colorPixelData[colorPixelIndex];           //Blue    
                            bytePlayer2[ImageIndex + 1] = _colorPixelData[colorPixelIndex + 1];   //Green
                            bytePlayer2[ImageIndex + 2] = _colorPixelData[colorPixelIndex + 2];   //Red
                            bytePlayer2[ImageIndex + 3] = 0xFF;                             //Alpha
                        }
                    }
                    else
                    {
                        //人以外は背景イメージへ描画
                        byteRoom[ImageIndex] = _colorPixelData[colorPixelIndex];           //Blue    
                        byteRoom[ImageIndex + 1] = _colorPixelData[colorPixelIndex + 1];   //Green
                        byteRoom[ImageIndex + 2] = _colorPixelData[colorPixelIndex + 2];   //Red
                        byteRoom[ImageIndex + 3] = 0xFF;                             //Alpha
                    }
                }
            }

            //byteからビットマップへ書出し
            _human_image1_bitmap.WritePixels(_screenImageRect, bytePlayer1, screenImageStride, 0);
            _human_image2_bitmap.WritePixels(_screenImageRect, bytePlayer2, screenImageStride, 0);
            _room_bitmap.WritePixels(_screenImageRect, byteRoom, screenImageStride, 0);

            //変更通知
            OnPropertyChanged("Human_image1_bitmap");
            OnPropertyChanged("Human_image2_bitmap");
            OnPropertyChanged("Room_bitmap");

            DepthMessage = string.Format("深さ＝{0}", depth);

        }


    }
}
