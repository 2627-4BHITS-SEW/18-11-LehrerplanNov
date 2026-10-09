using System;
using System.Collections.Generic;

namespace Lehrerplan
{
    /// <summary>Ein Lehrer / eine Lehrerin, wie auf der HTL-Salzburg-Homepage angeführt.</summary>
    [Serializable]
    public class Lehrer
    {
        public string name;
        public string raum;          // normalisiert, z.B. "B304"
        public string telefonDw;     // Durchwahl, z.B. "420"
        public string sprechstunde;
        public string url;
    }

    /// <summary>Wurzelobjekt für die JSON-Datei (JsonUtility kann keine Top-Level-Arrays).</summary>
    [Serializable]
    public class LehrerSammlung
    {
        public string quelle;
        public string abgerufenAm;
        public string telefonZentrale;   // z.B. "+43 662 453610"
        public List<Lehrer> lehrer = new List<Lehrer>();
    }
}
