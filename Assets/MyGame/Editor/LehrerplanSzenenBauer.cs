using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Lehrerplan.EditorTools
{
    /// <summary>
    /// Baut die 2D-Szene "Lehrerplan" nach der Skizze (MyGame/Pictures/Image.png):
    /// L-förmiger Grundriss mit Gang B (waagrecht) und Gang C (senkrecht),
    /// je ein Ebene für den 2. und 3. Stock, Räume als anklickbare Türschilder.
    /// </summary>
    public static class LehrerplanSzenenBauer
    {
        const string SzenenPfad = "Assets/MyGame/Scenes/Lehrerplan.unity";

        // Grundriss (Canvas-Pixel, 1920x1080, Ursprung oben links)
        const float GangBreite = 120f;
        const float EckeX = 330f, EckeY = 260f;           // innere obere linke Ecke der beiden Gänge
        const float SchildB = 170f, SchildH = 110f, Abstand = 20f;

        static readonly Color Hintergrund = new Color(0.93f, 0.95f, 0.97f);
        static readonly Color GangFarbe = new Color(0.78f, 0.80f, 0.83f);
        static readonly Color TuerFarbe = new Color(0.55f, 0.36f, 0.20f);
        static readonly Color Dunkel = new Color(0.13f, 0.17f, 0.22f);

        class Stock
        {
            public string name;
            public string[] bInnen;   // unterhalb von Gang B, von der Ecke weg
            public string[] bAussen;  // oberhalb von Gang B
            public string[] c;        // links von Gang C, von der Ecke nach unten
        }

        static readonly Stock[] Stockwerke =
        {
            new Stock { name = "2. Stock",
                bInnen = new[] { "B212", "B211", "B210", "B209", "B208" },
                bAussen = new string[0],
                c = new[] { "C214", "C213", "C212" } },
            new Stock { name = "3. Stock",
                bInnen = new[] { "B314", "B313", "B312", "B311", "B310" },
                bAussen = new[] { "B303", "B304", "B305" },
                c = new[] { "C313", "C312" } },
        };

        static Font schrift;

        [MenuItem("Lehrerplan/2 - Szene erstellen")]
        public static void Erstellen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            schrift = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var szene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Kamera (2D, orthografisch)
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hintergrund;
            camGo.transform.position = new Vector3(0, 0, -10);

            // EventSystem (neues Input System)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // Canvas
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var canvas = canvasGo.transform;

            Bild(canvas, "Hintergrund", 0, 0, 1920, 1080, Hintergrund).raycastTarget = false;
            Text(canvas, "Titel", 30, 15, 1200, 50, "HTL Salzburg – Gang B & C (Türschilder anklicken)", 34, FontStyle.Bold, Dunkel, TextAnchor.MiddleLeft);

            // Stockwerk-Buttons
            var buttons = new List<Button>();
            for (int i = 0; i < Stockwerke.Length; i++)
            {
                var b = Bild(canvas, "Button " + Stockwerke[i].name, 30 + i * 180, 80, 160, 50, new Color(0.55f, 0.6f, 0.65f));
                var btn = b.gameObject.AddComponent<Button>();
                Text(b.transform, "Text", 0, 0, 160, 50, Stockwerke[i].name, 24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                buttons.Add(btn);
            }

            // Stockwerke
            var stockGos = Stockwerke.Select(s => BaueStock(canvas, s)).ToArray();

            // Info-Panel (im Inneren des L)
            var panel = Bild(canvas, "InfoPanel", 520, 560, 1360, 490, Color.white);
            panel.gameObject.AddComponent<Outline>().effectColor = Dunkel;
            var titel = Text(panel.transform, "Titel", 30, 20, 1000, 50, "Raum", 40, FontStyle.Bold, Dunkel, TextAnchor.MiddleLeft);
            var telefon = Text(panel.transform, "Telefon", 30, 75, 1300, 34, "Tel.:", 24, FontStyle.Normal, new Color(0.15f, 0.35f, 0.6f), TextAnchor.MiddleLeft);
            var lehrer = Text(panel.transform, "Lehrer", 30, 125, 1300, 350, "", 21, FontStyle.Normal, Dunkel, TextAnchor.UpperLeft);
            lehrer.supportRichText = true;
            lehrer.verticalOverflow = VerticalWrapMode.Overflow;
            var x = Bild(panel.transform, "Schliessen", 1300, 10, 50, 50, new Color(0.8f, 0.25f, 0.25f));
            var xBtn = x.gameObject.AddComponent<Button>();
            Text(x.transform, "Text", 0, 0, 50, 50, "X", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            // Manager
            var mgr = new GameObject("Lehrerplan").AddComponent<LehrerplanManager>();
            mgr.stockwerke = stockGos;
            mgr.stockwerkButtons = buttons.ToArray();
            mgr.infoPanel = panel.gameObject;
            mgr.infoTitel = titel;
            mgr.infoTelefon = telefon;
            mgr.infoLehrer = lehrer;
            mgr.schliessenButton = xBtn;

            System.IO.Directory.CreateDirectory("Assets/MyGame/Scenes");
            EditorSceneManager.SaveScene(szene, SzenenPfad);

            var build = EditorBuildSettings.scenes.ToList();
            if (build.All(s => s.path != SzenenPfad))
            {
                build.Insert(0, new EditorBuildSettingsScene(SzenenPfad, true));
                EditorBuildSettings.scenes = build.ToArray();
            }
            Debug.Log("[Lehrerplan] Szene erstellt: " + SzenenPfad);
        }

        static GameObject BaueStock(Transform canvas, Stock s)
        {
            var root = new GameObject(s.name, typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // Gänge
            var gangB = Bild(root.transform, "Gang B", EckeX, EckeY, 1920 - 30 - EckeX, GangBreite, GangFarbe);
            Text(gangB.transform, "Label", 0, 0, 1920 - 60 - EckeX, GangBreite, "Gang B", 34, FontStyle.Bold, new Color(1, 1, 1, 0.9f), TextAnchor.MiddleRight);
            var gangC = Bild(root.transform, "Gang C", EckeX, EckeY, GangBreite, 1060 - EckeY, GangFarbe);
            Text(gangC.transform, "Label", 0, 1060 - EckeY - 80, GangBreite, 60, "Gang C", 30, FontStyle.Bold, new Color(1, 1, 1, 0.9f), TextAnchor.MiddleCenter);
            Text(root.transform, "Stockwerk", 1500, 80, 390, 50, s.name, 30, FontStyle.Bold, Dunkel, TextAnchor.MiddleRight);

            float startX = EckeX + GangBreite + Abstand;
            for (int i = 0; i < s.bInnen.Length; i++)
                Schild(root.transform, s.bInnen[i], startX + i * (SchildB + Abstand), EckeY + GangBreite + 12, TuerSeite.Oben);

            for (int i = 0; i < s.bAussen.Length; i++)
                Schild(root.transform, s.bAussen[i], startX + (i + 3) * (SchildB + Abstand), EckeY - 12 - SchildH, TuerSeite.Unten);

            for (int i = 0; i < s.c.Length; i++)
                Schild(root.transform, s.c[i], EckeX - 12 - SchildB, EckeY + GangBreite + 40 + i * (SchildH + 40), TuerSeite.Rechts);

            return root;
        }

        enum TuerSeite { Oben, Unten, Rechts }

        static void Schild(Transform parent, string raum, float x, float y, TuerSeite seite)
        {
            // Tür am Gang
            switch (seite)
            {
                case TuerSeite.Oben: Bild(parent, "Tür " + raum, x + 45, y - 12, 80, 10, TuerFarbe); break;
                case TuerSeite.Unten: Bild(parent, "Tür " + raum, x + 45, y + SchildH + 2, 80, 10, TuerFarbe); break;
                case TuerSeite.Rechts: Bild(parent, "Tür " + raum, x + SchildB + 2, y + 15, 10, 80, TuerFarbe); break;
            }

            var bild = Bild(parent, "Schild " + raum, x, y, SchildB, SchildH, Color.white);
            bild.gameObject.AddComponent<Outline>().effectColor = Dunkel;
            var btn = bild.gameObject.AddComponent<Button>();
            var farben = btn.colors;
            farben.highlightedColor = new Color(0.85f, 0.92f, 1f);
            farben.pressedColor = new Color(0.7f, 0.82f, 0.95f);
            btn.colors = farben;

            var schild = bild.gameObject.AddComponent<TuerSchild>();
            schild.raumNummer = raum;
            schild.nummerText = Text(bild.transform, "Nummer", 0, 6, SchildB, 40, raum, 30, FontStyle.Bold, Dunkel, TextAnchor.MiddleCenter);
            schild.namenText = Text(bild.transform, "Namen", 8, 48, SchildB - 16, 58, "", 14, FontStyle.Normal, new Color(0.3f, 0.33f, 0.38f), TextAnchor.UpperCenter);
            schild.namenText.resizeTextForBestFit = true;
            schild.namenText.resizeTextMinSize = 9;
            schild.namenText.resizeTextMaxSize = 14;
        }

        // ---- Hilfsfunktionen: Rechteck mit Ursprung oben links ----

        static RectTransform Rechteck(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static Image Bild(Transform parent, string name, float x, float y, float w, float h, Color farbe)
        {
            var img = Rechteck(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            img.color = farbe;
            return img;
        }

        static Text Text(Transform parent, string name, float x, float y, float w, float h, string inhalt,
            int groesse, FontStyle stil, Color farbe, TextAnchor ausrichtung)
        {
            var t = Rechteck(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            t.font = schrift;
            t.text = inhalt;
            t.fontSize = groesse;
            t.fontStyle = stil;
            t.color = farbe;
            t.alignment = ausrichtung;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }
    }
}
