

*** using static AlpImport;
*** 폴더에 alp4395.dll 이 있어야 함
*** x64로 해야 함.
 

public class ALP_43
{
    public ALP_43()
    {
        m_DmdWidth = m_DmdHeight = 0;
        m_DevId = UInt32.MaxValue;


        // Sequence IDs
        for (int i = 0; i < _SequenceID.Length; i++)
        {
            _SequenceID[i] = (uint)i;
        }

        bworker_StartProjection.DoWork += Bworker_StartProjection_DoWork;
    }



    #region ALP Control Methods

    // Allocate Device
    public bool Init()
    {
        return Allocate_Device();
    }

    // Deallocate Device
    public void DeInit()
    {
        Deallocate_Device();
    }

    // Start Projection Once
    public void Start_Projection(uint SequenceID)
    {
        if (!bworker_StartProjection.IsBusy)
        {
            // Set variables
            _bworker_start_seqID = SequenceID;

            bworker_StartProjection.RunWorkerAsync();
        }
    }


    // Start Projection Continuous
    public void Start_Projection_Cont(uint SequenceID)
    {
        // Start display continuous
        AlpImport.ProjStartCont(m_DevId, _SequenceID[SequenceID]);
    }


    // ALP in an idle wait state
    public string Halt()
    {
        AlpImport.Result result = AlpImport.ProjHalt(m_DevId);
        return AlpErrorString(result);
    }


    public void Clear_Memory(uint SequenceID)
    {
        // Sequence Free
        AlpImport.Result result = AlpImport.SeqFree(m_DevId, SequenceID);
    }

    // Set Sequence Image Data
    public void Set_SequenceData(SequenceImageData Sequence_Data)
    {
        DLP_SetImageData(Sequence_Data._SequenceID, Sequence_Data._Byte_Images, Sequence_Data._Timing_us);
    }

    #endregion



    #region Save & Load Sequence Image Data as File

    [Serializable]
    public class SequenceDataSerialized
    {

        public List<SequenceImageData> _SeqData = new List<SequenceImageData>();


        public void Add_Data(List<SequenceImageData> SequenceDataList)
        {
            _SeqData.Clear();

            // Copy Data
            foreach (SequenceImageData tempData in SequenceDataList)
            {
                _SeqData.Add(tempData);
            }
        }
    }


    public bool Save_SequenceDataList(string FileName, List<SequenceImageData> SequenceDataList)
    {

        try
        {
            SequenceDataSerialized _SeqDataListSerial = new SequenceDataSerialized();

            // Add Image Data
            _SeqDataListSerial.Add_Data(SequenceDataList);


            // Class Serialization
            var formatter = new BinaryFormatter();

            // Save as file
            Stream streamFileWrite = new FileStream(FileName, FileMode.Create, FileAccess.Write);
            formatter.Serialize(streamFileWrite, _SeqDataListSerial);
            streamFileWrite.Close();

            return true;
        }
        catch
        {
            return false;
        }
    }


    public bool Load_SequenceDataList(string FileName)
    {
        try
        {
            string tempFilePath = FileName;

            SequenceDataSerialized _SeqDataListSerial = new SequenceDataSerialized();

            // Class Serialization
            var formatter = new BinaryFormatter();

            // Load from File
            Stream streamFileRead = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read);
            _SeqDataListSerial = (SequenceDataSerialized)formatter.Deserialize(streamFileRead);
            streamFileRead.Close();

            // Set Image Data to DLP
            foreach (SequenceImageData tempData in _SeqDataListSerial._SeqData)
            {
                Set_SequenceData(tempData);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }


    #endregion




    #region Sequence Image Data

    [Serializable]
    public class SequenceImageData
    {
        // From Byte Array
        public SequenceImageData(uint SequenceID, List<byte[]> Byte_Images, int Timing_us)
        {
            _SequenceID = SequenceID;
            _Timing_us = Timing_us;

            foreach (byte[] Data in Byte_Images)
            {
                _Byte_Images.Add(Data);
            }
        }

        // From Bitmap
        public SequenceImageData(uint SequenceID, List<System.Drawing.Bitmap> Bitmap_Images, int Timing_us)
        {
            _SequenceID = SequenceID;
            _Timing_us = Timing_us;


            foreach (System.Drawing.Bitmap Data in Bitmap_Images)
            {
                // Bitmap to Byte Array
                byte[] tempBytes = Bitmap_to_ByteArray(Data);

                _Byte_Images.Add(tempBytes);
            }
        }

        // From File Path
        public SequenceImageData(uint SequenceID, string[] Image_FileNames, int Timing_us)
        {
            _SequenceID = SequenceID;
            _Timing_us = Timing_us;


            foreach (string FilePath in Image_FileNames)
            {
                // File path to bitmap
                Bitmap tempBitmap = new Bitmap(FilePath);

                // Bitmap to Byte Array
                byte[] tempBytes = Bitmap_to_ByteArray(tempBitmap);

                _Byte_Images.Add(tempBytes);
            }
        }


        // Internal Data Varialbes
        public uint _SequenceID = 0;
        public List<byte[]> _Byte_Images = new List<byte[]>();
        public int _Timing_us = 0;



        // Bitmap to Byte Array
        private byte[] Bitmap_to_ByteArray(System.Drawing.Bitmap bitmap)
        {
            byte[] tempByte = new byte[bitmap.Width * bitmap.Height];
            int byteIndex = 0;
            uint tempColor;
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    tempColor = (uint)bitmap.GetPixel(x, y).ToArgb();

                    if (tempColor == 0xFFFFFFFF)
                    {
                        // White
                        tempByte[byteIndex++] = 255;
                    }
                    else
                    {
                        // Black
                        tempByte[byteIndex++] = 0;
                    }

                }
            }

            return tempByte;
        }

    }




    #endregion






    #region Events

    public delegate void setImageDone(uint SequenceID);
    public event setImageDone SetImageDone;

    public delegate void projectionDone(uint SequenceID);
    public event projectionDone ProjectionDone;

    #endregion



    #region Start Projection BackgroundWorker
    uint _bworker_start_seqID = 0;
    private void Bworker_StartProjection_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
    {
        // Start display
        AlpImport.ProjStart(m_DevId, _SequenceID[_bworker_start_seqID]);


        // Wait for Projection Done
        ProjWait();


        // Projection Done Event
        try
        {
            ProjectionDone(_bworker_start_seqID);
        }
        catch { }
    }

    #endregion



    #region Private Variables

    private UInt32 m_DevId;
    private UInt32[] _SequenceID = new UInt32[51];
    private Int32 m_DmdWidth;
    private Int32 m_DmdHeight;


    System.ComponentModel.BackgroundWorker bworker_StartProjection = new System.ComponentModel.BackgroundWorker();

    #endregion



    #region Private Methods



    // 1. DevAlloc ( Allocate Device. Init System)
    // 2. SeqAlloc (Allocation Sequence. Set Bitplanes, Picture number, Seq ID
    // 3. SeqTiming (Set Timing)


    // Allocate Device
    private bool Allocate_Device()
    {
        string tx_result = "";

        AlpImport.Result result;

        // allocate one ALP device
        result = AlpImport.DevAlloc(0, 0, ref m_DevId);
        tx_result = "DevAlloc " + AlpErrorString(result);
        if (AlpImport.Result.ALP_OK != result) return false;  // error -> exit

        // determine image data size by DMD type
        Int32 DmdType = Int32.MaxValue;
        m_DmdWidth = 0; m_DmdHeight = 0;
        AlpImport.DevInquire(m_DevId, AlpImport.DevTypes.ALP_DEV_DMDTYPE, ref DmdType);
        AlpImport.DevInquire(m_DevId, AlpImport.DevTypes.ALP_DEV_DISPLAY_WIDTH, ref m_DmdWidth);
        AlpImport.DevInquire(m_DevId, AlpImport.DevTypes.ALP_DEV_DISPLAY_HEIGHT, ref m_DmdHeight);
        switch ((AlpImport.DmdTypes)DmdType)
        {
            case AlpImport.DmdTypes.ALP_DMDTYPE_XGA:
            case AlpImport.DmdTypes.ALP_DMDTYPE_XGA_055A:
            case AlpImport.DmdTypes.ALP_DMDTYPE_XGA_055X:
            case AlpImport.DmdTypes.ALP_DMDTYPE_XGA_07A:
                tx_result = String.Format("XGA DMD {0}", DmdType);
                m_DmdWidth = 1024;  // fall-back: old API versions did not support ALP_DEV_DISPLAY_WIDTH and _HEIGHT
                m_DmdHeight = 768;
                break;
            case AlpImport.DmdTypes.ALP_DMDTYPE_1080P_095A:
            case AlpImport.DmdTypes.ALP_DMDTYPE_DISCONNECT:
                tx_result = String.Format("1080p DMD {0}", DmdType);
                break;
            case AlpImport.DmdTypes.ALP_DMDTYPE_WUXGA_096A:
                tx_result = String.Format("WUXGA DMD {0}", DmdType);
                break;
            case AlpImport.DmdTypes.ALP_DMDTYPE_SXGA_PLUS:
                tx_result = String.Format("SXGA+ DMD {0}", DmdType);
                m_DmdWidth = 1400;
                m_DmdHeight = 1050;
                break;
            default:
                tx_result = String.Format("Unknown DMD Type {0}: {1}x{2}", DmdType, m_DmdWidth, m_DmdHeight);
                if (m_DmdHeight == 0 || m_DmdWidth == 0)
                    // Clean up... AlpImport.DevHalt(m_DevId); m_DevId = UInt32.MaxValue;
                    return false;
                else
                    // Continue, because at least the API DLL knows this DMD type :-)
                    break;
        }

        return true;
    }


    // Set Device Free when system closing
    private void Deallocate_Device()
    {
        AlpImport.ProjHalt(m_DevId);
        AlpImport.DevHalt(m_DevId);
        AlpImport.DevFree(m_DevId);
    }


    private bool DLP_SetImageData(uint SequenceID, List<byte[]> ImageList, int Timing_us)
    {
        uint tempSequenceID = SequenceID;


        // Allocate sequence of images. 1 bit plane
        if (!IS_ALP_OK(AlpImport.SeqAlloc(m_DevId, 1, ImageList.Count, ref tempSequenceID)))
        {
            return false;
        }

        // Reset Sequence ID cuz SeqAlloc increases SequenceID
        tempSequenceID = SequenceID;


        // Sequence Control for Binary Uninterrupted Mode
        if (!IS_ALP_OK(AlpImport.SeqControl(m_DevId, tempSequenceID, SeqTypes.ALP_BIN_MODE, (int)SeqTypes.ALP_BIN_UNINTERRUPTED)))
        {
            return false;
        }

        // Set Trigger Edge to Rising Edge
        if (!IS_ALP_OK(AlpImport.DevControl(m_DevId, DevTypes.ALP_TRIGGER_EDGE, (int)DevTypes.ALP_EDGE_FALLING)))
        {
            return false;
        }

        // Byte Buffer
        byte[] tempByte = new byte[1920 * 1080 * ImageList.Count];


        // Byte List to Byte Array
        for (int i = 0; i < ImageList.Count; i++)
        {
            Array.Copy(ImageList[i], 0, tempByte, 1920 * 1080 * i, 1920 * 1080);
        }

        // Put Sequence
        if (!IS_ALP_OK(AlpImport.SeqPut(m_DevId, tempSequenceID, 0, ImageList.Count, ref tempByte)))
        {
            return false;
        }


        // Timing
        if (!IS_ALP_OK(AlpImport.SeqTiming(m_DevId, tempSequenceID, AlpImport.ALP_DEFAULT, Timing_us, AlpImport.ALP_DEFAULT, AlpImport.ALP_DEFAULT, AlpImport.ALP_DEFAULT)))
        {
            return false;
        }


        // Set Image Done Event
        try
        {
            SetImageDone(tempSequenceID);
        }
        catch { }

        return true;
    }





    // Convert error string
    private string AlpErrorString(AlpImport.Result result)
    {
        return String.Format("{0}", result);
    }


    // Convert error string
    private bool IS_ALP_OK(AlpImport.Result result)
    {
        string tempResult = String.Format("{0}", result);

        if (tempResult == "ALP_OK")
        {
            return true;
        }
        else
        {
            return false;
        }
    }


    private string ProjWait()
    {
        AlpImport.Result result = AlpImport.ProjWait(m_DevId);
        return AlpErrorString(result);
    }



    #endregion

}
