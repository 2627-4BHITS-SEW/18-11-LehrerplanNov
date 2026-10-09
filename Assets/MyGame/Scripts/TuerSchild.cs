using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Lehrerplan
{
    /// <summary>
    /// Ein anklickbares Türschild. Zeigt Raumnummer und Nachnamen der Lehrer;
    /// ein Klick öffnet die Details (Lehrer, Sprechstunden, Telefon) im Info-Panel.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TuerSchild : MonoBehaviour
    {
        public string raumNummer;
        public Text nummerText;
        public Text namenText;

        void Start()
        {
            GetComponent<Button>().onClick.AddListener(() => LehrerplanManager.Instanz.ZeigeRaum(raumNummer));

            nummerText.text = raumNummer;
            var lehrer = LehrerplanManager.Instanz.LehrerInRaum(raumNummer);
            namenText.text = lehrer.Count == 0
                ? "–"
                : string.Join(", ", lehrer.Select(l => l.name.Split(' ', ',')[0]));
        }
    }
}
