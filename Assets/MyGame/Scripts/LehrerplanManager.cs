using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Lehrerplan
{
    /// <summary>
    /// Lädt die gesammelten Lehrerdaten (Resources/lehrer.json), verwaltet die Stockwerke
    /// und zeigt beim Klick auf ein Türschild die Infos des Raums an.
    /// </summary>
    public class LehrerplanManager : MonoBehaviour
    {
        public static LehrerplanManager Instanz { get; private set; }

        [Header("Stockwerke")]
        public GameObject[] stockwerke;
        public Button[] stockwerkButtons;

        [Header("Info-Anzeige")]
        public GameObject infoPanel;
        public Text infoTitel;
        public Text infoTelefon;
        public Text infoLehrer;
        public Button schliessenButton;

        LehrerSammlung daten;
        Dictionary<string, List<Lehrer>> nachRaum = new Dictionary<string, List<Lehrer>>();

        void Awake()
        {
            Instanz = this;
            Laden();
        }

        void Start()
        {
            for (int i = 0; i < stockwerkButtons.Length; i++)
            {
                int index = i;
                stockwerkButtons[i].onClick.AddListener(() => ZeigeStockwerk(index));
            }
            if (schliessenButton != null) schliessenButton.onClick.AddListener(SchliesseInfo);
            ZeigeStockwerk(0);
            SchliesseInfo();
        }

        void Laden()
        {
            var json = Resources.Load<TextAsset>("lehrer");
            if (json == null)
            {
                Debug.LogError("Resources/lehrer.json fehlt – Menü 'Lehrerplan/1 - Lehrerdaten laden' ausführen.");
                daten = new LehrerSammlung();
                return;
            }
            daten = JsonUtility.FromJson<LehrerSammlung>(json.text);
            nachRaum = daten.lehrer
                .Where(l => !string.IsNullOrEmpty(l.raum))
                .GroupBy(l => l.raum)
                .ToDictionary(g => g.Key, g => g.OrderBy(l => l.name).ToList());
        }

        public List<Lehrer> LehrerInRaum(string raum) =>
            nachRaum.TryGetValue(raum, out var liste) ? liste : new List<Lehrer>();

        /// <summary>Telefonnummer des Raums: Zentrale + Durchwahl(en) der Lehrer im Kabinett.</summary>
        public string TelefonVonRaum(string raum)
        {
            var dws = LehrerInRaum(raum)
                .Select(l => l.telefonDw)
                .Where(dw => !string.IsNullOrWhiteSpace(dw))
                .Distinct()
                .ToList();
            if (dws.Count == 0) return "keine Angabe";
            return string.Join(", ", dws.Select(dw => $"{daten.telefonZentrale} - {dw}"));
        }

        public void ZeigeStockwerk(int index)
        {
            for (int i = 0; i < stockwerke.Length; i++)
                stockwerke[i].SetActive(i == index);
            for (int i = 0; i < stockwerkButtons.Length; i++)
                stockwerkButtons[i].image.color = i == index ? new Color(0.20f, 0.45f, 0.75f) : new Color(0.55f, 0.6f, 0.65f);
            SchliesseInfo();
        }

        public void ZeigeRaum(string raum)
        {
            var lehrer = LehrerInRaum(raum);
            infoTitel.text = $"Raum {raum}";
            infoTelefon.text = $"Tel.: {TelefonVonRaum(raum)}";

            var sb = new StringBuilder();
            if (lehrer.Count == 0)
            {
                sb.Append("Laut HTL-Homepage ist hier kein Lehrer eingetragen\n(Klassen- oder Sonderraum).");
            }
            else
            {
                foreach (var l in lehrer)
                {
                    sb.Append("<b>").Append(l.name).Append("</b>\n");
                    sb.Append("   Sprechstunde: ")
                      .Append(string.IsNullOrWhiteSpace(l.sprechstunde) ? "keine Angabe" : l.sprechstunde)
                      .Append('\n');
                    sb.Append('\n');
                }
            }
            infoLehrer.text = sb.ToString().TrimEnd();
            infoPanel.SetActive(true);
        }

        public void SchliesseInfo()
        {
            if (infoPanel != null) infoPanel.SetActive(false);
        }
    }
}
