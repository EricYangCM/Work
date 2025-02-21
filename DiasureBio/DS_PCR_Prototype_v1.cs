using DS_PCR_Prototype_Test_v1.UART_cs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace DS_PCR_Prototype_Test_v1
{
    public class DS_PCR_Prototype_v1
    {
        public DS_PCR_Prototype_v1()
        {
            _UART.ConnectionChanged += _UART_ConnectionChanged;
            _UART.PacketReceived += _UART_PacketReceived;
        }

        

        #region PCR Commands

        // 16-bit DAC 설정
        public void CMD_Set_DAC(int DAC)
        {
            if ((DAC > 0) && (DAC < 65536))
            {
                _UART.Add_to_Tx_Buffer($"DAC,{DAC}");
            }
        }

        // 타겟 온도 설정 (메뉴얼)
        public void CMD_Set_TargetTemperature_Manual(double Temperature)
        {
            _UART.Add_to_Tx_Buffer($"Temp,Set,{Math.Round(Temperature, 2)}");
        }

        // 메뉴얼 온도 제어 on,off
        public void CMD_Set_Manual_TemperatureControl(bool OnOff)
        {
            if(OnOff)
            {
                _UART.Add_to_Tx_Buffer($"Temp,Run,1");
            }
            else
            {
                _UART.Add_to_Tx_Buffer($"Temp,Run,0");
            }
        }


        // PID 값 설정
        public void CMD_Set_PID(int P, int I, int D)
        {
            if(P > 0)
            {
                _UART.Add_to_Tx_Buffer($"PID,Set,{P},{I},{D}");
            }
        }

        // PID 값 읽기
        public void CMD_Get_PID()
        {
            _UART.Add_to_Tx_Buffer($"PID,Get");
        }


        // PCR 시작
        public void CMD_PCR_Run()
        {
            _UART.Add_to_Tx_Buffer("PCR,CMD,Run");
        }

        // PCR 중지
        public void CMD_PCR_Stop()
        {
            _UART.Add_to_Tx_Buffer("PCR,CMD,Stop");
        }

        // PCR 일시정지
        public void CMD_PCR_Pause()
        {
            _UART.Add_to_Tx_Buffer("PCR,CMD,Pause");
        }

        // PCR 다시시작
        public void CMD_PCR_Resume()
        {
            _UART.Add_to_Tx_Buffer("PCR,CMD,Resume");
        }

        // PCR Skip
        public void CMD_PCR_Skip()
        {
            _UART.Add_to_Tx_Buffer("PCR,CMD,Skip");
        }


        // Get Stage Setup
        public void CMD_StageSetup_Get()
        {
            _UART.Add_to_Tx_Buffer("PCR,StageSetup,Get");
        }

        // Set Stage Setup
        public void CMD_StageSetup_Set(List<SetupInfo> SetupInfo, int CycleNumber)
        {
            if(SetupInfo.Count == 6)
            {
                string tempSetupInfo = "";

                // Temperature & Duration
                foreach (SetupInfo info in SetupInfo)
                {
                    tempSetupInfo += $"{info.Temperature:N2},{info.Duration},";
                }

                // Add cycle Number
                tempSetupInfo += $"{CycleNumber}";

                _UART.Add_to_Tx_Buffer($"PCR,StageSetup,Set,{tempSetupInfo}");
            }
        }

        #endregion





        #region Data Class

        public class PCR_Info
        {
            public string Machine_State { get; set; }                   // 현재 PCR 기기 상태
            public int Current_Stage { get; set; }                  // 현재 스테이지 번호

            public PCR_Time Remaining_StageTime { get; set; }       // 남은 현재 스테이지 시간
            public PCR_Time Remaining_TotalTime { get; set; }       // 남은 전체 시간

            public int CurrentCycleNumber { get; set; }             // 현재 진행중인 싸이클 수
            public int TotalCycleNumber { get; set; }               // 전체 싸이클 수
        }

        public class PCR_Time
        {
            public int Hour { get; set; }
            public int Minute { get; set; }
            public int Second { get; set; }

            public PCR_Time(int hour, int minute, int second)
            {
                Hour = hour; Minute = minute; Second = second;
            }
        }

        public class SetupInfo
        {
            public double Temperature { get; set; }
            public int Duration { get; set; }

            public SetupInfo(double temperature, int duration)
            {
                Temperature = temperature;
                Duration = duration;
            }
        }

        public class PID
        {
            public double Kp { get; set; }
            public double Ki { get; set; }
            public double Kd { get; set; }

            public PID(double P, double I, double D)
            {
                Kp = P;
                Ki = I; 
                Kd = D;
            }
        }


        #endregion



        #region Event

        // UART Connection Changed Event
        public event EventHandler<UART_ConnectionChangedEventArgs> UART_ConnectionChanged;
        public class UART_ConnectionChangedEventArgs : EventArgs
        {
            public bool State { get; }
            public string Message { get; }

            public UART_ConnectionChangedEventArgs(bool state, string message)
            {
                State = state;
                Message = message;
            }
        }


        // Temperature Updated Event
        public event EventHandler<TemepratureUpdatedEventArgs> Temperature_Updated;
        public class TemepratureUpdatedEventArgs : EventArgs
        {
            public double Temperature { get; }
            public int DAC { get; }
            public string Message { get; }

            public TemepratureUpdatedEventArgs(double temperature, int DAC_val, string message)
            {
                Temperature = temperature;
                DAC = DAC;
                Message = message;
            }
        }


        // PID Event
        public event EventHandler<PIDUpdatedEventArgs> PID_Updated;
        public class PIDUpdatedEventArgs : EventArgs
        {
            public PID PID { get; }

            public PIDUpdatedEventArgs(double P, double I, double D)
            {
                PID.Kp = P;
                PID.Ki = I;
                PID.Kd = D;
            }
        }


        // PCR Info Updated
        public event EventHandler<PCR_InfoUpdatedEventArgs> PCR_InfoUpdated;
        public class PCR_InfoUpdatedEventArgs : EventArgs
        {
            public PCR_Info PCR_Info { get; }
            public string Message { get; }

            public PCR_InfoUpdatedEventArgs(PCR_Info PCRInfo, string message)
            {
                PCR_Info = PCRInfo;
                Message = message;
            }
        }

       

        // PCR Setup Info Updated
        public event EventHandler<PCR_SetupInfoUpdatedEventArgs> PCR_SetupInfoUpdated;
        public class PCR_SetupInfoUpdatedEventArgs : EventArgs
        {
            public List<SetupInfo> PCR_SetupInfo { get; }
            public int CycleNumber { get; }
            public string Message { get; }

            public PCR_SetupInfoUpdatedEventArgs(List<SetupInfo> PCRSetupInfo, int cycleNumber, string message)
            {
                PCR_SetupInfo = PCRSetupInfo.ToList();
                CycleNumber = cycleNumber;
                Message = message;
            }
        }

        #endregion




        #region UART

        UART _UART = new UART("DS_PCR", 115200);

        private void _UART_ConnectionChanged(object sender, UART.ConnectionChangedEventArgs e)
        {
           UART_ConnectionChanged?.Invoke(this, new UART_ConnectionChangedEventArgs(e.State, "UART 연결 상태 바뀜"));
        }

        private void _UART_PacketReceived(object sender, UART.PacketReceivedEventArgs e)
        {
            
            string[] tempRxPackets = e.Rx_Packet.Split(',');

            // 현재 온도 및 DAC 값 전달받음
            if (tempRxPackets[0] == "Temp")
            {
                // 현재 온도
                double temperature = PT100_Convert_ADC_to_Temperature(double.Parse(tempRxPackets[1]));

                // 현재 DAC
                int tempDAC = int.Parse(tempRxPackets[2]);

                // 이벤트 발생
                Temperature_Updated?.Invoke(this, new TemepratureUpdatedEventArgs(temperature, tempDAC, "온도 업데이트"));
            }

            // PID 값 전달 받음
            else if ((tempRxPackets[0] == "PID") && (tempRxPackets[1] == "Get"))
            {

                PID_Updated?.Invoke(this, new PIDUpdatedEventArgs(double.Parse(tempRxPackets[2]), double.Parse(tempRxPackets[3]), double.Parse(tempRxPackets[4])));
            }


            // PCR 정보 업데이트
            else if ((tempRxPackets[0] == "PCR") && (tempRxPackets[1] == "Info"))
            {
                PCR_Info _tempInfo = new PCR_Info();

                _tempInfo.Machine_State = tempRxPackets[2];                 // 기기 상태
                _tempInfo.Current_Stage = int.Parse(tempRxPackets[3]);      // 현재 스테이지

                int currentStageRemainingSeconds = int.Parse(tempRxPackets[4]); // 현재 스테이지 남은 시간
                _tempInfo.Remaining_StageTime =
                    new PCR_Time(currentStageRemainingSeconds / 3600,
                    currentStageRemainingSeconds / 60,
                    currentStageRemainingSeconds % 60);

                int allRemainingSeconds = int.Parse(tempRxPackets[5]);      // 전체 남은 시간
                _tempInfo.Remaining_TotalTime =
                    new PCR_Time(allRemainingSeconds / 3600,
                    allRemainingSeconds / 60,
                    allRemainingSeconds % 60);

                _tempInfo.CurrentCycleNumber = int.Parse(tempRxPackets[6]);     // 현재 진행중인 사이클링 수

                _tempInfo.TotalCycleNumber = int.Parse(tempRxPackets[7]);     // 전체 사이클링 수

                // 이벤트 발생
                PCR_InfoUpdated?.Invoke(this, new PCR_InfoUpdatedEventArgs(_tempInfo, "PCR 정보 업데이트"));
            }


            // PCR 스테이지 설정 읽음
            else if ((tempRxPackets[0] == "PCR") && (tempRxPackets[1] == "StageSetup"))
            {
                List<SetupInfo> tempInfo = new List<SetupInfo>();

                // StageSetup 데이터 파싱
                for (int i = 0; i < 6; i++)
                {
                    // 온도와 시간 데이터를 파싱 (패킷에서 시작 인덱스가 2부터)
                    double temperature = double.Parse(tempRxPackets[2 + (i * 2)]);
                    int time = int.Parse(tempRxPackets[3 + (i * 2)]);

                    tempInfo.Add(new SetupInfo(temperature, time));
                }

                // Cycle number
                int temp_cyclenum = int.Parse(tempRxPackets.Last());

                if (tempInfo.Count == 6)
                {
                    // 이벤트 발생
                    PCR_SetupInfoUpdated?.Invoke(this, new PCR_SetupInfoUpdatedEventArgs(tempInfo.ToList(), temp_cyclenum, "PCR 설정 정보 업데이트"));
                }
            }
            
        }

        #endregion





        #region PT100

        double PT100_Convert_ADC_to_Temperature(double Value)
        {
            const double R0 = 100.0;       // PT100의 0°C 저항값
            const double A = 3.9083e-3;   // CVD 상수 A
            const double B = -5.775e-7;   // CVD 상수 B
            const double R_REF = 360.0;   // MAX31865 기준 저항

            // ADC 값을 RTD 저항값으로 변환
            double R_T = ((double)Value / 32768.0) * R_REF;

            // 2차 방정식을 풀어 온도 계산
            double T = (-A + Math.Sqrt(A * A - 4.0 * B * (1.0 - R_T / R0))) / (2.0 * B);

            return T; // 계산된 온도 반환
        }

        int PT100_Convert_Temperature_to_ADC(double Temperature)
        {
            const double R0 = 100.0;         // PT100의 0°C 저항값
            const double A = 3.9083e-3;     // CVD 상수 A
            const double B = -5.775e-7;     // CVD 상수 B
            const double R_REF = 360.0;     // MAX31865 기준 저항

            // 온도를 RTD 저항 값으로 변환
            double R_T = R0 * (1.0 + A * Temperature + B * Temperature * Temperature);

            // RTD 저항 값을 ADC 값으로 변환
            double ADC_Value = (R_T / R_REF) * 32768.0;

            return (int)ADC_Value; // 계산된 ADC 값 반환
        }
        #endregion




    }
}
