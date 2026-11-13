using UnityEditor;
using UnityEngine;

public static class CarCatalogBalancer
{
    // Têm de coincidir com os valores do CarSpawner no Inspector
    const float LaunchAccelPerStat = 1.15f;
    const float FirstGearBoost = 1.2f;
    const float RatioExponent = 0.85f;

    class Car
    {
        public string name;
        public float topKmh, accel, handling, tolerance;
        public int gears;
        public Car(string name, float topKmh, float accel, float handling, int gears, float tolerance = 1f)
        {
            this.name = name; this.topKmh = topKmh; this.accel = accel;
            this.handling = handling; this.gears = gears; this.tolerance = tolerance;
        }
    }

    static readonly Car[] Table =
    {
        //          carro            km/h  acel  handling mudanças tolerância
        new Car("Taxi",          150f, 6.4f, 0.90f, 4, 1.60f),
        new Car("Police Sedan",  180f, 6.0f, 0.75f, 4, 1.45f),
        new Car("SUV",           190f, 6.0f, 0.60f, 4, 1.30f),
        new Car("Muscle",        170f, 8.5f, 0.45f, 4, 1.15f),
        new Car("Ambulance",     225f, 5.4f, 0.52f, 4, 1.20f),
        new Car("Sports",        250f, 8.2f, 0.28f, 6, 0.80f),
        new Car("Muscly",        225f, 7.0f, 0.38f, 5, 1.00f),
        new Car("Police Muscle", 215f, 8.2f, 0.33f, 5, 0.90f),
        new Car("Police Sports", 250f, 8.2f, 0.28f, 6, 0.80f),
    };

    // Rácios (1ª ... última). Velocidade de cada mudança ∝ 1/rácio.
    // Mudanças vizinhas ficam >= ~1.3x de distância, para a travagem nas curvas funcionar.
    static readonly float[] Ratios4 = { 3.5f, 2.1f, 1.4f, 1.0f };
    static readonly float[] Ratios5 = { 3.6f, 2.4f, 1.7f, 1.25f, 0.95f };
    static readonly float[] Ratios6 = { 3.9f, 2.7f, 1.95f, 1.45f, 1.1f, 0.85f };
    static readonly string[] Names = { "1st", "2nd", "3rd", "4th", "5th", "6th" };

    [MenuItem("Tools/Cars/Apply Balance To Catalog")]
    static void Apply()
    {
        string[] guids = AssetDatabase.FindAssets("t:CarCatalog");
        if (guids.Length == 0) { Debug.LogError("CarCatalog não encontrado."); return; }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        Object catalog = AssetDatabase.LoadMainAssetAtPath(path);
        var so = new SerializedObject(catalog);
        SerializedProperty cars = so.FindProperty("cars");
        if (cars == null) { Debug.LogError("Campo 'cars' não encontrado no CarCatalog."); return; }

        int applied = 0;
        for (int i = 0; i < cars.arraySize; i++)
        {
            SerializedProperty entry = cars.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = entry.FindPropertyRelative("displayName");
            if (nameProp == null) continue;

            Car car = Find(nameProp.stringValue);
            if (car == null)
            {
                Debug.LogWarning($"'{nameProp.stringValue}' não está na tabela de balanceamento.");
                continue;
            }

            SetNumber(entry, "topSpeed", car.topKmh);
            SetNumber(entry, "acceleration", car.accel);
            SetNumber(entry, "handling", car.handling);
            WriteGears(entry.FindPropertyRelative("gears"), car);
            applied++;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"Balanceamento aplicado a {applied} carro(s) em {path}.");
    }

    static Car Find(string name)
    {
        foreach (Car c in Table)
            if (c.name == name) return c;
        return null;
    }

    static void WriteGears(SerializedProperty gears, Car car)
    {
        if (gears == null) { Debug.LogWarning($"{car.name}: campo 'gears' não encontrado."); return; }

        float[] ratios = car.gears >= 6 ? Ratios6 : car.gears == 5 ? Ratios5 : Ratios4;
        float topMs = car.topKmh / 3.6f;
        float launch = car.accel * LaunchAccelPerStat;
        float rFirst = ratios[0];
        float rLast = ratios[ratios.Length - 1];

        gears.arraySize = ratios.Length;
        for (int g = 0; g < ratios.Length; g++)
        {
            SerializedProperty gear = gears.GetArrayElementAtIndex(g);

            SerializedProperty gearName = gear.FindPropertyRelative("name");
            if (gearName != null) gearName.stringValue = Names[g];

            // maxSpeed e acceleration são recalculados no CarSpawner; ficam aqui só para referência
            float accel = launch * Mathf.Pow(ratios[g] / rFirst, RatioExponent);
            if (g == 0) accel *= FirstGearBoost;

            SetNumber(gear, "gearRatio", ratios[g]);
            SetNumber(gear, "maxSpeed", topMs * rLast / ratios[g]);
            SetNumber(gear, "acceleration", accel);
            SetNumber(gear, "rpmIncrease", 400f);
            SetNumber(gear, "rpmDecrease", 400f);
        }
    }

    static void SetNumber(SerializedProperty parent, string field, float value)
    {
        SerializedProperty p = parent.FindPropertyRelative(field);
        if (p == null) { Debug.LogWarning($"Campo '{field}' não encontrado."); return; }

        if (p.propertyType == SerializedPropertyType.Integer) p.intValue = Mathf.RoundToInt(value);
        else p.floatValue = value;
    }
}