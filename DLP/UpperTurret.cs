using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DLP_Ctrl_v2
{
    public class FilterTurret_Upper
    {
        public FilterTurret_Upper(string PortName) 
        {
            _serialPort.BaudRate = 115200;
            _serialPort.PortName = PortName;

            _serialPort.DataReceived += _serialPort_DataReceived;
        }



        SerialPort _serialPort = new SerialPort();
        private StringBuilder _rxBuffer = new StringBuilder();

        public event Action HomingDoneReceived;
        public event Action MovingDoneReceived;
        public event Action<int> StateReceived;



        private string Convert_Packet(string Packet)
        {
            byte checksum = 0;

            foreach (char c in Packet)
                checksum ^= (byte)c;

            // MCU 방식: nibble + 0x30 (문자 '0'부터 시작, A~F 변환 없음)
            char high = (char)((checksum >> 4) + 0x30);
            char low = (char)((checksum & 0x0F) + 0x30);

            return $"${Packet}{high}{low}*";
        }

        public void Open()
        {
            _serialPort.Open();
        }


        public void Set_Filter(int Number)
        {
            _serialPort.Write(Convert_Packet($"Move,{Number}"));
        }

        public void Hominig()
        {
            _serialPort.Write(Convert_Packet("Homing"));
        }

        public void Get_State()
        {
            _serialPort.Write(Convert_Packet("State"));
        }


        private void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string incoming = _serialPort.ReadExisting();
            _rxBuffer.Append(incoming);

            while (true)
            {
                string buffer = _rxBuffer.ToString();
                int start = buffer.IndexOf('$');
                int end = buffer.IndexOf('*', start + 1);

                if (start >= 0 && end > start)
                {
                    string frame = buffer.Substring(start + 1, end - start - 1);  // 예: HomingDone0:
                    _rxBuffer.Remove(0, end + 1);  // 파싱한 부분 제거

                    ParseFrame(frame);
                }
                else
                {
                    break; // 전체 패킷 안 들어왔으면 나가기
                }
            }
        }

        private void ParseFrame(string frame)
        {
            if (frame.StartsWith("HomingDone"))
            {
                HomingDoneReceived?.Invoke();
            }
            else if (frame.StartsWith("MovingDone"))
            {
                MovingDoneReceived?.Invoke();
            }
            else if (frame.StartsWith("State"))
            {
                // 예: State,1,1,04;
                try
                {
                    string[] parts = frame.Split(',');
                    StateReceived?.Invoke(int.Parse(parts[1]));

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"State parse error: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Unknown frame: {frame}");
            }
        }
    }
}
