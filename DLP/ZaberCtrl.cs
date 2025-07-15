    public class ZaberCtrl : IDisposable
    {
        private SerialPort _serialPort = new SerialPort();
        private readonly BackgroundWorker _bworker_Tx = new BackgroundWorker();
        private readonly BackgroundWorker _bworker_Rx = new BackgroundWorker();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private ConcurrentQueue<(string Message, RequestType Type)> _txQueue = new ConcurrentQueue<(string, RequestType)>();
        private ConcurrentQueue<string> _rxQueue = new ConcurrentQueue<string>();
        private RequestType _currentRequest = RequestType.Unknown;

        private System.Timers.Timer _positionTimer;

        private int _prevPosX = -1;
        private int _prevPosY = -1;
        private bool _waitingForResponse = false;  // 응답 대기 플래그 추가

        public event Action PortOpened;
        public event EventHandler<PositionEventArgs> PositionChanged;

        public bool IsOpened { get; private set; }

        public ZaberCtrl()
        {
            _bworker_Tx.DoWork += _bworker_Tx_DoWork;
            _bworker_Rx.DoWork += _bworker_Rx_DoWork;
        }

        /*
        public void Test()
        {
            Tx_Message("/1 1 get pos\n", RequestType.PositionRequest);
        }
        */

        #region Open/Dispose

        public void Open(string portName)
        {
            try
            {
                _serialPort = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One);
                _serialPort.DataReceived += _serialPort_DataReceived;
                _serialPort.Open();

                Console.WriteLine($"Port {portName} opened.");
                Tx_Message("/1 get comm.address\n", RequestType.ConnectionRequest);

                _positionTimer = new System.Timers.Timer(400);  // 0.4초 주기 요청
                _positionTimer.Elapsed += (sender, e) => Request_Pos_XY();
                _positionTimer.AutoReset = true;
                _positionTimer.Start();

                StartBackgroundWorkers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening port: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _positionTimer?.Stop();
            _cancellationTokenSource.Cancel();
            _serialPort?.Dispose();
        }

        private void StartBackgroundWorkers()
        {
            if (!_bworker_Tx.IsBusy)
                _bworker_Tx.RunWorkerAsync();

            if (!_bworker_Rx.IsBusy)
                _bworker_Rx.RunWorkerAsync();
        }

        #endregion

        #region Commands

        public void Set_Home() => Tx_Message("/1 home\n", RequestType.HomeRequest);
        public void Jog_Abs_X(double pos_um) => Tx_Message($"/1 1 move abs {Convert_um_ToNative(pos_um)}\n", RequestType.MoveRequest);
        public void Jog_Abs_Y(double pos_um) => Tx_Message($"/1 2 move abs {Convert_um_ToNative(pos_um)}\n", RequestType.MoveRequest);
        public void Jog_Rel_X(double pos_um) => Tx_Message($"/1 1 move rel {Convert_um_ToNative(pos_um)}\n", RequestType.MoveRequest);
        public void Jog_Rel_Y(double pos_um) => Tx_Message($"/1 2 move rel {Convert_um_ToNative(pos_um)}\n", RequestType.MoveRequest);
        public void Jog_Cont_X(double velocity) => Tx_Message($"/1 1 move vel {velocity}\n", RequestType.VelocityRequest);
        public void Jog_Cont_Y(double velocity) => Tx_Message($"/1 2 move vel {velocity}\n", RequestType.VelocityRequest);
        public void Jog_Stop_X() => Tx_Message("/1 1 stop\n", RequestType.StopRequest);
        public void Jog_Stop_Y() => Tx_Message("/1 2 stop\n", RequestType.StopRequest);

        bool _PosReqToggle = false;
        public void Request_Pos_XY()
        {
            if(_PosReqToggle)
            {
                _PosReqToggle = !_PosReqToggle;
                Tx_Message("/1 1 get pos\n", RequestType.PositionRequest);
            }
            else
            {
                _PosReqToggle = !_PosReqToggle;
                Tx_Message("/1 2 get pos\n", RequestType.PositionRequest);
            }
            
        }

        private void Tx_Message(string message, RequestType type)
        {
            if (_waitingForResponse)
            {
                Console.WriteLine("Waiting for previous response. Command skipped.");
                return;
            }

            _waitingForResponse = true;
            _txQueue.Enqueue((message, type));

            if (!_bworker_Tx.IsBusy)
                _bworker_Tx.RunWorkerAsync();
        }

        #endregion

        #region TxD Worker

        private void _bworker_Tx_DoWork(object sender, DoWorkEventArgs e)
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (_txQueue.TryDequeue(out var request))
                {
                    _currentRequest = request.Type;
                    _serialPort.WriteLine(request.Message);
                    Console.WriteLine($"Sent: {request.Message}");
                    Thread.Sleep(10);
                }
            }
        }

        #endregion

        #region RxD Worker

        private void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string data = _serialPort.ReadLine();
            _rxQueue.Enqueue(data);
        }

        private void _bworker_Rx_DoWork(object sender, DoWorkEventArgs e)
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (_rxQueue.TryDequeue(out string response))
                {
                    ProcessReceivedData(response);
                }
            }
        }

        private void ProcessReceivedData(string response)
        {
            try
            {
                _waitingForResponse = false;  // 응답 수신 시 대기 플래그 해제

                if (_currentRequest == RequestType.ConnectionRequest && response.StartsWith("@01 0 OK"))
                {
                    Console.WriteLine("Port Connected Successfully!");
                    IsOpened = true;
                    PortOpened?.Invoke();
                    return;
                }

                if (response.StartsWith("@01 1 OK") || response.StartsWith("@01 2 OK"))
                {
                    string[] parts = response.Split(' ');

                    if (parts.Length >= 5)
                    {
                        string state = parts[3];
                        string value = parts[4];

                        if (value == "NI")
                        {
                            Console.WriteLine("Device not initialized.");
                        }
                    }

                    // Position Received
                    if (_currentRequest == RequestType.PositionRequest)
                    {
                        // X Position
                        if (response.StartsWith("@01 1 OK"))
                        {
                           int tempUm = int.Parse(response.Split(' ').Last());

                            TriggerPositionEvent(tempUm, _prevPosY);
                            _prevPosX = tempUm;
                        }
                        // Y Position
                        else if (response.StartsWith("@01 2 OK"))
                        {
                            int tempUm = int.Parse(response.Split(' ').Last());
                            TriggerPositionEvent(_prevPosX, tempUm);
                            _prevPosY = tempUm;
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Unhandled Response: {response}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing response: {ex.Message}");
            }
            finally
            {
                _currentRequest = RequestType.Unknown;
            }
        }

        private void TriggerPositionEvent(int newX, int newY)
        {
            if (newX != _prevPosX || newY != _prevPosY)
                PositionChanged?.Invoke(this, new PositionEventArgs(newX, newY));
        }

        #endregion

        #region Unit Conversion

        public double Convert_NativeTo_um(int nativePosition) => nativePosition * 0.15625;
        public int Convert_um_ToNative(double position_um) => (int)Math.Round(position_um / 0.15625);

        #endregion

        #region Request Type

        public enum RequestType
        {
            Unknown,
            PositionRequest,
            HomeRequest,
            VelocityRequest,
            StopRequest,
            ConnectionRequest,
            MoveRequest,
            RelativeRequest
        }

        public class PositionEventArgs : EventArgs
        {
            public int X { get; }
            public int Y { get; }
            public PositionEventArgs(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        #endregion
    }
