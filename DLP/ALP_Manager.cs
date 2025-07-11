
** 트리거 출력은 Rising Edge로 나옴.
** SetImages로 이미지 리스트를 시퀀스번호랑 같이 넣음
** StartProjection에 시퀀스 번호를 넣으면 동작 함.
** Halt는 continuous 에서만 동작함.
** 주사가 끝나면 마지막 이미지가 남으니까 마지막 이미지를 blank로 넣어두면 딱 좋을 듯



 public class DLP_Manager : IDisposable
 {
     private ALP_43 _ALP;
     private bool _isInitialized = false;
     private int _maxRetry = 5;
     private int _retryDelay_ms = 500;

     public DLP_Manager()
     {
         _ALP = new ALP_43();

         // ALP 이벤트 내부 처리
         _ALP.ProjectionDone += (seqID) =>
         {
             ProjectionDone?.Invoke(seqID);
             //Debug.WriteLine("[DLP_Manager] 시퀀스 투사 완료");
         };

         _ALP.SetImageDone += (seqID) =>
         {
             //Debug.WriteLine($"[DLP_Manager] 시퀀스 {seqID} 이미지 업로드 완료");

             SetImageDone?.Invoke(seqID);
         };

         TryAutoInit();
     }

     ~DLP_Manager()
     {
         DeInit();  // Finalizer에서도 호출
     }

     public void Dispose()
     {
         DeInit();
         GC.SuppressFinalize(this);
     }

     private void TryAutoInit()
     {
         for (int i = 0; i < _maxRetry; i++)
         {
             if (_ALP.Init())
             {
                 _isInitialized = true;
                 Debug.WriteLine("[DLP_Manager] ALP Init 성공");
                 return;
             }

             Debug.WriteLine($"[DLP_Manager] ALP Init 실패. 재시도 {i + 1}/{_maxRetry}...");
             Thread.Sleep(_retryDelay_ms);
         }

         Debug.WriteLine("[DLP_Manager] ALP Init 최종 실패");
     }

     private void DeInit()
     {
         if (_isInitialized)
         {
             _ALP.DeInit();
             _isInitialized = false;
             Debug.WriteLine("[DLP_Manager] ALP DeInit 완료");
         }
     }

     public bool IsInitialized => _isInitialized;



     public event Action<uint> SetImageDone;  // 시퀀스 ID를 전달하는 외부 이벤트
     public event Action<uint> ProjectionDone;





     /// <summary>
     /// 폴더 내 모든 이미지 파일을 시퀀스로 업로드함
     /// </summary>
     public void SetImagesFromDirectory(string DirectoryPath, uint SequenceID, int exposure_us = 5000)
     {
         string[] filePaths = Directory.GetFiles(DirectoryPath)
                                       .OrderBy(path => Path.GetFileName(path))  // 파일명 기준 정렬
                                       .ToArray();  // 전체 경로 유지

         SetImages(filePaths, SequenceID, exposure_us);
     }


     /// <summary>
     /// 이미지 파일을 시퀀스로 업로드함
     /// </summary>
     public void SetImages(string[] imagePaths, uint SequenceID, int exposure_us = 5000)
     {
         List<SequenceImageData> _SeqDataList = new List<SequenceImageData>();

         SequenceImageData tempSeq = new SequenceImageData(SequenceID, imagePaths, exposure_us);
         _SeqDataList.Add(tempSeq);

         _ALP.Set_SequenceData(tempSeq);
     }

     /// <summary>
     /// 단일 투사 시작
     /// </summary>
     public void StartProjection(uint SequenceID)
     {
         _ALP.Start_Projection(SequenceID);
     }

     /// <summary>
     /// 연속 투사 시작
     /// </summary>
     public void StartProjectionContinuous(uint SequenceID)
     {
         _ALP.Start_Projection_Cont(SequenceID);
     }

     /// <summary>
     /// 연속 투사 중지
     /// </summary>
     public void StopProjection()
     {
         _ALP.Halt();
     }
 }
