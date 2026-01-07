using UnityEngine;
using UnityEngine.UI;
using Unity.Sentis;
using System;
using System.Collections.Generic;
using System.Linq;

public class LiveYOLOFacing : MonoBehaviour
{
    [Header("YOLO Settings")]
    public ModelAsset modelAsset;
    public TextAsset classesAsset;
    public Texture2D borderTexture;
    public Font font;

    [Header("Renderers")]
    public Camera mainCamera;
    public RawImage displayImage;

    [Header("Highlight Logic")]
    [Tooltip("다음 박스로 하이라이트가 넘어가는 시간 간격(초)")]
    [SerializeField] private float highlightInterval = 0.1f;

    // --- 하이라이트 상태 관리 변수 ---
    private float highlightTimer = 0f;
    private int highlightedIndex = -1;
    private List<GameObject> activeBoxesForHighlight = new List<GameObject>();

    // --- YOLO 엔진 변수 ---
    const BackendType backend = BackendType.GPUCompute;
    private Worker worker;
    private string[] labels;
    private Sprite borderSprite;
    private Tensor<float> centersToCorners;
    private List<GameObject> boxPool = new();

    const int imageWidth = 640;
    const int imageHeight = 640;

    [SerializeField, Range(0, 1)]
    float iouThreshold = 0.5f;
    [SerializeField, Range(0, 1)]
    float scoreThreshold = 0.5f;

    private RenderTexture camRenderTexture;
    private Texture2D screenTexture;

    // --- Tracker ---
    private Tracker tracker;

    void Start()
    {
        labels = classesAsset.text.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToArray();
        LoadModel();

        borderSprite = Sprite.Create(borderTexture,
          new Rect(0, 0, borderTexture.width, borderTexture.height),
          new Vector2(0.5f, 0.5f));

        camRenderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        screenTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

        RectTransform rt = displayImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        displayImage.color = new Color(1, 1, 1, 0);

        // 트래커 초기화: maxAge = 5, minHits = 1, iouThreshold = 0.3 (조절 가능)
        tracker = new Tracker(maxAge: 5, minHits: 1, iouThreshold: 0.3f);
    }

    void LoadModel()
    {
        var model = ModelLoader.Load(modelAsset);
        centersToCorners = new Tensor<float>(new TensorShape(4, 4), new float[] { 1, 0, 1, 0, 0, 1, 0, 1, -0.5f, 0, 0.5f, 0, 0, -0.5f, 0, 0.5f });
        var graph = new FunctionalGraph();
        var inputs = graph.AddInputs(model);
        var modelOutput = Functional.Forward(model, inputs)[0];
        var boxCoords = modelOutput[0, 0..4, ..].Transpose(0, 1);
        var allScores = modelOutput[0, 4.., ..];
        var scores = Functional.ReduceMax(allScores, 0);
        var classIDs = Functional.ArgMax(allScores, 0);
        var boxCorners = Functional.MatMul(boxCoords, Functional.Constant(centersToCorners));
        var indices = Functional.NMS(boxCorners, scores, iouThreshold, scoreThreshold);
        var coords = Functional.IndexSelect(boxCoords, 0, indices);
        var labelIDs = Functional.IndexSelect(classIDs, 0, indices);
        worker = new Worker(graph.Compile(coords, labelIDs), backend);
    }

    void Update()
    {

        CaptureAndDetect();


        if (activeBoxesForHighlight.Count > 0)
        {
            highlightTimer += Time.deltaTime;


            if (highlightTimer >= highlightInterval)
            {
                highlightTimer = 0f;
                highlightedIndex = (highlightedIndex + 1) % activeBoxesForHighlight.Count;
            }
        }
        else
        {
            // 탐지된 박스가 없으면 인덱스 초기화
            highlightedIndex = -1;
        }

        // 모든 박스의 색상을 업데이트 (빨간색 or 투명)
        UpdateBoxColors();
    }

    void CaptureAndDetect()
    {
        ClearAnnotations();
        activeBoxesForHighlight.Clear(); // 매 프레임 리스트를 새로 만듭니다.

        var originalRT = mainCamera.targetTexture;
        mainCamera.targetTexture = camRenderTexture;
        mainCamera.Render();
        RenderTexture.active = camRenderTexture;
        screenTexture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenTexture.Apply();
        RenderTexture.active = null;
        mainCamera.targetTexture = originalRT;

        using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));
        TextureConverter.ToTensor(screenTexture, inputTensor, default);
        worker.Schedule(inputTensor);

        using var output = (worker.PeekOutput("output_0") as Tensor<float>).ReadbackAndClone();
        using var labelIDs = (worker.PeekOutput("output_1") as Tensor<int>).ReadbackAndClone();

        float displayWidth = displayImage.canvas.pixelRect.width;
        float displayHeight = displayImage.canvas.pixelRect.height;
        float scaleX = displayWidth / imageWidth;
        float scaleY = displayHeight / imageHeight;

        // 1) 모델의 raw detections -> detection list
        List<Detection> detections = new List<Detection>();
        for (int n = 0; n < output.shape[0]; n++)
        {
            var box = new BoundingBoxStruct
            {
                centerX = (output[n, 0] * scaleX),
                centerY = (output[n, 1] * scaleY),
                width = output[n, 2] * scaleX,
                height = output[n, 3] * scaleY,
                label = labels[labelIDs[n]],
            };

            // Convert to xyxy for IoU/matching
            float x1 = box.centerX - box.width / 2f;
            float y1 = box.centerY - box.height / 2f;
            float x2 = box.centerX + box.width / 2f;
            float y2 = box.centerY + box.height / 2f;

            detections.Add(new Detection
            {
                x1 = x1,
                y1 = y1,
                x2 = x2,
                y2 = y2,
                label = box.label
            });
        }

        // 2) Tracker 업데이트 -> active tracks 반환
        var tracks = tracker.Update(detections);

        // 3) 트랙들을 화면에 그림 (ID 포함 라벨) 및 콘솔 출력
        foreach (var tr in tracks)
        {
            float cx = (tr.x1 + tr.x2) / 2f;
            float cy = (tr.y1 + tr.y2) / 2f;
            float w = tr.x2 - tr.x1;
            float h = tr.y2 - tr.y1;

            var box = new BoundingBox
            {
                centerX = cx,
                centerY = cy,
                width = w,
                height = h,
                label = $"{tr.label} (ID:{tr.trackId})"
            };

            var go = DrawBox(box, tr.trackId, displayHeight * 0.05f);
            activeBoxesForHighlight.Add(go);

            // 콘솔에 트랙 정보 출력 (ID, bbox)
            Debug.Log($"[Tracker] ID={tr.trackId} x1={tr.x1:F1} y1={tr.y1:F1} x2={tr.x2:F1} y2={tr.y2:F1}");
        }

        if (activeBoxesForHighlight.Count > 0)
        {
            activeBoxesForHighlight = activeBoxesForHighlight.OrderBy(go => UnityEngine.Random.value).ToList();
        }
    }


    void UpdateBoxColors()
    {
        for (int i = 0; i < activeBoxesForHighlight.Count; i++)
        {
            if (i == highlightedIndex)
            {

                SetBoxColor(activeBoxesForHighlight[i], Color.red, true);
            }
            else
            {
                //SetBoxColor(activeBoxesForHighlight[i], Color.yellow, true);
                SetBoxColor(activeBoxesForHighlight[i], Color.clear, false);
            }
        }
    }

    struct BoundingBox { public float centerX, centerY, width, height; public string label; }
    struct BoundingBoxStruct { public float centerX, centerY, width, height; public string label; }

    GameObject DrawBox(BoundingBox box, int id, float fontSize)
    {
        GameObject panel;
        if (id < boxPool.Count)
        {
            panel = boxPool[id];
            panel.SetActive(true);
        }
        else
        {
            panel = CreateNewBox();
            // ensure boxPool list is large enough to index by id (simple approach: grow)
            while (boxPool.Count <= id) boxPool.Add(null);
            boxPool[id] = panel;
        }

        var rect = panel.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(box.width, box.height);

        var canvasRect = displayImage.rectTransform.rect;
        float anchoredX = box.centerX - (canvasRect.width / 2);
        float anchoredY = (canvasRect.height / 2) - box.centerY;
        rect.anchoredPosition = new Vector2(anchoredX, anchoredY);

        var label = panel.GetComponentInChildren<Text>();
        label.text = box.label;
        label.fontSize = (int)fontSize;

        return panel;
    }

    void SetBoxColor(GameObject box, Color color, bool showLabel)
    {
        if (box == null || !box.activeInHierarchy) return;

        Image img = box.GetComponent<Image>();
        Text txt = box.GetComponentInChildren<Text>();

        if (img != null)
        {
            img.color = color;
        }
        if (txt != null)
        {
            txt.color = Color.red;
            txt.enabled = showLabel;
        }
    }

    GameObject CreateNewBox()
    {
        var panel = new GameObject("ObjectBox");
        panel.AddComponent<CanvasRenderer>();
        var img = panel.AddComponent<Image>();
        img.sprite = borderSprite;
        img.type = Image.Type.Sliced;

        panel.transform.SetParent(displayImage.transform, false);

        var rect = panel.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 0.5f);

        var text = new GameObject("ObjectLabel");
        text.AddComponent<CanvasRenderer>();
        text.transform.SetParent(panel.transform, false);
        var txt = text.AddComponent<Text>();
        txt.font = font;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        var rt2 = text.GetComponent<RectTransform>();
        rt2.offsetMin = new Vector2(20, 0);
        rt2.offsetMax = new Vector2(0, 30);
        rt2.anchorMin = new Vector2(0.5f, 0.5f);
        rt2.anchorMax = new Vector2(0.5f, 0.5f);
        rt2.pivot = new Vector2(0.5f, 0.5f);
        rt2.anchoredPosition = Vector2.zero;

        return panel;
    }

    void ClearAnnotations()
    {
        foreach (var box in boxPool)
            if (box != null) box.SetActive(false);
    }

    void OnDestroy()
    {
        centersToCorners?.Dispose();
        worker?.Dispose();
        if (camRenderTexture) camRenderTexture.Release();
    }

    // ------------------------------
    // Detection & Tracking classes
    // ------------------------------
    class Detection
    {
        public float x1, y1, x2, y2;
        public string label;
    }

    class TrackResult
    {
        public int trackId;
        public float x1, y1, x2, y2;
        public string label;
    }

    // 간단한 칼만필터 (상태: cx, cy, w, h, vx, vy, vw, vh)
    class KalmanFilter
    {
        // 상태 벡터 8x1, 공분산 8x8
        public double[] x; // length 8
        public double[,] P; // 8x8

        private double[,] F; // 상태전이
        private double[,] Q; // 프로세스 노이즈
        private double[,] H; // 관측행렬 (4x8)
        private double[,] R; // 관측 노이즈 (4x4)

        public KalmanFilter(double cx, double cy, double w, double h)
        {
            x = new double[8];
            x[0] = cx; x[1] = cy; x[2] = w; x[3] = h;
            // velocities init 0
            for (int i = 4; i < 8; i++) x[i] = 0;

            P = new double[8, 8];
            for (int i = 0; i < 8; i++) P[i, i] = 1;

            F = new double[8, 8];
            for (int i = 0; i < 8; i++) F[i, i] = 1;
            // dt = 1
            F[0, 4] = 1; F[1, 5] = 1; F[2, 6] = 1; F[3, 7] = 1;

            Q = new double[8, 8];
            for (int i = 0; i < 8; i++) Q[i, i] = 1e-2; // process noise (조절 가능)

            H = new double[4, 8];
            H[0, 0] = 1; H[1, 1] = 1; H[2, 2] = 1; H[3, 3] = 1;

            R = new double[4, 4];
            for (int i = 0; i < 4; i++) R[i, i] = 1e-1; // 관측 노이즈 (조절 가능)
        }

        public void Predict()
        {
            x = MatMulVec(F, x);
            P = Add(MatMul(F, MatMul(P, Transpose(F))), Q);
        }

        public void Update(double[] z) // z length 4: cx,cy,w,h
        {
            var y = Sub(z, MatMulVec(H, x)); // residual
            var S = Add(MatMul(H, MatMul(P, Transpose(H))), R); // 4x4
            var K = MatMul(P, MatMul(Transpose(H), Inverse(S))); // 8x4

            var K_y = MatMulVec(K, y); // 8
            x = AddVec(x, K_y);
            var I = Identity(8);
            var KH = MatMul(K, H); // 8x8
            var I_KH = Sub(I, KH);
            P = MatMul(I_KH, P);
        }

        // helper matrix ops (최소한만 구현)
        private static double[] MatMulVec(double[,] A, double[] v)
        {
            int m = A.GetLength(0), n = A.GetLength(1);
            double[] r = new double[m];
            for (int i = 0; i < m; i++)
            {
                double s = 0;
                for (int j = 0; j < n; j++) s += A[i, j] * v[j];
                r[i] = s;
            }
            return r;
        }
        private static double[,] MatMul(double[,] A, double[,] B)
        {
            int m = A.GetLength(0), n = B.GetLength(1), p = A.GetLength(1);
            double[,] C = new double[m, n];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++)
                {
                    double s = 0;
                    for (int k = 0; k < p; k++) s += A[i, k] * B[k, j];
                    C[i, j] = s;
                }
            return C;
        }
        private static double[,] Transpose(double[,] A)
        {
            int m = A.GetLength(0), n = A.GetLength(1);
            double[,] B = new double[n, m];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++) B[j, i] = A[i, j];
            return B;
        }
        private static double[,] Add(double[,] A, double[,] B)
        {
            int m = A.GetLength(0), n = A.GetLength(1);
            double[,] C = new double[m, n];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++) C[i, j] = A[i, j] + B[i, j];
            return C;
        }
        private static double[] AddVec(double[] a, double[] b)
        {
            int n = a.Length;
            double[] r = new double[n];
            for (int i = 0; i < n; i++) r[i] = a[i] + b[i];
            return r;
        }
        private static double[] Sub(double[] a, double[] b)
        {
            int n = a.Length;
            double[] r = new double[n];
            for (int i = 0; i < n; i++) r[i] = a[i] - b[i];
            return r;
        }
        private static double[,] Inverse(double[,] A)
        {
            // Only used for small 4x4 S. Use Gauss-Jordan (naive).
            int n = A.GetLength(0);
            double[,] B = new double[n, n];
            double[,] M = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    M[i, j] = A[i, j];
                    B[i, j] = (i == j) ? 1.0 : 0.0;
                }
            for (int i = 0; i < n; i++)
            {
                double pivot = M[i, i];
                if (Math.Abs(pivot) < 1e-9) pivot = 1e-9;
                for (int j = 0; j < n; j++) { M[i, j] /= pivot; B[i, j] /= pivot; }
                for (int r = 0; r < n; r++)
                {
                    if (r == i) continue;
                    double factor = M[r, i];
                    for (int c = 0; c < n; c++)
                    {
                        M[r, c] -= factor * M[i, c];
                        B[r, c] -= factor * B[i, c];
                    }
                }
            }
            return B;
        }
        private static double[,] Identity(int n)
        {
            double[,] I = new double[n, n];
            for (int i = 0; i < n; i++) I[i, i] = 1;
            return I;
        }
        private static double[,] Sub(double[,] A, double[,] B)
        {
            int m = A.GetLength(0), n = A.GetLength(1);
            double[,] C = new double[m, n];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++) C[i, j] = A[i, j] - B[i, j];
            return C;
        }
    }

    class Track
    {
        public int id;
        public KalmanFilter kf;
        public int age; // frames since creation
        public int timeSinceUpdate;
        public int hits; // total hits
        public string label;

        public Track(int id_, Detection d)
        {
            id = id_;
            float cx = (d.x1 + d.x2) / 2f;
            float cy = (d.y1 + d.y2) / 2f;
            float w = d.x2 - d.x1;
            float h = d.y2 - d.y1;
            kf = new KalmanFilter(cx, cy, w, h);
            age = 1;
            timeSinceUpdate = 0;
            hits = 1;
            label = d.label;
        }

        public void Predict()
        {
            kf.Predict();
            age++;
            timeSinceUpdate++;
        }

        public void Update(Detection d)
        {
            float cx = (d.x1 + d.x2) / 2f;
            float cy = (d.y1 + d.y2) / 2f;
            float w = d.x2 - d.x1;
            float h = d.y2 - d.y1;
            kf.Update(new double[] { cx, cy, w, h });
            timeSinceUpdate = 0;
            hits++;
            label = d.label;
        }

        public float[] GetState() // returns x1,y1,x2,y2
        {
            var s = kf.x;
            float cx = (float)s[0];
            float cy = (float)s[1];
            float w = (float)s[2];
            float h = (float)s[3];
            float x1 = cx - w / 2f;
            float y1 = cy - h / 2f;
            float x2 = cx + w / 2f;
            float y2 = cy + h / 2f;
            return new float[] { x1, y1, x2, y2 };
        }
    }

    class Tracker
    {
        private List<Track> tracks = new List<Track>();
        private int nextId = 0;
        private int maxAge;
        private int minHits;
        private float iouThreshold;

        public Tracker(int maxAge = 5, int minHits = 1, float iouThreshold = 0.3f)
        {
            this.maxAge = maxAge;
            this.minHits = minHits;
            this.iouThreshold = iouThreshold;
        }

        public List<TrackResult> Update(List<Detection> detections)
        {
            // 1) predict existing tracks
            foreach (var t in tracks) t.Predict();

            int N = tracks.Count;
            int M = detections.Count;

            // 2) compute IoU cost matrix
            float[,] iouMatrix = new float[N, M];
            for (int i = 0; i < N; i++)
            {
                var st = tracks[i].GetState();
                for (int j = 0; j < M; j++)
                {
                    iouMatrix[i, j] = IoU(st[0], st[1], st[2], st[3], detections[j].x1, detections[j].y1, detections[j].x2, detections[j].y2);
                }
            }

            // 3) greedy matching (highest IoU first)
            int[] matchesT = new int[N]; for (int i = 0; i < N; i++) matchesT[i] = -1;
            int[] matchesD = new int[M]; for (int j = 0; j < M; j++) matchesD[j] = -1;

            List<(int t, int d, float iou)> pairs = new List<(int, int, float)>();
            for (int i = 0; i < N; i++)
                for (int j = 0; j < M; j++)
                    pairs.Add((i, j, iouMatrix[i, j]));
            pairs = pairs.OrderByDescending(p => p.iou).ToList();

            foreach (var p in pairs)
            {
                if (p.iou < iouThreshold) break;
                if (matchesT[p.t] != -1) continue;
                if (matchesD[p.d] != -1) continue;
                matchesT[p.t] = p.d;
                matchesD[p.d] = p.t;
            }

            // 4) update matched
            for (int i = 0; i < N; i++)
            {
                int d = matchesT[i];
                if (d != -1)
                {
                    tracks[i].Update(detections[d]);
                }
            }

            // 5) create new tracks for unmatched detections
            for (int j = 0; j < M; j++)
            {
                if (matchesD[j] == -1)
                {
                    var t = new Track(nextId++, detections[j]);
                    tracks.Add(t);
                }
            }

            // 6) remove dead tracks (timeSinceUpdate > maxAge)
            tracks.RemoveAll(t => t.timeSinceUpdate > maxAge);

            // 7) prepare results (only return tracks that have hits >= minHits or recently updated)
            List<TrackResult> results = new List<TrackResult>();
            foreach (var t in tracks)
            {
                var s = t.GetState();
                if (t.hits >= minHits && t.timeSinceUpdate <= maxAge)
                {
                    results.Add(new TrackResult
                    {
                        trackId = t.id,
                        x1 = s[0],
                        y1 = s[1],
                        x2 = s[2],
                        y2 = s[3],
                        label = t.label
                    });
                }
            }
            return results;
        }

        private static float IoU(float x1, float y1, float x2, float y2, float xx1, float yy1, float xx2, float yy2)
        {
            float ix1 = Math.Max(x1, xx1);
            float iy1 = Math.Max(y1, yy1);
            float ix2 = Math.Min(x2, xx2);
            float iy2 = Math.Min(y2, yy2);
            float iw = Math.Max(0, ix2 - ix1);
            float ih = Math.Max(0, iy2 - iy1);
            float inter = iw * ih;
            float areaA = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            float areaB = Math.Max(0, xx2 - xx1) * Math.Max(0, yy2 - yy1);
            float uni = areaA + areaB - inter;
            if (uni <= 0) return 0f;
            return inter / uni;
        }
    }
}