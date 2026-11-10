using UnityEngine;

[DefaultExecutionOrder(1000)]   // depois do follower (100), do ChaseTarget (150) e do Cinemachine (200)
public class JitterProbe : MonoBehaviour
{
    public Transform car;
    public Transform chaseTarget;
    public Transform cam;   // vazio = Camera.main

    const int N = 90;
    class Stat
    {
        readonly float[] b = new float[N]; int i, n;
        public void Add(float v) { b[i] = v; i = (i + 1) % N; if (n < N) n++; }
        public float Mean { get { float s = 0; for (int k = 0; k < n; k++) s += b[k]; return n > 0 ? s / n : 0; } }
        public float Rel
        {
            get
            {
                if (n < 2 || Mean < 1e-4f) return 0;
                float m = Mean, s = 0;
                for (int k = 0; k < n; k++) { float d = b[k] - m; s += d * d; }
                return Mathf.Sqrt(s / n) / m * 100f;
            }
        }
    }

    readonly Stat carV = new Stat(), targetV = new Stat(), camRel = new Stat(), frameMs = new Stat();
    Vector3 lastCar, lastTarget, lastLocal;
    bool init;

    void LateUpdate()
    {
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        if (car == null || chaseTarget == null || cam == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 local = chaseTarget.InverseTransformPoint(cam.position);
        if (init)
        {
            carV.Add((car.position - lastCar).magnitude / dt);
            targetV.Add((chaseTarget.position - lastTarget).magnitude / dt);
            camRel.Add((local - lastLocal).magnitude * 100f);   // cm por frame, relativamente ao alvo
            frameMs.Add(Time.unscaledDeltaTime * 1000f);
        }
        lastCar = car.position; lastTarget = chaseTarget.position; lastLocal = local;
        init = true;
    }

    void OnGUI()
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        GUI.Label(new Rect(10, 10, 900, 160),
            $"Velocidade do CARRO: ruído {carV.Rel:F1}%\n" +
            $"Velocidade do ALVO:  ruído {targetV.Rel:F1}%\n" +
            $"Câmara vs alvo: muda {camRel.Mean:F2} cm/frame\n" +
            $"Tempo de frame: {frameMs.Mean:F1} ms (variação {frameMs.Rel:F0}%)", s);
    }
}